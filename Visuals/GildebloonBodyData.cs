using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Gildebloon's body and animations as modelled in Blender (#25, tools/blender/gildebloon-boss.py), read from
/// Bundles/gildebloon-body.gba.gz (tools/blender/export-boss.py, which describes the format). Free of the game's types,
/// so the unit tests read it too; <see cref="GildebloonBody"/> makes the game's meshes from it.
/// </summary>
public sealed class GildebloonBodyData
{
    public const string Magic = "GBA1";

    /// <summary>A part's position, rotation and scale, in its parent's space (Unity's axes)</summary>
    public readonly record struct Pose(Vector3 Position, Quaternion Rotation, Vector3 Scale)
    {
        public static Pose Lerp(Pose a, Pose b, float t) =>
            new(Vector3.Lerp(a.Position, b.Position, t), Quaternion.Slerp(a.Rotation, b.Rotation, t),
                Vector3.Lerp(a.Scale, b.Scale, t));
    }

    /// <summary>A ramp row: shade, base and light colours (RGB bytes), where base and light start, how opaque</summary>
    public sealed record Row(bool SeeThrough, byte[] Shade, byte[] Base, byte[] Light, float Cut, float LightCut, float Alpha);

    public sealed class Part
    {
        public string Name = "";
        public int Parent;
        public bool Elite, SeeThrough;
        public Pose Rest;
        public int VertexCount, IndexCount;

        /// <summary>The mesh as the file has it, little-endian floats and ints, to copy into the game's arrays at once</summary>
        public byte[] Positions = [], Uvs = [], Indices = [];
    }

    public sealed class Clip
    {
        public string Name = "";
        public int Frames;
        public float Fps = 24;
        public bool Loops;

        /// <summary>Per part that moves in it, its pose on each frame</summary>
        public readonly Dictionary<int, Pose[]> Tracks = new();

        public float Seconds => Frames / Fps;

        /// <summary>The part's pose <paramref name="seconds"/> in, between frames; null when it doesn't move in this clip</summary>
        public Pose? Sample(int part, float seconds)
        {
            if (!Tracks.TryGetValue(part, out var frames) || frames.Length == 0) return null;
            var at = seconds * Fps;
            if (Loops) at %= frames.Length;
            at = Math.Clamp(at, 0, frames.Length - 1);
            var i = (int) MathF.Floor(at);
            var next = Loops ? (i + 1) % frames.Length : Math.Min(i + 1, frames.Length - 1);
            return Pose.Lerp(frames[i], frames[next], at - i);
        }
    }

    public List<Row> Rows { get; } = [];
    public List<Part> Parts { get; } = [];
    public Dictionary<string, Clip> Clips { get; } = new();

    public int PartIndex(string name) => Parts.FindIndex(part => part.Name == name);

    /// <summary>Reads a gzipped GBA1 file</summary>
    public static GildebloonBodyData Read(Stream gzipped)
    {
        // Unpacked whole first: read straight from the gzip stream, four bytes at a time, it took 6 s in the game
        using var memory = new MemoryStream();
        using (var gzip = new GZipStream(gzipped, CompressionMode.Decompress)) gzip.CopyTo(memory);
        memory.Position = 0;
        using var reader = new BinaryReader(memory);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic) throw new InvalidDataException($"not a {Magic} body");
        var data = new GildebloonBodyData();

        var rows = reader.ReadInt32();
        for (var i = 0; i < rows; i++)
        {
            var kind = reader.ReadByte();
            var shade = reader.ReadBytes(3);
            var @base = reader.ReadBytes(3);
            var light = reader.ReadBytes(3);
            data.Rows.Add(new Row(kind == 1, shade, @base, light, reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        }

        var parts = reader.ReadInt32();
        for (var i = 0; i < parts; i++)
        {
            var part = new Part {Name = ReadName(reader), Parent = reader.ReadInt32()};
            var flags = reader.ReadByte();
            part.Elite = (flags & 1) != 0;
            part.SeeThrough = (flags & 2) != 0;
            part.Rest = ReadPose(reader);
            if (part.Parent >= i) throw new InvalidDataException($"{part.Name}'s parent comes after it");
            part.VertexCount = reader.ReadInt32();
            if (part.VertexCount > 0)
            {
                part.Positions = ReadExactly(reader, part.VertexCount * 12);
                part.Uvs = ReadExactly(reader, part.VertexCount * 8);
                part.IndexCount = reader.ReadInt32();
                part.Indices = ReadExactly(reader, part.IndexCount * 4);
            }

            data.Parts.Add(part);
        }

        var clips = reader.ReadInt32();
        for (var i = 0; i < clips; i++)
        {
            var clip = new Clip {Name = ReadName(reader), Frames = reader.ReadInt32(), Fps = reader.ReadSingle(), Loops = reader.ReadByte() != 0};
            var tracks = reader.ReadInt32();
            for (var t = 0; t < tracks; t++)
            {
                var part = reader.ReadInt32();
                if (part < 0 || part >= data.Parts.Count) throw new InvalidDataException($"{clip.Name} moves a part that isn't there");
                var frames = new Pose[clip.Frames];
                for (var f = 0; f < clip.Frames; f++) frames[f] = ReadPose(reader);
                clip.Tracks[part] = frames;
            }

            data.Clips[clip.Name] = clip;
        }

        return data;
    }

    private static string ReadName(BinaryReader reader) => Encoding.UTF8.GetString(ReadExactly(reader, reader.ReadByte()));

    private static Pose ReadPose(BinaryReader reader)
    {
        var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        var scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        return new Pose(position, rotation, scale);
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new EndOfStreamException($"{count} bytes expected, {bytes.Length} there");
        return bytes;
    }
}

/// <summary>
/// Plays Gildebloon's animations (#25): a base loop (Idle, or a damage state's), one animation at a time over it, a
/// short blend between them. A new animation only cuts in over one that matters as much or less (<see cref="Priority"/>);
/// <see cref="Final"/> ones hold their last frame. Free of the game's types: the look asks it for each part's pose.
/// </summary>
public sealed class GildebloonClipPlayer
{
    /// <summary>What may cut in over what: higher wins; equal cuts in too</summary>
    public static readonly IReadOnlyDictionary<string, int> Priority = new Dictionary<string, int>
    {
        ["Death"] = 100, ["Escape"] = 100, ["Payout"] = 100,
        ["Spawn"] = 90, ["EliteSpawn"] = 90,
        ["Repossession"] = 80,
        ["VaultBreak"] = 70,
        ["Dispatch"] = 60,
        ["Skittish"] = 50,
        ["Toll"] = 40, ["Interest"] = 40, ["VaultUp"] = 40,
        ["TarnishHit"] = 30,
        ["Hit"] = 20,
        ["IdleCount"] = 10, ["IdlePolish"] = 10, ["IdleSmug"] = 10
    };

