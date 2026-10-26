using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Unity;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using Object = UnityEngine.Object;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Restarting a custom game keeps it custom, with all its tiers, and starts the tier count over
/// </summary>
public class RestartTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 7, SpawnRounds = "45", SpeedPerTier = 30, HealthPerTier = 20};
        yield return LoadIntoGame(SetupBossChallenge(CustomBossState.Prepare(BaseDcm(), options), options.Boss));
        Assert(CustomBossState.IsActive, "Custom state is not active in game");

        // Like restarting from the victory screen: every tier was defeated in the last attempt
        CustomBossState.OnBossDefeated(options.Tiers);
        Assert(CustomBossState.AllTiersDefeated, "All tiers defeated before the restart");
        var simulation = InGame.instance.bridge.Simulation.Pointer;
        InGame.instance.RunRestart();

        var start = Time.realtimeSinceStartup;
        while (InGame.instance == null || InGame.instance.bridge?.Simulation == null ||
               InGame.instance.bridge.Simulation.Pointer == simulation)
        {
            if (Time.realtimeSinceStartup - start > 30) Fail("The game did not restart");
            yield return new WaitForSecondsRealtime(0.25f);
        }
        yield return new WaitForSecondsRealtime(1);

        Assert(CustomBossState.IsActive, "A restarted custom game must stay custom");
        AssertEquals(CustomBossState.HighestDefeatedTier, 0, "Defeated tiers after a restart");
        Assert(ExtraTiers.HasAllTiers(InGame.instance.GetGameModel(), options), "Tier models after a restart");
        AssertScaledTiers(InGame.instance.GetGameModel(), options);
        AssertEquals(TierMath.FormatRounds(InGame.instance.bridge.Simulation.map.spawner.bossBloonManager.spawnRounds),
            TierMath.FormatRounds(options.GameSpawnRounds), "Spawn rounds after a restart");
        Assert(InGameData.CurrentGame.disableSaves, "A restarted custom game can be saved");

        yield return ToMainMenu();
    }
}

/// <summary>
/// A boss that isn't defeated in time loses the game, and a custom game never turns that into a win
/// </summary>
public class BossNotDefeatedTest : CustomBossTest
{
    public const float TimeoutSeconds = 240;

    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 3, SpawnRounds = "40, 41, 43"};
        var dcm = BaseDcm();
        dcm.startRules.round = 39;
        dcm.startRules.lives = dcm.startRules.maxLives = 5000;
        yield return LoadIntoGame(SetupBossChallenge(CustomBossState.Prepare(dcm, options), options.Boss));
        Assert(CustomBossState.IsActive, "Custom state is not active in game");

        // No towers: neither tier can be defeated
        var bridge = InGame.instance.bridge;
        bridge.SetFastForward(true);
        var start = Time.realtimeSinceStartup;
        while (!bridge.Simulation.GameOver)
        {
            Assert(!bridge.Simulation.gameWon, $"Won on round {bridge.GetCurrentRound() + 1} without defeating a boss");
            if (Time.realtimeSinceStartup - start > TimeoutSeconds)
            {
                Fail($"The game didn't end after {TimeoutSeconds}s, round {bridge.GetCurrentRound() + 1}");
            }

            if (!bridge.AreRoundsActive()) bridge.StartRound();
            yield return new WaitForSecondsRealtime(0.25f);
        }

        Assert(!bridge.Simulation.gameWon, "A lost game must not be won");
        AssertEquals(CustomBossState.HighestDefeatedTier, 0, "Defeated tiers");
        Log($"lost on round {bridge.GetCurrentRound() + 1}");

        // The defeat screen tells the rounds the tier really had instead of the game's 20
        BossDefeatScreen? screen = null;
        start = Time.realtimeSinceStartup;
        while ((screen = Object.FindObjectOfType<BossDefeatScreen>()) == null)
        {
            if (Time.realtimeSinceStartup - start > 30) Fail("The defeat screen did not open");
            yield return new WaitForSecondsRealtime(0.25f);
        }
        AssertEquals(screen.ranOutOfTimeSubTxt.text, Texts.OutOfTimeTier.T(1, 1), "Out of time text on the defeat screen");

        // Lost on the round the tier arrived on: retrying that round and its checkpoint would be the same
        Assert(screen.retryButton.gameObject.activeSelf, "Retry Last Round is offered");
        Assert(!screen.continueButton.gameObject.activeSelf, "No checkpoint retry for the tier's own round");
        yield return ToMainMenu();
    }
}

