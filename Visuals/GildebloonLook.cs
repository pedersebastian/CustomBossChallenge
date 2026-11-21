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
/// shrink as its health goes, the vault door bursts open with a spray of coins at each skull, and it bursts into coins
/// when it is beaten. Its Tax Collectors get a small gold vault of their own in place of the MOAB or BFB.
/// <br/>
/// No part of the body it replaces may show (#25). The game's animations turn the body's parts back on as it takes
/// damage, so they are kept from drawing with <c>forceRenderingOff</c>, which the animations leave alone, and checked
/// every frame. If the layout or the textures are missing, the Bloonarius body is tinted gold instead.
/// </summary>
public static class GildebloonLook
{
    public const string LayoutResource = "CustomBossChallenge.gildebloon.layout.json";
    public const string RootName = "GildebloonLook";

    /// <summary>How wide the look is in its own units: its ring of coins</summary>
    public const float ModelWidth = 3.8f;

    /// <summary>How wide it is on the map, in the game's units: about as wide as Bloonarius</summary>
    public const float MapWidth = 45;

    /// <summary>How long a Tax Collector's hull is in the look's units</summary>
    private const float CollectorModelLength = 2.7f;

    /// <summary>How long a Tax Collector is on the map, about as long as the MOAB (BFB) it replaces</summary>
    public const float CollectorLength = 26;

    public const float EliteCollectorLength = 34;

    /// <summary>The mutator the game gives bloons a boss sends, which dresses them in the boss's skin</summary>
    public const string BossSkinMutator = "BossSkinMutator";

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
        public bool Collector;
        public IntPtr Graphic;
        public GameObject? GraphicObject;
        public GameObject? Look;
        public readonly List<Renderer> Hidden = new();
        public int Frames;
        public Transform? Dial, Door, Ring, BagLeft, BagRight;
        public Vector3 BagScale, DoorPosition;
        public int Skulls;
        public float BurstStart = -100;
        public readonly List<(Transform Coin, Vector3 Velocity)> Spray = new();
        public bool Tinted;

