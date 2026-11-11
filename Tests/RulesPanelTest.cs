using System.Collections;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Pause;
using Il2CppTMPro;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The pause screen's Rules panel of a custom game lists the boss options in Standard Mode (#141), once however often
/// it is opened, and only the boss with a no-changes line for a setup with nothing changed
/// </summary>
public class RulesPanelTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions
        {
            Boss = BossType.Vortex, Elite = true, Tiers = 6, SpawnRounds = "40, 50, 60, 70, 80, 90", SpeedPerTier = 10,
            HealthPerTier = -5, SkullMode = SkullMode.EvenlySpaced, SkullCount = 4
        };
        yield return Play(options);
        yield return OpenRules();
        AssertLines(options, "changed setup");

        var folder = System.Environment.GetEnvironmentVariable("CBC_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(folder)) yield return ScreenshotTour.Capture(folder, "rules-panel");

        // Opened again: the lines are made again, not added a second time
        yield return CloseRules();
        yield return OpenRules();
        AssertLines(options, "reopened");
        yield return CloseRules();

        var plain = new BossOptions();
        yield return Play(plain);
        yield return OpenRules();
        AssertLines(plain, "default setup");
        Assert(RulesPanel.Lines(plain).Last() == Texts.RulesNoChanges.T(), "The default setup has the no-changes line");
        yield return CloseRules();

        yield return ToMainMenu();
    }

    private void AssertLines(BossOptions options, string when)
    {
        var screen = AssertNotNull(Screen(), $"Rules panel open ({when})")!;
        var content = screen.gameRuleContent;
        var ours = Enumerable.Range(0, content.childCount).Select(i => content.GetChild(i))
            .Where(child => child.name == RulesPanel.Name).ToList();
        AssertEquals(ours.Count, 1, $"Boss rules in the Rules box ({when})");
        var text = ours[0].GetComponent<TMP_Text>();
        AssertEquals(text.text, string.Join("\n", RulesPanel.Lines(options)), $"Boss rules ({when})");
        Assert(ours[0].gameObject.activeInHierarchy, $"Boss rules shown ({when})");
        Log($"{when}: {text.text.Replace("\n", " | ")}");

        // In the box the game shows, without scrolling: a Standard setup has at most 8 lines
        var scroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        var view = scroll != null && scroll.viewport != null ? scroll.viewport : content.parent.Cast<RectTransform>();
        var rect = text.rectTransform;
        var textBottom = view.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMin))).y;
        var textRight = view.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMax, rect.rect.yMin))).x;
        Log($"{when}: text bottom {textBottom:0}, right {textRight:0}, view {view.rect.xMin:0}..{view.rect.xMax:0} x " +
            $"{view.rect.yMin:0}..{view.rect.yMax:0}, content height {content.Cast<RectTransform>().rect.height:0}");
        Assert(textBottom >= view.rect.yMin - 1, $"Boss rules fit in the Rules box ({when})");
        Assert(textRight <= view.rect.xMax + 1, $"Boss rules fit across the Rules box ({when})");
    }

    private static ChallengeRulesScreen? Screen() =>
        MenuManager.instance.GetCurrentMenu()?.TryCast<ChallengeRulesScreen>();

    private IEnumerator Play(BossOptions options)
    {
        if (InGame.instance != null) yield return ToMainMenu();
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(BaseDcm(), BossGameLauncher.Resolve(options)));
        yield return new WaitForSecondsRealtime(1);
    }

    /// <summary>
    /// Opens the pause screen and its Rules button, as the player does
    /// </summary>
    private IEnumerator OpenRules()
    {
        MenuManager.instance.OpenMenu(SceneNames.PauseScreenUI);
        yield return WaitFor(() => MenuManager.instance.GetCurrentMenu()?.TryCast<PauseScreen>() != null &&
                                   !MenuManager.instance.IsTransitioning, "Opening the pause screen");
        MenuManager.instance.GetCurrentMenu().Cast<PauseScreen>().ShowChallengeRules();
        yield return WaitFor(() => Screen() != null && !MenuManager.instance.IsTransitioning, "Opening the Rules panel");
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private IEnumerator CloseRules()
    {
        // The Rules panel goes back to the pause screen, which Continue closes like the player's button
        for (var tries = 0; tries < 6; tries++)
        {
            var menu = MenuManager.instance.GetCurrentMenu();
            var name = menu == null ? "none" : menu.GetIl2CppType().Name;
            Log($"closing {name}");
            if (Screen() != null) MenuManager.instance.Back();
            else if (menu?.TryCast<PauseScreen>() is { } pause) pause.ContinueClicked();
            else yield break;
            yield return WaitFor(() => !MenuManager.instance.IsTransitioning, "Closing a menu");
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Assert(false, "The Rules panel and the pause screen did not close");
    }
}
