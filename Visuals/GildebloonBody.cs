using System;
using BTD_Mod_Helper;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Gildebloon's body as modelled in Blender, with its animations (#25): read from Bundles/gildebloon-body.gba.gz
/// (<see cref="GildebloonBodyData"/>) at the main menu, built as the game's meshes here, posed every frame by a
/// <see cref="GildebloonClipPlayer"/>. Nothing can come from an asset bundle (57.4), so, like the pile of money
/// (<see cref="GildebloonPileMesh"/>), the toon bands are baked into UVs on a ramp texture made here. Without the file
/// <see cref="GildebloonLook"/> builds its older code-made body.
/// </summary>
public static class GildebloonBody
{
    public const string Resource = "CustomBossChallenge.gildebloon-body.gba.gz";

    /// <summary>How wide the body is in its own units, bags and coin ring included</summary>
    public const float ModelWidth = 3.2f;

    /// <summary>
    /// Which way its face looks: Blender's front (-Y) comes out as -Z; the game turns a bloon's display to face where it
    /// goes. Not seen in the game yet (2026-11-21): if it backs along the track, this is 0.
    /// </summary>
    public const float FacingYaw = 180;

    private const int RampWidth = 64;
    private const int RowHeight = 4;

    public static GildebloonBodyData? Data { get; private set; }
    private static Mesh?[] meshes = [];

    /// <summary>Per part, its UVs for each way to face (see <see cref="GildebloonBodyData.LightSteps"/>)</summary>
    private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2>[]?[] uvSets = [];

    /// <summary>The light step the shared meshes have now</summary>
    private static int shownStep;
    private static Material? opaque, seeThrough;
    private static bool loaded;
    private static int rootPart = -1;

    /// <summary>Whether the modelled body is there to use</summary>
    public static bool Ready => Data != null && Materials();

    /// <summary>A body on a bloon: its parts, in the file's order, and what plays its animations</summary>
    public sealed class Instance
    {
        public GameObject Root = null!;
        public Transform?[] Parts = [];
        public GildebloonClipPlayer Player = null!;
        public bool Elite;
    }

    /// <summary>Reads the body; at the main menu, with Gildebloon's look</summary>
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var stream = typeof(GildebloonBody).Assembly.GetManifestResourceStream(Resource);
            if (stream == null)
            {
                ModHelper.Warning<CustomBossChallenge>($"{Resource} is missing; Gildebloon has its older body");
                return;
            }

            var data = GildebloonBodyData.Read(stream);
            meshes = new Mesh?[data.Parts.Count];
            uvSets = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2>[]?[data.Parts.Count];
            shownStep = 0;
            var vertices = 0;
            for (var i = 0; i < data.Parts.Count; i++)
            {
                var part = data.Parts[i];
                if (part.VertexCount == 0) continue;
                var mesh = new Mesh {name = $"Gildebloon {part.Name}"};
                if (part.VertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = GildebloonPileMesh.ToIl2Cpp<Vector3>(part.Positions, part.VertexCount);
                var sets = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2>[data.LightSteps];
                for (var step = 0; step < data.LightSteps; step++)
                {
                    sets[step] = GildebloonPileMesh.ToIl2Cpp<Vector2>(part.UvsFor(step, data.LightSteps), part.VertexCount);
                }

                uvSets[i] = sets;
                mesh.uv = sets[0];
                mesh.triangles = GildebloonPileMesh.ToIl2Cpp<int>(part.Indices, part.IndexCount);
                mesh.RecalculateBounds();
                mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
                meshes[i] = mesh;
                vertices += part.VertexCount;
            }

            rootPart = data.PartIndex("Gildebloon");
            if (rootPart < 0) throw new InvalidOperationException("it has no part Gildebloon");
            Data = data;
            ModHelper.Msg<CustomBossChallenge>(
                $"Gildebloon's body loaded: {data.Parts.Count} parts, {vertices} vertices, {data.Clips.Count} animations in {watch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            Data = null;
            ModHelper.Warning<CustomBossChallenge>($"Gildebloon's body could not be loaded: {e.Message}; it has its older body");
        }
    }

