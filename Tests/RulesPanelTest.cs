using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using BTD_Mod_Helper.Api.Enums;
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
/// The pause screen's Rules panel of a custom game in Standard Mode (#141, #147): the sliders as rows of the Modifiers
/// list after the game's own, the boss and Boss Event Rounds as tiles after the game's own rule tiles, the spawn rounds
/// as tier cards and the skulls on a bar; once however often it is opened, and for a setup with nothing changed only
/// the boss and a No changes tile
/// </summary>
public class RulesPanelTest : RulesPanelTestBase
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions
        {
            Boss = BossType.Vortex, Elite = true, Tiers = 6, SpawnRounds = "40, 50, 60, 70, 80, 90", SpeedPerTier = 10,
            HealthPerTier = -5, SkullMode = SkullMode.EvenlySpaced, SkullCount = 4, BossEventRounds = true
        };
        var dcm = BaseDcm();
        dcm.bloonModifiers.healthMultipliers.boss = 3.69f;
        dcm.bloonModifiers.bossSpeedMultiplier = 1.3f;
        // Two of the game's own rule tiles, which keep their place first
        dcm.disableSelling = true;
        dcm.disableMK = true;
        yield return Play(options, dcm);
        yield return OpenRules();
        AssertView(options, "changed setup", fitsView: false, gameTiles: 2);
        // Boss Event Rounds has no tile of its own outside CHIMPS: the game's Custom Rounds tile says it
        AssertEquals(string.Join(",", RulesPanel.Tiles(RulesSummary.View(options)).Select(tile => tile.Label)),
            Visuals.BossHud.Name(BossType.Vortex, true), "Tiles");

        var folder = System.Environment.GetEnvironmentVariable("CBC_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(folder)) yield return ScreenshotTour.Capture(folder, "rules-panel");

        // Opened again: everything is made again, not added a second time
        yield return CloseRules();
        yield return OpenRules();
        AssertView(options, "reopened", fitsView: false, gameTiles: 2);
        yield return CloseRules();

        var plain = new BossOptions();
        yield return Play(plain);
        yield return OpenRules();
        AssertView(plain, "default setup");
        AssertEquals(RulesPanel.Tiles(RulesSummary.View(plain)).Last().Label, Texts.RulesNoChanges.T(), "The default setup has the No changes tile");
        yield return CloseRules();

        yield return ToMainMenu();
    }
}

