using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper.Extensions;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The Repossession on the map (#25), so it is clear which towers Gildebloon seized at a skull and for how long:
/// <list type="bullet">
/// <item>a gold ring grows from Gildebloon out to how far it reaches,</item>
/// <item>coins fly from each seized tower to it,</item>
/// <item>each seized tower gets a gold outline and a padlock over it, with a disc under the padlock that shrinks as its
/// time runs out (3 s for most towers, 12 s for Banana Farms, whose padlock is bigger),</item>
/// <item>and when a tower is let go, its padlock bursts into coins.</item>
/// </list>
/// Made like Gildebloon's own look, from <see cref="GildebloonMeshes"/> and its materials.
/// </summary>
public static class GildebloonSeizeLook
{
    public const string PadlockName = "GildebloonPadlock";
    public const string RingName = "GildebloonReach";

    /// <summary>How high over a tower its padlock floats</summary>
    private const float PadlockHeight = 45;

    private const float RingSeconds = 1.3f;
    private const float FlySeconds = 0.7f;
    private const float BurstSeconds = 0.6f;

    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly Color Gold = new(1f, 0.78f, 0.15f);

    private sealed class Padlock
    {
        public Tower Tower = null!;
        public GameObject Root = null!;
        public Transform Timer = null!;
        public float Total;
        public float Phase;
        public readonly List<(Renderer Renderer, MaterialPropertyBlock? Before)> Outlined = new();
    }

    private sealed class Flying
    {
        public Transform Coin = null!;
        public Vector3 From, To, Velocity;
        public float Start, Seconds;
        public bool Arc;
    }

    private static readonly Dictionary<IntPtr, Padlock> Padlocks = new();
    private static readonly List<(GameObject Ring, float Radius, float Start)> Rings = new();
    private static readonly List<Flying> Coins = new();
    private static bool subscribed;

    /// <summary>How many padlocks are on the map now, for tests</summary>
    public static int PadlockCount => Padlocks.Count;

    /// <summary>How many reach rings were shown this session, for tests</summary>
    public static int RingsShown { get; private set; }

    /// <summary>Called every frame</summary>
    public static void Update()
    {
        if (!subscribed)
        {
            subscribed = true;
            GildebloonBrain.Repossessed += OnRepossessed;
        }

        if (!Experimental.Gildebloon || !CustomBossState.IsActive || InGame.instance?.bridge?.Simulation == null)
        {
            Clear();
            return;
        }

        var seized = GildebloonBrain.SeizedNow();
        var now = new HashSet<IntPtr>();
        foreach (var (tower, left, farm) in seized)
        {
            if (tower == null || tower.IsDestroyed) continue;
            now.Add(tower.Pointer);
            if (!Padlocks.TryGetValue(tower.Pointer, out var padlock))
            {
                var made = Make(tower, farm);
                if (made == null) continue;
                Padlocks[tower.Pointer] = padlock = made;
            }
            Place(padlock, left);
        }

        foreach (var key in Padlocks.Keys.Where(key => !now.Contains(key)).ToList())
        {
            Release(Padlocks[key], true);
            Padlocks.Remove(key);
        }

        UpdateRings();
        UpdateCoins();
    }

    private static void OnRepossessed(GildebloonBrain.Repossession repossession)
    {
        try
        {
            var boss = repossession.Boss.GetUnityDisplayNode();
            if (boss == null) return;
            var centre = boss.transform.position;
            var gold = Material("Gold");
            if (gold == null) return;

            var ring = OnMap(new GameObject(RingName));
            ring.transform.position = centre;
            ring.AddComponent<MeshFilter>().sharedMesh = GildebloonLook.MeshFor("Gold_Reach", "ring", [64, 0.9f]);
            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = gold;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Rings.Add((ring, repossession.Radius, Time.time));
            RingsShown++;

            // Coins fly from every seized tower to the boss
            var coin = GildebloonLook.MeshFor("Gold_Coin", "cylinder", [16, 1, 0.15f]);
            foreach (var (tower, _, _) in GildebloonBrain.SeizedNow())
            {
                var display = tower?.GetUnityDisplayNode();
                if (display == null) continue;
                for (var i = 0; i < 3; i++)
                {
                    var flying = Coin(coin, gold, display.transform.position + Vector3.up * (PadlockHeight * 0.5f), 2.5f);
                    Coins.Add(new Flying
                    {
                        Coin = flying, From = flying.position, To = centre + Vector3.up * 5, Start = Time.time + i * 0.12f,
                        Seconds = FlySeconds, Arc = true
                    });
                }
            }
        }
        catch (Exception)
        {
            // The boss went with the match
        }
    }

    private static Padlock? Make(Tower tower, bool farm)
    {
        var display = tower.GetUnityDisplayNode();
        var gold = Material("Gold");
        var iron = Material("Iron");
        if (display == null || gold == null || iron == null) return null;

        // Lying flat, seen from above: the body, the shackle at its top (+z, up on the screen) and the keyhole
        var root = OnMap(new GameObject(PadlockName));
        var box = GildebloonLook.MeshFor("Gold_Box", "box", []);
        var size = farm ? 2.2f : 1.6f;
        Part(root.transform, box, iron, new Vector3(0, -0.2f, -0.6f), new Vector3(7, 2, 6));
        Part(root.transform, box, gold, new Vector3(0, 0.2f, -0.6f), new Vector3(6.2f, 2, 5.2f));
        Part(root.transform, box, iron, new Vector3(-2.2f, 0, 2.8f), new Vector3(1.3f, 1.3f, 3.6f));
        Part(root.transform, box, iron, new Vector3(2.2f, 0, 2.8f), new Vector3(1.3f, 1.3f, 3.6f));
        Part(root.transform, box, iron, new Vector3(0, 0, 4.6f), new Vector3(5.7f, 1.3f, 1.3f));
        Part(root.transform, box, iron, new Vector3(0, 1.4f, -0.6f), new Vector3(1.2f, 0.4f, 2.2f));
        var timer = Part(root.transform, GildebloonLook.MeshFor("Gold_Reach", "ring", [64, 0.9f]), gold, new Vector3(0, -1.5f, 0),
            Vector3.one * 7);
        root.transform.localScale = Vector3.one * (size / MapScale());

        var padlock = new Padlock
        {
            Tower = tower, Root = root, Timer = timer, Total = farm ? GildebloonBrain.SeizeSeconds : GildebloonBrain.FreezeSeconds,
            Phase = (tower.Pointer.ToInt64() % 100) / 10f
        };

        // A gold outline on the tower itself, through its property block so its materials stay as they are
        foreach (var renderer in display.GetComponentsInChildren<Renderer>())
        {
            if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(OutlineColor)) continue;
            MaterialPropertyBlock? before = null;
            if (renderer.HasPropertyBlock())
            {
                before = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(before);
            }
            var block = new MaterialPropertyBlock();
            if (before != null) renderer.GetPropertyBlock(block);
            block.SetColor(OutlineColor, Gold);
            renderer.SetPropertyBlock(block);
            padlock.Outlined.Add((renderer, before));
        }
        return padlock;
    }

    private static void Place(Padlock padlock, float left)
    {
        var display = padlock.Tower.GetUnityDisplayNode();
        if (display == null) return;
        var bob = Mathf.Sin(Time.time * 4 + padlock.Phase) * 1.2f;
        padlock.Root.transform.position = display.transform.position + Vector3.up * PadlockHeight + new Vector3(0, 0, bob);
        var share = padlock.Total > 0 ? Mathf.Clamp01(left / padlock.Total) : 0;
        padlock.Timer.localScale = Vector3.one * (7 * (0.25f + 0.75f * share));
    }

    /// <summary>Takes a padlock off; let go in play it bursts into coins</summary>
    private static void Release(Padlock padlock, bool burst)
    {
        try
        {
            foreach (var (renderer, before) in padlock.Outlined)
            {
                if (renderer != null) renderer.SetPropertyBlock(before);
            }

            if (burst && padlock.Root != null && Material("Gold") is { } gold)
            {
                var coin = GildebloonLook.MeshFor("Gold_Coin", "cylinder", [16, 1, 0.15f]);
                var centre = padlock.Root.transform.position;
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Mathf.PI * 2 / 8;
                    var flying = Coin(coin, gold, centre, 2.5f);
                    Coins.Add(new Flying
                    {
                        Coin = flying, From = centre, Velocity = new Vector3(Mathf.Cos(angle) * 40, 30, Mathf.Sin(angle) * 40),
                        Start = Time.time, Seconds = BurstSeconds
                    });
                }
            }
        }
        catch (Exception)
        {
            // The tower went with the match
        }

        if (padlock.Root != null) Object.Destroy(padlock.Root);
    }

    private static void UpdateRings()
    {
        for (var i = Rings.Count - 1; i >= 0; i--)
        {
            var (ring, radius, start) = Rings[i];
            var age = Time.time - start;
            if (ring == null || age > RingSeconds)
            {
                if (ring != null) Object.Destroy(ring);
                Rings.RemoveAt(i);
                continue;
            }

            // Out to its reach in half the time, then it stays a moment
            var grow = Mathf.Clamp01(age / (RingSeconds * 0.5f));
            var scale = radius * (0.15f + 0.85f * (1 - (1 - grow) * (1 - grow)));
            scale /= MapScale();
            ring.transform.localScale = new Vector3(scale, 1, scale);
        }
    }

    private static void UpdateCoins()
    {
        var delta = Time.deltaTime * Math.Max(1, Time.timeScale);
        for (var i = Coins.Count - 1; i >= 0; i--)
        {
            var flying = Coins[i];
            var age = Time.time - flying.Start;
            if (flying.Coin == null || age > flying.Seconds)
            {
                if (flying.Coin != null) Object.Destroy(flying.Coin.gameObject);
                Coins.RemoveAt(i);
                continue;
            }
            if (age < 0) continue;

            if (!flying.Coin.gameObject.activeSelf) flying.Coin.gameObject.SetActive(true);
            if (flying.Arc)
            {
                var t = age / flying.Seconds;
                flying.Coin.position = Vector3.Lerp(flying.From, flying.To, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 12);
            }
            else
            {
                flying.Velocity += Vector3.down * (90 * delta);
                flying.Coin.position += flying.Velocity * delta;
            }
            flying.Coin.Rotate(500 * delta, 0, 0, Space.Self);
        }
    }

    private static Transform Coin(Mesh mesh, Material material, Vector3 position, float size)
    {
        var coin = OnMap(new GameObject("Coin"));
        coin.transform.position = position;
        coin.transform.localScale = new Vector3(size, size * 0.6f, size) / MapScale();
        coin.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = coin.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coin.SetActive(false);
        return coin.transform;
    }

    private static Transform Part(Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var part = new GameObject("Part");
        part.transform.SetParent(parent, false);
        part.layer = parent.gameObject.layer;
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part.transform;
    }

    private static readonly Dictionary<string, Material> OnTop = new();

    /// <summary>
    /// The look's material for a texture, made from the map's own like the pile of money, but drawn after the map,
    /// the towers and the bloons, so nothing covers the padlocks and the ring
    /// </summary>
    private static Material? Material(string key)
    {
        if (OnTop.TryGetValue(key, out var made) && made != null) return made;
        var map = BossPresentation.MapRoot();
        if (map == null) return null;
        made = new Material(GildebloonLook.MaterialFor(key, GildebloonMapLook.Template(map)))
        {
            name = $"Gildebloon {key} on top", renderQueue = 3500, hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        return OnTop[key] = made;
    }

    /// <summary>How much bigger the map draws what is on it than the game's units</summary>
    private static float MapScale()
    {
        var scale = BossPresentation.MapRoot()?.lossyScale.x ?? 1;
        return scale > 0.0001f ? scale : 1;
    }

    /// <summary>Puts a new object on the map, where the game draws what is on it</summary>
    private static GameObject OnMap(GameObject made)
    {
        var map = BossPresentation.MapRoot();
        if (map == null) return made;
        made.transform.SetParent(map, false);
        made.layer = map.gameObject.layer;
        return made;
    }

    /// <summary>Takes everything off, at the end of a match</summary>
    private static void Clear()
    {
        if (Padlocks.Count == 0 && Rings.Count == 0 && Coins.Count == 0) return;
        foreach (var padlock in Padlocks.Values) Release(padlock, false);
        Padlocks.Clear();
        foreach (var (ring, _, _) in Rings)
        {
            if (ring != null) Object.Destroy(ring);
        }
        Rings.Clear();
        foreach (var flying in Coins)
        {
            if (flying.Coin != null) Object.Destroy(flying.Coin.gameObject);
        }
        Coins.Clear();
    }
}