    /// <summary>
    /// Builds the body under a bloon's display, <paramref name="width"/> of the game's units across, its hovering part
    /// on the display's centre
    /// </summary>
    public static Instance? Build(Transform display, bool elite, float width)
    {
        if (!Ready) return null;
        var data = Data!;
        var root = new GameObject(GildebloonLook.RootName);
        root.transform.SetParent(display, false);
        var parts = new Transform?[data.Parts.Count];
        for (var i = 0; i < data.Parts.Count; i++)
        {
            var part = data.Parts[i];
            var made = new GameObject(part.Name);
            made.transform.SetParent(part.Parent >= 0 ? parts[part.Parent] : root.transform, false);
            parts[i] = made.transform;
            // See-through parts (the Vault's shell, the shadow) only with a material that blends: the game's URP
            // Unlit set to blend came out as a dark, solid film (2026-11-21)
            if (meshes[i] is not { } mesh || part.SeeThrough && seeThrough == null) continue;
            made.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = made.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = part.SeeThrough ? seeThrough : opaque;
        }

        var scale = display.lossyScale.x;
        var size = width / ModelWidth / (scale > 0 ? scale : 1);
        root.transform.localScale = Vector3.one * size;
        root.transform.localRotation = Quaternion.Euler(0, FacingYaw, 0);
        // The file's base is the ground under it; the display is where it hovers
        var hover = data.Parts[rootPart].Rest.Position;
        root.transform.localPosition = root.transform.localRotation * -new Vector3(hover.X, hover.Y, hover.Z) * size;

        var instance = new Instance {Root = root, Parts = parts, Player = new GildebloonClipPlayer(data), Elite = elite};
        Apply(instance);
        return instance;
    }

    /// <summary>How long the Tax Collector (the gold MOAB of Dispatch) is in the body's units, fins included</summary>
    public const float CollectorModelLength = 1.3f;

    /// <summary>
    /// Builds a Tax Collector's look under its display: the gold MOAB Gildebloon sends out in Dispatch, the parts under
    /// <c>CollectorProxy</c>, <paramref name="length"/> of the game's units long. Null without the body.
    /// </summary>
    public static GameObject? BuildCollector(Transform display, float length)
    {
        if (!Ready) return null;
        var data = Data!;
        var proxy = data.PartIndex("CollectorProxy");
        if (proxy < 0) return null;
        var root = new GameObject(GildebloonLook.RootName + "Collector");
        root.transform.SetParent(display, false);
        var made = new Transform?[data.Parts.Count];
        made[proxy] = root.transform;
        for (var i = proxy + 1; i < data.Parts.Count; i++)
        {
            var part = data.Parts[i];
            if (part.Parent < 0 || made[part.Parent] == null) continue;
            var go = new GameObject(part.Name);
            go.transform.SetParent(made[part.Parent], false);
            var rest = part.Rest;
            go.transform.localPosition = new Vector3(rest.Position.X, rest.Position.Y, rest.Position.Z);
            go.transform.localRotation = new Quaternion(rest.Rotation.X, rest.Rotation.Y, rest.Rotation.Z, rest.Rotation.W);
            go.transform.localScale = new Vector3(rest.Scale.X, rest.Scale.Y, rest.Scale.Z);
            made[i] = go.transform;
            if (meshes[i] is not { } mesh || part.SeeThrough) continue;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = opaque;
        }

        var scale = display.lossyScale.x;
        root.transform.localScale = Vector3.one * (length / CollectorModelLength / (scale > 0 ? scale : 1));
        root.transform.localRotation = Quaternion.Euler(0, FacingYaw, 0);
        root.transform.localPosition = Vector3.zero;
        return root;
    }