/// <summary>
/// The Rules panel in Advanced Mode (#142, #147): a card per tier with its round and boss, a boss picked at random kept
/// a secret until it arrives, and a 10-tier line-up that fits across and scrolls at every screen format
/// </summary>
[FullSuiteOnly("the Rules panel in Advanced Mode games (about 30 s)")]
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
        AssertView(options, "before any boss", 0, fitsView: false);
        AssertSecret(3, true, "before any boss");
        AssertSecret(9, true, "before any boss");
        AssertSecret(2, false, "before any boss");
        var tiers = AssertNotNull(RulesPanel.Tiers, "Tier cards")!;
        AssertEquals(tiers.Cards[3].SkullCount.Text.text, "4", "Tier 4's own skulls");

        var folder = System.Environment.GetEnvironmentVariable("CBC_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(folder)) yield return ScreenshotTour.Capture(folder, "rules-panel-advanced");

        // The cards are smaller here than in the Boss Creator, still a card's lines don't run into each other (#234)
        foreach (var card in AssertNotNull(RulesPanel.Tiers, "Tier cards")!.Cards.Where(card => card.Panel.gameObject.activeInHierarchy))
        {
            var lines = new[] {card.Tier, card.Round, card.Speed, card.Health}.Select(line => LayoutChecks.InkRect(line.Text)).ToList();
            for (var i = 0; i + 1 < lines.Count; i++)
            {
                Assert(!LayoutChecks.Overlap(lines[i], lines[i + 1]),
                    $"{card.Panel.name}: line {i + 1} ({LayoutChecks.Describe(lines[i])}) runs into line {i + 2} ({LayoutChecks.Describe(lines[i + 1])})");
            }
        }
        yield return CloseRules();

        // Tier 3 arrives: its boss is shown from then on, tier 9's is still a secret
        var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        manager.SpawnBoss(3, false);
        yield return new WaitForSecondsRealtime(1.5f);
        AssertEquals(RulesPanel.ArrivedTier(), 3, "Arrived tier");
        yield return OpenRules();
        AssertView(options, "tier 3 arrived", 3, fitsView: false);
        AssertSecret(3, false, "tier 3 arrived");
        AssertSecret(9, true, "tier 3 arrived");
        yield return CloseRules();

        // Every screen format the layout test checks: the cards fit across and the box scrolls to them
        var (width, height, mode) = (UnityEngine.Screen.width, UnityEngine.Screen.height, UnityEngine.Screen.fullScreenMode);
        try
        {
            foreach (var format in LayoutTest.Formats)
            {
                yield return SetResolution(format.Width, format.Height);
                yield return OpenRules();
                AssertView(options, format.Name, 3, fitsView: false);
                yield return CloseRules();
            }
        }
        finally
        {
            UnityEngine.Screen.SetResolution(width, height, mode);
        }
        yield return SetResolution(width, height);

        yield return ToMainMenu();
    }

    private void AssertSecret(int tier, bool secret, string when)
    {
        var card = AssertNotNull(RulesPanel.Tiers, $"Tier cards ({when})")!.Cards[tier - 1];
        AssertEquals(card.Health.Text.text == "?" && card.SkullCount.Text.text == "?", secret,
            $"Tier {tier} is {(secret ? "" : "not ")}a secret ({when}): health {card.Health.Text.text}, skulls {card.SkullCount.Text.text}");
    }

    private IEnumerator SetResolution(int width, int height)
    {
        UnityEngine.Screen.SetResolution(width, height, FullScreenMode.Windowed);
        var start = Time.realtimeSinceStartup;
        while ((UnityEngine.Screen.width != width || UnityEngine.Screen.height != height) && Time.realtimeSinceStartup - start < 5)
        {
            yield return null;
        }
        AssertEquals($"{UnityEngine.Screen.width}x{UnityEngine.Screen.height}", $"{width}x{height}", "Screen size");
        yield return new WaitForSecondsRealtime(1);
    }
}

