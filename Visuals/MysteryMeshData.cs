using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The mystery map effects modelled in Blender (#285), read from Bundles/mystery-fx.mesh.gz (written by
/// tools/blender/export-mystery.py, which describes the format). Free of the game's types, so the unit tests read the
/// real file; <see cref="MysteryMapFx"/> makes the game's meshes from it.
/// </summary>
public sealed class MysteryMeshData
{
    public const string Magic = "CBM1";

    /// <summary>A row of the ramp texture: a material's colours, or a see-through one's colour and strength</summary>
    public sealed record Row(byte Kind, byte[] Shade, byte[] Base, byte[] Light, float Cut, float LightCut, float Alpha)
    {
        public const byte Toon = 0, Fading = 1;

        /// <summary>The colour at <paramref name="x"/> (0-1) along the row, as RGBA bytes</summary>
        public byte[] ColourAt(float x)
        {
            if (Kind == Fading)
            {
                var strength = (1 - Math.Clamp(x, 0, 1)) * (1 - Math.Clamp(x, 0, 1)) * Alpha;
                return [Base[0], Base[1], Base[2], (byte) Math.Round(255 * Math.Clamp(strength, 0, 1))];
            }

            var colour = x >= LightCut ? Light : x >= Cut ? Base : Shade;
            return [colour[0], colour[1], colour[2], 255];
        }
    }

    /// <summary>A mesh: corners as the game wants them (raw little-endian floats and ints, copied in one block)</summary>
    public sealed class Part
    {
        public string Name { get; init; } = "";
        public bool SeeThrough { get; init; }
        public int VertexCount { get; init; }

        /// <summary>3 floats per corner</summary>
        public byte[] Positions { get; init; } = [];

        /// <summary>The ramp row's y of each corner</summary>
        public float[] RowYs { get; init; } = [];

        /// <summary>How lit each corner is (or how faded), 0-255, per step: one step, or one per way a die can lie</summary>
        public byte[][] Steps { get; init; } = [];

        public int IndexCount { get; init; }

        /// <summary>int32 per index</summary>
        public byte[] Indices { get; init; } = [];

        public Vector3 Position(int corner) => new(
            BitConverter.ToSingle(Positions, corner * 12), BitConverter.ToSingle(Positions, corner * 12 + 4),
            BitConverter.ToSingle(Positions, corner * 12 + 8));

        public int Index(int i) => BitConverter.ToInt32(Indices, i * 4);

        /// <summary>The UVs into the ramp for <paramref name="step"/>, as 2 floats per corner (raw bytes)</summary>
        public byte[] UvsFor(int step)
        {
            var lit = Steps[Math.Clamp(step, 0, Steps.Length - 1)];
            var uvs = new byte[VertexCount * 8];
            for (var i = 0; i < VertexCount; i++)
            {
                BitConverter.TryWriteBytes(uvs.AsSpan(i * 8), lit[i] / 255f);
                BitConverter.TryWriteBytes(uvs.AsSpan(i * 8 + 4), RowYs[i]);
            }
            return uvs;
        }

        /// <summary>How far its corners reach from its origin, the largest of x, y and z</summary>
        public float Extent()
        {
            var most = 0f;
            for (var i = 0; i < VertexCount; i++)
            {
                var p = Vector3.Abs(Position(i));
                most = Math.Max(most, Math.Max(p.X, Math.Max(p.Y, p.Z)));
            }
            return most;
        }
    }

    public IReadOnlyList<Row> Rows { get; private init; } = [];
    public IReadOnlyDictionary<string, Part> Parts { get; private init; } = new Dictionary<string, Part>();

    /// <summary>The ways a die can lie, the step of a part with more than one is for</summary>
    public IReadOnlyList<Quaternion> Orientations { get; private init; } = [];

    /// <summary>Named spots: where the dice lie (DieSpot0..), where pips twinkle (PipSpot0..), with a size</summary>
    public IReadOnlyList<(string Name, Vector3 Position, float Size)> Points { get; private init; } = [];

    /// <summary>The step whose way to lie is nearest to <paramref name="rotation"/></summary>
    public int NearestOrientation(Quaternion rotation)
    {
        var best = 0;
        var bestDot = -1f;
        for (var i = 0; i < Orientations.Count; i++)
        {
            var dot = Math.Abs(Quaternion.Dot(Quaternion.Normalize(rotation), Orientations[i]));
            if (dot <= bestDot) continue;
            bestDot = dot;
            best = i;
        }
        return best;
    }

    /// <summary>
    /// The ramp texture: <paramref name="width"/> texels per row, <paramref name="rowHeight"/> high each, as RGBA bytes
    /// from the bottom row up (as Unity's textures)
    /// </summary>
    public byte[] RampPixels(int width, int rowHeight)
    {
        var pixels = new byte[width * rowHeight * Rows.Count * 4];
        for (var row = 0; row < Rows.Count; row++)
        {
            for (var x = 0; x < width; x++)
            {
                var colour = Rows[row].ColourAt((x + 0.5f) / width);
                for (var y = 0; y < rowHeight; y++) colour.CopyTo(pixels, ((row * rowHeight + y) * width + x) * 4);
            }
        }
        return pixels;
    }

    public static MysteryMeshData Read(Stream gzipped)
    {
        // Unpacked into memory first: reading straight from the gzip stream was slow for Gildebloon's body (#25)
        using var memory = new MemoryStream();
        using (var gzip = new GZipStream(gzipped, CompressionMode.Decompress, true)) gzip.CopyTo(memory);
        memory.Position = 0;
        using var reader = new BinaryReader(memory);

        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic) throw new InvalidDataException($"not a {Magic} file");

        var rows = new List<Row>();
        int rowCount = reader.ReadByte();
        for (var i = 0; i < rowCount; i++)
        {
            var kind = reader.ReadByte();
            var colours = Exactly(reader, 9);
            rows.Add(new Row(kind, colours[..3], colours[3..6], colours[6..], reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        }

        var parts = new Dictionary<string, Part>();
        var partCount = Count(reader);
        for (var i = 0; i < partCount; i++)
        {
            var name = Name(reader);
            var seeThrough = (reader.ReadByte() & 1) != 0;
            var vertices = Count(reader);
            var positions = Exactly(reader, vertices * 12);
            var ys = new float[vertices];
            for (var v = 0; v < vertices; v++) ys[v] = reader.ReadSingle();
            int stepCount = reader.ReadByte();
            if (stepCount == 0) throw new InvalidDataException($"{name} has no steps");
            var steps = new byte[stepCount][];
            for (var s = 0; s < stepCount; s++) steps[s] = Exactly(reader, vertices);
            var indexCount = Count(reader);
            var indices = Exactly(reader, indexCount * 4);
            var part = new Part
            {
                Name = name, SeeThrough = seeThrough, VertexCount = vertices, Positions = positions, RowYs = ys, Steps = steps,
                IndexCount = indexCount, Indices = indices
            };
            for (var x = 0; x < indexCount; x++)
            {
                if ((uint) part.Index(x) >= (uint) vertices) throw new InvalidDataException($"{name} has an index out of range");
            }
            parts[name] = part;
        }

        var orientations = new List<Quaternion>();
        var orientationCount = Count(reader);
        for (var i = 0; i < orientationCount; i++)
        {
            orientations.Add(new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        }

        var points = new List<(string, Vector3, float)>();
        var pointCount = Count(reader);
        for (var i = 0; i < pointCount; i++)
        {
            var name = Name(reader);
            points.Add((name, new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle()));
        }

        foreach (var part in parts.Values)
        {
            if (part.Steps.Length > 1 && part.Steps.Length != orientations.Count)
            {
                throw new InvalidDataException($"{part.Name} has {part.Steps.Length} steps for {orientations.Count} ways to lie");
            }
        }

        return new MysteryMeshData {Rows = rows, Parts = parts, Orientations = orientations, Points = points};
    }

    private static int Count(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count is < 0 or > 10_000_000) throw new InvalidDataException($"a count of {count}");
        return count;
    }

    private static string Name(BinaryReader reader) => Encoding.UTF8.GetString(Exactly(reader, reader.ReadByte()));

    private static byte[] Exactly(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new EndOfStreamException("the file ends early");
        return bytes;
    }
}
