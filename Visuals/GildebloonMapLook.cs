using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The map's boss effects while Gildebloon is the boss shown (#25). Gildebloon is played as Bloonarius, so the game puts
/// Bloonarius's slime on the map; it is hidden, and where its slime lay at the track's start there is a pile of money
/// instead: the one modelled in Blender (<see cref="GildebloonPileMesh"/>), or, without it, coins and money bags made
/// like Gildebloon's own look (<see cref="GildebloonLook"/>). When a game boss is shown again (Advanced Mode), its
/// effects come back.
/// </summary>
public static class GildebloonMapLook
{
    public const string PileName = "GildebloonMoneyPile";

    /// <summary>How wide the pile is on the map, in the game's units</summary>
    public const float PileWidth = 40;

    /// <summary>The game's boss effects hidden while Gildebloon is shown</summary>
    private static readonly List<GameObject> Hidden = new();

    private static GameObject? pile;
    private static readonly List<(Transform Coin, float Phase)> Glints = new();
    private static readonly List<(Transform Sparkle, float Size, float Phase)> Sparkles = new();

    /// <summary>Whether the pile on the map is the modelled one, for tests</summary>
    public static bool Modelled { get; private set; }

    /// <summary>The pile of money on the map now, for tests</summary>
    public static GameObject? Pile => pile != null ? pile : null;

    /// <summary>Hides the game's boss effects on <paramref name="map"/> and puts the pile where the track's one was</summary>
    public static void Show(Transform map, GameObject[] effects, string trackSuffix)
    {
        foreach (var effect in effects)
        {
            if (!effect.activeSelf) continue;
            effect.SetActive(false);
            if (!Hidden.Any(hidden => hidden != null && hidden.Pointer == effect.Pointer)) Hidden.Add(effect);
        }

        if (pile != null || effects.Length == 0) return;
        var track = effects.FirstOrDefault(effect => effect.name.EndsWith(trackSuffix));
        if (track == null) return;
        try
        {
            pile = Build(map, track.transform.position);
            ModHelper.Msg<CustomBossChallenge>($"Gildebloon's pile of money in place of {track.name}");
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Couldn't put Gildebloon's pile of money on the map: {e.Message}");
        }
    }

    /// <summary>Takes the pile away and gives the map its boss effects back</summary>
    public static void Hide()
    {
        if (pile == null && Hidden.Count == 0) return;
        foreach (var effect in Hidden)
        {
            if (effect != null) effect.SetActive(true);
        }
        Hidden.Clear();
        if (pile != null) Object.Destroy(pile);
        pile = null;
        Glints.Clear();
        Sparkles.Clear();
        Modelled = false;
    }

    /// <summary>Called every frame: a few coins on top turn slowly, catching the light</summary>
    public static void Update()
    {
        if (pile == null)
        {
            Glints.Clear();
            Sparkles.Clear();
            return;
        }

        var time = Time.time;
        // The sparkles face the camera and twinkle, one after another
        var camera = Camera.main;
        foreach (var (sparkle, size, phase) in Sparkles)
        {
            if (sparkle == null) continue;
            if (camera != null) sparkle.rotation = Quaternion.FromToRotation(Vector3.up, -camera.transform.forward) * Quaternion.Euler(0, time * 30, 0);
            var twinkle = Mathf.Max(0, Mathf.Sin(time * 2.2f + phase));
            sparkle.localScale = Vector3.one * (size * (0.2f + 0.8f * twinkle));
        }

        foreach (var (coin, phase) in Glints)
        {
            if (coin == null) continue;
            coin.localRotation = Quaternion.Euler(70 + 20 * Mathf.Sin(time * 1.3f + phase), time * 40 + phase * 50, 0);
        }
    }

    private static GameObject Build(Transform map, Vector3 position)
    {
        var root = new GameObject(PileName);
        root.transform.SetParent(map, true);
        root.transform.position = position;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * (PileWidth / 2f / Math.Max(0.0001f, root.transform.parent.lossyScale.x));

        if (GildebloonPileMesh.Ready) return BuildModelled(root);

        var template = Template(map);
        var gold = GildebloonLook.MaterialFor("Gold", template);
        var sack = GildebloonLook.MaterialFor("Sack", template);
        var coinMesh = GildebloonLook.MeshFor("Gold_Coin", "cylinder", [16, 1, 0.15f]);
        var bagMesh = GildebloonLook.MeshFor("Sack_Bag", "sphere", [20, 14]);

        // Coins heaped in a low mound: many at the middle, fewer and lower towards the edge
        var random = new System.Random(25);
        for (var i = 0; i < 90; i++)
        {
            var distance = (float) Math.Sqrt(random.NextDouble());
            var angle = (float) (random.NextDouble() * Math.PI * 2);
            var height = (1 - distance) * 0.55f + (float) random.NextDouble() * 0.05f;
            var coin = Part(root.transform, "Coin", coinMesh, gold,
                new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance * 0.8f),
                Quaternion.Euler((float) (random.NextDouble() * 50 - 25), (float) (random.NextDouble() * 360), (float) (random.NextDouble() * 50 - 25)),
                new Vector3(0.2f, 1.2f, 0.2f));
            if (i % 9 == 0 && distance < 0.6f) Glints.Add((coin, i));
        }

        // Three money bags leaning on the heap
        var bags = new[] {(-0.55f, 0.15f, 0.9f), (0.5f, -0.25f, 0.75f), (0.05f, 0.45f, 0.65f)};
        foreach (var (x, z, size) in bags)
        {
            Part(root.transform, "Bag", bagMesh, sack, new Vector3(x, 0.25f * size, z), Quaternion.Euler(0, x * 90, 8),
                new Vector3(0.32f, 0.38f, 0.32f) * size);
        }

        return root;
    }

    /// <summary>The pile modelled in Blender: one mesh, and its sparkles</summary>
    private static GameObject BuildModelled(GameObject root)
    {
        var material = GildebloonPileMesh.Material!;
        Part(root.transform, "Model", GildebloonPileMesh.Pile!, material, Vector3.zero, Quaternion.identity, Vector3.one);
        if (GildebloonPileMesh.Sparkle != null)
        {
            var i = 0;
            foreach (var (position, size) in GildebloonPileMesh.Sparkles)
            {
                var sparkle = Part(root.transform, "Sparkle", GildebloonPileMesh.Sparkle, material, position, Quaternion.identity,
                    Vector3.one * size);
                Sparkles.Add((sparkle, size, i++ * 1.7f));
            }
        }

        Modelled = true;
        return root;
    }

    private static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation,
        Vector3 scale)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part.transform;
    }

    /// <summary>A material of the map to make the pile's from, so it is lit like the map</summary>
    internal static Material? Template(Transform map)
    {
        foreach (var renderer in map.GetComponentsInChildren<MeshRenderer>(true))
        {
            var material = renderer.sharedMaterial;
            if (material == null || renderer.transform.IsChildOf(map) == false) continue;
            if (material.HasProperty("_BaseMap") || material.HasProperty("_MainTex")) return material;
        }
        return null;
    }
}
