using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Visuals;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Simulation.Bloons;
using Il2CppAssets.Scripts.Simulation.Track;
using Il2CppAssets.Scripts.Unity.Display;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using Path = System.IO.Path;
using Object = UnityEngine.Object;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Spawns the tiers of a boss one after another in a custom game, holding each one still in the middle of the map
/// </summary>
internal static class TierLookScene
{
    public const float SpawnTimeout = 15;

    /// <summary>
    /// Bosses to look at, all by default; <c>CBC_TIERLOOK_BOSSES=Bloonarius,Lych</c> picks some
    /// </summary>
    public static BossType[] Bosses
    {
        get
        {
            var chosen = System.Environment.GetEnvironmentVariable("CBC_TIERLOOK_BOSSES");
            return string.IsNullOrWhiteSpace(chosen)
                ? Enum.GetValues<BossType>()
                : chosen.Split(',').Select(name => Enum.Parse<BossType>(name.Trim())).ToArray();
        }
    }

    /// <summary>
    /// How far along the track the boss is held, about the middle of the screen
    /// </summary>
    public const float Distance = 600;

    public static BossBloonManager Manager => InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;

    /// <summary>
    /// A custom game with all 10 tiers, whose own spawn rounds are far away so only the test spawns bosses
    /// </summary>
    public static IEnumerator Start(BossOptions options, Func<InGameData, IEnumerator> loadIntoGame)
    {
        var dcm = BaseDcm();
        dcm.startRules.round = 1;
        dcm.startRules.lives = dcm.startRules.maxLives = 5000;
        var data = SetupBossChallenge(CustomBossState.Prepare(dcm, options with {Tiers = 10, SpawnRounds = "500,501,502,503,504,505,506,507,508,509"}),
            options.Boss, elite: options.Elite);
        yield return loadIntoGame(data);
        yield return new WaitForSecondsRealtime(2);
    }

    /// <summary>
    /// Spawns the boss's <paramref name="tier"/> and holds it and its segments still on the track
    /// </summary>
    public static IEnumerator Spawn(BossOptions options, int tier, Action<Bloon> spawned)
    {
        var manager = Manager;
        manager.SpawnBoss(tier, false);
        Bloon? boss = null;
        var start = Time.realtimeSinceStartup;
        while ((boss = manager.CurrentBoss) == null || boss.bloonModel.id != options.BossId(tier) || boss.GetUnityDisplayNode() == null)
        {
            if (Time.realtimeSinceStartup - start > SpawnTimeout)
            {
                throw new Exception($"Tier {tier} did not spawn: current boss {manager.CurrentBoss?.bloonModel.id}, tier {manager.CurrentBossTier}");
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }

        // Diamondback's segments come out one after another; wait for all of them, then move the whole body along
        var count = 0;
        for (var i = 0; i < 40; i++)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            var now = BossBloons().Count;
            if (now == count && i >= 4) break;
            count = now;
        }

        // Diamondback comes out of the ground in an animation of its own; its segments only show once that is over
        start = Time.realtimeSinceStartup;
        while (!BossBloons().Any(bloon => bloon.GetUnityDisplayNode()?.GetComponentsInChildren<Renderer>().ToArray()
                   .Any(renderer => renderer.enabled && renderer.TryCast<ParticleSystemRenderer>() == null) == true))
        {
            if (Time.realtimeSinceStartup - start > SpawnTimeout) throw new Exception($"Tier {tier} never showed");
            yield return new WaitForSecondsRealtime(0.25f);
        }

        var bloons = BossBloons();
        var shift = (options.Boss == BossType.Diamondback ? Distance - 150 : Distance) - boss.DistanceTraveled;
        foreach (var bloon in bloons)
        {
            bloon.SetPath(bloon.path, bloon.DistanceTraveled + shift, true);
            bloon.stopMovementUntilPop = true;
        }
        yield return new WaitForSecondsRealtime(1);
        spawned(boss);
    }

    public static List<Bloon> BossBloons() =>
        InGame.instance.GetBloons().ToArray().Where(bloon => bloon.bloonModel.isBoss || bloon.bloonModel.isBossSegment).ToList();

