using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Main;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using static CustomBossChallenge.Tests.TestUtils;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Shared steps for the tests of continuing a custom game after the game was closed
/// </summary>
public abstract class ContinueTestBase : CustomBossTest
{
    protected static ContinueStore Store => CustomBossRetries.Store;

    /// <summary>
    /// Says what the value was when it isn't the expected one
    /// </summary>
    protected void AssertEquals<T>(T actual, T expected, string what)
    {
        if (!Equals(actual, expected)) Fail($"{what}: {actual?.ToString() ?? "null"}, expected {expected?.ToString() ?? "null"}");
    }

    protected static DailyChallengeModelBuilder Challenge(int startRound, float bossHealth = 1) => new(startRound, bossHealth);

    protected sealed record DailyChallengeModelBuilder(int StartRound, float BossHealth)
    {
        public Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel Build()
        {
            var dcm = BaseDcm();
            dcm.name = "Continue Test";
            dcm.startRules.round = StartRound;
            dcm.startRules.lives = dcm.startRules.maxLives = 5000;
            dcm.startRules.cash = 20000;
            dcm.bloonModifiers.healthMultipliers.boss = BossHealth;
            dcm.disableMK = true;
            return dcm;
        }
    }

    protected IEnumerator StartCustomGame(BossOptions options, Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel dcm)
    {
        yield return LoadIntoGame(SetupBossChallenge(CustomBossState.Prepare(dcm, options), options.Boss));
        Assert(CustomBossState.IsActive, "Custom state is not active in game");
        NoAutoPlay();
    }

    /// <summary>
    /// Rounds only start when the test starts them, whatever the player's auto play setting is. This is the match's
    /// own flag, the setting itself is left alone.
    /// </summary>
    protected static void NoAutoPlay()
    {
        var simulation = InGame.instance.bridge.Simulation;
        simulation.autoPlay = false;
        simulation.blockAutoPlay = true;
        simulation.pauseAutoPlay = true;
    }

    /// <summary>
    /// Plays the next round to its end
    /// </summary>
    /// <param name="exact">False for a round a boss tier may be defeated on: the game can go on to the next round by
    /// itself then</param>
    protected IEnumerator PlayRound(bool exact = true)
    {
        var bridge = InGame.instance.bridge;
        bridge.SetFastForward(true);
        var round = bridge.GetCurrentRound();

        // A round started while the game is still settling after loading is ignored, so it is started until it runs
        var start = Time.realtimeSinceStartup;
        var started = false;
        while (true)
        {
            if (exact && bridge.GetCurrentRound() > round + 1) Fail($"Round {round + 2} was started too, the test must start every round itself");
            if (bridge.GetCurrentRound() > round && !bridge.AreRoundsActive()) break;
            // Only until it runs: a boss round's bloons can be done while its boss is still there
            if (bridge.AreRoundsActive()) started = true;
            else if (!started && bridge.GetCurrentRound() == round) bridge.StartRound();
            NoAutoPlay();
            if (Time.realtimeSinceStartup - start > BossNotDefeatedTest.TimeoutSeconds)
            {
                var boss = bridge.Simulation.map.spawner.bossBloonManager;
                Fail($"Playing round {round + 1} took more than {BossNotDefeatedTest.TimeoutSeconds}s: now on " +
                     $"{bridge.GetCurrentRound() + 1}, rounds active {bridge.AreRoundsActive()}, game over " +
                     $"{bridge.Simulation.GameOver}, won {bridge.Simulation.gameWon}, auto play {bridge.Simulation.autoPlay}/{bridge.Simulation.blockAutoPlay}, time scale {Time.timeScale}, boss {boss.CurrentBoss?.bloonModel.id ?? "none"}, tiers " +
                     $"{CustomBossState.HighestDefeatedTier}, bloons {InGame.instance.GetBloons().Count}");
            }
            yield return new WaitForSecondsRealtime(0.25f);
        }
        yield return new WaitForSecondsRealtime(0.5f);
        Log($"played round {round + 1}");
    }