    /// <summary>Puts every part where the animation playing has it now. Elite's band and crown show on an Elite unless an
    /// animation moves them itself.</summary>
    public static void Apply(Instance instance)
    {
        var data = Data;
        if (data == null) return;
        Relight(data, instance);
        for (var i = 0; i < instance.Parts.Length; i++)
        {
            var transform = instance.Parts[i];
            if (transform == null) continue;
            var pose = instance.Player.PoseOf(i);
            var part = data.Parts[i];
            var scale = pose.Scale;
            if (part.Elite && part.Parent >= 0 && !data.Parts[part.Parent].Elite)
            {
                // an Elite group's own scale: shown on an Elite, hidden otherwise, unless the animation says
                if (!instance.Player.Moves(i)) scale = instance.Elite ? System.Numerics.Vector3.One : System.Numerics.Vector3.Zero;
            }

            transform.localPosition = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
            transform.localRotation = new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W);
            transform.localScale = new Vector3(scale.X, scale.Y, scale.Z);
        }
    }

    /// <summary>
    /// Keeps the light where it is on the screen as the body turns: the bands are baked for every 30 degrees, and the
    /// meshes get the set nearest to how far the body is turned from facing the camera
    /// </summary>
    private static void Relight(GildebloonBodyData data, Instance instance)
    {
        var camera = Camera.main;
        if (camera == null || instance.Root == null || data.LightSteps < 2) return;
        var front = instance.Root.transform.TransformDirection(new Vector3(0, 0, -1)); // Blender's front, -Y
        var toward = -camera.transform.forward;
        front.y = 0;
        toward.y = 0;
        if (front.sqrMagnitude < 1e-6f || toward.sqrMagnitude < 1e-6f) return;
        var step = data.LightStepFor(Vector3.SignedAngle(toward, front, Vector3.up));
        if (step == shownStep) return;
        shownStep = step;
        for (var i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] is { } mesh && uvSets[i] is { } sets) mesh.uv = sets[step];
        }
    }

    /// <summary>The material with the ramp</summary>
    private static bool Materials()
    {
        if (opaque != null) return true;
        if (Data == null) return false;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return false;
        var ramp = Ramp(Data);
        opaque = new Material(shader) {name = "Gildebloon Body", hideFlags = HideFlags.DontUnloadUnusedAsset, mainTexture = ramp};
        if (opaque.HasProperty("_BaseMap")) opaque.SetTexture("_BaseMap", ramp);
        if (opaque.HasProperty("_BaseColor")) opaque.SetColor("_BaseColor", Color.white);
        // Unity's own sprite shader blends by the texture's alpha, as any game with sprites has it
        var sprites = Shader.Find("Sprites/Default");
        if (sprites != null)
        {
            seeThrough = new Material(sprites)
            {
                name = "Gildebloon Body See-through", hideFlags = HideFlags.DontUnloadUnusedAsset, mainTexture = ramp,
                renderQueue = (int) RenderQueue.Transparent
            };
        }
        else ModHelper.Warning<CustomBossChallenge>("Sprites/Default isn't there: Gildebloon's shadow and Vault aren't drawn");
        return true;
    }

    private static Texture2D Ramp(GildebloonBodyData data)
    {
        var texture = new Texture2D(RampWidth, data.Rows.Count * RowHeight, TextureFormat.RGBA32, false)
        {
            name = "Gildebloon Body Ramp",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        for (var row = 0; row < data.Rows.Count; row++)
        {
            var r = data.Rows[row];
            for (var x = 0; x < RampWidth; x++)
            {
                var lit = (x + 0.5f) / RampWidth;
                var rgb = lit >= r.LightCut ? r.Light : lit >= r.Cut ? r.Base : r.Shade;
                var colour = new Color(rgb[0] / 255f, rgb[1] / 255f, rgb[2] / 255f, r.AlphaAt(lit));
                for (var y = 0; y < RowHeight; y++) texture.SetPixel(x, row * RowHeight + y, colour);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    /// <summary>Takes a body off its bloon</summary>
    public static void Destroy(Instance? instance)
    {
        if (instance?.Root != null) Object.Destroy(instance.Root);
    }
}
