using System;
using System.Collections;
using System.Linq;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.UI.Menus;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Visuals;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Ranked practice (#83): the Show Event Timer setting shows the ranked time with the upgrade tiers and cash used in
/// custom games, the time survives a free retry and Continue, and the victory screen shows the time of every tier
/// </summary>
public class RankedTimerTest : ContinueTestBase
{
    private const string ShowTimerKey = Texts.KeyPrefix + "ShowTimer Setting Name";
    private const string RankedPracticeKey = Texts.KeyPrefix + Settings.RankedPracticeName + " Category";

    /// <summary>
    /// The panel is shown or not, and when shown has the right lines, on screen and clear of the game's controls
    /// </summary>
    private void AssertPanel(bool shown, string when)
    {
        RankedHud.Update(true);
        var panel = RankedHud.Panel;
        AssertEquals(panel != null && panel.gameObject.activeInHierarchy, shown, $"Ranked panel shown {when}");
        if (panel == null) return;

        var texts = panel.GetComponentsInChildren<TMP_Text>().Select(text => text.text ?? "").ToList();
        var expected = RankedHud.PanelLines().SelectMany(line => new[] {line.Label, line.Value}).ToList();
        AssertEquals(string.Join(" | ", texts), string.Join(" | ", expected), $"Ranked panel {when}");

        var rect = LayoutChecks.ScreenRect(panel.RectTransform);
        foreach (var text in GameObject.Find("InGame/UIRect/MainHudLeftAlign(Clone)/LeftGroup").GetComponentsInChildren<TMP_Text>())
        {
            if (text.isActiveAndEnabled && LayoutChecks.Overlap(rect, LayoutChecks.LettersRect(text)))
                Fail($"The ranked panel covers '{text.text}' {when}: {LayoutChecks.Describe(rect)} / {LayoutChecks.Describe(LayoutChecks.LettersRect(text))}");
        }
        Assert(LayoutChecks.Inside(rect, LayoutChecks.Screen), $"The ranked panel is off screen {when}: {LayoutChecks.Describe(rect)}");
        foreach (var (name, other) in LayoutChecks.OtherControls(panel.transform))
        {
            if (LayoutChecks.Overlap(rect, other)) Fail($"The ranked panel covers {name} {when}: {LayoutChecks.Describe(rect)} / {LayoutChecks.Describe(other)}");
        }
    }

    /// <summary>
    /// The game rounds its total to whole milliseconds, the tiers' times are exact frames
    /// </summary>
    private void AssertClose(TimeSpan actual, TimeSpan expected, string what)
    {
        if (Math.Abs((actual - expected).TotalMilliseconds) > 5) Fail($"{what}: {actual}, expected {expected}");
    }

    private TimeSpan Total => AssertNotNull(RankedHud.TotalTime, "No ranked time")!.Value;

    /// <summary>
    /// Plays rounds until <paramref name="done"/>, starting each round itself
    /// </summary>
    private IEnumerator PlayUntil(Func<bool> done, string what)
    {
        var bridge = InGame.instance.bridge;
        bridge.SetFastForward(true);
        var start = Time.realtimeSinceStartup;
        while (!done())
        {
            if (Time.realtimeSinceStartup - start > BossNotDefeatedTest.TimeoutSeconds)
                Fail($"{what} took more than {BossNotDefeatedTest.TimeoutSeconds}s: round {bridge.GetCurrentRound() + 1}, tiers {CustomBossState.HighestDefeatedTier}");
            if (bridge.Simulation.GameOver) Fail($"The game was lost while waiting for: {what}");
            if (!bridge.AreRoundsActive() && !bridge.Simulation.gameWon) bridge.StartRound();
            NoAutoPlay();
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    protected override IEnumerator Run()
    {
        AssertEquals(Settings.ShowTimer.GetDefaultValue(), false, "Show Event Timer is off by default");

        // Mod Helper translates the settings with keys of its own
        ModSettingsMenu.Open(ModContent.GetInstance<CustomBossChallenge>());
        yield return new WaitForSecondsRealtime(3);
        var menuTexts = Object.FindObjectsOfType<TMP_Text>().Where(text => text.isActiveAndEnabled).Select(text => text.text ?? "").ToList();
        Log($"mod settings: {string.Join(" | ", menuTexts.Where(text => text.Length < 80))}");
        foreach (var key in new[] {RankedPracticeKey, ShowTimerKey})
        {
            Assert(menuTexts.Contains(key.T()), $"Mod Settings must show '{key.T()}'");
        }
        Assert(!menuTexts.Any(text => text.Contains(Texts.KeyPrefix)), "Mod Settings shows a text key");
        var option = AssertNotNull(Object.FindObjectsOfType<ModHelperOption>().FirstOrDefault(found =>
            found.GetComponentsInChildren<TMP_Text>().Any(text => text.text == ShowTimerKey.T())), "No Show Event Timer option")!;
        option.InfoButton.Button.onClick.Invoke();
        Popup? info = null;
        yield return WaitForPopup(found => info = found);
        AssertEquals(info!.body.text, (Texts.KeyPrefix + "ShowTimer Setting Description").T(), "Show Event Timer's info");
        yield return ClosePopup(info, true);
        yield return ToMainMenu();

        RankedHud.ShowTimerOverride = false;
        var options = new BossOptions {Tiers = 3, SpawnRounds = "40, 42, 44", SpeedPerTier = 20, HealthPerTier = 30};
        yield return StartCustomGame(options, Challenge(39, 0.1f).Build());
        AssertPanel(false, "with the setting off");

        RankedHud.ShowTimerOverride = true;
        AssertPanel(true, "with the setting on");
        ExtraTierGameTest.PlaceStrongTower(InGame.instance.GetGameModel().GetTowerFromId("DartMonkey"));

        // Buying a tower counts its price as cash used, and its upgrade tier
        var (cash, cashUsed, tiersUsed) = (InGame.instance.bridge.GetCash(), RankedHud.CashUsed, RankedHud.TiersUsed);
        CreateTowerAt(InGame.instance.bridge, new Vector2(-40, 40), Game.instance.model.GetTowerFromId("DartMonkey"));
        yield return WaitFor(() => RankedHud.CashUsed != cashUsed, "Cash used after buying a tower");
        AssertEquals(RankedHud.CashUsed - cashUsed, (int) Math.Round(cash - InGame.instance.bridge.GetCash()), "Cash used for a tower");
        AssertEquals(RankedHud.TiersUsed, tiersUsed + 1, "Upgrade tiers used after buying a tower");
        AssertPanel(true, "after buying a tower");

        // Tier 1 arrives on round 40; once it is defeated the timer shows the time it was alive
        yield return PlayUntil(() => CustomBossState.HighestDefeatedTier >= 1 && !InGame.instance.bridge.AreRoundsActive(), "Tier 1");
        var afterTier1 = Total;
        Assert(afterTier1 > TimeSpan.Zero, "Tier 1 took no time");
        AssertPanel(true, "between bosses");
        AssertClose(RankedHud.TierTimes()[0]!.Value, afterTier1, "Tier 1's time is the whole time so far");

        // Round 41 has no boss: its end is the snapshot round 42 is retried from
        yield return PlayUntil(() => InGame.instance.bridge.GetCurrentRound() + 1 >= 42 && !InGame.instance.bridge.AreRoundsActive(), "Round 41");
        AssertEquals(Total, afterTier1, "Time without a boss alive");
        var retryRound = InGame.instance.bridge.GetCurrentRound() + 1;
        Assert(CustomBossRetries.HasLastRound(retryRound), $"No snapshot for round {retryRound}");

        // Tier 2 counts while it is alive; retrying its round takes the lost attempt's time back
        InGame.instance.bridge.StartRound();
        yield return WaitFor(() => Total > afterTier1, "Tier 2's time", 60);
        AssertPanel(true, "with a boss alive");
        if (ScreenshotTour.Folder is { Length: > 0 } aliveFolder) yield return ScreenshotTour.Capture(aliveFolder, "ranked-hud-boss");
        CustomBossRetries.RetryFrom(retryRound, true);
        yield return WaitForLoadedGame();
        AssertEquals(Total, afterTier1, "Time after a free retry");
        AssertPanel(true, "after a free retry");

        // Continuing the game after closing it keeps the time and what was used
        var beforeClosing = (RankedHud.CashUsed, RankedHud.TiersUsed);
        yield return CloseGame();
        ClickBossCreator();
        Popup? popup = null;
        yield return WaitForPopup(found => popup = found);
        yield return ClosePopup(popup!, true);
        yield return WaitForLoadedGame();
        AssertEquals(Total, afterTier1, "Time of a continued game");
        AssertEquals((RankedHud.CashUsed, RankedHud.TiersUsed), beforeClosing, "Cash and upgrade tiers used in a continued game");
        AssertPanel(true, "in a continued game");
        if (ScreenshotTour.Folder is { Length: > 0 } hudFolder) yield return ScreenshotTour.Capture(hudFolder, "ranked-hud");

        // Switching the setting off in the game hides what the mod showed
        RankedHud.ShowTimerOverride = false;
        AssertPanel(false, "after switching the setting off");
        RankedHud.ShowTimerOverride = true;

        // The victory screen shows the time of every tier, the total and the tiers and cash used
        ExtraTierGameTest.PlaceStrongTower(InGame.instance.GetGameModel().GetTowerFromId("DartMonkey"));
        yield return PlayUntil(() => InGame.instance.bridge.Simulation.gameWon, "Victory");
        var total = Total;
        var tierTimes = RankedHud.TierTimes();
        Assert(tierTimes.All(time => time > TimeSpan.Zero), $"Every tier has a time: {string.Join(", ", tierTimes)}");
        AssertClose(tierTimes.Aggregate(TimeSpan.Zero, (sum, time) => sum + time!.Value) + RankedHud.PenaltyTime,
            total, "The total is the tiers' times added up");

        BossVictoryScreen? screen = null;
        yield return WaitFor(() => (screen = Object.FindObjectOfType<BossVictoryScreen>()) != null, "The victory screen", 30);
        yield return new WaitForSecondsRealtime(5);
        var summary = AssertNotNull(RankedHud.Summary, "No tier times on the victory screen")!.transform;
        var summaryRect = LayoutChecks.ScreenRect(summary.GetComponent<RectTransform>());
        var cardWorld = AssertNotNull(RankedHud.CardBounds(AssertNotNull(RankedHud.SummaryCard(screen!), "No stats card")!), "The stats card shows nothing")!.Value;
        var camera = summary.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
        var cardMin = RectTransformUtility.WorldToScreenPoint(camera, cardWorld.min);
        var cardMax = RectTransformUtility.WorldToScreenPoint(camera, cardWorld.max);
        var cardRect = Rect.MinMaxRect(cardMin.x, cardMin.y, cardMax.x, cardMax.y);
        Assert(!LayoutChecks.Overlap(summaryRect, cardRect), $"The victory summary covers the stats: {LayoutChecks.Describe(summaryRect)} / {LayoutChecks.Describe(cardRect)}");
        Assert(LayoutChecks.Inside(summaryRect, LayoutChecks.Screen), $"The victory summary is off screen: {LayoutChecks.Describe(summaryRect)}");
        foreach (var (name, other) in LayoutChecks.OtherControls(summary))
        {
            if (LayoutChecks.Overlap(summaryRect, other)) Fail($"The victory summary covers {name}: {LayoutChecks.Describe(summaryRect)} / {LayoutChecks.Describe(other)}");
        }
        var texts = summary.GetComponentsInChildren<TMP_Text>().Select(text => text.text ?? "").ToList();
        var expected = RankedHud.SummaryLines(total).SelectMany(line => new[] {line.Label, line.Value}).ToList();
        AssertEquals(string.Join(" | ", texts), string.Join(" | ", expected), "Victory summary");
        AssertEquals(RankedHud.SummaryLines(total).Count, options.Tiers + 3 + (RankedHud.PenaltyTime > TimeSpan.Zero ? 1 : 0),
            "A line per tier, then total, tiers and cash");
        Log($"victory summary: {string.Join(" | ", texts)}");
        if (ScreenshotTour.Folder is { Length: > 0 } folder) yield return ScreenshotTour.Capture(folder, "ranked-victory");

        yield return ToMainMenu();
        RankedHud.Update(true);
    }
}