    /// <summary>
    /// What a player sees of a game: round, cash, lives and towers
    /// </summary>
    protected static string GameState()
    {
        var bridge = InGame.instance.bridge;
        var towers = InGame.instance.GetTowers().Select(t => t.towerModel.name).OrderBy(name => name);
        return $"round {bridge.GetCurrentRound() + 1}, cash {Math.Round(bridge.GetCash())}, lives {Math.Round(bridge.GetHealth())}, " +
               $"towers {string.Join(", ", towers)}";
    }

    /// <summary>
    /// Leaves the game and forgets everything the mod keeps in memory, like closing the game would
    /// </summary>
    protected IEnumerator CloseGame()
    {
        yield return ToMainMenu();
        CustomBossState.Clear();
    }

    /// <summary>
    /// Clicks the Boss Creator button on the main menu
    /// </summary>
    protected static void ClickBossCreator()
    {
        var mainMenu = Object.FindObjectOfType<MainMenu>();
        var button = mainMenu.transform.Find(MainMenuButton.Name).GetComponentInChildren<Button>(true);
        button.onClick.Invoke();
    }

    protected IEnumerator WaitForLoadedGame()
    {
        yield return WaitFor(() => InGame.instance?.bridge?.Simulation != null && !InGame.instance.quitting &&
                                   MenuManager.instance?.IsTransitioning == false, "Loading the game", 60);
        NoAutoPlay();
        yield return new WaitForSecondsRealtime(2);
    }