    public static void RemoveBosses()
    {
        foreach (var bloon in InGame.instance.GetBloons().ToArray()) bloon.Destroy();
    }

    public static List<UnityDisplayNode> Graphics() =>
        BossBloons().Select(bloon => bloon.GetUnityDisplayNode()).Where(graphic => graphic != null).ToList();

    /// <summary>
    /// Where the bosses' bodies are on the screen
    /// </summary>
    public static Vector2 ScreenCenter(List<UnityDisplayNode> graphics)
    {
        // Diamondback's own display is empty: its body is the segments
        graphics = graphics.Where(graphic => graphic.GetComponentsInChildren<Renderer>().ToArray().Any(renderer => renderer.enabled)).ToList();
        if (graphics.Count == 0) return new Vector2(Screen.width / 2f, Screen.height / 2f);
        var center = graphics.Aggregate(Vector3.zero, (sum, graphic) => sum + graphic.transform.position) / graphics.Count;
        return Camera.main.WorldToScreenPoint(center);
    }

    /// <summary>
    /// Saves a square part of the screen around <paramref name="center"/>
    /// </summary>
    public static IEnumerator Crop(string path, Vector2 center, int side = 560)
    {
        yield return new WaitForEndOfFrame();
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        side = Math.Min(side, Math.Min(shot.width, shot.height));
        var x = Mathf.Clamp((int) center.x - side / 2, 0, shot.width - side);
        var y = Mathf.Clamp((int) center.y - side / 2, 0, shot.height - side);
        var crop = new Texture2D(side, side, TextureFormat.RGB24, false);
        crop.SetPixels(shot.GetPixels(x, y, side, side));
        crop.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(crop));
        Object.Destroy(shot);
        Object.Destroy(crop);
    }

    public static bool IsGlow(Material? material) => material != null && material.name.StartsWith("TierGlow");

    public static List<Material> Glows(UnityDisplayNode graphic) =>
        graphic.GetComponentsInChildren<Renderer>(true).ToArray().SelectMany(renderer => renderer.sharedMaterials.ToArray()).Where(IsGlow).ToList();

    public static Transform? Aura(UnityDisplayNode graphic) =>
        graphic.GetComponentsInChildren<Transform>(true).ToArray().FirstOrDefault(t => t.name == TierLookApplier.AuraName);
}

