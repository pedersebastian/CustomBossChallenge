using System;
using BTD_Mod_Helper;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Gildebloon's effect round the map (#25): every game boss has one (Bloonarius's slime creeping in from the edges,
/// <c>BloonariusAmbientMapFx</c>), which <see cref="GildebloonMapLook"/> hides. In its place gold oozes in from the
/// edges, with drops, coins, gold bars and sparkles, slowly swelling: modelled in Blender (tools/blender/gildebloon-map.py),
/// read from Bundles/gildebloon-map.gba.gz (tools/blender/export-map.py, the body's format) and played like the body.
/// It is stretched over the area the hidden effect covered.
/// </summary>
public static class GildebloonMapOoze
{
    public const string Resource = "CustomBossChallenge.gildebloon-map.gba.gz";
    public const string RootName = "GildebloonMapOoze";

    /// <summary>The map the effect was made for in Blender, in its units: it is stretched from this to the game's</summary>
    private const float ModelWidth = 18, ModelDepth = 13.6f;

    private static GildebloonBodyData? data;
    private static Mesh?[] meshes = [];
    private static Material? material, seeThrough;

    /// <summary>The map it was put round: when that map is gone (another match), so is the gold</summary>
    private static Transform? forMap;
    private static bool loaded;

    private static GameObject? shown;
    private static Transform?[] parts = [];
    private static GildebloonClipPlayer? player;

    /// <summary>The effect on the map now, for tests</summary>
    public static GameObject? Shown => shown != null ? shown : null;

    /// <summary>How far in front of the camera's near plane it sits: room for its bead to rise towards the camera</summary>
    private const float Distance = 20;

    /// <summary>The view it frames now (width and height in its size), for tests and the log</summary>
    public static Bounds Area { get; private set; }

    /// <summary>Reads the effect; at the main menu, with Gildebloon's look</summary>
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var stream = typeof(GildebloonMapOoze).Assembly.GetManifestResourceStream(Resource);
            if (stream == null)
            {
                ModHelper.Warning<CustomBossChallenge>($"{Resource} is missing; Gildebloon has no effect round the map");
                return;
            }

            var read = GildebloonBodyData.Read(stream);
            meshes = new Mesh?[read.Parts.Count];
            var vertices = 0;
            for (var i = 0; i < read.Parts.Count; i++)
            {
                var part = read.Parts[i];
                if (part.VertexCount == 0) continue;
                var mesh = new Mesh {name = $"Gildebloon map {part.Name}"};
                if (part.VertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = GildebloonPileMesh.ToIl2Cpp<Vector3>(part.Positions, part.VertexCount);
                mesh.uv = GildebloonPileMesh.ToIl2Cpp<Vector2>(part.UvsFor(0, read.LightSteps), part.VertexCount);
                mesh.triangles = GildebloonPileMesh.ToIl2Cpp<int>(part.Indices, part.IndexCount);
                mesh.RecalculateBounds();
                mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
                meshes[i] = mesh;
                vertices += part.VertexCount;
            }

            if (!read.Clips.ContainsKey("Ooze")) throw new InvalidOperationException("it has no animation Ooze");
            data = read;
            ModHelper.Msg<CustomBossChallenge>(
                $"Gildebloon's gold round the map loaded: {read.Parts.Count} parts, {vertices} vertices in {watch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            data = null;
            ModHelper.Warning<CustomBossChallenge>($"Gildebloon's gold round the map could not be loaded: {e.Message}");
        }
    }

    /// <summary>
    /// Puts the gold round the edges of what <paramref name="camera"/> shows: on the camera, facing it, as big as its
    /// view. On the map's ground it went askew and hid under walls and raised ground (2026-11-21); on the camera it is
    /// a frame that always fits the screen.
    /// </summary>
    public static void Show(Camera camera, Transform map)
    {
        if (shown != null || data == null || !Material() || !camera.orthographic) return;
        var height = camera.orthographicSize * 2;
        var width = height * camera.aspect;
        var root = new GameObject(RootName);
        root.transform.SetParent(camera.transform, false);
        // In front of the map, behind the HUD (a canvas of its own); its bead rises towards the camera
        root.transform.localPosition = new Vector3(0, 0, camera.nearClipPlane + Distance);
        // Its up (Unity's y, Blender's z) towards the camera, its top (Unity's z) to the top of the screen
        root.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        root.transform.localScale = new Vector3(width / ModelWidth, width / ModelWidth, height / ModelDepth);

        parts = new Transform?[data.Parts.Count];
        for (var i = 0; i < data.Parts.Count; i++)
        {
            var part = data.Parts[i];
            var made = new GameObject(part.Name);
            made.transform.SetParent(part.Parent >= 0 ? parts[part.Parent] : root.transform, false);
            parts[i] = made.transform;
            if (meshes[i] is not { } mesh || part.SeeThrough && seeThrough == null) continue;
            made.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = made.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = part.SeeThrough ? seeThrough : material;
        }

        shown = root;
        forMap = map;
        Area = new Bounds(Vector3.zero, new Vector3(width, 0, height));
        player = new GildebloonClipPlayer(data, "Ooze");
        Pose();
        ModHelper.Msg<CustomBossChallenge>($"Gildebloon's gold round the screen: {width:0} x {height:0}");
    }

    /// <summary>Takes the gold away</summary>
    public static void Hide()
    {
        if (shown != null) Object.Destroy(shown);
        shown = null;
        forMap = null;
        player = null;
        parts = [];
    }

    /// <summary>Called every frame: it swells, its drops stretch, coins rock, sparkles twinkle</summary>
    public static void Update()
    {
        if (shown == null || player == null) return;
        // It is on the camera, which stays from match to match: it goes with the map it was put round
        if (forMap == null || !GildebloonMapLook.Showing)
        {
            Hide();
            return;
        }

        player.Advance(Time.deltaTime);
        Pose();
    }

    private static void Pose()
    {
        if (data == null || player == null) return;
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] is not { } transform) continue;
            var pose = player.PoseOf(i);
            transform.localPosition = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
            transform.localRotation = new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W);
            transform.localScale = new Vector3(pose.Scale.X, pose.Scale.Y, pose.Scale.Z);
        }
    }

    private static bool Material()
    {
        if (material != null) return true;
        if (data == null) return false;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return false;
        var ramp = GildebloonBody.Ramp(data);
        material = new Material(shader) {name = "Gildebloon Map Ooze", hideFlags = HideFlags.DontUnloadUnusedAsset, mainTexture = ramp};
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", ramp);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        // the glow is see-through: Unity's sprite shader blends by the ramp's alpha (as the body's Vault and shadow)
        if (Shader.Find("Sprites/Default") is { } sprites)
        {
            seeThrough = new Material(sprites)
            {
                name = "Gildebloon Map Glow", hideFlags = HideFlags.DontUnloadUnusedAsset, mainTexture = ramp,
                renderQueue = (int) RenderQueue.Transparent
            };
        }
        return true;
    }
}