    protected IEnumerator WaitForBossCreator()
    {
        yield return WaitFor(() => Object.FindObjectOfType<ChallengeEditor>() != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Boss Creator");
        yield return new WaitForSecondsRealtime(1);
        Assert(BossMode.Active, "The editor must be the Boss Creator");
    }

    /// <summary>
    /// The continued game is still the custom game it was, with all its rules
    /// </summary>
    protected void AssertCustomGame(BossOptions options)
    {
        Assert(CustomBossState.IsActive, "A continued game must be custom");
        AssertEquals(CustomBossState.ActiveOptions, options, "Options of the continued game");
        Assert(InGameData.CurrentGame.disableSaves, "A continued game can be saved by the game");
        Assert(!InGameData.CurrentGame.bossData.bossRankedMode, "A continued game must be unranked");
        AssertEquals(TierMath.FormatRounds(InGame.instance.bridge.Simulation.map.spawner.bossBloonManager.spawnRounds),
            TierMath.FormatRounds(options.GameSpawnRounds), "Spawn rounds of the continued game");
        Assert(ExtraTiers.HasAllTiers(InGame.instance.GetGameModel(), options), "Tier models of the continued game");
        AssertScaledTiers(InGame.instance.GetGameModel(), options);
    }

    protected static double MonkeyMoney => Game.instance.playerService.Player.Data.monkeyMoney.Value;
}

/// <summary>
/// The store writes a whole game and reads it back, and ignores files it can't use
/// </summary>
public class ContinueStoreTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Boss = BossType.Lych, Elite = true, Tiers = 7, SpeedPerTier = 10, SpawnRounds = "30, 35, 40"};
        yield return StartCustomGame(options, Challenge(20).Build());
        var save = InGame.instance.CreateCurrentMapSave(InGame.instance.bridge.GetCurrentRound(), CustomBossRetries.SaveId);
        var json = MapSaveJson.Serialize(save);
        var dcm = InGameData.CurrentGame.dcModel;
        yield return ToMainMenu();

        var store = new ContinueStore(Path.Combine(TempFolder, "Nested", "Continue.json"));
        AssertEquals(store.TryLoad(), null, "A missing file must not load");
        Assert(!store.Exists, "No file yet");

        store.Save(options, dcm, new[] {new RoundSnapshot(35, 2, json), new RoundSnapshot(30, 1, json)});
        Assert(store.Exists, "The file was not written");
        Assert(!File.Exists(store.FilePath + ".tmp"), "The temporary file was left behind");

        var game = AssertNotNull(store.TryLoad(), "The saved game did not load");
        AssertEquals(game.Options, options, "Options");
        AssertEquals(game.Dcm.name, dcm.name, "Challenge name");
        AssertEquals(game.Dcm.startRules.cash, 20000, "Start cash");
        Assert(ConfigApplier.IsMarked(game.Dcm), "The challenge must stay custom");
        AssertEquals(string.Join(", ", game.Snapshots.Select(s => $"{s.Snapshot.Round}/{s.Snapshot.DefeatedTiers}")), "30/1, 35/2",
            "Snapshots");
        AssertEquals(game.Latest.Snapshot.Round, 35, "Latest snapshot");
        AssertEquals(game.Latest.Save.round, save.round, "Round in the map save");
        AssertEquals(game.Latest.Save.placedTowers.Count, save.placedTowers.Count, "Towers in the map save");

        // A hand edited file is held to the Boss Creator's limits
        var file = File.ReadAllText(store.FilePath);
        File.WriteAllText(store.FilePath, file.Replace("\"Tiers\": 7", "\"Tiers\": 99").Replace("\"DefeatedTiers\": 2", "\"DefeatedTiers\": 50"));
        game = AssertNotNull(store.TryLoad(), "An edited file did not load");
        AssertEquals(game.Options.Tiers, TierMath.MaxTiers, "Tiers are clamped");
        AssertEquals(game.Latest.Snapshot.DefeatedTiers, TierMath.MaxTiers, "Defeated tiers are clamped");

        foreach (var (what, broken) in new[]
                 {
                     ("broken JSON", "{ not json"),
                     ("empty file", ""),
                     ("newer version", file.Replace($"\"Version\": {ContinueStore.Version}", "\"Version\": 99")),
                     ("no snapshots", file[..file.IndexOf("\"Snapshots\"", StringComparison.Ordinal)] + "\"Snapshots\": []}"),
                     ("no challenge", file.Replace("\"Challenge\":", "\"Ignored\":")),
                     ("unusable map save", JsonConvert.SerializeObject(new
                     {
                         Version = ContinueStore.Version, Options = options, Challenge = DcmJson.Serialize(dcm),
                         Snapshots = new[] {new RoundSnapshot(35, 0, "not json")}
                     }))
                 })
        {
            File.WriteAllText(store.FilePath, broken);
            AssertEquals(store.TryLoad(), null, $"A file with {what} must not load");
        }

        store.Delete();
        Assert(!store.Exists, "The file was not deleted");
        store.Delete();
    }
}

