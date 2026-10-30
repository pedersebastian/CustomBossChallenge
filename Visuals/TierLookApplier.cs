using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Simulation.Bloons;
using Il2CppAssets.Scripts.Simulation.Track;
using Il2CppAssets.Scripts.Unity.Display;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Gives the bosses of tiers above 5 in a custom game the look of <see cref="TierLook"/>, made from what the game
/// already has: the outline in the tier's colour, an additive glow over the body, an aura behind it and a slightly
/// bigger model. Glow and aura pulse.
/// <br/>
/// The game reuses boss displays, so everything is undone when the boss is gone or the game ends: a later tier 5 or a
/// normal game never keeps the look. Nothing is allocated per frame; the pulse only changes the colour of the few
/// shared materials.
/// </summary>
public static class TierLookApplier
{
    public const string GlowShaderName = "Universal Render Pipeline/Unlit";

    /// <summary>
    /// How often the boss manager and the tracked displays are checked, in seconds
    /// </summary>
    public const float CheckInterval = 0.25f;

    /// <summary>
    /// How long after a new boss spawned to keep looking for its segments (Diamondback), in seconds
    /// </summary>
    public const float SegmentSearchTime = 6;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineThickness = Shader.PropertyToID("_OutlineThickness");

    /// <summary>
    /// Colours of the boss's own effects that take the tier colour, like Vortex's lightning
    /// </summary>
    private static readonly int[] EffectColors = {Shader.PropertyToID("_Wind_Color")};

    private static readonly string[] TextureNames = {"_MainTex", "_MeshTexture", "_BaseMap"};

    private sealed class Applied
    {
        public Applied(Renderer renderer, bool hadBlock) => (Renderer, HadBlock) = (renderer, hadBlock);

        public readonly Renderer Renderer;
        public readonly bool HadBlock;
    }

    private sealed class Tracked
    {
        public Tracked(Bloon bloon, string boss, int tier) => (Bloon, Boss, Tier) = (bloon, boss, tier);

        public readonly Bloon Bloon;
        public readonly string Boss;
        public readonly int Tier;
        public UnityDisplayNode? Graphic;
        public readonly List<Applied> Renderers = new();
        /// <summary>
        /// The display's size before the look made it bigger, and how often the game set a size of its own since
        /// </summary>
        public Vector3 BaseScale;

        public bool Scaled;
        public int ScaleResets;
        public GameObject? Aura;
    }

    /// <summary>
    /// A material whose colour pulses, with the strength it pulses around
    /// </summary>
    private sealed record Pulsing(Material Material, Color Color, float Alpha, int Tier);

    /// <summary>
    /// How often the game may set a boss's size before the look leaves the size to the game
    /// </summary>
    private const int MaxScaleResets = 2;

    /// <summary>
    /// Every how many checks the materials are checked, about once a second
    /// </summary>
    private const int MaterialCheckEvery = 4;

    private static int checks;

    private static readonly List<Tracked> Bosses = new();
    private static readonly Dictionary<(string Boss, int Tier, IntPtr Texture), Material> Glows = new();
    private static readonly HashSet<IntPtr> GlowPointers = new();
    private static readonly Dictionary<(string Boss, int Tier), Material> Auras = new();
    private static readonly List<Pulsing> Pulses = new();
    private static readonly MaterialPropertyBlock Block = new();

    private static Shader? glowShader;
    private static Texture2D? auraTexture;
    private static Sprite? auraSprite;
    private static float nextCheck;
    private static IntPtr lastBoss;
    private static float segmentSearchUntil;
    private static bool warned;

    /// <summary>
    /// Whether the game has what the look is made of; without it, bosses keep their normal look
    /// </summary>
    public static bool Available => GlowShader != null;

    /// <summary>
    /// Looked up again when the game unloaded it, which it does with every unused asset when a scene changes
    /// </summary>
    private static Shader? GlowShader => glowShader != null ? glowShader : glowShader = Shader.Find(GlowShaderName);

    private static Material? keepShader;