    /// <summary>Animations that end it: they hold their last frame instead of going back to the base loop</summary>
    public static readonly IReadOnlyCollection<string> Final = ["Death", "Escape", "Payout"];

    public const float BlendSeconds = 0.12f;

    /// <summary>The loop under the animations at a share of health: Idle, then the three damage states</summary>
    public static string BaseFor(float share) =>
        share <= 0.10f ? "Damaged10" : share <= 0.33f ? "Damaged33" : share <= 0.66f ? "Damaged66" : "Idle";

    private readonly GildebloonBodyData data;
    private GildebloonBodyData.Clip? baseClip, clip, from;
    private float baseTime, time, fromTime, blend = BlendSeconds;
    private string? queued;

    public GildebloonClipPlayer(GildebloonBodyData data, string baseClip = "Idle")
    {
        this.data = data;
        SetBase(baseClip);
    }

    /// <summary>The animation playing over the base loop, or null</summary>
    public string? Current => clip?.Name;

    public string? Base => baseClip?.Name;

    /// <summary>The last animation started, for tests</summary>
    public string? LastPlayed { get; private set; }

    /// <summary>Whether an animation that ends it has played to its last frame</summary>
    public bool Finished => clip != null && Final.Contains(clip.Name) && time >= clip.Seconds;

    /// <summary>Changes the loop under the animations (a damage state's); the time carries on so it doesn't jump</summary>
    public void SetBase(string name)
    {
        if (baseClip?.Name == name || !data.Clips.TryGetValue(name, out var next)) return;
        if (clip == null) StartBlend(baseClip, baseTime);
        baseClip = next;
    }

    /// <summary>Starts an animation if it is there and may cut in over the one playing; false if it may not</summary>
    public bool Play(string name)
    {
        if (!data.Clips.TryGetValue(name, out var next)) return false;
        if (clip != null && Rank(name) < Rank(clip.Name)) return false;
        if (clip != null && Final.Contains(clip.Name) && !Final.Contains(name)) return false;
        StartBlend(clip ?? baseClip, clip != null ? time : baseTime);
        clip = next;
        time = 0;
        LastPlayed = name;
        return true;
    }

    /// <summary>Plays an animation once the one playing now is done (Payout after Death)</summary>
    public void Queue(string name)
    {
        if (clip == null || (Finished && clip.Name != name)) Play(name);
        else queued = name;
    }

    public void Advance(float seconds)
    {
        if (seconds <= 0) return;
        baseTime += seconds;
        blend += seconds;
        fromTime += seconds;
        if (clip == null) return;
        time += seconds;
        if (time < clip.Seconds) return;
        if (queued != null)
        {
            var next = queued;
            queued = null;
            if (Final.Contains(clip.Name)) clip = null; // the queued one follows on from it
            Play(next);
            return;
        }

        if (Final.Contains(clip.Name))
        {
            time = clip.Seconds;
            return;
        }

        StartBlend(clip, clip.Seconds);
        clip = null;
    }

    /// <summary>The pose of a part now</summary>
    public GildebloonBodyData.Pose PoseOf(int part)
    {
        var now = PoseIn(clip, time, part) ?? PoseIn(baseClip, baseTime, part) ?? data.Parts[part].Rest;
        if (blend >= BlendSeconds) return now;
        var before = PoseIn(from, fromTime, part) ?? PoseIn(baseClip, baseTime, part) ?? data.Parts[part].Rest;
        return GildebloonBodyData.Pose.Lerp(before, now, blend / BlendSeconds);
    }

    /// <summary>Whether a part has a pose of its own in the animation playing, for Elite's parts</summary>
    public bool Moves(int part) => clip?.Tracks.ContainsKey(part) == true || (clip == null && baseClip?.Tracks.ContainsKey(part) == true);

    private static GildebloonBodyData.Pose? PoseIn(GildebloonBodyData.Clip? c, float t, int part) =>
        c == null ? null : c.Sample(part, c.Loops ? t : Math.Min(t, c.Seconds));

    private static int Rank(string name) => Priority.TryGetValue(name, out var rank) ? rank : 0;

    private void StartBlend(GildebloonBodyData.Clip? source, float sourceTime)
    {
        from = source;
        fromTime = sourceTime;
        blend = 0;
    }
}