        /// <summary>The body modelled in Blender, when it is the one shown (<see cref="GildebloonBody"/>)</summary>
        public GildebloonBody.Instance? Body;
        public float HealthShare = 1, LastHitTime = -100, NextIdle, LastAcidNear = -100;
        public int Dashes;
        public bool Armoured;
    }

    /// <summary>The looks on the bloons alive, by the bloon</summary>
    private static readonly Dictionary<IntPtr, Shown> Looks = new();

    /// <summary>The boss's look, while one is alive</summary>
    private static Shown? Boss => Looks.Values.FirstOrDefault(look => !look.Collector);

    /// <summary>
    /// Bodies of bloons that are gone, still hidden while the game plays their end on them; drawn again once the game
    /// puts the display away for the next bloon that uses it
    /// </summary>
    private static readonly List<(GameObject Graphic, List<Renderer> Hidden, float Until)> Leaving = new();

    /// <summary>The longest a gone bloon's body stays hidden, for its end to play</summary>
    private const float LeavingSeconds = 6;

    /// <summary>Looks bursting into coins after their bloon was beaten</summary>
    private static readonly List<Shown> Bursts = new();

    private const float BurstSeconds = 1.4f;

    /// <summary>The longest a modelled body plays its end (Death or Escape, then Payout) after its bloon is gone</summary>
    private const float BodyEndSeconds = 8;

    /// <summary>Bosses whose Spawn has played, so a new display for the same bloon doesn't play it again</summary>
    private static readonly HashSet<IntPtr> Spawned = new();

    /// <summary>A Toll or a Tax Collector's leak not yet shown, how much; and when a payout came, for the body's end</summary>
    private static double pendingToll;
    private static float payoutAt = -100;
    private static int pendingInterest;
    private static bool listening;

    /// <summary>A Toll at least this big makes it smug afterwards (<c>IdleSmug</c>)</summary>
    private const double SmugToll = 250;

    /// <summary>How many times a hit tarnished it (TarnishHit played), for tests</summary>
    public static int TarnishHits { get; private set; }

    /// <summary>How near the boss an acid tower's projectile counts as hitting it, in the game's units</summary>
    private const float AcidReach = 60;

    /// <summary>Per projectile model, whether it carries Tarnish (<see cref="Gildebloon.Tarnish"/>)</summary>
    private static readonly Dictionary<IntPtr, bool> AcidModels = new();

    /// <summary>The animation the boss's modelled body played last, for tests</summary>
    public static string? LastAnimation => Boss?.Body?.Player.LastPlayed;

    /// <summary>The look on the Gildebloon alive, for tests</summary>
    public static GameObject? Look => Boss?.Look;

    /// <summary>Whether the Gildebloon alive is tinted gold because the model couldn't be loaded, for tests</summary>
    public static bool Tinted => Boss?.Tinted ?? false;

    /// <summary>How many Tax Collectors have their own look now, for tests</summary>
    public static int CollectorLooks => Looks.Values.Count(look => look.Collector && look.Look != null);

    /// <summary>How many looks burst into coins, for tests</summary>
    public static int BurstCount { get; private set; }

    /// <summary>How many body parts the game turned back on and were hidden again, for tests</summary>
    public static int BodyShownAgain { get; private set; }

    /// <summary>Called every frame</summary>
    public static void Update()
    {
        var sim = InGame.instance?.bridge?.Simulation;
        if (!Experimental.Gildebloon || !CustomBossState.IsActive || sim == null ||
            CustomBossState.ActiveOptions is not {UsesGildebloon: true})
        {
            ClearAll();
            return;
        }

        var alive = new HashSet<IntPtr>();
        var boss = sim.map?.spawner?.bossBloonManager?.CurrentBoss;
        if (boss != null && Gildebloon.IsGildebloon(boss.bloonModel?.id) && boss.health > 0 && !boss.IsDestroyed)
        {
            Track(boss, false, alive);
        }

        foreach (var bloon in InGame.instance!.GetBloons())
        {
            if (bloon != null && !bloon.IsDestroyed && Gildebloon.IsCollector(bloon.bloonModel?.id)) Track(bloon, true, alive);
        }

        foreach (var key in Looks.Keys.Where(key => !alive.Contains(key)).ToList())
        {
            Finish(Looks[key], true);
            Looks.Remove(key);
        }

        UpdateLeaving();
        UpdateBursts();
    }

    private static void Track(Bloon bloon, bool collector, HashSet<IntPtr> alive)
    {
        alive.Add(bloon.Pointer);
        // A Tax Collector is sent with the skin of the boss Gildebloon is made from; it has its own look
        if (collector && !Looks.ContainsKey(bloon.Pointer)) bloon.RemoveMutatorsById(BossSkinMutator);

        var graphic = bloon.GetUnityDisplayNode();
        Looks.TryGetValue(bloon.Pointer, out var look);
        if (graphic == null) return;

        if (look == null || look.Graphic != graphic.Pointer)
        {
            if (look != null)
            {
                Finish(look, false);
                Looks.Remove(bloon.Pointer);
            }

            look = new Shown {Bloon = bloon, Collector = collector, Graphic = graphic.Pointer, GraphicObject = graphic.gameObject};
            // The game builds a new display over a few frames; the look waits until it has its parts
            if (!Attach(look, graphic.gameObject, bloon.bloonModel.id.Contains(TierBossMath.EliteSuffix))) return;
            Looks[bloon.Pointer] = look;
            // after the skull's Repossession, which sends them
            if (collector) Boss?.Body?.Player.Queue("Dispatch");
        }

        look.Frames++;
        if (look.Look != null) HideBody(look, graphic.gameObject);
        if (!collector) Animate(look, bloon);
    }

    /// <summary>
    /// Keeps every part of the body that isn't the look from drawing, also the ones the game added or turned back on
    /// since the last frame
    /// </summary>
    private static void HideBody(Shown look, GameObject graphic)
    {
        var root = look.Look!.transform;
        foreach (var renderer in graphic.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.forceRenderingOff || renderer.transform.IsChildOf(root)) continue;
            renderer.forceRenderingOff = true;
            look.Hidden.Add(renderer);
            if (look.Frames > 1) BodyShownAgain++;
        }
    }

    /// <summary>Whether the bloon has its look on now, for tests</summary>
    public static bool HasLook(Bloon bloon) => Looks.TryGetValue(bloon.Pointer, out var look) && look.Look != null;

    /// <summary>Whether a part of the body the look replaces draws now, for tests</summary>
    public static bool BodyShows(Bloon bloon)
    {
        var graphic = bloon.GetUnityDisplayNode();
        if (graphic == null) return false;
        Looks.TryGetValue(bloon.Pointer, out var look);
        var root = look?.Look != null ? look.Look.transform : null;
        return graphic.GetComponentsInChildren<Renderer>(true).Any(r =>
            r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy && (root == null || !r.transform.IsChildOf(root)));
    }

    /// <summary>
    /// Where renderers drawing now belong to Bloonarius (by their object, mesh or material), for tests
    /// </summary>
    public static List<string> BloonariusShowing()
    {
        var found = new List<string>();
        foreach (var renderer in Object.FindObjectsOfType<Renderer>())
        {
            if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy || IsOurs(renderer.transform)) continue;
            var material = renderer.sharedMaterial;
            var mesh = renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            var names = $"{renderer.name} {(material != null ? material.name : "")} {(mesh != null ? mesh.name : "")} {PathOf(renderer.transform)}";
            if (names.Contains("Bloonarius", StringComparison.OrdinalIgnoreCase)) found.Add(PathOf(renderer.transform));
        }
        return found;
    }

    /// <summary>Whether a part is one of the mod's own: a look or the pile of money</summary>
    private static bool IsOurs(Transform transform)
    {
        for (var t = transform; t != null; t = t.parent)
        {
            if (t.name.StartsWith(RootName) || t.name == GildebloonMapLook.PileName) return true;
        }
        return false;
    }

    private static string PathOf(Transform transform)
    {
        var parts = new List<string>();
        for (var t = transform; t != null && parts.Count < 6; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <returns>Whether the look is on; false while the display has no parts yet</returns>
    private static bool Attach(Shown look, GameObject graphic, bool elite)
    {
        var renderers = graphic.GetComponentsInChildren<Renderer>(true).Where(r => !r.forceRenderingOff).ToList();
        if (renderers.Count == 0) return false;
        if (!loaded) Load();
        if (!look.Collector && GildebloonBody.Ready && GildebloonBody.Build(graphic.transform, elite, MapWidth) is { } body)
        {
            look.Body = body;
            look.Look = body.Root;
            HideBody(look, graphic);
            look.Skulls = SkullsPassed(look.Bloon);
            look.Dashes = GildebloonBrain.Dashes;
            look.Armoured = GildebloonBrain.Armour(look.Bloon) > 0;
            look.HealthShare = HealthShare(look.Bloon);
            look.NextIdle = Time.time + 8;
            if (Spawned.Add(look.Bloon.Pointer)) body.Player.Play(elite ? "EliteSpawn" : "Spawn");
            return true;
        }

        // A Tax Collector: the gold MOAB Gildebloon sends out in Dispatch, when the modelled body loaded
        if (look.Collector && GildebloonBody.BuildCollector(graphic.transform, elite ? EliteCollectorLength : CollectorLength) is { } collector)
        {
            look.Look = collector;
            HideBody(look, graphic);
            return true;
        }

        // A Tax Collector is always the bright gold one, Elite's only bigger
        var layout = Layouts.TryGetValue(elite && !look.Collector ? "GildebloonElite" : "Gildebloon", out var own) ? own
            : Layouts.TryGetValue("Gildebloon", out var normal) ? normal : null;
        if (Problem != null || layout == null)
        {
            Tint(look, renderers);
            return true;
        }

        var instance = Build(layout, graphic.transform);
        var parts = instance.GetComponentsInChildren<Renderer>(true);
        if (parts.Length == 0)
        {
            Object.Destroy(instance);
            Tint(look, renderers);
            return true;
        }

        // The parts get materials made here, from the body's own material where it has one, so they are lit like the
        // game's bloons
        var template = renderers.Select(r => r.sharedMaterial).FirstOrDefault(m => m != null);
        foreach (var renderer in parts)
        {
            var mesh = renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            var key = mesh == null ? "Gold" : mesh.name.Split('_')[0];
            renderer.sharedMaterial = MaterialFor(key, template);
        }

        // As wide on the map as Bloonarius, centred on the bloon. The body's own bounds can't be used: its 70-odd
        // meshes include parts far outside it. The look is ModelWidth wide in its own units. A Tax Collector is a
        // small vault, the hull and its door, as long as the MOAB or BFB it replaces.
        var scale = graphic.transform.lossyScale.x;
        var size = MapWidth / ModelWidth;
        if (look.Collector)
        {
            foreach (var name in new[] {"BagLeft", "BagRight", "CoinRing", "Dial"})
            {
                if (Find(instance, name) is { } part) Object.DestroyImmediate(part.gameObject);
            }
            size = (elite ? EliteCollectorLength : CollectorLength) / CollectorModelLength;
            instance.name = RootName + "Collector";
        }
        if (scale > 0) instance.transform.localScale = Vector3.one * (size / scale);
        instance.transform.localPosition = Vector3.zero;

        look.Look = instance;
        HideBody(look, graphic);
        look.Dial = Find(instance, "Dial");
        look.Door = Find(instance, "VaultDoor");
        look.Ring = Find(instance, "CoinRing");
        look.BagLeft = Find(instance, "BagLeft");
        look.BagRight = Find(instance, "BagRight");
        if (look.BagLeft != null) look.BagScale = look.BagLeft.localScale;
        if (look.Door != null) look.DoorPosition = look.Door.localPosition;
        if (!look.Collector) look.Skulls = SkullsPassed(look.Bloon);
        return true;
    }

    /// <summary>
    /// Takes a look off its bloon. When the bloon is gone it bursts into coins where it was, and the body stays hidden
    /// while the game plays its end; when only its display changed, the old display gets its parts back.
    /// </summary>
    private static void Finish(Shown look, bool gone)
    {
        try
        {
            if (look.Look != null)
            {
                // Gone: it bursts where it was, on the map, also when the game put its display away at once
                if (gone && look.Body != null)
                {
                    look.Look.transform.SetParent(BossPresentation.MapRoot(), true);
                    look.BurstStart = Time.time;
                    look.Body.Player.Play(Leaked(look.Bloon) ? "Escape" : "Death");
                    Spawned.Remove(look.Bloon.Pointer);
                    Bursts.Add(look);
                    BurstCount++;
                }
                else if (gone)
                {
                    look.Look.transform.SetParent(BossPresentation.MapRoot(), true);
                    look.BurstStart = Time.time;
                    foreach (var (coin, _) in look.Spray)
                    {
                        if (coin != null) Object.Destroy(coin.gameObject);
                    }
                    look.Spray.Clear();
                    Spray(look, look.Collector ? 10 : 24);
                    Bursts.Add(look);
                    BurstCount++;
                }
                else Object.Destroy(look.Look);
            }

            if (gone && look.GraphicObject != null)
            {
                Leaving.Add((look.GraphicObject, new List<Renderer>(look.Hidden), Time.realtimeSinceStartup + LeavingSeconds));
            }
            else Restore(look.Hidden);
            if (look.Tinted && look.GraphicObject != null)
            {
                foreach (var renderer in look.GraphicObject.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(null);
            }
        }
        catch (Exception)
        {
            // The display went with the match
        }
    }

    private static void UpdateLeaving()
    {
        for (var i = Leaving.Count - 1; i >= 0; i--)
        {
            var (graphic, hidden, until) = Leaving[i];
            try
            {
                // Still out and in time: also what the game adds for the end stays hidden. Put away (inactive) or past
                // the time: the parts come back for the next bloon that uses the display.
                if (graphic != null && graphic.activeInHierarchy && Time.realtimeSinceStartup < until)
                {
                    foreach (var renderer in graphic.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer.forceRenderingOff) continue;
                        renderer.forceRenderingOff = true;
                        hidden.Add(renderer);
                    }
                    continue;
                }
                Restore(hidden);
            }
            catch (Exception)
            {
                // The display went with the match
            }
            Leaving.RemoveAt(i);
        }
    }

    private static void UpdateBursts()
    {
        var delta = Time.deltaTime * Math.Max(1, Time.timeScale);
        for (var i = Bursts.Count - 1; i >= 0; i--)
        {
            var look = Bursts[i];
            var age = Time.time - look.BurstStart;
            if (look.Body != null && look.Look != null)
            {
                if (PlayEnd(look, age, delta)) continue;
                Object.Destroy(look.Look);
                Bursts.RemoveAt(i);
                continue;
            }

            if (look.Look == null || age > BurstSeconds)
            {
                if (look.Look != null) Object.Destroy(look.Look);
                Bursts.RemoveAt(i);
                continue;
            }

            // The vault flies apart: its parts shrink away while the coins fly out
            foreach (var name in new[] {"Hull", "VaultDoor", "Dial", "BagLeft", "BagRight", "CoinRing"})
            {
                if (Find(look.Look, name) is { } part) part.localScale *= Math.Max(0.05f, 1 - delta * 2.5f);
            }
            MoveSpray(look, delta, BurstSeconds);
        }
    }

    /// <summary>
    /// Plays a gone modelled body's end: Death or Escape, and after Death the Payout once the brain paid out. False
    /// once it has played (or took too long), to take it away.
    /// </summary>
    private static bool PlayEnd(Shown look, float age, float delta)
    {
        if (age > BodyEndSeconds) return false;
        var player = look.Body!.Player;
        if (player.LastPlayed == "Death" && payoutAt >= look.BurstStart - 1)
        {
            payoutAt = -100;
            player.Queue("Payout");
        }

        player.Advance(delta);
        GildebloonBody.Apply(look.Body);
        if (!player.Finished) return true;
        // After Death, a moment for a payout to come (it isn't paid when a lower tier was beaten before)
        return player.LastPlayed == "Death" && age < 5f;
    }

    private static void Restore(List<Renderer> hidden)
    {
        foreach (var renderer in hidden)
        {
            if (renderer != null) renderer.forceRenderingOff = false;
        }
        hidden.Clear();
    }

    /// <summary>Takes every look off, at the end of a match</summary>
    private static void ClearAll()
    {
        Spawned.Clear();
        AcidModels.Clear();
        pendingToll = 0;
        pendingInterest = 0;
        if (Looks.Count == 0 && Leaving.Count == 0 && Bursts.Count == 0) return;
        foreach (var look in Looks.Values) Finish(look, false);
        Looks.Clear();
        foreach (var (_, hidden, _) in Leaving)
        {
            try
            {
                Restore(hidden);
            }
            catch (Exception)
            {
                // The display went with the match
            }
        }
        Leaving.Clear();
        foreach (var burst in Bursts)
        {
            if (burst.Look != null) Object.Destroy(burst.Look);
        }
        Bursts.Clear();
    }

    /// <summary>Builds the parts of a look under <paramref name="parent"/>, from its layout and the meshes made for it</summary>
    internal static GameObject Build(Newtonsoft.Json.Linq.JArray layout, Transform parent)
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

    internal static Mesh MeshFor(string name, string kind, float[] args)
    {
        if (Meshes.TryGetValue(name, out var mesh) && mesh != null) return mesh;
        mesh = GildebloonMeshes.Make(kind, args);
        mesh.name = name;
        Meshes[name] = mesh;
        return mesh;
    }

    internal static Material MaterialFor(string key, Material? template)
    {
        // One per texture and the shader it is made with: the boss's from its body, the map's pile from the map
        var cacheKey = template != null && template.shader != null ? $"{key}|{template.shader.name}" : key;
        if (Materials.TryGetValue(cacheKey, out var made) && made != null) return made;
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

        Materials[cacheKey] = material;
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
        if (look.Body != null)
        {
            AnimateBody(look, look.Body, boss);
            return;
        }

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
            Spray(look, 14);
        }

        if (look.Door != null)
        {
            var age = time - look.BurstStart;
            var open = age < 1.2f ? Mathf.Sin(Mathf.Clamp01(age / 1.2f) * Mathf.PI) : 0;
            look.Door.localPosition = look.DoorPosition + Vector3.up * (0.35f * open);
            look.Door.localRotation = Quaternion.Euler(0, 0, -70 * open);
        }

        MoveSpray(look, delta, 1.5f);
    }

    /// <summary>
    /// Whether a gone boss leaked rather than was beaten: it still has health. Read from the bloon itself, not the
    /// last frame's share, since the hit that beats it may come in the same frame it is gone.
    /// </summary>
    private static bool Leaked(Bloon boss)
    {
        try
        {
            return boss.health > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether a projectile of the acid towers (Tarnish) is at the boss now. The game's damage can't be hooked
    /// (patching <c>Bloon.Damage</c> crashes it), so a hit right after one is near counts as theirs.
    /// </summary>
    private static bool AcidNear(Bloon boss)
    {
        try
        {
            var position = boss.Position;
            foreach (var projectile in InGame.instance.GetProjectiles())
            {
                var model = projectile?.projectileModel;
                if (model == null || !IsAcid(model)) continue;
                var dx = projectile!.Position.X - position.X;
                var dy = projectile.Position.Y - position.Y;
                if (dx * dx + dy * dy <= AcidReach * AcidReach) return true;
            }
        }
        catch (Exception)
        {
            // a projectile went in the middle of the look
        }

        return false;
    }

    private static bool IsAcid(Il2CppAssets.Scripts.Models.Towers.Projectiles.ProjectileModel model)
    {
        if (AcidModels.TryGetValue(model.Pointer, out var acid)) return acid;
        acid = model.GetBehaviors<Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.DamageModifierForTagModel>()
            .Any(modifier => modifier.name.EndsWith(Gildebloon.Tarnish));
        AcidModels[model.Pointer] = acid;
        return acid;
    }

    private static float HealthShare(Bloon boss) =>
        boss.bloonModel.maxHealth > 0 ? Math.Clamp(boss.health / (float) boss.bloonModel.maxHealth, 0, 1) : 1;

    /// <summary>
    /// Starts the modelled body's animations from what happened since the last frame, then poses it. Tarnish has an
    /// animation but nothing starts it yet: telling an Alchemist's or Glue Gunner's hit apart needs a hook in the
    /// game's damage, and patching <c>Bloon.Damage</c> crashes it (see docs/gildebloon.md).
    /// </summary>
    private static void AnimateBody(Shown look, GildebloonBody.Instance body, Bloon boss)
    {
        var player = body.Player;
        var time = Time.time;

        if (AcidNear(boss)) look.LastAcidNear = time;
        var share = HealthShare(boss);
        player.SetBase(GildebloonClipPlayer.BaseFor(share));
        if (share < look.HealthShare - 0.002f && time - look.LastHitTime > 0.4f)
        {
            look.LastHitTime = time;
            // an Alchemist's potion or a Glue Gunner's glue at it just now: the hit tarnishes the gold
            if (time - look.LastAcidNear < 0.3f && player.Play("TarnishHit")) TarnishHits++;
            else player.Play("Hit");
        }
        look.HealthShare = share;

        var skulls = SkullsPassed(boss);
        if (skulls > look.Skulls)
        {
            look.Skulls = skulls;
            player.Play("Repossession");
        }

        if (GildebloonBrain.Dashes > look.Dashes)
        {
            look.Dashes = GildebloonBrain.Dashes;
            player.Play("Skittish");
        }

        var armoured = GildebloonBrain.Armour(boss) > 0;
        if (armoured != look.Armoured)
        {
            player.Play(armoured ? "VaultUp" : "VaultBreak");
            look.Armoured = armoured;
        }

        if (pendingInterest > 0)
        {
            pendingInterest = 0;
            player.Play("Interest");
        }

        if (pendingToll > 0)
        {
            var toll = pendingToll;
            pendingToll = 0;
            if (player.Play("Toll") && toll >= SmugToll) player.Queue("IdleSmug");
        }

        // Now and then, when nothing else plays, one of its idles
        if (player.Current != null) look.NextIdle = Math.Max(look.NextIdle, time + 6);
        else if (time >= look.NextIdle && player.Base == "Idle")
        {
            player.Play(UnityEngine.Random.value < 0.5f ? "IdleCount" : "IdlePolish");
            look.NextIdle = time + UnityEngine.Random.Range(9f, 15f);
        }

        player.Advance(Time.deltaTime * Math.Max(1, Time.timeScale));
        GildebloonBody.Apply(body);
    }

    /// <summary>Throws <paramref name="count"/> coins out of the look's top</summary>
    private static void Spray(Shown look, int count)
    {
        var coin = look.Ring != null && look.Ring.childCount > 0 ? look.Ring.GetChild(0) : null;
        if (look.Look == null) return;
        var template = coin != null ? coin.gameObject : CoinFor(look.Look);
        if (template == null) return;
        for (var i = 0; i < count; i++)
        {
            var angle = i * Mathf.PI * 2 / count;
            var copy = Object.Instantiate(template, look.Look.transform, false).transform;
            copy.localPosition = new Vector3(0, 0.9f, 0);
            copy.localScale = coin != null ? coin.localScale : new Vector3(0.16f, 1, 0.16f);
            var speed = 1.8f + (i % 3) * 0.4f;
            look.Spray.Add((copy, new Vector3(Mathf.Cos(angle) * speed, 2.5f, Mathf.Sin(angle) * speed)));
        }
    }

    /// <summary>A coin for a look without a ring of coins (a Tax Collector's)</summary>
    private static GameObject? CoinFor(GameObject root)
    {
        var hull = Find(root, "Hull");
        var renderer = hull != null ? hull.GetComponent<MeshRenderer>() : null;
        if (renderer == null) return null;
        var coin = new GameObject("Coin");
        coin.transform.SetParent(root.transform, false);
        coin.AddComponent<MeshFilter>().sharedMesh = MeshFor("Gold_Coin", "cylinder", [16, 1, 0.15f]);
        coin.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
        coin.SetActive(false);
        return coin;
    }

    /// <summary>Moves the sprayed coins, and takes them away <paramref name="seconds"/> after the spray</summary>
    private static void MoveSpray(Shown look, float delta, float seconds)
    {
        var time = Time.time;
        for (var i = look.Spray.Count - 1; i >= 0; i--)
        {
            var (coin, velocity) = look.Spray[i];
            if (coin == null || time - look.BurstStart > seconds)
            {
                if (coin != null) Object.Destroy(coin.gameObject);
                look.Spray.RemoveAt(i);
                continue;
            }

            if (!coin.gameObject.activeSelf) coin.gameObject.SetActive(true);
            velocity += Vector3.down * (4 * delta);
            look.Spray[i] = (coin, velocity);
            coin.localPosition += velocity * delta;
            coin.Rotate(400 * delta, 0, 0, Space.Self);
        }
    }

    private static int SkullsPassed(Bloon boss)
    {
        var skulls = SkullModifier.GetSkulls(boss.bloonModel);
        if (skulls == null || boss.bloonModel.maxHealth <= 0) return 0;
        var share = boss.health / (double) boss.bloonModel.maxHealth;
        return skulls.Count(skull => share <= skull);
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
            GildebloonPileMesh.Load();
            GildebloonBody.Load();
            if (!listening)
            {
                listening = true;
                GildebloonBrain.CashTaken += cash =>
                {
                    if (cash > 0) pendingToll += cash;
                    else payoutAt = Time.time;
                };
                GildebloonBrain.InterestAdded += _ => pendingInterest++;
            }
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