    /// <summary>
    /// Finds the shader while the main menu still has it loaded, and keeps it loaded with a material of its own;
    /// a game update that removes it only turns the look off
    /// </summary>
    public static void Prepare()
    {
        if (keepShader != null) return;
        if (GlowShader == null)
        {
            ModHelper.Warning<CustomBossChallenge>($"Shader {GlowShaderName} not found, tiers above 5 keep the tier 5 look");
            warned = true;
            return;
        }

        keepShader = new Material(GlowShader) {name = "CustomBossTierLook", hideFlags = HideFlags.DontUnloadUnusedAsset};
    }

    /// <summary>
    /// The bosses that have the look right now, for tests
    /// </summary>
    public static int Count => Bosses.Count(boss => boss.Graphic != null);

    /// <summary>
    /// Whether the look made this display bigger, for tests: it leaves the size alone when the game keeps setting it
    /// </summary>
    public static bool IsScaled(UnityDisplayNode graphic) =>
        Bosses.Any(boss => boss.Scaled && boss.Graphic != null && boss.Graphic.Pointer == graphic.Pointer);

    /// <summary>
    /// Called every frame
    /// </summary>
    public static void Update()
    {
        if (Bosses.Count == 0 && !CustomBossState.IsActive) return;

        if (!CustomBossState.IsActive || InGame.instance?.bridge?.Simulation == null)
        {
            Clear();
            return;
        }

        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + CheckInterval;
            Check();
        }