/// <summary>
/// A custom game is saved after every round in the mod's own file and can be continued from the main menu after the
/// game was closed, with its towers, cash, lives, rules and defeated tiers, and still be won. The player's own saves
/// and Monkey Money are never touched.
/// </summary>
public class ContinueTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var savedMaps = RetryTest.SavedMaps();
        var monkeyMoney = MonkeyMoney;
        var options = new BossOptions {Tiers = 3, SpawnRounds = "40, 42, 44", SpeedPerTier = 20, HealthPerTier = 30};
        yield return StartCustomGame(options, Challenge(39, 0.1f).Build());
        AssertEquals(Store.TryLoad(), null, "Nothing is saved before a round is played");

        ExtraTierGameTest.PlaceStrongTower(InGame.instance.GetGameModel().GetTowerFromId("DartMonkey"));
        CreateTowerAt(InGame.instance.bridge, new Vector2(-40, 40), Game.instance.model.GetTowerFromId("DartMonkey-203"), costOverride: 0);
        CreateTowerAt(InGame.instance.bridge, new Vector2(40, 40), Game.instance.model.GetTowerFromId("Quincy"), costOverride: 0);

        yield return PlayRound();
        var game = AssertNotNull(Store.TryLoad(), "Round 39 was not saved");
        AssertEquals(game.Latest.Snapshot.Round, 40, "Saved round after round 39");
        AssertEquals(game.Latest.Snapshot.DefeatedTiers, 0, "Defeated tiers after round 39");

        // Tier 1 arrives on round 40 and is defeated by the strong tower
        yield return PlayRound(false);
        AssertEquals(CustomBossState.HighestDefeatedTier, 1, "Tier 1 must be defeated on round 40");
        var latest = InGame.instance.bridge.GetCurrentRound() + 1;
        game = AssertNotNull(Store.TryLoad(), "Round 40 was not saved");
        var snapshots = string.Join(", ", game.Snapshots.Select(s => $"{s.Snapshot.Round}/{s.Snapshot.DefeatedTiers}"));
        AssertEquals(snapshots, $"40/0, {latest}/1", "Snapshots after tier 1: its round and the latest");
        var before = GameState();
        Log($"before closing: {before}");

        yield return CloseGame();
        Assert(Store.Exists, "Leaving the game must keep it");

        ClickBossCreator();
        Popup? popup = null;
        yield return WaitForPopup(found => popup = found);
        AssertEquals(popup!.body.text, MainMenuButton.UnfinishedGameText(game), "Unfinished game popup");
        if (ScreenshotTour.Folder is { Length: > 0 } folder) yield return ScreenshotTour.Capture(folder, "continue-popup");
        Assert(popup.body.text.Contains("Continue Test") && popup.body.text.Contains(latest.ToString()), $"Popup text: {popup.body.text}");
        yield return ClosePopup(popup, true);
        yield return WaitForLoadedGame();

        var after = GameState();
        Log($"after continuing: {after}");
        if (ScreenshotTour.Folder is { Length: > 0 } shots) yield return ScreenshotTour.Capture(shots, "continue-game");
        AssertEquals(after, before, "The continued game");
        AssertCustomGame(options);
        AssertEquals(CustomBossState.HighestDefeatedTier, 1, "Defeated tiers of the continued game");
        Assert(CustomBossRetries.HasLastRound(latest), "Retry Last Round must survive closing the game");
        AssertNotNull(CustomBossRetries.SaveFor(40), "Tier 1's checkpoint must survive closing the game");

        // The strong tower comes from a change to the match's model, not from the game state: the continued match gets
        // a strong tower of its own to win with
        var model = InGame.instance.GetGameModel().GetTowerFromId("DartMonkey");
        ExtraTierGameTest.MakeStrong(model);
        var towers = InGame.instance.GetTowers().Count;
        CreateTowerAt(InGame.instance.bridge, new Vector2(-80, -40), model, costOverride: 0);
        yield return WaitFor(() => InGame.instance.GetTowers().Count > towers, "Placing a strong tower in the continued game");


        // The first round played after continuing keeps the tiers, and the game can still be won
        yield return PlayRound(false);
        Assert(CustomBossState.HighestDefeatedTier >= 1, "Defeated tiers after a round of the continued game");
        AssertEquals(AssertNotNull(Store.TryLoad(), "The continued game is saved on").Latest.Snapshot.Round,
            InGame.instance.bridge.GetCurrentRound() + 1, "Saved round of the continued game");

        var bridge = InGame.instance.bridge;
        yield return WaitFor(() =>
        {
            if (bridge.Simulation.GameOver && !bridge.Simulation.gameWon) Fail($"Lost on round {bridge.GetCurrentRound() + 1}");
            if (!bridge.AreRoundsActive()) bridge.StartRound();
            return bridge.Simulation.gameWon;
        }, "Winning the continued game", BossNotDefeatedTest.TimeoutSeconds);
        AssertEquals(CustomBossState.HighestDefeatedTier, options.Tiers, "Defeated tiers when the game is won");
        Assert(!Store.Exists, "A won game must leave nothing to continue");

        yield return ToMainMenu();
        AssertEquals(RetryTest.SavedMaps(), savedMaps, "The player's map saves");
        AssertEquals(MonkeyMoney, monkeyMoney, "Monkey Money");
    }
}

