using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.Rounds;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Boss Event Rounds (#80): the boss events' own rounds, moved to the tiers' Spawn Rounds, and in Advanced Mode (#84)
/// every tier's round from its own boss; the game's own round sets stay as they are
/// </summary>
public class BossEventRoundsGameTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        foreach (var boss in Enum.GetValues<BossType>())
        {
            AssertNotNull(GameData.Instance.MergeableRoundSetByName(BossOptions.BossEventRoundSet(boss)),
                $"The game's round set for {boss}");
        }

        var defaults = Rounds(GameData.Instance.RoundSetByName("DefaultRoundSet"));

        // Off, the default rounds
        var chimps = BaseDcm();
        chimps.mode = "Clicks";
        yield return Play(new BossOptions(), chimps);
        AssertRounds(defaults, "Without Boss Event Rounds");

        // Every boss's rounds as the game merges them, and the same from our own conversion for Advanced Mode
        var merged = new Dictionary<BossType, string[]>();
        var defaultRounds = GameData.Instance.RoundSetByName("DefaultRoundSet").rounds;
        foreach (var boss in Enum.GetValues<BossType>())
        {
            yield return Play(new BossOptions {Boss = boss, BossEventRounds = true}, chimps);
            var game = InGame.instance.GetGameModel().roundSet.rounds;
            merged[boss] = Rounds(InGame.instance.GetGameModel().roundSet);
            var set = BossOptions.BossEventRoundSet(boss);
            foreach (var info in GameData.Instance.MergeableRoundSetByName(set).rounds)
            {
                var round = info.roundNumber;
                AssertEquals(Exact(BossEventRounds.RoundOf(set, round, defaultRounds[round - 1])!), Exact(game[round - 1]),
                    $"Round {round} of {set}, converted like the game merges it");
            }
        }

        // On in CHIMPS, the boss's rounds where the event has them
        var events = merged[BossType.Bloonarius];
        Assert(events[39] != defaults[39], $"Round 40 is the default one: {events[39]}");
        AssertEquals(events[40], defaults[40], "Round 41 is no boss round");
        var on = new BossOptions {BossEventRounds = true};
        yield return Play(on, chimps);
        Assert(!InGameData.CurrentGame.dcModel.roundSets.Contains("lych"), "Only the boss's own round set");

        // Moved with the tiers, also after a restart
        var moved = on with {SpawnRounds = "30, 50, 70, 90, 110"};
        var expected = Moved(events, moved);
        yield return Play(moved, chimps);
        AssertRounds(expected, "Rounds moved to the spawn rounds");
        AssertEquals(Rounds(InGame.instance.GetGameModel().roundSet)[29], events[39], "Round 30 is the event's round 40");
        yield return Restart();
        AssertRounds(expected, "Rounds after a restart");

        // Advanced Mode: each tier arrives on its own boss's round, the other rounds follow the first tier's boss
        // (tier 2 gets a boss whose round 60 differs from Lych's, so the test sees where it comes from)
        var second = Enum.GetValues<BossType>().First(boss => merged[boss][59] != merged[BossType.Lych][59]);
        var advanced = on with {Tiers = 3, SpawnRounds = "30, 50, 70", TierBosses = $"Lych,{second}Elite,Bloonarius"};
        expected = Moved(merged[BossType.Lych], advanced);
        expected[49] = merged[second][59];
        expected[69] = merged[BossType.Bloonarius][79];
        yield return Play(advanced, chimps);
        AssertRounds(expected, $"Advanced Mode, each tier with its own boss's round ({second} on tier 2)");

        // Vortex first brings its extra rounds along
        yield return Play(on with {Tiers = 2, TierBosses = "Vortex,Lych"}, BaseDcm());
        var vortexFirst = Rounds(InGame.instance.GetGameModel().roundSet);
        AssertEquals(vortexFirst[4], merged[BossType.Vortex][4], "Round 5 follows Vortex on tier 1");
        Assert(vortexFirst[4].Contains("Yellow"), $"Vortex's round 5 has its Yellow bloons: {vortexFirst[4]}");
        AssertEquals(vortexFirst[59], merged[BossType.Lych][59], "Tier 2 arrives on Lych's round 60");

        // A Random tier plays the round of the boss it got, also after a restart
        var random = BossGameLauncher.Resolve(on with {Tiers = 2, TierBosses = "Lych,Random"});
        Assert(!random.HasRandom, "Random tiers get their boss when the game starts");
        yield return Play(random, chimps);
        var picked = random.BossOf(2).Boss;
        AssertEquals(Rounds(InGame.instance.GetGameModel().roundSet)[59], merged[picked][59],
            $"The Random tier plays the round of {picked}");
        yield return Restart();
        AssertEquals(Rounds(InGame.instance.GetGameModel().roundSet)[59], merged[picked][59], "The same round after a restart");

        yield return ToMainMenu();
        AssertEquals(string.Join("|", Rounds(GameData.Instance.RoundSetByName("DefaultRoundSet"))), string.Join("|", defaults),
            "The game's own default rounds");
    }

    /// <summary>The rounds of a boss's event set, moved to the given options' spawn rounds</summary>
    private static string[] Moved(string[] rounds, BossOptions options) =>
        BossRoundsMath.Sources(options.EffectiveSpawnRounds, rounds.Length).Select(source => rounds[source - 1]).ToArray();

    /// <summary>A round's bloons with their exact times</summary>
    private static string Exact(RoundModel round) =>
        string.Join(" ", round.groups.Select(g => $"{g.count}x{g.bloon}@{g.start:R}-{g.end:R}"));

    private IEnumerator Play(BossOptions options, Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel dcm)
    {
        if (InGame.instance != null) yield return ToMainMenu();
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        Assert(CustomBossState.IsActive, "Custom state is not active in game");
    }

    private IEnumerator Restart()
    {
        var simulation = InGame.instance.bridge.Simulation.Pointer;
        InGame.instance.RunRestart();
        yield return WaitFor(() => InGame.instance?.bridge?.Simulation != null &&
                                   InGame.instance.bridge.Simulation.Pointer != simulation, "Restarting the game", 30);
        yield return new WaitForSecondsRealtime(1);
    }

    private void AssertRounds(string[] expected, string what)
    {
        var actual = Rounds(InGame.instance.GetGameModel().roundSet);
        var differ = Enumerable.Range(0, Math.Max(actual.Length, expected.Length))
            .Where(i => i >= actual.Length || i >= expected.Length || actual[i] != expected[i]).Select(i => i + 1).ToList();
        if (differ.Count > 0) Fail($"{what}: rounds {string.Join(", ", differ.Take(10))} differ");
    }

    /// <summary>The bloons of every round, one text per round</summary>
    public static string[] Rounds(RoundSetModel set) => set.rounds.Select(round =>
        string.Join(" ", round.groups.Select(g => $"{g.count}x{g.bloon}@{g.start:0}-{g.end:0}"))).ToArray();
}

