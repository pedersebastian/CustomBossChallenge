using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The Boss Creator, its tier picker and Past Events don't keep their old windows alive after they close, and opening
/// them again and again, or playing custom games, doesn't make the game's memory keep growing (#235). Il2CppInterop
/// never frees a delegate handed to the game, so a listener that held a window kept all of it.
/// </summary>
public class LeakTest : RulesPanelTestBase
{
    private const int Visits = 8;
    private const int Games = 3;

    /// <summary>
    /// What a visit, with the tier picker and Past Events, may still add. Mod Helper's components and the game's copies
    /// of the listeners are never freed: a visit keeps about 0.12 MB of managed and 1.2 MB of the game's memory. Before
    /// the fix it kept every window, 0.18 MB and 3.4 MB.
    /// </summary>
    private const double ManagedPerVisitMb = 0.16;
    private const double GamePerVisitMb = 2.2;
    private const double ManagedPerGameMb = 0.1;

    private readonly List<(string What, WeakReference Ref)> windows = new();

    protected override IEnumerator Run()
    {
        // The first visit fills caches, like the boss portraits and the past events
        yield return Visit(0);
        var (managed, game) = (0.0, 0.0);
        yield return Measure((m, g) => (managed, game) = (m, g));
        for (var visit = 1; visit <= Visits; visit++) yield return Visit(visit);
        var (managedAfter, gameAfter) = (0.0, 0.0);
        yield return Measure((m, g) => (managedAfter, gameAfter) = (m, g));

        var managedPerVisit = (managedAfter - managed) / Visits;
        var gamePerVisit = (gameAfter - game) / Visits;
        Log($"{Visits} visits of the Boss Creator: {managedPerVisit:0.000} MB managed and {gamePerVisit:0.000} MB game memory each");

        // Every window but the last Boss Creator's is gone; that one goes when the Boss Creator opens again
        var alive = windows.Where(window => window.What != $"Boss Creator {Visits}" && window.Ref.IsAlive).Select(window => window.What).ToList();
        Assert(alive.Count == 0, $"Windows still in memory after they closed: {string.Join(", ", alive)}");
        Assert(managedPerVisit < ManagedPerVisitMb, $"Managed memory grows by {managedPerVisit:0.000} MB per visit");
        Assert(gamePerVisit < GamePerVisitMb, $"The game's memory grows by {gamePerVisit:0.000} MB per visit");

        // Custom games, with the boss line-up and the Rules panel
        yield return Game();
        yield return Measure((m, g) => (managed, game) = (m, g));
        for (var i = 0; i < Games; i++) yield return Game();
        yield return Measure((m, g) => (managedAfter, gameAfter) = (m, g));
        var managedPerGame = (managedAfter - managed) / Games;
        Log($"{Games} custom games: {managedPerGame:0.000} MB managed and {(gameAfter - game) / Games:0.0} MB game memory each");
        Assert(managedPerGame < ManagedPerGameMb, $"Managed memory grows by {managedPerGame:0.000} MB per custom game");
    }

    private IEnumerator Visit(int visit)
    {
        yield return ToMainMenu();
        BossMode.Enter();
        yield return WaitFor(() => BossEditorSection.Current?.Editor != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Boss Creator", 30);
        yield return new WaitForSecondsRealtime(0.5f);
        var section = BossEditorSection.Current!;
        windows.Add(($"Boss Creator {visit}", new WeakReference(section)));

        section.SetAdvanced(true);
        var picker = AssertNotNull(section.OpenPicker(1), "The tier picker didn't open")!;
        windows.Add(($"tier picker {visit}", new WeakReference(picker)));
        picker.Hide();
        section.SetAdvanced(false);

        var browser = section.OpenPastEvents();
        windows.Add(($"Past Events {visit}", new WeakReference(browser)));
        yield return null;
        browser.Hide();

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return ToMainMenu();
    }

    private IEnumerator Game()
    {
        yield return ToMainMenu();
        var dcm = BaseDcm();
        dcm.startRules.lives = dcm.startRules.maxLives = 5000;
        yield return Play(new BossOptions {Boss = BossType.Lych, Tiers = 3, SpeedPerTier = 10}, dcm);
        InGame.instance.bridge.Simulation.map.spawner.bossBloonManager.SpawnBoss(1, false);
        yield return new WaitForSecondsRealtime(0.7f);
        Visuals.BossHud.Update(now: true);
        yield return OpenRules();
        yield return CloseRules();
        yield return ToMainMenu();
    }

    /// <summary>
    /// The managed and the game's (Il2Cpp) memory in MB, after both have collected all they can
    /// </summary>
    private static IEnumerator Measure(Action<double, double> found)
    {
        yield return new WaitForSecondsRealtime(1);
        var unload = Resources.UnloadUnusedAssets();
        while (!unload.isDone) yield return null;
        for (var i = 0; i < 6; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Il2CppSystem.GC.Collect();
            yield return null;
        }

        found(GC.GetTotalMemory(true) / 1048576.0, Il2CppSystem.GC.GetTotalMemory(false) / 1048576.0);
    }
}