/// <summary>
/// A lost game can be closed on the defeat screen and still be continued, and its free retries still work
/// </summary>
public class ContinueRetryTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var savedMaps = RetryTest.SavedMaps();
        var monkeyMoney = MonkeyMoney;
        var options = new BossOptions {Tiers = 3, SpawnRounds = "40, 43, 46"};
        yield return StartCustomGame(options, Challenge(38).Build());

        BossDefeatScreen? screen = null;
        yield return LoseAndOpenDefeatScreen(found => screen = found);
        var lostRound = InGame.instance.bridge.GetCurrentRound() + 1;
        Assert(lostRound is > 40 and < 43, $"Lost on round {lostRound}, after the checkpoint and before tier 2");
        var game = AssertNotNull(Store.TryLoad(), "A lost game must be kept");
        AssertEquals(game.Latest.Snapshot.Round, lostRound, "The kept game continues on the lost round");

        yield return CloseGame();
        BossGameLauncher.Continue(AssertNotNull(Store.TryLoad(), "The lost game")!);
        yield return WaitForLoadedGame();
        AssertEquals(InGame.instance.bridge.GetCurrentRound() + 1, lostRound, "Round of the continued game");
        Assert(!InGame.instance.bridge.Simulation.GameOver, "A continued game goes on");
        AssertCustomGame(options);

        // Lose again: both free retries are there, from snapshots taken before the game was closed
        yield return LoseAndOpenDefeatScreen(found => screen = found);
        AssertEquals(InGame.instance.bridge.GetCurrentRound() + 1, lostRound, "Lost round of the continued game");
        Assert(screen!.retryButton.gameObject.activeSelf, "Retry Last Round is offered");
        Assert(screen.continueButton.gameObject.activeSelf, "Retry From Checkpoint is offered");
        AssertEquals(screen.continueDescTxt.text, Texts.RetryFromRound.T(40), "Retry From Checkpoint text");
        screen.continueButton.onClick.Invoke();
        yield return WaitForResume();
        AssertEquals(InGame.instance.bridge.GetCurrentRound() + 1, 40, "Round after Retry From Checkpoint");
        AssertCustomGame(options);

        yield return ToMainMenu();
        AssertEquals(RetryTest.SavedMaps(), savedMaps, "The player's map saves");
        AssertEquals(MonkeyMoney, monkeyMoney, "Monkey Money");
    }
}

/// <summary>
/// Restarting or starting another custom game replaces the unfinished one, other games never write it, and New Boss
/// in the popup keeps it until a new game is played
/// </summary>
public class ContinueLifecycleTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 2, SpawnRounds = "40, 45"};

        // No unfinished game: the button opens the Boss Creator right away
        ClickBossCreator();
        yield return WaitForBossCreator();
        AssertEquals(PopupScreen.instance.GetFirstActivePopup(), null, "No popup without an unfinished game");
        yield return ToMainMenu();

        // A restart starts over and replaces the game
        yield return StartCustomGame(options, Challenge(10).Build());
        yield return PlayRound();
        Assert(Store.Exists, "A played round must be saved");
        var simulation = InGame.instance.bridge.Simulation.Pointer;
        InGame.instance.RunRestart();
        yield return WaitFor(() => InGame.instance?.bridge?.Simulation != null && InGame.instance.bridge.Simulation.Pointer != simulation,
            "Restarting", 30);
        NoAutoPlay();
        yield return new WaitForSecondsRealtime(1);
        Assert(!Store.Exists, "A restart must replace the unfinished game");
        yield return PlayRound();
        AssertEquals(AssertNotNull(Store.TryLoad(), "The restarted game").Latest.Snapshot.Round, 11, "Saved round after a restart");

        // New Boss keeps the unfinished game; playing a new one replaces it
        yield return CloseGame();
        ClickBossCreator();
        Popup? popup = null;
        yield return WaitForPopup(found => popup = found);
        yield return ClosePopup(popup!, false);
        yield return WaitForBossCreator();
        Assert(Store.Exists, "New Boss must keep the unfinished game until a new one is played");
        yield return ToMainMenu();

        yield return StartCustomGame(options, Challenge(20).Build());
        Assert(!Store.Exists, "Starting a new custom game must replace the unfinished one");
        yield return PlayRound();
        AssertEquals(AssertNotNull(Store.TryLoad(), "The new game").Latest.Snapshot.Round, 21, "Saved round of the new game");
        yield return CloseGame();

        // A normal Boss Challenge is neither saved here nor replaces the unfinished custom game
        var before = File.ReadAllText(Store.FilePath);
        yield return LoadIntoGame(SetupBossChallenge(Challenge(10).Build(), BossType.Bloonarius));
        Assert(!CustomBossState.IsActive, "A normal Boss Challenge must not be custom");
        NoAutoPlay();
        yield return PlayRound();
        yield return ToMainMenu();
        AssertEquals(File.ReadAllText(Store.FilePath), before, "A normal Boss Challenge must not touch the unfinished custom game");
    }
}