/// <summary>
/// A game with Boss Event Rounds is continued with them, from a file older versions of the mod leave alone, and saving
/// the setup again doesn't keep the round set in the challenge
/// </summary>
public class BossEventRoundsContinueTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 3, SpawnRounds = "30, 32, 34", BossEventRounds = true};
        yield return StartCustomGame(options, Challenge(28).Build());
        var rounds = BossEventRoundsGameTest.Rounds(InGame.instance.GetGameModel().roundSet);
        yield return PlayRound();
        Assert(System.IO.File.ReadAllText(Store.FilePath).Contains($"\"Version\": {ContinueStore.Version}"),
            "A game with Boss Event Rounds is saved as version 3");

        var game = AssertNotNull(Store.TryLoad(), "Round 29 was not saved");
        AssertEquals(game.Options, options, "Options in the file");
        Assert(!ConfigApplier.Unprepare(game.Dcm).roundSets.Contains("bloonarius"),
            "The round set is no part of the setup");

        yield return CloseGame();
        BossGameLauncher.Continue(game);
        yield return WaitForLoadedGame();
        AssertCustomGame(options);
        AssertEquals(string.Join("|", BossEventRoundsGameTest.Rounds(InGame.instance.GetGameModel().roundSet)),
            string.Join("|", rounds), "Rounds of the continued game");

        // Advanced Mode, with every tier's own round
        var mixed = options with {TierBosses = "Vortex,Lych,Phayze"};
        yield return CloseGame();
        yield return StartCustomGame(mixed, Challenge(28).Build());
        rounds = BossEventRoundsGameTest.Rounds(InGame.instance.GetGameModel().roundSet);
        yield return PlayRound();
        game = AssertNotNull(Store.TryLoad(), "Round 29 of the Advanced game was not saved");
        AssertEquals(game.Options, mixed, "Advanced options in the file");
        yield return CloseGame();
        BossGameLauncher.Continue(game);
        yield return WaitForLoadedGame();
        AssertCustomGame(mixed);
        AssertEquals(string.Join("|", BossEventRoundsGameTest.Rounds(InGame.instance.GetGameModel().roundSet)),
            string.Join("|", rounds), "Rounds of the continued Advanced game");
        yield return ToMainMenu();
    }
}

