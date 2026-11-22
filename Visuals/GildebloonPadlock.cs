using System;
using BTD_Mod_Helper;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The padlock over a tower Gildebloon seized at a skull (#25): modelled in Blender (tools/blender/gildebloon-padlock.py),
/// read from Bundles/gildebloon-padlock.gba.gz in the body's format and played like the body. A gold padlock facing the
/// camera, a green $ on it (a banana on a Banana Farm's), and a ring of <see cref="Ticks"/> gold ticks round it that go
/// one by one as the tower's time runs out. It drops in (Lock), sways (Hold) and springs open when the tower is let go
/// (Unlock).
/// </summary>
public static class GildebloonPadlock
{
    public const string Resource = "CustomBossChallenge.gildebloon-padlock.gba.gz";

    /// <summary>How many ticks the ring has</summary>
    public const int Ticks = 16;

    private static GildebloonBodyData? data;
    private static Mesh?[] meshes = [];
    private static Material? material;
    private static bool loaded;

    /// <summary>A padlock on the map: its parts in the file's order, its ticks, and what plays its animations</summary>
    public sealed class Instance
    {
        public GameObject Root = null!;
        public Transform?[] Parts = [];
        public Renderer?[] TickRenderers = new Renderer?[Ticks];
        public GildebloonClipPlayer Player = null!;
        public int TicksShown = Ticks;

        /// <summary>How long it has been springing open, or null while it holds</summary>
        public float? Unlocking;
    }

    /// <summary>Whether the modelled padlock is there to use</summary>
    public static bool Ready => data != null && Material();

    /// <summary>Reads the padlock; at the main menu, with Gildebloon's look</summary>
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            using var stream = typeof(GildebloonPadlock).Assembly.GetManifestResourceStream(Resource);
            if (stream == null)
            {
                ModHelper.Warning<CustomBossChallenge>($"{Resource} is missing; seized towers get the older padlock");
                return;
            }

            var read = GildebloonBodyData.Read(stream);
            foreach (var clip in new[] {"Lock", "Hold", "Unlock"})
            {
                if (!read.Clips.ContainsKey(clip)) throw new InvalidOperationException($"it has no animation {clip}");
            }

            meshes = new Mesh?[read.Parts.Count];
            for (var i = 0; i < read.Parts.Count; i++)
            {
                var part = read.Parts[i];
                if (part.VertexCount == 0) continue;
                var mesh = new Mesh {name = $"Gildebloon padlock {part.Name}"};
                mesh.vertices = GildebloonPileMesh.ToIl2Cpp<Vector3>(part.Positions, part.VertexCount);
                mesh.uv = GildebloonPileMesh.ToIl2Cpp<Vector2>(part.UvsFor(0, read.LightSteps), part.VertexCount);
                mesh.triangles = GildebloonPileMesh.ToIl2Cpp<int>(part.Indices, part.IndexCount);
                mesh.RecalculateBounds();
                mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
                meshes[i] = mesh;
            }

            data = read;
        }
        catch (Exception e)
        {
            data = null;
            ModHelper.Warning<CustomBossChallenge>($"The padlock could not be loaded: {e.Message}");
        }
    }

    /// <summary>
    /// Builds a padlock under <paramref name="parent"/>, <paramref name="size"/> of its units for one of the model's
    /// (the lock's body is one wide), with the banana for a Banana Farm and the $ otherwise, dropping in. Null without
    /// the file.
    /// </summary>
    public static Instance? Build(Transform parent, float size, bool farm, int layer)
    {
        if (!Ready) return null;
        var model = data!;
        var root = new GameObject(GildebloonSeizeLook.PadlockName);
        root.transform.SetParent(parent, false);
        root.layer = layer;
        root.transform.localScale = Vector3.one * size;
        var instance = new Instance {Root = root, Parts = new Transform?[model.Parts.Count]};
        for (var i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            var made = new GameObject(part.Name) {layer = layer};
            made.transform.SetParent(part.Parent >= 0 ? instance.Parts[part.Parent] : root.transform, false);
            instance.Parts[i] = made.transform;
            if (meshes[i] is not { } mesh) continue;
            // a farm's padlock holds a banana, any other a $
            if (part.Name.StartsWith("LockBanana") ? !farm : part.Name == "LockDollar" && farm) continue;
            made.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = made.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = material;
            if (part.Name.StartsWith("Tick") && int.TryParse(part.Name.Substring(4), out var tick) && tick is >= 0 and < Ticks)
                instance.TickRenderers[tick] = renderer;
        }

        instance.Player = new GildebloonClipPlayer(model, "Hold");
        instance.Player.Play("Lock");
        Pose(instance);
        return instance;
    }

    /// <summary>How many of the ring's ticks show for a share of the time left: all at the start, none at the end</summary>
    public static int TicksFor(float share) => Math.Clamp((int) Math.Ceiling(share * Ticks), 0, Ticks);

    /// <summary>Moves a padlock on: its animation, the ticks for the time it has left, and facing the camera</summary>
    public static void Update(Instance instance, float share, float delta)
    {
        if (instance.Unlocking is { } unlocking)
        {
            // Unlock holds its last frame: the player would go back to Hold after it
            var left = data!.Clips["Unlock"].Seconds - unlocking;
            delta = Math.Max(0, Math.Min(delta, left - 0.001f));
            instance.Unlocking = unlocking + delta;
        }
        instance.Player.Advance(delta);
        Pose(instance);
        var shown = TicksFor(share);
        if (shown != instance.TicksShown)
        {
            instance.TicksShown = shown;
            // the last ticks go first: the ring empties anticlockwise back to the top
            for (var i = 0; i < Ticks; i++)
            {
                if (instance.TickRenderers[i] is { } tick) tick.enabled = i < shown;
            }
        }

        // its face (-z) towards the camera, upright on the screen
        if (Camera.main is { } camera)
            instance.Root.transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
    }

    /// <summary>Springs it open; <see cref="Opened"/> says when it is done</summary>
    public static void Unlock(Instance instance)
    {
        if (instance.Unlocking != null) return;
        instance.Player.Play("Unlock");
        instance.Unlocking = 0;
    }

    /// <summary>Whether its Unlock has played to its end</summary>
    public static bool Opened(Instance instance) =>
        data == null || instance.Unlocking is { } unlocking && unlocking >= data.Clips["Unlock"].Seconds - 0.002f;

    private static void Pose(Instance instance)
    {
        for (var i = 0; i < instance.Parts.Length; i++)
        {
            if (instance.Parts[i] is not { } transform) continue;
            var pose = instance.Player.PoseOf(i);
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
        // drawn after the map, the towers and the bloons, so nothing covers it (like the ring of a Repossession)
        material = new Material(shader)
        {
            name = "Gildebloon Padlock", hideFlags = HideFlags.DontUnloadUnusedAsset, mainTexture = ramp, renderQueue = 3500
        };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", ramp);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        return true;
    }

    /// <summary>Takes a padlock away</summary>
    public static void Destroy(Instance instance)
    {
        if (instance.Root != null) Object.Destroy(instance.Root);
    }
}
