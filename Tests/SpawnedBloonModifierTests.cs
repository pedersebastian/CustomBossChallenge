using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The bloons a boss spawns get All Camo, All Regrow and All Fortified in custom games (#90), for every boss, Elite or
/// not, and the extra tiers; the game's own Boss Challenge is left as it is
/// </summary>
public class SpawnedBloonModelTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var game = Game.instance.model;
        var original = game.GetBloon("Bloonarius1");
        var originalIds = SpawnedBloonModifiers.SpawnedIds(original);
        AssertEquals(originalIds.Contains("Ceramic"), true, $"Bloonarius spawns Ceramics: {string.Join(", ", originalIds)}");

        // Every modifier on, with extra tiers
        var dcm = BaseDcm();
        dcm.bloonModifiers.allCamo = dcm.bloonModifiers.allRegen = dcm.bloonModifiers.allFortified = true;
        yield return Play(dcm, new BossOptions {Boss = BossType.Bloonarius, Tiers = 7});
        var match = InGame.instance.GetGameModel();
        var bosses = match.bloons.Where(bloon => bloon.isBoss).ToList();
        foreach (var boss in bosses)
        {
            foreach (var id in SpawnedBloonModifiers.SpawnedIds(boss))
            {
                var further = DailyChallengeModel.AddCamoRegenToBloonString(id, true, true, true);
                Assert(further == id || match.GetBloon(further) == null,
                    $"{boss.id} spawns {id}, though {further} exists");
            }
        }
        Assert(SpawnedBloonModifiers.SpawnedIds(match.GetBloon("Bloonarius1")).Contains("CeramicRegrowFortifiedCamo"),
            "Bloonarius spawns camo regrow fortified Ceramics");
        Assert(SpawnedBloonModifiers.SpawnedIds(match.GetBloon("Bloonarius7")).Contains("RainbowRegrowCamo"),
            "The extra tier 7 spawns camo regrow Rainbows");
        Assert(SpawnedBloonModifiers.SpawnedIds(match.GetBloon("DiamondbackElite1")).Contains("MoabFortified"),
            "Elite Diamondback spawns fortified MOABs");
        AssertEquals(string.Join(", ", SpawnedBloonModifiers.SpawnedIds(match.GetBloon("Lych1"))),
            string.Join(", ", SpawnedBloonModifiers.SpawnedIds(game.GetBloon("Lych1"))), "Lych's minis have no camo, they stay");
        AssertEquals(string.Join(", ", SpawnedBloonModifiers.SpawnedIds(game.GetBloon("Bloonarius1"))), string.Join(", ", originalIds),
            "The game's own model is untouched");

        // Without the modifiers nothing changes
        yield return Play(BaseDcm(), new BossOptions {Boss = BossType.Bloonarius});
        AssertEquals(Ids("Bloonarius1"), string.Join(", ", originalIds), "No modifiers: Bloonarius spawns its own bloons");

        // Advanced Mode: every tier's boss gets them
        var regrow = BaseDcm();
        regrow.bloonModifiers.allRegen = true;
        yield return Play(regrow, new BossOptions {Tiers = 2, TierBosses = "Bloonarius,Diamondback"});
        Assert(SpawnedBloonModifiers.SpawnedIds(InGame.instance.GetGameModel().GetBloon("Diamondback2")).Contains("ZebraRegrow"),
            $"Tier 2's Diamondback spawns regrow Zebras: {Ids("Diamondback2")}");

        // The game's own Boss Challenge stays as it is
        yield return ToMainMenu();
        yield return LoadIntoGame(SetupBossChallenge(dcm, BossType.Bloonarius));
        Assert(!CustomBossState.IsActive, "A normal Boss Challenge must not be custom");
        AssertEquals(Ids("Bloonarius1"), string.Join(", ", originalIds), "The normal Boss Challenge's Bloonarius");
        yield return ToMainMenu();
    }

    private static string Ids(string boss) =>
        string.Join(", ", SpawnedBloonModifiers.SpawnedIds(InGame.instance.GetGameModel().GetBloon(boss)));

    private IEnumerator Play(DailyChallengeModel dcm, BossOptions options)
    {
        if (InGame.instance != null) yield return ToMainMenu();
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, BossGameLauncher.Resolve(options)));
    }
}

/// <summary>
/// In a real game the bloons Bloonarius spawns are camo with All Camo, and not without it (#90)
/// </summary>
public class SpawnedBloonGameTest : CustomBossTest
{
    public const float TimeoutSeconds = 90;

    protected override IEnumerator Run()
    {
        foreach (var camo in new[] {true, false})
        {
            var dcm = BaseDcm();
            dcm.bloonModifiers.allCamo = camo;
            dcm.bloonModifiers.healthMultipliers.boss = 0.05f;
            if (InGame.instance != null) yield return ToMainMenu();
            yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, BossGameLauncher.Resolve(new BossOptions {Boss = BossType.Bloonarius})));

            // The test tower hurts the boss, which spawns its Ceramics at its first health trigger
            var game = AssertNotNull(InGame.instance, "The game did not load")!;
            var model = game.GetGameModel();
            ExtraTierGameTest.PlaceStrongTower(model.GetTowerFromId("DartMonkey"));
            game.bridge.Simulation.map.spawner.bossBloonManager.SpawnBoss(1, false);
            game.bridge.SetFastForward(true);

            var seen = new HashSet<string>();
            var start = Time.realtimeSinceStartup;
            while (!seen.Any(id => id.StartsWith("Ceramic")))
            {
                if (Time.realtimeSinceStartup - start > TimeoutSeconds) Fail($"Bloonarius spawned no Ceramics, saw {string.Join(", ", seen)}");
                foreach (var bloon in game.GetBloons().ToArray())
                {
                    var bloonModel = bloon.bloonModel;
                    if (bloonModel.isBoss || !seen.Add(bloonModel.id)) continue;
                    Log($"All Camo {camo}: {bloonModel.id}");
                    AssertEquals(bloonModel.isCamo, camo, $"{bloonModel.id} is camo with All Camo {(camo ? "on" : "off")}");
                }
                // The rounds keep the game going; their bloons follow All Camo too
                if (!game.bridge.AreRoundsActive()) game.bridge.StartRound();
                yield return null;
            }
        }
        yield return ToMainMenu();
    }
}