/// <summary>
/// A lost custom game can be retried from the last round and from the round the tier arrived on, as often as the
/// player wants, without Monkey Money and without touching the player's own saves
/// </summary>
public class RetryTest : CustomBossTest
{
    internal static string SavedMaps()
    {
        var maps = new List<string>();
        foreach (var entry in Game.instance.playerService.Player.Data.savedMaps) maps.Add($"{entry.Key}@{entry.Value.round}");
        return string.Join(", ", maps.OrderBy(map => map));
    }

    protected override IEnumerator Run()
    {
        var savedMaps = SavedMaps();
        var options = new BossOptions {Tiers = 3, SpawnRounds = "40, 43, 46"};
        var dcm = BaseDcm();
        dcm.startRules.round = 38;
        dcm.startRules.lives = dcm.startRules.maxLives = 5000;
        yield return LoadIntoGame(SetupBossChallenge(CustomBossState.Prepare(dcm, options), options.Boss));
        var monkeyMoney = Game.instance.playerService.Player.Data.monkeyMoney.Value;

        // Tier 1 arrives on round 40 and is lost to before tier 2 arrives on 43
        BossDefeatScreen? screen = null;
        yield return LoseAndOpenDefeatScreen(found => screen = found);
        var lostRound = InGame.instance.bridge.GetCurrentRound() + 1;
        Assert(lostRound is > 40 and < 43, $"Lost on round {lostRound}, after the checkpoint and before tier 2");
        Assert(screen!.retryButton.gameObject.activeSelf, "Retry Last Round is offered");
        Assert(screen.continueButton.gameObject.activeSelf, "Retry From Checkpoint is offered");
        AssertEquals(screen.retryCostTxt.text, Texts.Free.T(), "Retry Last Round cost");
        AssertEquals(screen.continueCostTxt.text, Texts.Free.T(), "Retry From Checkpoint cost");
        AssertEquals(screen.continueDescTxt.text, Texts.RetryFromRound.T(40), "Retry From Checkpoint text");

        screen.retryButton.onClick.Invoke();
        yield return WaitForResume();
        AssertEquals(InGame.instance.bridge.GetCurrentRound() + 1, lostRound, "Round after Retry Last Round");
        AssertRetried(options, monkeyMoney, savedMaps);

        // Again, from the checkpoint this time
        yield return LoseAndOpenDefeatScreen(found => screen = found);
        screen!.continueButton.onClick.Invoke();
        yield return WaitForResume();
        AssertEquals(InGame.instance.bridge.GetCurrentRound() + 1, 40, "Round after Retry From Checkpoint");
        AssertRetried(options, monkeyMoney, savedMaps);

        yield return ToMainMenu();
        AssertEquals(SavedMaps(), savedMaps, "The player's map saves after leaving");
    }

    private void AssertRetried(BossOptions options, double monkeyMoney, string savedMaps)
    {
        var bridge = InGame.instance.bridge;
        Assert(!bridge.Simulation.GameOver, "The game goes on after a retry");
        Assert(CustomBossState.IsActive, "A retried custom game stays custom");
        AssertEquals(CustomBossState.HighestDefeatedTier, 0, "Defeated tiers after a retry");
        AssertEquals(TierMath.FormatRounds(bridge.Simulation.map.spawner.bossBloonManager.spawnRounds),
            TierMath.FormatRounds(options.GameSpawnRounds), "Spawn rounds after a retry");
        Assert(ExtraTiers.HasAllTiers(InGame.instance.GetGameModel(), options), "Tier models after a retry");
        AssertEquals(Game.instance.playerService.Player.Data.monkeyMoney.Value, monkeyMoney, "Monkey Money after a retry");
        AssertEquals(SavedMaps(), savedMaps, "The player's map saves after a retry");
    }
}