/// <summary>
/// Opening and checking the pause screen's Rules panel, for the Standard and Advanced Mode tests
/// </summary>
public abstract class RulesPanelTestBase : CustomBossTest
{
    /// <param name="arrivedTier">The highest tier whose boss has arrived</param>
    /// <param name="fitsView">Whether everything must fit without scrolling; else the box must scroll to all of it</param>
    /// <param name="gameTiles">How many rule tiles of the game's own the Rules box has at least</param>
    protected void AssertView(BossOptions options, string when, int arrivedTier = 0, bool fitsView = true, int gameTiles = 0)
    {
        var screen = AssertNotNull(Screen(), $"Rules panel open ({when})")!;
        var view = RulesSummary.View(options, arrivedTier, screen.dcm.mode == BossOptions.ChimpsMode);
        var content = screen.gameRuleContent;
        var children = Enumerable.Range(0, content.childCount).Select(i => content.GetChild(i)).ToList();

        // The tiles: after the game's own, with the label and no value, like the game's own
        var ours = children.Where(child => child.name == RulesPanel.TileName).ToList();
        var expected = RulesPanel.Tiles(view);
        AssertEquals(ours.Count, expected.Count, $"Boss tiles in the Rules box ({when})");
        for (var i = 0; i < expected.Count; i++)
        {
            AssertEquals(ours[i].Find("Layout/Lbl").GetComponent<TMP_Text>().Shown(), ScreenText.Plain(expected[i].Label), $"Tile {i + 1} ({when})");
            Assert(ours[i].gameObject.activeInHierarchy, $"Tile {i + 1} shown ({when})");
            Assert(ours[i].GetComponentsInChildren<UnityEngine.UI.Button>().All(button => !button.enabled), $"Tile {i + 1} takes no focus ({when})");
        }
        var game = children.Where(child => child.name is not RulesPanel.TileName and not RulesPanel.Name && child.gameObject.activeSelf).ToList();
        Assert(game.Count >= gameTiles, $"The game's own {gameTiles} rule tiles are there ({when}): {game.Count}");
        if (game.Count > 0 && ours.Count > 0)
            Assert(ours[0].GetSiblingIndex() > game[^1].GetSiblingIndex(), $"Boss tiles after the game's ({when})");

        // No plain text lines: the part under the grid only holds the cards and the skull bar
        var parts = children.Where(child => child.name == RulesPanel.Name).ToList();
        var hasStrips = view.TierStrip || view.Skulls != null;
        AssertEquals(parts.Count, hasStrips ? 1 : 0, $"Cards and skull bar under the grid ({when})");
        AssertEquals(RulesPanel.Tiers != null, view.TierStrip, $"Tier cards ({when})");
        AssertEquals(RulesPanel.Skulls != null, view.Skulls != null, $"Skull bar ({when})");

        if (RulesPanel.Tiers is { } strip)
        {
            var tiers = TierMath.ClampTiers(options.Tiers);
            var rounds = options.EffectiveSpawnRounds;
            AssertEquals(strip.Cards.Count(card => card.Panel.gameObject.activeSelf), tiers, $"A card per tier ({when})");
            for (var tier = 1; tier <= tiers; tier++)
                AssertEquals(strip.Cards[tier - 1].Round.Text.text, $"R{rounds[tier - 1]}", $"Tier {tier}'s round ({when})");
            Assert(strip.Cards.All(card => card.Button == null && !card.Edit.gameObject.activeInHierarchy), $"The cards are no buttons ({when})");
            Assert(strip.Notes.All(note => !note.gameObject.activeSelf), $"No warnings under the cards ({when})");
        }
        if (RulesPanel.Skulls is { } skulls)
        {
            var percents = options.SkullPercents ?? System.Array.Empty<float>();
            AssertEquals(skulls.Markers.Count(marker => marker.Panel.gameObject.activeSelf), percents.Length, $"Skulls on the bar ({when})");
        }

        // Everything fits across the box, and in it or as far as it scrolls
        var scroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        var viewRect = scroll != null && scroll.viewport != null ? scroll.viewport : content.parent.Cast<RectTransform>();
        var box = content.Cast<RectTransform>();
        var shown = ours.Select(tile => tile.Cast<RectTransform>()).ToList();
        if (parts.Count > 0)
        {
            shown.Add(parts[0].Cast<RectTransform>());
            foreach (var panel in new[] {RulesPanel.Tiers?.Panel, RulesPanel.Skulls?.Panel})
                if (panel != null) shown.Add(panel.RectTransform);
            foreach (var card in RulesPanel.Tiers?.Cards.Where(card => card.Panel.gameObject.activeSelf) ?? Enumerable.Empty<TierStrip.Card>())
                shown.Add(card.Panel.RectTransform);
        }
        foreach (var rect in shown)
        {
            var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            rect.GetWorldCorners(corners);
            var inView = corners.Select(corner => viewRect.InverseTransformPoint(corner)).ToList();
            var inBox = corners.Select(corner => box.InverseTransformPoint(corner)).ToList();
            Assert(inView.Min(p => p.x) >= viewRect.rect.xMin - 1 && inView.Max(p => p.x) <= viewRect.rect.xMax + 1,
                $"{rect.name} fits across the Rules box ({when}): {inView.Min(p => p.x):0}..{inView.Max(p => p.x):0} in " +
                $"{viewRect.rect.xMin:0}..{viewRect.rect.xMax:0}");
            if (fitsView) Assert(inView.Min(p => p.y) >= viewRect.rect.yMin - 1, $"{rect.name} fits in the Rules box ({when})");
            else Assert(inBox.Min(p => p.y) >= box.rect.yMin - 1, $"The Rules box scrolls to all of {rect.name} ({when})");
        }
        if (!fitsView) Assert(scroll != null && scroll.vertical, $"The Rules box scrolls ({when})");
        Log($"{when}: tiles [{string.Join(", ", expected.Select(tile => tile.Label))}], cards {view.TierStrip}, skulls " +
            $"{view.Skulls != null}, secret [{string.Join(",", view.SecretTiers)}], box {box.rect.width:0}x{box.rect.height:0}, " +
            $"view {viewRect.rect.width:0}x{viewRect.rect.height:0}");

        AssertModifiers(screen, options, when);
    }

