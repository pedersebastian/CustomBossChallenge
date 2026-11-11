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
/// The pause screen's Rules panel of a custom game lists the boss options in Standard Mode (#141): the sliders as rows
/// of the Modifiers list after the game's own, the rest in the Rules box, once however often it is opened, and only the
/// boss with a no-changes line for a setup with nothing changed
/// </summary>
public class RulesPanelTest : RulesPanelTestBase
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions
        {
            Boss = BossType.Vortex, Elite = true, Tiers = 6, SpawnRounds = "40, 50, 60, 70, 80, 90", SpeedPerTier = 10,
            HealthPerTier = -5, SkullMode = SkullMode.EvenlySpaced, SkullCount = 4
        };
        var dcm = BaseDcm();
        dcm.bloonModifiers.healthMultipliers.boss = 3.69f;
        dcm.bloonModifiers.bossSpeedMultiplier = 1.3f;
        yield return Play(options, dcm);
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
        Assert(RulesPanel.Lines(plain, 0).Last() == Texts.RulesNoChanges.T(), "The default setup has the no-changes line");
        yield return CloseRules();

        yield return ToMainMenu();
    }
}

/// <summary>
/// The Rules panel in Advanced Mode (#142): a line per tier with its round, boss and what differs from it, a boss picked
/// at random kept a secret until it arrives, and a 10-tier line-up that the Rules box scrolls to
/// </summary>
public class RulesPanelAdvancedTest : RulesPanelTestBase
{
    protected override IEnumerator Run()
    {
        var setup = new BossOptions
        {
            Tiers = 10,
            TierBosses = "Lych,VortexElite@120/80,Random,Phayze#4,,Bloonarius#60%;30%,DreadbloonElite,Blastapopoulos,Random,Lych@200/150"
        };
        yield return Play(setup);
        var options = AssertNotNull(CustomBossState.ActiveOptions, "The game's options")!;
        Assert(options.WasRandom(3) && options.WasRandom(9), $"Tiers 3 and 9 were Random: {options.RandomTiers}");

        yield return OpenRules();
        AssertLines(options, "before any boss", 0, fitsView: false);
        var lines = RulesPanel.Lines(options, 0);
        Assert(lines.Any(line => line.StartsWith(Texts.RulesTier.T(3, 80, Texts.RandomBoss.T()))), "Tier 3 is still a secret");
        Assert(lines.Any(line => line.StartsWith(Texts.RulesTier.T(2, 60, ""))) &&
               lines.Single(line => line.StartsWith(Texts.RulesTier.T(2, 60, ""))).Contains(Texts.RulesTierSpeed.T(120)),
            "Tier 2 shows its own speed");

        var folder = System.Environment.GetEnvironmentVariable("CBC_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(folder)) yield return ScreenshotTour.Capture(folder, "rules-panel-advanced");
        yield return CloseRules();

        // Tier 3 arrives: its boss is shown from then on, tier 9's is still a secret
        var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        manager.SpawnBoss(3, false);
        yield return new WaitForSecondsRealtime(1.5f);
        AssertEquals(RulesPanel.ArrivedTier(), 3, "Arrived tier");
        yield return OpenRules();
        AssertLines(options, "tier 3 arrived", 3, fitsView: false);
        var (boss, elite) = options.BossOf(3);
        var after = RulesPanel.Lines(options, 3);
        Assert(after.Any(line => line.StartsWith(Texts.RulesTier.T(3, 80, Visuals.BossHud.Name(boss, elite)))), "Tier 3 shows its boss");
        Assert(after.Any(line => line.StartsWith(Texts.RulesTier.T(9, 200, Texts.RandomBoss.T()))), "Tier 9 is still a secret");
        yield return CloseRules();

        yield return ToMainMenu();
    }
}

/// <summary>
/// Opening and checking the pause screen's Rules panel, for the Standard and Advanced Mode tests
/// </summary>
public abstract class RulesPanelTestBase : CustomBossTest
{
    /// <param name="arrivedTier">The highest tier whose boss has arrived</param>
    /// <param name="fitsView">Whether the text must fit without scrolling; else the box must scroll to all of it</param>
    protected void AssertLines(BossOptions options, string when, int arrivedTier = 0, bool fitsView = true)
    {
        var screen = AssertNotNull(Screen(), $"Rules panel open ({when})")!;
        var content = screen.gameRuleContent;
        var ours = Enumerable.Range(0, content.childCount).Select(i => content.GetChild(i))
            .Where(child => child.name == RulesPanel.Name).ToList();
        AssertEquals(ours.Count, 1, $"Boss rules in the Rules box ({when})");
        var text = ours[0].GetComponent<TMP_Text>();
        AssertEquals(text.text, string.Join("\n", RulesPanel.Lines(options, arrivedTier)), $"Boss rules ({when})");
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
        Assert(textRight <= view.rect.xMax + 1, $"Boss rules fit across the Rules box ({when})");
        if (fitsView) Assert(textBottom >= view.rect.yMin - 1, $"Boss rules fit in the Rules box ({when})");
        else
        {
            var box = content.Cast<RectTransform>();
            var inContent = box.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMin))).y;
            Assert(scroll != null && scroll.vertical, $"The Rules box scrolls ({when})");
            Assert(inContent >= box.rect.yMin - 1, $"The Rules box scrolls to all the boss rules ({when})");
        }

        // The sliders: rows after the game's own Boss Health and Boss Speed, like them, and the game's own kept
        var modifiers = screen.modifierContent;
        var rows = Enumerable.Range(0, modifiers.childCount).Select(i => modifiers.GetChild(i)).ToList();
        var ourRows = rows.Where(row => row.name == RulesPanel.ModifierName).ToList();
        var expected = RulesPanel.ModifierRows(options);
        AssertEquals(ourRows.Count, expected.Count, $"Boss Creator rows in the Modifiers list ({when})");
        for (var i = 0; i < expected.Count; i++)
        {
            var label = ourRows[i].Find("Layout/Lbl").GetComponent<TMP_Text>().text;
            var value = ourRows[i].Find("Layout/Value").GetComponent<TMP_Text>().text;
            AssertEquals($"{label} {value}", $"{expected[i].Label} {expected[i].Value}", $"Modifier row {i + 1} ({when})");
            Assert(ourRows[i].gameObject.activeInHierarchy, $"Modifier row {i + 1} shown ({when})");
        }
        var gameRows = rows.Where(row => row.name != RulesPanel.ModifierName && row.gameObject.activeSelf).ToList();
        if (ourRows.Count > 0 && gameRows.Count > 0)
            Assert(ourRows[0].GetSiblingIndex() > gameRows[^1].GetSiblingIndex(), $"Boss Creator rows after the game's ({when})");
        var gameLabels = string.Join(", ", gameRows.Select(row => row.Find("Layout/Lbl")?.GetComponent<TMP_Text>()?.text));
        Log($"{when}: game modifiers [{gameLabels}], ours [{string.Join(", ", expected.Select(r => $"{r.Label} {r.Value}"))}]");

        // More rows than fit scroll, like the game's own list
        var modifierScroll = modifiers.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        var modifierView = modifierScroll != null && modifierScroll.viewport != null ? modifierScroll.viewport : null;
        Log($"{when}: modifiers scroll {modifierScroll != null}, content height {modifiers.Cast<RectTransform>().rect.height:0}, " +
            $"view height {(modifierView != null ? modifierView.rect.height : 0):0}");
        if (modifierView == null || modifierScroll == null || !modifierScroll.vertical)
        {
            foreach (var row in ourRows)
            {
                var r = row.Cast<RectTransform>();
                var bottom = modifiers.parent.Cast<RectTransform>().InverseTransformPoint(r.TransformPoint(new Vector3(0, r.rect.yMin))).y;
                Assert(bottom >= modifiers.parent.Cast<RectTransform>().rect.yMin - 1, $"Modifier rows fit without a scroll ({when})");
            }
        }
    }

    protected static ChallengeRulesScreen? Screen() =>
        MenuManager.instance.GetCurrentMenu()?.TryCast<ChallengeRulesScreen>();

    protected IEnumerator Play(BossOptions options, Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel? dcm = null)
    {
        if (InGame.instance != null) yield return ToMainMenu();
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm ?? BaseDcm(), BossGameLauncher.Resolve(options)));
        yield return new WaitForSecondsRealtime(1);
    }

    /// <summary>
    /// Opens the pause screen and its Rules button, as the player does
    /// </summary>
    protected IEnumerator OpenRules()
    {
        MenuManager.instance.OpenMenu(SceneNames.PauseScreenUI);
        yield return WaitFor(() => MenuManager.instance.GetCurrentMenu()?.TryCast<PauseScreen>() != null &&
                                   !MenuManager.instance.IsTransitioning, "Opening the pause screen");
        MenuManager.instance.GetCurrentMenu().Cast<PauseScreen>().ShowChallengeRules();
        yield return WaitFor(() => Screen() != null && !MenuManager.instance.IsTransitioning, "Opening the Rules panel");
        yield return new WaitForSecondsRealtime(0.5f);
    }

    protected IEnumerator CloseRules()
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