/// <summary>
/// The Boss Event Rounds tick in the Boss Creator: right under Spawn Rounds, in Standard and Advanced Mode, with Undo,
/// the keyboard, the reset and a version 4 code
/// </summary>
public class BossEventRoundsCreatorTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var folder = System.IO.Path.Combine(TempFolder, "Creator");
        var store = BossMode.Store;
        var saved = BossMode.Saved;
        BossMode.Store = new LastSetupStore(System.IO.Path.Combine(folder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(System.IO.Path.Combine(folder, "SavedSetups.json"));
        try
        {
            yield return Edit();
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
        }

        yield return ToMainMenu();
    }

    private IEnumerator Edit()
    {
        BossMode.Enter();
        yield return WaitFor(() => Menus.BossEditorSection.Current?.Editor != null &&
                                   Il2CppAssets.Scripts.Unity.Menu.MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name ==
                                   nameof(Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor.ChallengeEditor),
            "Opening the Boss Creator");
        yield return new WaitForSecondsRealtime(1);
        var section = Menus.BossEditorSection.Current!;
        section.ResetAll();
        var row = section.BossEventRoundsRow;

        Assert(!BossMode.Options.BossEventRounds && !section.BossEventRounds.Toggle.isOn, "Boss Event Rounds start off");
        Assert(row.activeInHierarchy, "The tick shows in Standard");
        var spawnRounds = section.SpawnRounds.transform.parent;
        AssertEquals(row.transform.GetSiblingIndex(), spawnRounds.GetSiblingIndex() + 1, "The tick is right under Spawn Rounds");

        // The tick, and Undo
        section.BossEventRounds.Toggle.isOn = true;
        Assert(BossMode.Options.BossEventRounds, "Ticking turns Boss Event Rounds on");
        BossHistory.Undo(section.Editor);
        yield return null;
        Assert(!BossMode.Options.BossEventRounds && !section.BossEventRounds.Toggle.isOn, "Undo turns it off again");
        BossHistory.Redo(section.Editor);
        yield return null;
        Assert(BossMode.Options.BossEventRounds && section.BossEventRounds.Toggle.isOn, "Redo turns it on again");

        // Codes and the setup summary
        var code = SetupCode.Encode(BossMode.Options, section.Editor.dcm);
        Assert(code.StartsWith(SetupCode.Version4Prefix), $"Boss Event Rounds give a version 4 code: {code}");
        Assert(SetupCode.TryDecode(code, out var read, out _, out var problem), $"The code reads back: {problem}");
        AssertEquals(read, BossMode.Options, "Options read back from the code");
        Assert(Menus.BossEditorSection.Summary(read, section.Editor.dcm).Contains(Menus.Texts.BossEventRounds.T()),
            "The pasted code's summary mentions Boss Event Rounds");

        // Advanced Mode shows the tick too, still right under Spawn Rounds, and plays it (#84)
        section.SetAdvanced(true);
        yield return null;
        Assert(row.activeInHierarchy && section.BossEventRounds.Toggle.isOn, "Advanced shows the tick, still on");
        AssertEquals(row.transform.GetSiblingIndex(), spawnRounds.GetSiblingIndex() + 1, "Under Spawn Rounds in Advanced");
        Assert(BossMode.Options.Mixed && BossMode.Options.UsesBossEventRounds, "Advanced plays the event rounds");
        var mixedCode = SetupCode.Encode(BossMode.Options, section.Editor.dcm);
        Assert(mixedCode.StartsWith(SetupCode.Version4Prefix), $"Advanced with the tick is a version 4 code: {mixedCode}");
        Assert(SetupCode.TryDecode(mixedCode, out var mixedRead, out _, out problem), $"The Advanced code reads back: {problem}");
        AssertEquals(mixedRead, BossMode.Options, "Advanced options read back from the code");
        section.SetAdvanced(false);
        yield return null;
        Assert(row.activeInHierarchy && section.BossEventRounds.Toggle.isOn, "Standard shows the tick again, still on");

        // The keyboard: Enter toggles, Delete turns it off
        var stop = section.FocusStops.Single(s => s.Name == "BossEventRounds");
        stop.Activate!();
        Assert(!BossMode.Options.BossEventRounds, "Enter on the tick turns it off");
        stop.Activate!();
        Assert(BossMode.Options.BossEventRounds, "Enter again turns it on");
        stop.Reset!();
        Assert(!BossMode.Options.BossEventRounds, "Delete turns it off");

        // The reset turns it off
        section.BossEventRounds.Toggle.isOn = true;
        section.ResetAll();
        Assert(!BossMode.Options.BossEventRounds && !section.BossEventRounds.Toggle.isOn, "Reset turns it off");
    }
}
