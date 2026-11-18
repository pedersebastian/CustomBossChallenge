using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Simulation.Bloons;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Gildebloon's own look (#25): the gold vault-blimp, built from the layout in Bundles/gildebloon.layout.json and the
/// textures in Images/ (both made by UnityProject's BuildGildebloon), takes the place of the Bloonarius body its
/// models come with. Its parts are moved here, looked up by name: the dial spins, the coins circle it, the money bags
/// shrink as its health goes, and the vault door bursts open with a spray of coins at each skull.
/// <br/>
/// If the layout or the textures are missing, the Bloonarius body is tinted gold instead.
/// </summary>
public static class GildebloonLook
{
    public const string LayoutResource = "CustomBossChallenge.gildebloon.layout.json";
    public const string RootName = "GildebloonLook";

    /// <summary>How wide the look is in its own units: its ring of coins</summary>
    public const float ModelWidth = 3.8f;

    /// <summary>How wide it is on the map, in the game's units: about as wide as Bloonarius</summary>
    public const float MapWidth = 45;

    /// <summary>The textures the parts get, by the start of their mesh's name ("Gold_Coin" gets Gold)</summary>
    private static readonly string[] TextureNames = {"Gold", "GoldElite", "Iron", "IronElite", "Sack"};

    private static readonly Dictionary<string, Texture2D> Textures = new();

    /// <summary>Materials made in the game for the textures, kept for the session</summary>
    private static readonly Dictionary<string, Material> Materials = new();

    private static bool loaded;
    /// <summary>The parts of each look ("Gildebloon", "GildebloonElite") from Bundles/gildebloon.layout.json</summary>
    private static readonly Dictionary<string, Newtonsoft.Json.Linq.JArray> Layouts = new();

    private static readonly Dictionary<string, Mesh> Meshes = new();

    /// <summary>Why the model isn't used, or null when it is</summary>
    public static string? Problem { get; private set; }

    private sealed class Shown
    {
        public Bloon Bloon = null!;
        public IntPtr Graphic;
        public GameObject? Look;
        public readonly List<Renderer> Hidden = new();
        public Transform? Dial, Door, Ring, BagLeft, BagRight;
        public Vector3 BagScale, DoorPosition;
        public int Skulls;
        public float BurstStart = -100;
        public readonly List<(Transform Coin, Vector3 Velocity)> Spray = new();
        public bool Tinted;
    }

    private static Shown? shown;

    /// <summary>The look on the Gildebloon alive, for tests</summary>
    public static GameObject? Look => shown?.Look;

    /// <summary>Whether the Gildebloon alive is tinted gold because the model couldn't be loaded, for tests</summary>
    public static bool Tinted => shown?.Tinted ?? false;

    /// <summary>Called every frame</summary>
    public static void Update()
    {
        var manager = InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager;
        var boss = manager?.CurrentBoss;
        if (!Experimental.Gildebloon || !CustomBossState.IsActive || boss == null || !Gildebloon.IsGildebloon(boss.bloonModel?.id))
        {
            Clear();
            return;
        }

        var graphic = boss.GetUnityDisplayNode();
        if (graphic == null) return;

        if (shown == null || shown.Bloon.Pointer != boss.Pointer || shown.Graphic != graphic.Pointer)
        {
            Clear();
            shown = new Shown {Bloon = boss, Graphic = graphic.Pointer};
            Attach(shown, graphic.gameObject, boss.bloonModel.id.Contains(TierBossMath.EliteSuffix));
        }

        Animate(shown, boss);
    }

    private static void Attach(Shown look, GameObject graphic, bool elite)
    {
        var renderers = graphic.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList();
        if (!loaded) Load();
        var layout = Layouts.TryGetValue(elite ? "GildebloonElite" : "Gildebloon", out var own) ? own
            : Layouts.TryGetValue("Gildebloon", out var normal) ? normal : null;
        if (Problem != null || layout == null || renderers.Count == 0)
        {
            Tint(look, renderers);
            return;
        }

        // Sized and placed like the body it replaces
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);

        var instance = Build(layout, graphic.transform);
        var parts = instance.GetComponentsInChildren<Renderer>(true);
        if (parts.Length == 0)
        {
            Object.Destroy(instance);
            Tint(look, renderers);
            return;
        }

        // The parts get materials made here, from the body's own material where it has one, so they are lit like the
        // game's bloons
        var template = renderers.Select(r => r.sharedMaterial).FirstOrDefault(m => m != null);
        foreach (var renderer in parts)
        {
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            var key = mesh == null ? "Gold" : mesh.name.Split('_')[0];
            renderer.sharedMaterial = MaterialFor(key, template);
        }

