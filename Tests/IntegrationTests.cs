using System.Collections;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Simulation.Bloons;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Plays a vanilla and then a custom Bloonarius challenge on the same map, and compares the spawned bosses.
/// Proves that vanilla games stay untouched and that every custom rule takes effect in a real match.
/// </summary>
public class BossChallengeGameTest : CustomBossTest
{
    private Bloon? boss;
    private double startCash;

    protected override IEnumerator Run()
    {
        // Vanilla game
        yield return LoadBossChallenge(BaseDcm());

        Assert(!CustomBossState.IsActive, "Vanilla challenge became custom");
        AssertEquals(SkullModifier.GetSkulls(boss!.bloonModel)!.Length, 3, "Vanilla boss skull count");
        var vanillaSpeed = boss.bloonModel.speed;
        var vanillaHealth = boss.health;
        Log($"vanilla boss speed {vanillaSpeed}, health {vanillaHealth}");

        yield return ToMainMenu();

        // Custom game, started as ranked to check that it gets forced to unranked
        var options = CustomOptions;
        var editorDcm = CustomEditorDcm();
        yield return LoadBossChallenge(CustomBossState.Prepare(editorDcm, options), ranked: true);

        Assert(CustomBossState.IsActive, "Custom state is not active in game");
        var game = AssertNotNull(InGameData.CurrentGame, "No current game");
        Assert(!game.bossData.bossRankedMode, "Custom game is ranked");
        Assert(game.disableSaves, "Custom game can be saved");

        Assert(System.Math.Abs(startCash - editorDcm.startRules.cash) < 1, $"Starting cash was {startCash}");

        var speedRatio = boss!.bloonModel.speed / vanillaSpeed;
        var healthRatio = boss.health / (float) vanillaHealth;
        Log($"custom boss speed {boss.bloonModel.speed} (x{speedRatio}), health {boss.health} (x{healthRatio})");

        Assert(System.Math.Abs(speedRatio - editorDcm.bloonModifiers.bossSpeedMultiplier) < 0.05f,
            $"Boss speed ratio was {speedRatio}");
        Assert(System.Math.Abs(healthRatio - editorDcm.bloonModifiers.healthMultipliers.boss) < 0.05f,
            $"Boss health ratio was {healthRatio}");
        Assert(SameValues(SkullModifier.GetSkulls(boss.bloonModel), options.SkullPercents),
            $"Boss skulls are {Show(SkullModifier.GetSkulls(boss.bloonModel))}");

        Assert(SameValues(SkullModifier.GetSkulls(Game.instance.model.GetBloon("Bloonarius1")),
            new[] {0.75f, 0.5f, 0.25f}), "The base game model must stay untouched");

        yield return ToMainMenu();
    }

    private IEnumerator LoadBossChallenge(DailyChallengeModel dcm, bool ranked = false)
    {
        // Profile bonuses (double cash, monkey knowledge) would make the starting cash depend on the test account
        dcm.disableDoubleCash = true;
        dcm.disableMK = true;
        var data = SetupBossChallenge(dcm, BossType.Bloonarius, ranked);
        yield return LoadIntoGame(data);
        startCash = InGame.instance.GetCash();

        var bossManager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        bossManager.SpawnBoss(1, false);
        yield return null;
        boss = AssertNotNull(bossManager.CurrentBoss, "Boss did not spawn");
    }
}