/// <summary>
/// Survives a real crash: with CBC_CRASH_PHASE=1 a custom game is played and the game process is killed after a
/// round; with CBC_CRASH_PHASE=2 the next start continues it and checks it. CBC_CRASH_FOLDER holds the game and what
/// it should look like. Without CBC_CRASH_PHASE the test does nothing.
/// </summary>
public class ContinueAfterCrashTest : ContinueTestBase
{
    private static readonly BossOptions Options = new() {Tiers = 4, SpawnRounds = "40, 45, 50, 55", HealthPerTier = 10};

    protected override IEnumerator Run()
    {
        var phase = System.Environment.GetEnvironmentVariable("CBC_CRASH_PHASE");
        var folder = System.Environment.GetEnvironmentVariable("CBC_CRASH_FOLDER");
        if (string.IsNullOrEmpty(phase) || string.IsNullOrEmpty(folder)) yield break;

        CustomBossRetries.Store = new ContinueStore(Path.Combine(folder, "Continue.json"));
        var expectedFile = Path.Combine(folder, "Expected.txt");
        if (phase == "1")
        {
            CustomBossRetries.Store.Delete();
            yield return StartCustomGame(Options, Challenge(36).Build());
            CreateTowerAt(InGame.instance.bridge, new Vector2(-40, 0), Game.instance.model.GetTowerFromId("DartMonkey-024"), costOverride: 0);
            CreateTowerAt(InGame.instance.bridge, new Vector2(40, 30), Game.instance.model.GetTowerFromId("BananaFarm-200"), costOverride: 0);
            yield return PlayRound();
            yield return PlayRound();
            File.WriteAllText(expectedFile, GameState() + "\n" + RetryTest.SavedMaps() + "\n" + MonkeyMoney);
            Log($"crashing on purpose: {GameState()}");
            System.Environment.FailFast("CustomBossChallenge crash test, phase 1");
        }

        var expected = File.ReadAllLines(expectedFile);
        ClickBossCreator();
        Popup? popup = null;
        yield return WaitForPopup(found => popup = found);
        yield return ClosePopup(popup!, true);
        yield return WaitForLoadedGame();
        Log($"continued after the crash: {GameState()}");
        AssertEquals(GameState(), expected[0], "The game continued after the crash");
        AssertCustomGame(Options);
        AssertEquals(RetryTest.SavedMaps(), expected[1], "The player's map saves");
        AssertEquals(MonkeyMoney.ToString(), expected[2], "Monkey Money");
        yield return PlayRound();
        yield return ToMainMenu();
        CustomBossRetries.Store.Delete();
    }
}