        // As wide on the map as Bloonarius, centred on the boss. The body's own bounds can't be used: its 70-odd
        // meshes include parts far outside it. The look is ModelWidth wide in its own units.
        var scale = graphic.transform.lossyScale.x;
        if (scale > 0) instance.transform.localScale = Vector3.one * (MapWidth / ModelWidth / scale);
        instance.transform.localPosition = Vector3.zero;

        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
            look.Hidden.Add(renderer);
        }

        look.Look = instance;
        look.Dial = Find(instance, "Dial");
        look.Door = Find(instance, "VaultDoor");
        look.Ring = Find(instance, "CoinRing");
        look.BagLeft = Find(instance, "BagLeft");
        look.BagRight = Find(instance, "BagRight");
        if (look.BagLeft != null) look.BagScale = look.BagLeft.localScale;
        if (look.Door != null) look.DoorPosition = look.Door.localPosition;
        look.Skulls = SkullsPassed(look.Bloon);
    }

    /// <summary>Builds the parts of a look under <paramref name="parent"/>, from its layout and the meshes made for it</summary>
    private static GameObject Build(Newtonsoft.Json.Linq.JArray layout, Transform parent)
    {
        var made = new List<GameObject>();
        foreach (var token in layout)
        {
            var part = new GameObject((string?) token["name"] ?? "Part");
            var index = (int?) token["parent"] ?? -1;
            part.transform.SetParent(index >= 0 && index < made.Count ? made[index].transform : parent, false);
            var p = token["p"]!;
            var r = token["r"]!;
            var sc = token["s"]!;
            part.transform.localPosition = new Vector3((float) p[0]!, (float) p[1]!, (float) p[2]!);
            part.transform.localRotation = new Quaternion((float) r[0]!, (float) r[1]!, (float) r[2]!, (float) r[3]!);
            part.transform.localScale = new Vector3((float) sc[0]!, (float) sc[1]!, (float) sc[2]!);
            if ((string?) token["mesh"] is {Length: > 0} name)
            {
                var mesh = MeshFor(name, (string?) token["kind"] ?? "box",
                    token["args"] is Newtonsoft.Json.Linq.JArray args ? args.Select(arg => (float) arg).ToArray() : []);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            made.Add(part);
        }

        made[0].name = RootName;
        return made[0];
    }

    private static Mesh MeshFor(string name, string kind, float[] args)
    {
        if (Meshes.TryGetValue(name, out var mesh) && mesh != null) return mesh;
        mesh = GildebloonMeshes.Make(kind, args);
        mesh.name = name;
        Meshes[name] = mesh;
        return mesh;
    }

    private static Material MaterialFor(string key, Material? template)
    {
        if (Materials.TryGetValue(key, out var made) && made != null) return made;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = template != null ? new Material(template) : new Material(shader);
        material.name = $"Gildebloon {key}";
        material.hideFlags = HideFlags.DontUnloadUnusedAsset;
        if (Textures.TryGetValue(key, out var texture))
        {
            material.mainTexture = texture;
            foreach (var property in new[] {"_BaseMap", "_MainTex", "_BaseTexture"})
            {
                if (material.HasProperty(property)) material.SetTexture(property, texture);
            }
        }

        foreach (var property in new[] {"_BaseColor", "_Color"})
        {
            if (material.HasProperty(property)) material.SetColor(property, Color.white);
        }

        Materials[key] = material;
        return material;
    }

    private static Transform? Find(GameObject root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    /// <summary>Without the model the Bloonarius body is tinted gold</summary>
    private static void Tint(Shown look, List<Renderer> renderers)
    {
        var block = new MaterialPropertyBlock();
        var gold = new Color(1f, 0.78f, 0.2f);
        foreach (var renderer in renderers)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", gold);
            block.SetColor("_BaseColor", gold);
            renderer.SetPropertyBlock(block);
        }

        look.Tinted = true;
    }

    private static void Animate(Shown look, Bloon boss)
    {
        if (look.Look == null) return;
        var time = Time.time;
        var delta = Time.deltaTime * Math.Max(1, Time.timeScale);
        if (look.Dial != null) look.Dial.Rotate(0, 120 * delta, 0, Space.Self);
        if (look.Ring != null) look.Ring.Rotate(0, -45 * delta, 0, Space.Self);

        // The bags empty as its health goes
        var health = boss.bloonModel.maxHealth > 0 ? Math.Clamp(boss.health / (float) boss.bloonModel.maxHealth, 0, 1) : 1;
        var bag = look.BagScale * (0.55f + 0.45f * health);
        if (look.BagLeft != null) look.BagLeft.localScale = bag;
        if (look.BagRight != null) look.BagRight.localScale = bag;

        // At a skull the door bursts open and coins spray out
        var skulls = SkullsPassed(boss);
        if (skulls > look.Skulls)
        {
            look.Skulls = skulls;
            look.BurstStart = time;
            Spray(look);
        }

        if (look.Door != null)
        {
            var age = time - look.BurstStart;
            var open = age < 1.2f ? Mathf.Sin(Mathf.Clamp01(age / 1.2f) * Mathf.PI) : 0;
            look.Door.localPosition = look.DoorPosition + Vector3.up * (0.35f * open);
            look.Door.localRotation = Quaternion.Euler(0, 0, -70 * open);
        }

        for (var i = look.Spray.Count - 1; i >= 0; i--)
        {
            var (coin, velocity) = look.Spray[i];
            if (coin == null || time - look.BurstStart > 1.5f)
            {
                if (coin != null) Object.Destroy(coin.gameObject);
                look.Spray.RemoveAt(i);
                continue;
            }

            velocity += Vector3.down * (4 * delta);
            look.Spray[i] = (coin, velocity);
            coin.localPosition += velocity * delta;
            coin.Rotate(400 * delta, 0, 0, Space.Self);
        }
    }

    private static void Spray(Shown look)
    {
        var coin = look.Ring != null && look.Ring.childCount > 0 ? look.Ring.GetChild(0) : null;
        if (coin == null || look.Look == null) return;
        for (var i = 0; i < 14; i++)
        {
            var angle = i * Mathf.PI * 2 / 14;
            var copy = Object.Instantiate(coin.gameObject, look.Look.transform, false).transform;
            copy.localPosition = new Vector3(0, 0.9f, 0);
            look.Spray.Add((copy, new Vector3(Mathf.Cos(angle) * 2.2f, 2.5f, Mathf.Sin(angle) * 2.2f)));
        }
    }

    private static int SkullsPassed(Bloon boss)
    {
        var skulls = SkullModifier.GetSkulls(boss.bloonModel);
        if (skulls == null || boss.bloonModel.maxHealth <= 0) return 0;
        var share = boss.health / (double) boss.bloonModel.maxHealth;
        return skulls.Count(skull => share <= skull);
    }

    /// <summary>Takes the look off, before the game reuses the display for another bloon</summary>
    private static void Clear()
    {
        if (shown == null) return;
        try
        {
            if (shown.Look != null) Object.Destroy(shown.Look);
            foreach (var renderer in shown.Hidden)
            {
                if (renderer != null) renderer.enabled = true;
            }

            if (shown.Tinted && shown.Bloon.GetUnityDisplayNode() is { } graphic)
            {
                foreach (var renderer in graphic.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(null);
            }
        }
        catch (Exception)
        {
            // The display went with the match
        }

        shown = null;
    }

    // ---------- Loading ----------

    /// <summary>
    /// Reads the layout and the textures. Nothing comes from a bundle: loading a prefab, mesh or texture from one
    /// crashes the game (57.4), so the meshes are made here (<see cref="GildebloonMeshes"/>) and the textures are
    /// pictures of the mod like its icon. Done at the main menu.
    /// </summary>
    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            ReadLayouts();
            foreach (var name in TextureNames)
            {
                var texture = BTD_Mod_Helper.Api.ModContent.GetTexture<CustomBossChallenge>($"Gildebloon{name}Texture");
                if (texture == null) continue;
                texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Textures[name] = texture;
            }

            if (!Layouts.ContainsKey("Gildebloon")) Problem = $"{LayoutResource} is missing";
            else if (Textures.Count < TextureNames.Length) Problem = "Gildebloon's textures are missing";
        }
        catch (Exception e)
        {
            Problem = $"Gildebloon's look could not be loaded: {e.Message}";
        }

        if (Problem != null) ModHelper.Warning<CustomBossChallenge>($"{Problem}; Gildebloon is a gold Bloonarius instead");
        else ModHelper.Msg<CustomBossChallenge>("Gildebloon's look loaded");
    }

    private static void ReadLayouts()
    {
        using var stream = typeof(GildebloonLook).Assembly.GetManifestResourceStream(LayoutResource);
        if (stream == null) return;
        using var reader = new StreamReader(stream);
        var json = Newtonsoft.Json.Linq.JObject.Parse(reader.ReadToEnd());
        foreach (var (name, value) in json)
        {
            if (value is Newtonsoft.Json.Linq.JArray parts && parts.Count > 0) Layouts[name] = parts;
        }
    }
}