    /// <summary>The sliders: rows after the game's own Boss Health and Boss Speed, like them, and the game's own kept</summary>
    private void AssertModifiers(ChallengeRulesScreen screen, BossOptions options, string when)
    {
        var modifiers = screen.modifierContent;
        var rows = Enumerable.Range(0, modifiers.childCount).Select(i => modifiers.GetChild(i)).ToList();
        var ourRows = rows.Where(row => row.name == RulesPanel.ModifierName).ToList();
        var expected = RulesPanel.ModifierRows(options);
        AssertEquals(ourRows.Count, expected.Count, $"Boss Creator rows in the Modifiers list ({when})");
        for (var i = 0; i < expected.Count; i++)
        {
            var label = ourRows[i].Find("Layout/Lbl").GetComponent<TMP_Text>().Shown();
            var value = ourRows[i].Find("Layout/Value").GetComponent<TMP_Text>().Shown();
            // Label and value apart: in Arabic the space between them doesn't keep their order (#291)
            AssertEquals((label, value), (ScreenText.Plain(expected[i].Label), ScreenText.Plain(expected[i].Value)),
                $"Modifier row {i + 1} ({when})");
            Assert(ourRows[i].gameObject.activeInHierarchy, $"Modifier row {i + 1} shown ({when})");
        }

        // Speed and health rows have icons of their own, the Boss Creator's, so they are told apart (#306)
        var icons = expected.Select((row, i) => (row.Icon, Sprite: ourRows[i].Find("Icon")?.GetComponent<UnityEngine.UI.Image>()?.sprite?.name)).ToList();
        Log($"Modifier icons ({when}): {string.Join(", ", icons.Select(icon => $"{icon.Icon}={icon.Sprite}"))}");
        foreach (var (icon, sprite) in icons)
        {
            Assert(sprite != null, $"A modifier row has no icon ({when})");
            Assert(icon is not (VanillaSprites.FasterBossIcon or VanillaSprites.BossBoostIcon),
                $"A modifier row keeps the game's speed or health icon ({when})");
        }
        var speed = icons.Where(icon => icon.Icon == EditorRows.SpeedIcon).Select(icon => icon.Sprite).Distinct().ToList();
        var health = icons.Where(icon => icon.Icon == EditorRows.HealthIcon).Select(icon => icon.Sprite).Distinct().ToList();
        if (speed.Count > 0 && health.Count > 0)
            Assert(!speed.Intersect(health).Any(), $"Speed and health rows show the same icon ({when})");
        var gameRows = rows.Where(row => row.name != RulesPanel.ModifierName && row.gameObject.activeSelf).ToList();
        if (ourRows.Count > 0 && gameRows.Count > 0)
            Assert(ourRows[0].GetSiblingIndex() > gameRows[^1].GetSiblingIndex(), $"Boss Creator rows after the game's ({when})");

        // More rows than fit scroll, like the game's own list
        var modifierScroll = modifiers.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (modifierScroll == null || modifierScroll.viewport == null || !modifierScroll.vertical)
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