/// <summary>
/// Tiers above 5 get the look of their boss's theme, stronger per tier; tier 5 and the displays the game reuses keep
/// the game's look, and the look doesn't make new materials while the boss is alive
/// </summary>
public class TierLookTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        Assert(TierLookApplier.Available, $"Shader {TierLookApplier.GlowShaderName} is missing");
        // The released mod must carry a bundle the game can use; players without it only lose the rim glow
        Assert(TierGlowBundle.Shader != null, $"The glow shader can't be used: {TierGlowBundle.Problem}");

        foreach (var boss in TierLookScene.Bosses)
        {
            foreach (var elite in new[] {false, true})
            {
                var options = new BossOptions {Boss = boss, Elite = elite, Tiers = 10};
                yield return TierLookScene.Start(options, data => LoadIntoGame(data));
                yield return CheckBoss(options);
                yield return ToMainMenu();
                AssertEquals(TierLookApplier.Count, 0, "The look must be gone after leaving the game");
            }
        }
    }

    private IEnumerator CheckBoss(BossOptions options)
    {
        var name = options.BossId(0).TrimEnd('0');
        var colors = new List<Color>();
        for (var tier = 5; tier <= 10; tier++)
        {
            Bloon? boss = null;
            yield return TierLookScene.Spawn(options, tier, spawned => boss = spawned);
            yield return new WaitForSecondsRealtime(TierLookApplier.CheckInterval * 3);
            var graphics = TierLookScene.Graphics();
            Assert(graphics.Count > 0, $"{name} {tier} has no display");

            if (!TierLook.HasLook(tier))
            {
                AssertEquals(TierLookApplier.Count, 0, $"{name} tier {tier} must keep the game's look");
                Assert(graphics.All(graphic => TierLookScene.Glows(graphic).Count == 0 && TierLookScene.Aura(graphic) == null),
                    $"{name} tier {tier} has a glow or aura");
            }
            else
            {
                var glows = graphics.SelectMany(TierLookScene.Glows).ToList();
                Assert(glows.Count > 0, $"{name} tier {tier} has no glow");
                Assert(graphics.Any(graphic => TierLookScene.Aura(graphic) != null), $"{name} tier {tier} has no aura");
                var color = glows[0].GetColor(glows[0].HasProperty("_TierColor") ? "_TierColor" : "_BaseColor");
                AssertEquals(glows[0].shader.name, TierGlowBundle.ShaderName, $"{name} tier {tier} glow shader");
                var expected = TierLook.Color(options.Boss.ToString(), tier);
                Assert(Math.Abs(color.r - expected.R) < 0.01f && Math.Abs(color.g - expected.G) < 0.01f && Math.Abs(color.b - expected.B) < 0.01f,
                    $"{name} tier {tier} glows in {color}, expected {expected.Hex}");
                Assert(colors.All(other => Math.Abs(other.r - color.r) + Math.Abs(other.g - color.g) + Math.Abs(other.b - color.b) > 0.1f),
                    $"{name} tier {tier} has the colour of an earlier tier");
                colors.Add(color);

                if (tier == 10) yield return CheckNoNewMaterials(name);
            }

            // Removing the boss takes the look off its display before the game can reuse it
            var scales = graphics.Select(graphic => graphic.transform.localScale).ToList();
            var scaled = graphics.Select(TierLookApplier.IsScaled).ToList();
            TierLookScene.RemoveBosses();
            yield return new WaitForSecondsRealtime(TierLookApplier.CheckInterval * 3);
            for (var g = 0; g < graphics.Count; g++)
            {
                var graphic = graphics[g];
                if (graphic == null) continue;
                AssertEquals(TierLookScene.Glows(graphic).Count, 0, $"{name} tier {tier} kept its glow");
                Assert(TierLookScene.Aura(graphic) == null, $"{name} tier {tier} kept its aura");
                var scale = graphic.transform.localScale;
                var expected = scaled[g] ? scales[g] / TierLook.Scale(tier) : scales[g];
                Assert((scale - expected).magnitude < 0.001f, $"{name} tier {tier} kept its size: {scale}, expected {expected}");
            }
        }
    }

    /// <summary>
    /// The pulse only changes colours: no new materials while the boss is alive
    /// </summary>
    private IEnumerator CheckNoNewMaterials(string name)
    {
        var before = Resources.FindObjectsOfTypeAll<Material>().Length;
        for (var i = 0; i < 300; i++) yield return null;
        var after = Resources.FindObjectsOfTypeAll<Material>().Length;
        Assert(after <= before, $"{name}: {after - before} materials were made in 300 frames");
        Log($"{name}: {before} materials before and {after} after 300 frames");
    }
}

/// <summary>
/// Saves tiers 5-10 of every boss next to each other for visual checks and the README. Does nothing unless the game
/// is started (without -batchmode) with <c>CBC_SCREENSHOTS=&lt;folder&gt;</c>; <c>CBC_TIERLOOK_BOSSES</c> picks bosses.
/// </summary>
public class TierLookTour : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var folder = ScreenshotTour.Folder;
        if (string.IsNullOrEmpty(folder))
        {
            Log("skipped, no screenshot folder given");
            yield break;
        }

        folder = Path.Combine(folder, "tier-look");
        foreach (var boss in TierLookScene.Bosses)
        {
            var options = new BossOptions {Boss = boss, Tiers = 10};
            yield return TierLookScene.Start(options, data => LoadIntoGame(data));
            for (var tier = 5; tier <= 10; tier++)
            {
                yield return TierLookScene.Spawn(options, tier, _ => { });
                yield return new WaitForSecondsRealtime(1);
                var center = TierLookScene.ScreenCenter(TierLookScene.Graphics());
                yield return TierLookScene.Crop(Path.Combine(folder, $"{boss}-{tier}.png"), center, boss == BossType.Diamondback ? 760 : 560);
                if (tier is 5 or 7 or 10)
                {
                    var ui = Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.InGame.Races.BossUI>();
                    if (ui?.stars is {Length: > 2} stars)
                    {
                        var rect = LayoutChecks.ScreenRect(stars[2].rectTransform);
                        yield return TierLookScene.Crop(Path.Combine(folder, $"{boss}-panel-{tier}.png"), rect.center, 360);
                    }
                }
                if (tier < 10)
                {
                    TierLookScene.RemoveBosses();
                    yield return new WaitForSecondsRealtime(1);
                }
            }

            yield return ToMainMenu();
        }
    }
}