        Pulse();
    }

    /// <summary>
    /// Takes the look off every boss, e.g. when the match ends or restarts
    /// </summary>
    public static void Clear()
    {
        foreach (var boss in Bosses) Restore(boss);
        Bosses.Clear();
        lastBoss = IntPtr.Zero;
        segmentSearchUntil = 0;
    }

    private static void Check()
    {
        var manager = InGame.instance.bridge.Simulation.map?.spawner?.bossBloonManager;
        if (manager == null) return;

        // A boss that is gone loses the look before the game can reuse its display
        for (var i = Bosses.Count - 1; i >= 0; i--)
        {
            var boss = Bosses[i];
            if (boss.Bloon.IsDestroyed || boss.Bloon.WasCollected)
            {
                Restore(boss);
                Bosses.RemoveAt(i);
            }
        }

        var current = manager.CurrentBoss;
        var pointer = current?.Pointer ?? IntPtr.Zero;
        if (pointer != lastBoss)
        {
            lastBoss = pointer;
            if (current != null) segmentSearchUntil = Time.unscaledTime + SegmentSearchTime;
        }

        var tier = manager.CurrentBossTier;
        if (current != null && TierLook.HasLook(tier))
        {
            var boss = CustomBossState.ActiveOptions!.Boss.ToString();
            Track(current, boss, TierMath.TierOf(current.bloonModel.id) is var own and > 0 ? own : tier);

            // Diamondback's body is its segments, which share tier 5's models and spawn one after another
            if (Time.unscaledTime < segmentSearchUntil) TrackSegments(boss, tier);
        }

        // Looking through every renderer's materials makes garbage, so that is only done every few checks
        var materials = ++checks % MaterialCheckEvery == 0;
        foreach (var boss in Bosses) Refresh(boss, materials);
    }

    private static void TrackSegments(string boss, int tier)
    {
        foreach (var bloon in InGame.instance.GetBloons())
        {
            if (bloon.bloonModel.isBossSegment) Track(bloon, boss, tier);
        }
    }

    private static void Track(Bloon bloon, string boss, int tier)
    {
        foreach (var tracked in Bosses)
        {
            if (tracked.Bloon.Pointer == bloon.Pointer) return;
        }

        Bosses.Add(new Tracked(bloon, boss, tier));
    }

    /// <summary>
    /// Gives the boss the look once its display exists, again when the game swapped the display, and puts back what
    /// the game took away (it swaps materials for some animations)
    /// </summary>
    private static void Refresh(Tracked boss, bool materials)
    {
        var graphic = boss.Bloon.GetUnityDisplayNode();
        if (graphic == null) return;

        if (boss.Graphic == null || boss.Graphic.Pointer != graphic.Pointer)
        {
            Restore(boss);
            boss.Graphic = graphic;
            Apply(boss);
            return;
        }

        if (materials)
        {
            foreach (var applied in boss.Renderers)
            {
                if (applied.Renderer != null && !HasGlow(applied.Renderer)) AddGlow(boss, applied);
            }
        }

        // The game may set the display's size itself; build on it, but don't fight an animation that sets it all the time
        if (boss.Scaled && (graphic.transform.localScale - boss.BaseScale * TierLook.Scale(boss.Tier)).sqrMagnitude > 1e-6f)
        {
            boss.BaseScale = graphic.transform.localScale;
            if (++boss.ScaleResets > MaxScaleResets) boss.Scaled = false;
            else graphic.transform.localScale = boss.BaseScale * TierLook.Scale(boss.Tier);
        }

        // Some bodies only show up a moment after their display exists (Diamondback's segments)
        if (boss.Aura == null) boss.Aura = CreateAura(boss, graphic);
    }

    private static void Apply(Tracked boss)
    {
        if (GlowShader == null)
        {
            if (!warned) ModHelper.Warning<CustomBossChallenge>($"Shader {GlowShaderName} not found, tiers above 5 keep the tier 5 look");
            warned = true;
            return;
        }

        try
        {
            var graphic = boss.Graphic!;
            var color = ToColor(TierLook.Color(boss.Boss, boss.Tier));
            foreach (var renderer in graphic.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name == AuraName) continue;
                SetColors(renderer, color, boss.Tier, out var hadBlock, out var changed);
                var main = MainMaterial(renderer);
                if (main == null)
                {
                    // Effects like Vortex's lightning only take the colour
                    if (changed) boss.Renderers.Add(new Applied(renderer, hadBlock));
                    continue;
                }

                var applied = new Applied(renderer, hadBlock);
                boss.Renderers.Add(applied);
                AddGlow(boss, applied);
            }

            boss.BaseScale = graphic.transform.localScale;
            graphic.transform.localScale = boss.BaseScale * TierLook.Scale(boss.Tier);
            boss.Scaled = true;

            boss.Aura = CreateAura(boss, graphic);
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Couldn't give {boss.Bloon.bloonModel.id} the tier {boss.Tier} look: {e.Message}");
            Restore(boss);
        }
    }

    /// <summary>
    /// The renderer's own material for the body, which the glow copies the texture of, or null for effects, overlays
    /// (fire, glue) and shadows
    /// </summary>
    private static Material? MainMaterial(Renderer renderer)
    {
        if (renderer.TryCast<ParticleSystemRenderer>() != null || renderer.TryCast<SpriteRenderer>() != null) return null;

        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null || material.shader == null || material.renderQueue >= (int) RenderQueue.GeometryLast) continue;
            var name = material.shader.name;
            if (name.Contains("Outline") && !name.Contains("SimpleUnlitOutline") || name.Contains("Stencil") ||
                name.Contains("Overlay") || name.Contains("Shadow")) continue;
            if (Texture(material) != null) return material;
        }

        return null;
    }

    private static Texture? Texture(Material material)
    {
        foreach (var name in TextureNames)
        {
            if (material.HasProperty(name) && material.GetTexture(name) is { } texture) return texture;
        }

        return null;
    }

    private static void AddGlow(Tracked boss, Applied applied)
    {
        var main = MainMaterial(applied.Renderer);
        if (main == null) return;

        var glow = Glow(boss, Texture(main)!);
        var materials = applied.Renderer.sharedMaterials.ToList();
        materials.Add(glow);
        applied.Renderer.sharedMaterials = materials.ToArray();
    }

    private static bool HasGlow(Renderer renderer)
    {
        foreach (var material in renderer.sharedMaterials)
        {
            if (material != null && GlowPointers.Contains(material.Pointer)) return true;
        }

        return false;
    }

    /// <summary>
    /// Takes the glow off a renderer and leaves its other materials as they are now: the game may have swapped them
    /// since the glow was added
    /// </summary>
    private static void RemoveGlow(Renderer renderer)
    {
        if (!HasGlow(renderer)) return;
        renderer.sharedMaterials = renderer.sharedMaterials.Where(material => material == null || !GlowPointers.Contains(material.Pointer)).ToArray();
    }

    /// <summary>
    /// Outline and effect colours go through the renderer's property block, so the game's materials stay untouched
    /// </summary>
    private static void SetColors(Renderer renderer, Color color, int tier, out bool hadBlock, out bool changed)
    {
        hadBlock = renderer.HasPropertyBlock();
        changed = false;
        renderer.GetPropertyBlock(Block);
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            if (material.HasProperty(OutlineColor))
            {
                Block.SetColor(OutlineColor, color);
                changed = true;
            }

            if (material.HasProperty(OutlineThickness))
            {
                Block.SetFloat(OutlineThickness, material.GetFloat(OutlineThickness) * (1 + 0.15f * TierLook.Step(tier)));
                changed = true;
            }

            foreach (var id in EffectColors)
            {
                if (!material.HasProperty(id)) continue;
                Block.SetColor(id, color);
                changed = true;
            }
        }

        if (changed) renderer.SetPropertyBlock(Block);
        Block.Clear();
    }

    private static Material Glow(Tracked boss, Texture texture)
    {
        var key = (boss.Boss, boss.Tier, texture.Pointer);
        if (Glows.TryGetValue(key, out var glow) && glow != null) return glow;

        var color = ToColor(TierLook.Color(boss.Boss, boss.Tier));
        if (TierGlowBundle.Shader is { } shader)
        {
            // The mod's own shader pulses by itself
            glow = new Material(shader) {name = $"TierGlow {boss.Boss} {boss.Tier}", hideFlags = HideFlags.DontUnloadUnusedAsset};
            glow.SetColor(TierColor, color);
            glow.SetFloat(Strength, 1);
            glow.SetFloat(RimStrength, TierLook.RimStrength(boss.Tier));
            glow.SetFloat(FillStrength, TierLook.FillStrength(boss.Tier));
            glow.SetFloat(ShimmerStrength, TierLook.ShimmerStrength(boss.Tier));
            glow.SetFloat(PulseAmplitude, TierLook.PulseAmplitude(boss.Tier));
            glow.SetFloat(PulsePeriod, TierLook.PulsePeriod(boss.Tier));
        }
        else
        {
            glow = Additive($"TierGlow {boss.Boss} {boss.Tier}");
            Pulses.Add(new Pulsing(glow, color, TierLook.GlowAlpha(boss.Tier), boss.Tier));
        }

        glow.SetTexture(BaseMap, texture);
        Glows[key] = glow;
        GlowPointers.Add(glow.Pointer);
        return glow;
    }

    private static readonly int TierColor = Shader.PropertyToID("_TierColor");
    private static readonly int Strength = Shader.PropertyToID("_Strength");
    private static readonly int RimStrength = Shader.PropertyToID("_RimStrength");
    private static readonly int FillStrength = Shader.PropertyToID("_FillStrength");
    private static readonly int ShimmerStrength = Shader.PropertyToID("_ShimmerStrength");
    private static readonly int PulseAmplitude = Shader.PropertyToID("_PulseAmplitude");
    private static readonly int PulsePeriod = Shader.PropertyToID("_PulsePeriod");

    /// <summary>
    /// A material that adds its colour to what is behind it
    /// </summary>
    private static Material Additive(string name)
    {
        // Kept when the game unloads unused assets, so the shared materials and the shader stay usable
        var material = new Material(GlowShader!) {name = name, hideFlags = HideFlags.DontUnloadUnusedAsset};
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 2);
        material.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float) BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float) BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float) BlendMode.One);
        material.SetFloat("_ZWrite", 0);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int) RenderQueue.Transparent;
        return material;
    }

    public const string AuraName = "CustomBossTierAura";

    private static GameObject? CreateAura(Tracked boss, UnityDisplayNode graphic)
    {
        var bounds = BodyBounds(boss);
        if (bounds == null) return null;

        var aura = new GameObject(AuraName);
        aura.transform.SetParent(graphic.transform, false);
        var size = Mathf.Max(bounds.Value.size.x, bounds.Value.size.y, bounds.Value.size.z);
        PlaceAura(boss, aura.transform, Camera.main.transform);
        var parentScale = graphic.transform.lossyScale.x;
        aura.transform.localScale = Vector3.one * (size * TierLook.AuraSize(boss.Tier) / Mathf.Max(parentScale, 0.0001f));

        var renderer = aura.AddComponent<SpriteRenderer>();
        renderer.sprite = AuraSprite;
        renderer.sharedMaterial = AuraMaterial(boss);
        return aura;
    }

    private static Bounds? BodyBounds(Tracked boss)
    {
        Bounds? bounds = null;
        foreach (var applied in boss.Renderers)
        {
            if (applied.Renderer == null || !applied.Renderer.enabled || !applied.Renderer.gameObject.activeInHierarchy ||
                MainMaterial(applied.Renderer) == null) continue;
            if (bounds == null) bounds = applied.Renderer.bounds;
            else
            {
                var b = bounds.Value;
                b.Encapsulate(applied.Renderer.bounds);
                bounds = b;
            }
        }

        return bounds;
    }

    private static Material AuraMaterial(Tracked boss)
    {
        if (Auras.TryGetValue((boss.Boss, boss.Tier), out var material) && material != null) return material;

        material = Additive($"TierAura {boss.Boss} {boss.Tier}");
        material.SetTexture(BaseMap, AuraTexture);
        material.SetTexture("_MainTex", AuraTexture);
        Auras[(boss.Boss, boss.Tier)] = material;
        Pulses.Add(new Pulsing(material, ToColor(TierLook.Color(boss.Boss, boss.Tier)), TierLook.AuraAlpha(boss.Tier), boss.Tier));
        return material;
    }

    /// <summary>
    /// A soft round spot, one unit across, made once
    /// </summary>
    private static Texture2D AuraTexture
    {
        get
        {
            if (auraTexture != null) return auraTexture;

            const int size = 128;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    var alpha = Mathf.Clamp01(1 - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte) (Mathf.SmoothStep(0, 1, alpha) * 255));
                }
            }

            auraTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {name = "CustomBossTierAura", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset};
            auraTexture.SetPixels32(pixels);
            auraTexture.Apply(false, true);
            return auraTexture;
        }
    }

    private static Sprite AuraSprite
    {
        get
        {
            if (auraSprite != null) return auraSprite;
            var texture = AuraTexture;
            auraSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            auraSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return auraSprite;
        }
    }

    private static void Pulse()
    {
        var time = Time.unscaledTime;
        foreach (var pulsing in Pulses)
        {
            var color = pulsing.Color;
            color.a = Mathf.Clamp01(pulsing.Alpha * TierLook.Pulse(pulsing.Tier, time));
            pulsing.Material.SetColor(BaseColor, color);
        }

        // The aura stays round and behind the body however the boss turns
        var camera = Camera.main;
        if (camera == null) return;
        foreach (var boss in Bosses)
        {
            if (boss.Aura != null) PlaceAura(boss, boss.Aura.transform, camera.transform);
        }
    }

    /// <summary>
    /// Centres the aura on the bloon, where the game puts the middle of the body: the bounds of animated meshes can be
    /// out of date
    /// </summary>
    private static void PlaceAura(Tracked boss, Transform aura, Transform camera)
    {
        aura.rotation = camera.rotation;
        aura.position = boss.Graphic!.transform.position;
    }

    private static void Restore(Tracked boss)
    {
        foreach (var applied in boss.Renderers)
        {
            if (applied.Renderer == null) continue;
            RemoveGlow(applied.Renderer);
            if (!applied.HadBlock)
            {
                applied.Renderer.SetPropertyBlock(null);
                continue;
            }

            // Another block was there before: put the materials' own values back into it
            applied.Renderer.GetPropertyBlock(Block);
            foreach (var material in applied.Renderer.sharedMaterials)
            {
                if (material == null) continue;
                if (material.HasProperty(OutlineColor)) Block.SetColor(OutlineColor, material.GetColor(OutlineColor));
                if (material.HasProperty(OutlineThickness)) Block.SetFloat(OutlineThickness, material.GetFloat(OutlineThickness));
                foreach (var id in EffectColors)
                {
                    if (material.HasProperty(id)) Block.SetColor(id, material.GetColor(id));
                }
            }

            applied.Renderer.SetPropertyBlock(Block);
            Block.Clear();
        }

        if (boss.Scaled && boss.Graphic != null) boss.Graphic.transform.localScale = boss.BaseScale;

        if (boss.Aura != null) Object.Destroy(boss.Aura);
        boss.Renderers.Clear();
        boss.Scaled = false;
        boss.ScaleResets = 0;
        boss.Aura = null;
        boss.Graphic = null;
    }

    private static Color ToColor(TierColor color) => new(color.R, color.G, color.B, 1);
}
