using System;
using System.Collections;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Models.Towers.Filters;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Extra tier models can be made for every boss, normal and elite
/// </summary>
public class ExtraTierModelTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        foreach (var boss in Enum.GetValues<BossType>())
        {
            foreach (var elite in new[] {false, true})
            {
                var options = new BossOptions {Boss = boss, Elite = elite, Tiers = 10, ExtraTierHealth = 150};
                var tier5 = AssertNotNull(Game.instance.model.GetBloon(options.BossId(5)), $"No {options.BossId(5)}");
                var skulls = SkullModifier.GetSkulls(tier5);

                for (var tier = 6; tier <= 10; tier++)
                {
                    var model = ExtraTiers.CreateTier(tier5, options, tier);
                    AssertEquals(model.id, options.BossId(tier), "Extra tier id");
                    AssertEquals(model.name, options.BossId(tier), "Extra tier name");
                    Assert(model.isBoss && !model.isBossSegment, $"{model.id} must be a boss");
                    AssertEquals(model.baseId, tier5.baseId, $"{model.id} base id");
                    AssertEquals(model.maxHealth,
                        TierMath.ScaleHealth(tier5.maxHealth, Math.Pow(1.5, tier - 5)), $"{model.id} health");
                    Assert(SameValues(SkullModifier.GetSkulls(model), skulls), $"{model.id} skulls");
                }

                AssertEquals(tier5.id, options.BossId(5), "The tier 5 model must stay untouched");
            }
        }

        Assert(!Game.instance.model.DoesBloonExist("Bloonarius6"), "Extra tiers must not leak into the base game model");
        yield break;
    }
}

/// <summary>
/// Plays real custom games with a strong tower: the boss spawns on its own on the chosen rounds, and the game is
/// won exactly when the last chosen tier is defeated - both with more and with fewer tiers than vanilla
/// </summary>
public class ExtraTierGameTest : CustomBossTest
{
    public const float TimeoutSeconds = 240;

    protected override IEnumerator Run()
    {
        yield return PlayToVictory(new BossOptions {Tiers = 6, ExtraTierHealth = 200, SpawnRounds = "40,41,42,43,44,45"});
        yield return ToMainMenu();
        // On easy a normal game ends at round 40, which used to end boss games after the first tier
        yield return PlayToVictory(new BossOptions {Tiers = 2, SpawnRounds = "40,45"}, "Easy");
        yield return ToMainMenu();
    }

    private IEnumerator PlayToVictory(BossOptions options, string difficulty = "Medium")
    {
        var dcm = BaseDcm();
        dcm.difficulty = difficulty;
        dcm.startRules.round = 39;
        dcm.startRules.lives = dcm.startRules.maxLives = 5000;
        dcm.bloonModifiers.healthMultipliers.boss = 0.1f;
        dcm.disableMK = true;
        var data = SetupBossChallenge(CustomBossState.Prepare(dcm, options), options.Boss);
        yield return LoadIntoGame(data);

        var gameModel = InGame.instance.GetGameModel();
        Assert(ExtraTiers.HasAllTiers(gameModel, options), "The match is missing boss tier models");
        if (options.Tiers > 5)
        {
            AssertEquals(gameModel.GetBloon(options.BossId(6)).maxHealth,
                gameModel.GetBloon(options.BossId(5)).maxHealth * 2, "Tier 6 health at 200% per tier");
        }

        var bossManager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        AssertEquals(TierMath.FormatRounds(bossManager.spawnRounds), TierMath.FormatRounds(options.GameSpawnRounds),
            "Spawn rounds in the match");

        PlaceStrongTower(gameModel.GetTowerFromId("DartMonkey"));
        InGame.instance.bridge.SetFastForward(true);

        var highestTier = 0;
        var start = Time.realtimeSinceStartup;
        while (!InGame.instance.bridge.Simulation.gameWon)
        {
            if (InGame.instance.bridge.Simulation.GameOver) Fail($"The game was lost on round {InGame.instance.bridge.GetCurrentRound() + 1}");
            if (Time.realtimeSinceStartup - start > TimeoutSeconds)
            {
                Fail($"No victory after {TimeoutSeconds}s; highest tier {highestTier}, " +
                     $"round {InGame.instance.bridge.GetCurrentRound()}");
            }

            // The tier counter moves on once the last boss is defeated, so only count tiers that have a boss
            if (bossManager.CurrentBossTier > highestTier && bossManager.CurrentBoss != null)
            {
                highestTier = bossManager.CurrentBossTier;
                AssertEquals(bossManager.CurrentBoss.bloonModel.id, options.BossId(highestTier), "Spawned boss");
                Log($"tier {highestTier} spawned on round {InGame.instance.bridge.GetCurrentRound() + 1}");
                AssertEquals(InGame.instance.bridge.GetCurrentRound() + 1, options.EffectiveSpawnRounds[highestTier - 1],
                    $"Round tier {highestTier} spawned on");
            }

            if (!InGame.instance.bridge.AreRoundsActive()) InGame.instance.bridge.StartRound();
            yield return new WaitForSecondsRealtime(0.25f);
        }

        AssertEquals(highestTier, options.Tiers, "Victory must come with the last chosen tier");
        Log($"won after tier {highestTier}");
    }

    /// <summary>
    /// Makes the match's dart monkey pop everything on screen, including bosses
    /// </summary>
    private static void PlaceStrongTower(TowerModel towerModel)
    {
        foreach (var attack in towerModel.GetAttackModels())
        {
            attack.range = 2000;
            foreach (var weapon in attack.weapons)
            {
                weapon.rate = 0.02f;
                weapon.projectile.pierce = 100000;
                var damage = weapon.projectile.GetDamageModel();
                if (damage == null) continue;
                damage.damage = 1000000;
                damage.immuneBloonProperties = Il2Cpp.BloonProperties.None;
            }
        }

        towerModel.range = 2000;
        towerModel.GetDescendants<FilterInvisibleModel>().ForEach(filter => filter.isActive = false);
        CreateTowerAt(InGame.instance.bridge, Vector2.zero, towerModel, costOverride: 0);
    }
}
