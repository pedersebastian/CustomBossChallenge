using System;
using System.Collections;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Guards <see cref="TierMath.MaxGameHealth"/>: the game multiplies a boss model's health by Boss Health (through a
/// float) into an int when a match starts, and a boss with more than about 2.147 billion health gets 1 health or less.
/// <br/>
/// In vanilla Boss Challenges Bloonarius 1 gets the most model health the mod allows for a Boss Health, which must
/// spawn with the expected health and take damage normally; health just above the game's limit is only logged, to
/// notice if a game update moves it. Custom games with a tier far above the limit must hold it to the limit.
/// </summary>
public class BossHealthLimitTest : CustomBossTest
{
    public const string BossId = "Bloonarius1";
    public const float DamageSeconds = 10;

    public static readonly float[] BossHealths = {1, 3, ChallengeLimits.MaxBossHealth};

    /// <summary>
    /// Health just above what an int holds once Boss Health is applied
    /// </summary>
    public const double OverLimit = 2_150_000_000;

    protected override IEnumerator Run()
    {
        var model = Game.instance.model.GetBloon(BossId);
        var original = model.maxHealth;
        try
        {
            foreach (var bossHealth in BossHealths)
            {
                model.maxHealth = TierMath.MaxModelHealth(bossHealth);
                yield return Vanilla(bossHealth, true);
                model.maxHealth = (int) Math.Ceiling(OverLimit / bossHealth);
                yield return Vanilla(bossHealth, false);
            }
        }
        finally
        {
            model.maxHealth = original;
        }

        // Tier 10 with +200 % health per tier is far above the limit with the highest Boss Health
        var options = new BossOptions {Tiers = 10, HealthPerTier = 200};
        var tier10 = Game.instance.model.GetBloon(options.BossId(5)).maxHealth * TierMath.ExtraTierFactor(10) *
                     TierMath.HealthFactor(10, options.HealthPerTier);
        foreach (var bossHealth in new[] {1, ChallengeLimits.MaxBossHealth})
        {
            var dcm = BaseDcm();
            dcm.bloonModifiers.healthMultipliers.boss = bossHealth;
            dcm.disableMK = true;
            yield return LoadIntoGame(SetupBossChallenge(CustomBossState.Prepare(dcm, options), options.Boss));
            Assert(CustomBossState.IsActive, "The custom game is not active");
            var expected = Math.Min(tier10, TierMath.MaxModelHealth(bossHealth)) * bossHealth;
            yield return SpawnAndDamage(10, expected, $"custom tier 10 ({tier10:N0}) x{bossHealth}", true);

            // The boss's health bar with the most health a boss can have, for a visual check
            if (ScreenshotTour.Folder is { Length: > 0 } folder)
            {
                InGame.instance.bridge.SetFastForward(false);
                yield return new WaitForSecondsRealtime(1);
                yield return ScreenshotTour.Capture(folder, $"boss-health-x{bossHealth}");
            }
            yield return ToMainMenu();
        }
    }

    private IEnumerator Vanilla(float bossHealth, bool mustWork)
    {
        var health = Game.instance.model.GetBloon(BossId).maxHealth;
        var dcm = BaseDcm();
        dcm.bloonModifiers.healthMultipliers.boss = bossHealth;
        dcm.disableMK = true;
        yield return LoadIntoGame(SetupBossChallenge(dcm, BossType.Bloonarius));
        Assert(!CustomBossState.IsActive, "The probe must run in a vanilla Boss Challenge");
        yield return SpawnAndDamage(1, (double) health * bossHealth, $"{BossId} {health:N0} x{bossHealth}", mustWork);
        yield return ToMainMenu();
    }

    /// <summary>
    /// Spawns a tier of the boss and lets an overpowered tower hit it: the boss must spawn with
    /// <paramref name="expected"/> health and lose health without dying at once
    /// </summary>
    private IEnumerator SpawnAndDamage(int tier, double expected, string what, bool mustWork)
    {
        var bossManager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        bossManager.SpawnBoss(tier, false);
        yield return null;
        var boss = bossManager.CurrentBoss;
        var spawned = boss != null ? (double) boss.health : double.NaN;

        ExtraTierGameTest.PlaceStrongTower(InGame.instance.GetGameModel().GetTowerFromId("DartMonkey"));
        InGame.instance.bridge.SetFastForward(true);
        InGame.instance.bridge.StartRound();
        var start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < DamageSeconds && bossManager.CurrentBoss != null &&
               Mathf.Approximately((float) bossManager.CurrentBoss.health, (float) spawned))
        {
            yield return null;
        }

        var after = bossManager.CurrentBoss;
        var damaged = after != null ? (double) after.health : double.NaN;
        var works = spawned > 0 && Math.Abs(spawned / expected - 1) < 0.001 && damaged > 0 && damaged < spawned;
        var result = $"{what}: spawned {spawned:N0}, expected {expected:N0}, " +
                     (after == null ? "gone after damage" : $"after damage {damaged:N0}");
        Log($"{(works ? "works" : "breaks")}, {result}");

        if (mustWork) Assert(works, $"The boss doesn't work: {result}");
        else if (works) Log($"health above {TierMath.MaxGameHealth:N0} works now, so the game's limit may have moved");
    }
}