/// <summary>
/// The boss panel shows tiers above 5, a restart takes the look off the old boss and gives it to the new one, and a
/// normal Boss Challenge never gets the look or the tier number
/// </summary>
public class TierLookLifecycleTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Boss = BossType.Vortex, Tiers = 10};
        yield return TierLookScene.Start(options, data => LoadIntoGame(data));
        yield return SpawnTier7(options);
        var graphics = TierLookScene.Graphics();

        var simulation = InGame.instance.bridge.Simulation.Pointer;
        InGame.instance.RunRestart();
        yield return WaitFor(() => InGame.instance?.bridge?.Simulation != null && InGame.instance.bridge.Simulation.Pointer != simulation,
            "Restarting", 30);
        yield return new WaitForSecondsRealtime(1);
        AssertEquals(TierLookApplier.Count, 0, "The look must be gone after a restart");
        foreach (var graphic in graphics.Where(graphic => graphic != null))
        {
            AssertEquals(TierLookScene.Glows(graphic).Count, 0, "The old boss's display kept its glow after a restart");
        }

        yield return SpawnTier7(options);
        yield return ToMainMenu();

        // A normal Boss Challenge, set up like the Boss Challenge screen does, uses the same panel
        yield return LoadIntoGame(SetupBossChallenge(BaseDcm(), BossType.Vortex));
        Assert(!CustomBossState.IsActive, "A normal Boss Challenge must not be custom");
        TierLookScene.Manager.SpawnBoss(5, false);
        yield return WaitFor(() => TierLookScene.Manager.CurrentBoss?.GetUnityDisplayNode() != null, "Spawning tier 5");
        yield return new WaitForSecondsRealtime(TierLookApplier.CheckInterval * 3);
        AssertEquals(TierLookApplier.Count, 0, "A normal Boss Challenge must keep the game's look");
        var ui = Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.InGame.Races.BossUI>();
        if (ui != null)
        {
            var label = ui.stars[ui.stars.Length - 1].transform.parent.Find(TierStars.LabelName);
            Assert(label == null || !label.gameObject.activeSelf, "A normal Boss Challenge shows a tier number");
            Assert(ui.stars.All(star => star.color == ui.starColorOn || star.color == ui.starColorOff), "A normal Boss Challenge has coloured stars");
        }

        yield return ToMainMenu();
    }

    private IEnumerator SpawnTier7(BossOptions options)
    {
        yield return TierLookScene.Spawn(options, 7, _ => { });
        yield return new WaitForSecondsRealtime(TierLookApplier.CheckInterval * 3);
        Assert(TierLookApplier.Count > 0, "Tier 7 has no look");

        var ui = AssertNotNull(Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.InGame.Races.BossUI>(), "No boss panel")!;
        var color = TierLook.Color(options.Boss.ToString(), 7);
        for (var i = 0; i < ui.stars.Length; i++)
        {
            var star = ui.stars[i].color;
            var coloured = Math.Abs(star.r - color.R) < 0.01f && Math.Abs(star.g - color.G) < 0.01f && Math.Abs(star.b - color.B) < 0.01f;
            Assert(coloured == i < 2, $"Star {i + 1} at tier 7 is {star}");
        }

        var label = ui.stars[ui.stars.Length - 1].transform.parent.Find(TierStars.LabelName);
        Assert(label != null && label.gameObject.activeSelf, "Tier 7 has no tier number");
        AssertEquals(label!.GetComponent<Il2CppTMPro.TMP_Text>().text, "7", "Tier number");
    }
}
