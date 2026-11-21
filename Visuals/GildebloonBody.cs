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
            var vertices = 0;
            for (var i = 0; i < data.Parts.Count; i++)
            {
                var part = data.Parts[i];
                if (part.VertexCount == 0) continue;
                var mesh = new Mesh {name = $"Gildebloon {part.Name}"};
                if (part.VertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = GildebloonPileMesh.ToIl2Cpp<Vector3>(part.Positions, part.VertexCount);
                mesh.uv = GildebloonPileMesh.ToIl2Cpp<Vector2>(part.Uvs, part.VertexCount);
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
            if (meshes[i] is not { } mesh) continue;
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

    /// <summary>Puts every part where the animation playing has it now. Elite's band and crown show on an Elite unless an
    /// animation moves them itself.</summary>
    public static void Apply(Instance instance)
    {
        var data = Data;
        if (data == null) return;
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

    /// <summary>The opaque material with the ramp, and a see-through one for the Vault and the shadow</summary>
    private static bool Materials()
    {
        if (opaque != null && seeThrough != null) return true;
        if (Data == null) return false;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return false;
        var ramp = Ramp(Data);
        opaque = new Material(shader) {name = "Gildebloon Body", hideFlags = HideFlags.DontUnloadUnusedAsset};
        seeThrough = new Material(shader) {name = "Gildebloon Body See-through", hideFlags = HideFlags.DontUnloadUnusedAsset};
        foreach (var material in new[] {opaque, seeThrough})
        {
            material.mainTexture = ramp;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", ramp);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        }

        seeThrough.SetFloat("_Surface", 1);
        seeThrough.SetFloat("_Blend", 0);
        seeThrough.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
        seeThrough.SetFloat("_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
        seeThrough.SetFloat("_ZWrite", 0);
        seeThrough.SetOverrideTag("RenderType", "Transparent");
        seeThrough.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        seeThrough.renderQueue = (int) RenderQueue.Transparent;
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
                var colour = new Color(rgb[0] / 255f, rgb[1] / 255f, rgb[2] / 255f, r.Alpha);
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
