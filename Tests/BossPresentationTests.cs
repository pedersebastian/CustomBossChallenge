using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Visuals;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The boss music and the map's boss effects follow each tier's boss in Advanced Mode (#85), the same as the game shows
/// for that boss on its own
/// </summary>
public class BossPresentationTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        // What the game itself shows for each boss on its own
        var music = new Dictionary<BossType, string?>();
        var effects = new Dictionary<BossType, string>();
        foreach (var boss in Enum.GetValues<BossType>())
        {
            yield return Play(new BossOptions {Boss = boss});
            var before = Effects();
            yield return Spawn(1);
            music[boss] = BossPresentation.MusicPlaying;
            effects[boss] = Names();
            Log($"{boss}: music {music[boss]}, map effects {effects[boss]}");
            AssertNotNull(music[boss], $"{boss}'s boss music");
            Assert(effects[boss].Contains(boss.ToString()), $"{boss}'s map effects: {effects[boss]}");
            AssertEquals(Effects(), before, $"Standard {boss}: the map effects are the game's own, untouched");
        }

        // Advanced Mode: the second tier's boss brings its own music and map effects
        var options = new BossOptions {Tiers = 3, TierBosses = "Bloonarius,LychElite,Lych"};
        yield return Play(options);
        AssertEquals(Names(), effects[BossType.Bloonarius], "Tier 1's map effects at the start");
        yield return Spawn(2);
        AssertEquals(Names(), effects[BossType.Lych], "Tier 2's map effects");
        AssertEquals(BossPresentation.MusicPlaying, music[BossType.Lych], "Tier 2's music");

        // The same boss again, Elite or not, keeps what is there
        var lych = Effects();
        yield return Spawn(3);
        AssertEquals(Effects(), lych, "Tier 3, Lych again, keeps the map effects");

        // A restart keeps the map, so tier 1's effects come back
        yield return Restart();
        AssertEquals(Names(), effects[BossType.Bloonarius], "Tier 1's map effects after a restart");
        yield return Spawn(1);
        AssertEquals(BossPresentation.MusicPlaying, music[BossType.Bloonarius], "Tier 1's music after a restart");

        yield return ToMainMenu();
    }

    /// <summary>The boss effects on the map, by name</summary>
    public static string Names() => string.Join(", ", BossPresentation.MapEffects);

    /// <summary>The boss effects on the map, as objects, to see whether they were made anew</summary>
    private static string Effects() => string.Join(", ", UnityEngine.Object.FindObjectsOfType<GameObject>()
        .Where(go => BossPresentation.MapEffects.Contains(go.name)).Select(go => go.GetInstanceID()).OrderBy(id => id));

    private IEnumerator Restart()
    {
        var simulation = InGame.instance.bridge.Simulation.Pointer;
        InGame.instance.RunRestart();
        yield return WaitFor(() => InGame.instance?.bridge?.Simulation != null &&
                                   InGame.instance.bridge.Simulation.Pointer != simulation, "Restarting the game", 30);
        yield return new WaitForSecondsRealtime(1.5f);
    }

    private IEnumerator Play(BossOptions options)
    {
        if (InGame.instance != null) yield return ToMainMenu();
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(BaseDcm(), BossGameLauncher.Resolve(options)));
    }

    private static IEnumerator Spawn(int tier)
    {
        InGame.instance.bridge.Simulation.map.spawner.bossBloonManager.SpawnBoss(tier, false);
        yield return new WaitForSecondsRealtime(1.5f);
    }
}

/// <summary>
/// A continued Advanced game shows the map effects of the boss beaten last, not those of the first tier the map is
/// loaded with
/// </summary>
public class BossPresentationContinueTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 3, SpawnRounds = "40, 60, 80", TierBosses = "Bloonarius,Lych,Phayze"};
        yield return StartCustomGame(options, Challenge(28).Build());
        var json = Persistence.MapSaveJson.Serialize(
            InGame.instance.CreateCurrentMapSave(InGame.instance.bridge.GetCurrentRound(), CustomBossRetries.SaveId));
        var dcm = InGameData.CurrentGame.dcModel;
        yield return CloseGame();

        Store.Save(options, dcm, new[] {new Persistence.RoundSnapshot(29, 2, json)});
        var game = AssertNotNull(Store.TryLoad(), "The saved game did not load");
        BossGameLauncher.Continue(game);
        yield return WaitForLoadedGame();
        yield return new WaitForSecondsRealtime(1.5f);
        var effects = BossPresentationTest.Names();
        Assert(effects.Length > 0 && effects.Split(", ").All(effect => effect.StartsWith("Lych")),
            $"After tier 2 the map shows Lych's effects: {effects}");
        yield return ToMainMenu();
    }
}
