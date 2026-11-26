using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BTD_Mod_Helper;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The map's boss effects while the boss Random picked is still a secret (#285). The game's own (<c>&lt;Boss&gt;AmbientMapFx</c>
/// and <c>&lt;Boss&gt;TrackFx</c>) would give the boss away, so they are switched off then (#283); these take their place,
/// in the style of the die the Random tiers show: a soft ivory haze coming in from the screen's edges with a few pips
/// twinkling in it, and three small dice at the boss's spawn point that hop, tumble and land on new faces now and then.
/// Modelled in Blender (tools/blender/mystery-fx.py), read from Bundles/mystery-fx.mesh.gz (<see cref="MysteryMeshData"/>).
/// <br/>
/// Each takes the place of one of the game's: a map or an accessibility setting without the game's effects gets none
/// of these either. <see cref="BossPresentation"/> shows them with the game's switched off and takes them away when
/// the game's are shown again; <see cref="Update"/> also takes them away once nothing is a secret or the map is gone.
/// </summary>
public static class MysteryMapFx
{
    public const string Resource = "CustomBossChallenge.mystery-fx.mesh.gz";
    public const string AmbientName = "MysteryAmbientMapFx", TrackName = "MysteryTrackFx";

    /// <summary>A die's size on the map, in the game's units (a red bloon is about 10 across)</summary>
    public const float DieSize = 6;

    /// <summary>The screen the haze was made for in Blender, in its units: it is stretched from this to the camera's view</summary>
    private const float ModelWidth = 18, ModelHeight = 13.6f;

    /// <summary>How far in front of the camera's near plane the haze sits, in front of the map and behind the HUD</summary>
    private const float Distance = 20;

    private const int RampWidth = 64, RowHeight = 4;

    private static MysteryMeshData? data;
    private static bool loaded;
    private static readonly Dictionary<string, Mesh> Meshes = new();
    private static Material? solid, seeThrough, haze;

    private static GameObject? ambient, track;
    private static Transform? forMap;
    private static readonly List<(Transform Pip, float Size, float Period, float Phase)> Pips = new();
    private static readonly List<Die> Dice = new();
    private static Transform? glow;
    private static Random random = new(285);

    private sealed class Die
    {
        public Transform Transform = null!;
        public Mesh Mesh = null!;
        public Vector3 Rest;
        public Quaternion From, To;
        public int Step;
        public float NextRoll, RollStart = -1;
        public Vector3 Axis;
    }

    /// <summary>The haze round the screen now, or null</summary>
    public static GameObject? Ambient => ambient != null ? ambient : null;

    /// <summary>The dice at the boss's spawn point now, or null</summary>
    public static GameObject? Track => track != null ? track : null;

    /// <summary>Whether the effects were read from the mod and can be shown</summary>
    public static bool Ready => data != null;

    /// <summary>How many times the dice have rolled since they were put on the map, for the tests</summary>
    public static int Rolls { get; private set; }

    /// <summary>Reads the effects; at the main menu</summary>
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var stream = typeof(MysteryMapFx).Assembly.GetManifestResourceStream(Resource);
            if (stream == null)
            {
                ModHelper.Warning<CustomBossChallenge>($"{Resource} is missing; a secret boss has no map effects");
                return;
            }

            var read = MysteryMeshData.Read(stream);
            foreach (var name in new[] {"Die", "Glow", "Haze", "Twinkle"})
            {
                if (!read.Parts.ContainsKey(name)) throw new InvalidDataException($"it has no {name}");
            }

            foreach (var part in read.Parts.Values.Where(part => part.Name != "Die")) Meshes[part.Name] = MeshOf(part, 0);
            data = read;
            ModHelper.Msg<CustomBossChallenge>($"The mystery map effects loaded in {watch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            data = null;
            ModHelper.Warning<CustomBossChallenge>($"The mystery map effects could not be loaded: {e.Message}");
        }
    }

    /// <summary>
    /// Shows the mystery effects in place of the game's <paramref name="effects"/> on <paramref name="map"/>, which are
    /// switched off: the haze if the game had an effect round the map, the dice where its track effect is. Does nothing
    /// for what is shown already, so it can be called again and again.
    /// </summary>
    public static void Show(Transform map, GameObject[] effects)
    {
        Load();
        if (data == null || !MakeMaterials()) return;
        if (forMap != null && forMap.Pointer != map.Pointer) Hide();
        forMap = map;

        if (ambient == null && effects.Any(effect => effect.name.EndsWith(BossPresentation.AmbientSuffix)) &&
            Camera.main is {orthographic: true} camera)
        {
            try
            {
                ambient = BuildAmbient(camera);
            }
            catch (Exception e)
            {
                ModHelper.Warning<CustomBossChallenge>($"Couldn't put the mystery haze round the map: {e.Message}");
            }
        }

        if (track == null && effects.FirstOrDefault(effect => effect.name.EndsWith(BossPresentation.TrackSuffix)) is { } trackFx)
        {
            try
            {
                track = BuildTrack(map, trackFx.transform.position);
            }
            catch (Exception e)
            {
                ModHelper.Warning<CustomBossChallenge>($"Couldn't put the mystery dice on the map: {e.Message}");
            }
        }
    }

    /// <summary>Takes the mystery effects away</summary>
    public static void Hide()
    {
        if (ambient != null) Object.Destroy(ambient);
        if (track != null) Object.Destroy(track);
        foreach (var die in Dice)
        {
            if (die.Mesh != null) Object.Destroy(die.Mesh);
        }
        ambient = null;
        track = null;
        forMap = null;
        glow = null;
        Pips.Clear();
        Dice.Clear();
    }

    /// <summary>Called every frame: the haze breathes, pips twinkle, the dice roll now and then</summary>
    public static void Update()
    {
        if (ambient == null && track == null)
        {
            if (Dice.Count > 0 || Pips.Count > 0) Hide();
            return;
        }

        // The haze is on the camera, which stays from match to match: it goes with the map it was put round, and once
        // nothing is a secret (a normal game, the boss arrived, back at the main menu)
        if (forMap == null || !CustomBossState.IsActive || !Patches.MixedBosses.PresentedHidden)
        {
            Hide();
            return;
        }

        var time = Time.time;
        if (haze != null) haze.color = new Color(1, 1, 1, 0.75f + 0.25f * Mathf.Sin(time * Mathf.PI * 2 / 8));
        foreach (var (pip, size, period, phase) in Pips)
        {
            if (pip == null) continue;
            // A short flash once a period: up and down in about a fifth of it
            var t = Mathf.Repeat(time / period + phase, 1) * 5;
            var flash = t < 1 ? Mathf.Sin(t * Mathf.PI) : 0;
            pip.localScale = Vector3.one * (size * flash);
        }

        if (glow != null) glow.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(time * Mathf.PI * 2 / 5));
        foreach (var die in Dice) Roll(die, time);
    }

    private const float RollSeconds = 0.75f, HopHeight = 1.1f;

    private static void Roll(Die die, float time)
    {
        if (die.Transform == null) return;
        if (die.RollStart < 0)
        {
            if (time < die.NextRoll) return;
            die.RollStart = time;
            die.From = die.Transform.rotation;
            die.To = Lying(random.Next(data!.Orientations.Count));
            die.Axis = Quaternion.Euler(0, (float) random.NextDouble() * 360, 0) * Vector3.right;
            Rolls++;
        }

        var done = Mathf.Clamp01((time - die.RollStart) / RollSeconds);
        var eased = done * done * (3 - 2 * done);
        // A full turn over a sideways axis on the way from how it lay to how it lands, which ends where it lands
        die.Transform.rotation = Quaternion.AngleAxis(360 * eased, die.Axis) * Quaternion.Slerp(die.From, die.To, eased);
        die.Transform.localPosition = die.Rest + Vector3.up * (HopHeight * Mathf.Sin(done * Mathf.PI));
        // Its light is baked for how it lies: the one it lands on from halfway through the tumble
        var step = data!.NearestOrientation(done < 0.5f ? ToNumerics(die.From) : ToNumerics(die.To));
        if (step != die.Step)
        {
            die.Step = step;
            die.Mesh.uv = ToIl2Cpp<Vector2>(data.Parts["Die"].UvsFor(step), die.Mesh.vertexCount);
        }

        if (done < 1) return;
        die.RollStart = -1;
        die.Transform.rotation = die.To;
        die.Transform.localPosition = die.Rest;
        die.NextRoll = time + 3 + (float) random.NextDouble() * 2.5f;
    }

    /// <summary>One of the ways a die can lie, turned a little round the upright so the dice don't look lined up</summary>
    private static Quaternion Lying(int orientation)
    {
        var o = data!.Orientations[orientation];
        return Quaternion.Euler(0, (float) (random.NextDouble() * 50 - 25), 0) * new Quaternion(o.X, o.Y, o.Z, o.W);
    }

    private static GameObject BuildAmbient(Camera camera)
    {
        var height = camera.orthographicSize * 2;
        var width = height * camera.aspect;
        var root = new GameObject(AmbientName);
        root.transform.SetParent(camera.transform, false);
        root.transform.localPosition = new Vector3(0, 0, camera.nearClipPlane + Distance);
        // Its up (Unity's y, Blender's z) towards the camera, its top (Unity's z) to the top of the screen
        root.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        root.transform.localScale = new Vector3(width / ModelWidth, width / ModelWidth, height / ModelHeight);
        Part(root.transform, "Haze", Meshes["Haze"], haze!, Vector3.zero);

        Pips.Clear();
        foreach (var (name, position, size) in data!.Points.Where(point => point.Name.StartsWith("PipSpot")))
        {
            // A little towards the camera, over the haze
            var pip = Part(root.transform, name, Meshes["Twinkle"], seeThrough!, new Vector3(position.X, 0.05f, position.Z));
            pip.localScale = Vector3.zero;
            Pips.Add((pip, size * 1.3f, 5 + (float) random.NextDouble() * 5, (float) random.NextDouble()));
        }

        ModHelper.Msg<CustomBossChallenge>($"The mystery haze round the screen: {width:0} x {height:0}");
        return root;
    }

    private static GameObject BuildTrack(Transform map, Vector3 position)
    {
        var root = new GameObject(TrackName);
        root.transform.SetParent(map, true);
        root.transform.position = position;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * (DieSize / Math.Max(0.0001f, map.lossyScale.x));

        glow = Part(root.transform, "Glow", Meshes["Glow"], seeThrough!, new Vector3(0, 0.01f, 0));
        Dice.Clear();
        var time = Time.time;
        var i = 0;
        foreach (var (name, spot, _) in data!.Points.Where(point => point.Name.StartsWith("DieSpot")))
        {
            var mesh = MeshOf(data.Parts["Die"], 0);
            var rest = new Vector3(spot.X, spot.Y, spot.Z);
            var die = new Die
            {
                Transform = Part(root.transform, name, mesh, solid!, rest), Mesh = mesh, Rest = rest,
                // One after another, not all at once
                NextRoll = time + 1.5f + i++ * 1.3f + (float) random.NextDouble()
            };
            die.Transform.rotation = Lying(random.Next(data.Orientations.Count));
            die.Step = data.NearestOrientation(ToNumerics(die.Transform.rotation));
            mesh.uv = ToIl2Cpp<Vector2>(data.Parts["Die"].UvsFor(die.Step), mesh.vertexCount);
            Dice.Add(die);
        }

        Rolls = 0;
        ModHelper.Msg<CustomBossChallenge>($"The mystery dice at {position}");
        return root;
    }

    /// <summary>
    /// The queue the dice are drawn at, for the tests. They lie on the track where the game's track effect lies, and
    /// are drawn like most of those (Bloonarius's, Diamondback's and Lych's at render queue 1999-2000, under the track's
    /// start arrows; only Phayze's particles are drawn over them, 2026-11-26). The same for every boss, so the order
    /// gives none away.
    /// </summary>
    public static int DiceQueue => solid != null ? solid.renderQueue : -1;

    private static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return part.transform;
    }

    private static Mesh MeshOf(MysteryMeshData.Part part, int step)
    {
        var mesh = new Mesh {name = $"Mystery {part.Name}", hideFlags = HideFlags.DontUnloadUnusedAsset};
        mesh.vertices = ToIl2Cpp<Vector3>(part.Positions, part.VertexCount);
        mesh.uv = ToIl2Cpp<Vector2>(part.UvsFor(step), part.VertexCount);
        mesh.triangles = ToIl2Cpp<int>(part.Indices, part.IndexCount);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Unlit with the ramp for the dice (the bands are baked); Unity's sprite shader, which blends by the ramp's alpha, for the rest</summary>
    private static bool MakeMaterials()
    {
        if (solid != null && seeThrough != null && haze != null) return true;
        if (data == null) return false;
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var sprites = Shader.Find("Sprites/Default");
        if (unlit == null || sprites == null) return false;

        var ramp = new Texture2D(RampWidth, RowHeight * data.Rows.Count, TextureFormat.RGBA32, false)
        {
            name = "Mystery Ramp", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        var pixels = data.RampPixels(RampWidth, RowHeight);
        for (var y = 0; y < ramp.height; y++)
        {
            for (var x = 0; x < RampWidth; x++)
            {
                var i = (y * RampWidth + x) * 4;
                ramp.SetPixel(x, y, new Color32(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]));
            }
        }
        ramp.Apply(false, false);

        solid = new Material(unlit) {name = "Mystery Dice", hideFlags = HideFlags.DontUnloadUnusedAsset, mainTexture = ramp};
        if (solid.HasProperty("_BaseMap")) solid.SetTexture("_BaseMap", ramp);
        if (solid.HasProperty("_BaseColor")) solid.SetColor("_BaseColor", Color.white);
        seeThrough = new Material(sprites)
        {
            name = "Mystery Glow", hideFlags = HideFlags.DontUnloadUnusedAsset, mainTexture = ramp,
            renderQueue = (int) RenderQueue.Transparent
        };
        haze = new Material(seeThrough) {name = "Mystery Haze", hideFlags = HideFlags.DontUnloadUnusedAsset};
        return true;
    }

    private static System.Numerics.Quaternion ToNumerics(Quaternion q) => new(q.x, q.y, q.z, q.w);

    /// <summary>
    /// The game's array of <paramref name="count"/> <typeparamref name="T"/> holding <paramref name="bytes"/>, copied in
    /// one block: one by one goes through the interop for each (17 s for Gildebloon's pile, #25). An Il2Cpp array's
    /// elements start after its header of four pointers.
    /// </summary>
    private static Il2CppStructArray<T> ToIl2Cpp<T>(byte[] bytes, int count) where T : unmanaged
    {
        if (bytes.Length != count * Marshal.SizeOf<T>()) throw new EndOfStreamException($"{bytes.Length} bytes for {count} {typeof(T).Name}");
        var array = new Il2CppStructArray<T>(count);
        Marshal.Copy(bytes, 0, IntPtr.Add(array.Pointer, 4 * IntPtr.Size), bytes.Length);
        if (count > 0 && !EqualityComparer<T>.Default.Equals(array[count - 1],
                MemoryMarshal.Read<T>(bytes.AsSpan(bytes.Length - Marshal.SizeOf<T>()))))
        {
            throw new InvalidDataException($"the copied {typeof(T).Name} array doesn't match");
        }
        return array;
    }
}
