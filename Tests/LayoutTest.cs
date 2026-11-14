using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.Main;
using Il2CppAssets.Scripts.Unity.UI_New.Main.EventPanel;
using UnityEngine;
using UnityEngine.UI;
using static CustomBossChallenge.Tests.LayoutChecks;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The Boss Creator button and editor fit on every common screen format: nothing the mod adds overlaps the game's own
/// buttons or sticks out of the screen or the Boss section. The game's canvas scales with the screen, so the aspect
/// ratio is what matters and small sizes are enough.
/// </summary>
public class LayoutTest : CustomBossTest
{
    public static readonly (string Name, int Width, int Height)[] Formats =
    {
        ("16:9", 1280, 720),
        ("16:10", 1280, 800),
        ("4:3", 1024, 768),
        ("21:9", 1680, 720),
        ("32:9", 1920, 540)
    };

    private readonly List<string> problems = new();

    protected override IEnumerator Run()
    {
        var (width, height, mode) = (UnityEngine.Screen.width, UnityEngine.Screen.height, UnityEngine.Screen.fullScreenMode);
        var folder = Path.Combine(Path.GetTempPath(), "CustomBossChallengeLayout-" + Guid.NewGuid().ToString("N"));
        var store = BossMode.Store;
        var saved = BossMode.Saved;
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(folder, "SavedSetups.json"));
        try
        {
            foreach (var format in Formats)
            {
                yield return SetResolution(format.Width, format.Height);
                yield return CheckFormat(format.Name);
            }
        }
        finally
        {
            UnityEngine.Screen.SetResolution(width, height, mode);
            BossMode.Store = store;
            BossMode.Saved = saved;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        yield return SetResolution(width, height);
        AssertEquals(string.Join("\n", problems), "", "Layout problems");
    }

    private IEnumerator CheckFormat(string format)
    {
        yield return ToMainMenu();
        yield return new WaitForSecondsRealtime(1);
        CheckMainMenu(format);

        BossMode.Enter();
        ChallengeEditor? editor = null;
        var start = Time.realtimeSinceStartup;
        while ((editor = Object.FindObjectOfType<ChallengeEditor>()) == null ||
               MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name != nameof(ChallengeEditor))
        {
            if (Time.realtimeSinceStartup - start > 20) Fail($"{format}: the Boss Creator did not open");
            yield return new WaitForSecondsRealtime(0.25f);
        }

        yield return new WaitForSecondsRealtime(1);
        CheckEditor(format, editor!);

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return ToMainMenu();
    }

    private void CheckMainMenu(string format)
    {
        var mainMenu = AssertNotNull(Object.FindObjectOfType<MainMenu>(), "No main menu")!;
        var button = AssertNotNull(mainMenu.transform.Find(MainMenuButton.Name), "No Boss Creator button")!;
        var rect = ScreenRect(button.GetComponentInChildren<Button>(true));
        Log($"{format}: Boss Creator button {Describe(rect)} on a {UnityEngine.Screen.width}x{UnityEngine.Screen.height} screen");
        Expect(Inside(rect, LayoutChecks.Screen), $"{format}: the Boss Creator button ({Describe(rect)}) is not on the screen");

        var icons = mainMenu.GetComponentInChildren<MainMenuEventPanel>(true)?.iconContainer;
        for (var i = 0; icons != null && i < icons.childCount; i++)
        {
            var icon = icons.GetChild(i);
            if (!icon.gameObject.activeInHierarchy) continue;
            Expect(!Overlap(rect, ScreenRect(icon.GetComponent<RectTransform>())),
                $"{format}: the Boss Creator button overlaps event button {icon.name}");
        }

        // The other buttons on the screen, the main menu's and any another mod adds on a canvas of its own
        foreach (var (name, otherRect) in OtherControls(button))
        {
            Expect(!Overlap(rect, otherRect), $"{format}: the Boss Creator button overlaps {name} ({Describe(otherRect)})");
        }
    }

    private void CheckEditor(string format, ChallengeEditor editor)
    {
        var section = AssertNotNull(BossEditorSection.Current, $"{format}: no Boss section")!;

        // In Advanced Mode, so its hint and the cards' pencils are checked too
        section.SetAdvanced(true);
        var play = ScreenRect(section.Play.GetComponentInChildren<Button>(true));
        var reset = ScreenRect(editor.transform.Find("Reset").GetComponentInChildren<Button>(true));
        Expect(Inside(play, LayoutChecks.Screen), $"{format}: Play ({Describe(play)}) is not on the screen");
        Expect(Inside(reset, LayoutChecks.Screen), $"{format}: Reset ({Describe(reset)}) is not on the screen");
        Expect(!Overlap(play, reset), $"{format}: Play ({Describe(play)}) overlaps Reset ({Describe(reset)})");

        // The editor's panels and their dark backgrounds cover Play, like they cover Reset
        var resetIndex = editor.transform.Find("Reset").GetSiblingIndex();
        var covering = new List<string>();
        foreach (var (name, panel) in new[]
                 {
                     ("Modifiers", editor.modifierSettings.transform), ("Add towers", editor.towerSelectObject.transform),
                     ("Tower Restrictions", editor.towerRestrictionMenu.transform), ("Map select", editor.mapSelectPanel.transform)
                 })
        {
            var top = panel;
            while (top.parent != null && top.parent != section.Play.transform.parent) top = top.parent;
            if (top.parent == null || top.GetSiblingIndex() < resetIndex) continue;
            covering.Add(name);
            Expect(section.Play.transform.GetSiblingIndex() < top.GetSiblingIndex(), $"{format}: Play is drawn on top of {name}");
        }

        Assert(covering.Contains("Modifiers"), $"{format}: the Modifiers panel must be checked; checked {string.Join(", ", covering)}");

        var banner = editor.transform.Find("TopInfo/Banner")?.GetComponent<RectTransform>();
        if (banner != null)
        {
            var bannerRect = ScreenRect(banner);
            Expect(!Overlap(play, bannerRect), $"{format}: Play ({Describe(play)}) overlaps the title ({Describe(bannerRect)})");
        }

        // Every control and text of the Boss section stays within the section's width
        var sectionRect = ScreenRect(section.Section.GetComponent<RectTransform>());
        var controls = section.Section.GetComponentsInChildren<Selectable>().Select(control => (Component) control)
            .Concat(section.Section.GetComponentsInChildren<Il2CppTMPro.TMP_Text>().Select(text => (Component) text));
        var checkedControls = 0;
        foreach (var control in controls)
        {
            var rect = ScreenRect(control);
            if (rect.width < 1) continue;
            checkedControls++;
            Expect(InsideHorizontally(rect, sectionRect),
                $"{format}: {NamePath(control.transform, section.Section.transform)} ({Describe(rect)}) sticks out of the Boss section ({Describe(sectionRect)})");
        }

        Assert(checkedControls > 20, $"{format}: only {checkedControls} controls of the Boss section were checked");

        // The reset buttons (issue #36) cover nothing else in their row, and Reset Boss Options not the section's title
        foreach (var button in new[] {section.ResetBossButton, section.ResetSkullsButton, section.ResetCustomSkullsButton})
        {
            if (!button.gameObject.activeInHierarchy) continue;
            var rect = ScreenRect(button.Button);
            foreach (var other in button.transform.parent.GetComponentsInChildren<Selectable>())
            {
                if (other.transform.IsChildOf(button.transform)) continue;
                var otherRect = ScreenRect(other);
                Expect(otherRect.width < 1 || !Overlap(rect, otherRect),
                    $"{format}: {button.name} ({Describe(rect)}) overlaps {NamePath(other.transform, section.Section.transform)} ({Describe(otherRect)})");
            }

            foreach (var text in button.transform.parent.GetComponentsInChildren<Il2CppTMPro.TMP_Text>())
            {
                if (text.transform.IsChildOf(button.transform)) continue;
                var letters = LettersRect(text);
                Expect(letters.width < 1 || !Overlap(rect, letters),
                    $"{format}: {button.name} ({Describe(rect)}) covers the text '{text.text}' ({Describe(letters)})");
            }
        }

        var resetAll = ScreenRect(section.ResetAllButton.Button);
        Expect(InsideHorizontally(resetAll, sectionRect), $"{format}: Reset Boss Options ({Describe(resetAll)}) sticks out of the section");
        var heading = section.Section.transform.Find("Heading")?.GetComponent<Il2CppTMPro.TMP_Text>();
        if (heading != null)
        {
            var letters = LettersRect(heading);
            Expect(!Overlap(resetAll, letters), $"{format}: Reset Boss Options ({Describe(resetAll)}) covers the title ({Describe(letters)})");
        }

        foreach (var row in section.Section.transform.Find(BossEditorSection.RowsName).GetComponentsInChildren<Selectable>())
        {
            var rowRect = ScreenRect(row);
            Expect(rowRect.width < 1 || !Overlap(resetAll, rowRect),
                $"{format}: Reset Boss Options ({Describe(resetAll)}) overlaps {NamePath(row.transform, section.Section.transform)}");
        }
        CheckPicker(format, section);
        section.SetAdvanced(false);
        CheckWarningLines(format, editor, section);
        Log($"{format}: Play {Describe(play)}, Reset {Describe(reset)}, section {Describe(sectionRect)}, {checkedControls} controls checked");
    }

    /// <summary>
    /// With every warning line under the Tier Preview (issue #70), the lines stay inside the row, below the cards and
    /// apart from each other, and the rows below move down with them
    /// </summary>
    private void CheckWarningLines(string format, ChallengeEditor editor, BossEditorSection section)
    {
        var round = editor.dcm.startRules.round;
        var lives = editor.dcm.startRules.lives;
        var spawnRounds = BossMode.Options.SpawnRounds;
        var eventRounds = BossMode.Options.BossEventRounds;
        var mode = editor.dcm.mode;
        var (tiers, bossHealth, healthPerTier, speedPerTier) =
            (section.Tiers.Slider.value, section.BossHealth.Slider.value, section.HealthPerTier.Slider.value, section.SpeedPerTier.Slider.value);
        try
        {
            // Every kind of warning at once (#170): health limit, tiers at the limits, close tiers, a close start round,
            // the CHIMPS boss events' rounds and a very hard setup
            section.Tiers.Slider.value = 10;
            BossMode.Options = BossMode.Options with {SpawnRounds = "40, 41", BossEventRounds = true};
            editor.dcm.mode = BossOptions.ChimpsMode;
            section.BossHealth.Slider.value = section.BossHealth.Slider.maxValue;
            section.HealthPerTier.Slider.value = section.HealthPerTier.Slider.maxValue;
            section.SpeedPerTier.Slider.value = section.SpeedPerTier.Slider.maxValue;
            editor.dcm.startRules.round = BossMode.Options.EffectiveSpawnRounds[0] - 2;
            editor.dcm.startRules.lives = 1;
            section.RulesChanged();
            Canvas.ForceUpdateCanvases();

            var lines = section.Strip.Notes.Where(note => note.gameObject.activeSelf).ToList();
            AssertEquals(lines.Count, Enum.GetValues<SetupWarning>().Length, $"{format}: warning lines shown");
            var row = ScreenRect(section.Strip.Row.GetComponent<RectTransform>());
            var cards = section.Strip.Cards.Where(card => card.Panel.gameObject.activeSelf).Select(card => ScreenRect(card.Panel.RectTransform)).ToList();
            var shown = new List<Rect>();
            foreach (var line in lines)
            {
                var letters = LettersRect(line.Text);
                Expect(Inside(letters, row), $"{format}: the warning '{line.Text.text}' ({Describe(letters)}) is outside the Tier Preview ({Describe(row)})");
                Expect(!cards.Any(card => Overlap(card, letters)), $"{format}: the warning '{line.Text.text}' ({Describe(letters)}) covers a tier card");
                Expect(!shown.Any(other => Overlap(other, letters)), $"{format}: the warning '{line.Text.text}' ({Describe(letters)}) covers another warning");
                shown.Add(letters);
            }

            // The row below the Tier Preview starts below its last line
            var next = section.Strip.Row.transform.parent.GetChild(section.Strip.Row.transform.GetSiblingIndex() + 1);
            var nextRect = ScreenRect(next.GetComponent<RectTransform>());
            Expect(!Overlap(nextRect, row), $"{format}: the row below the Tier Preview ({Describe(nextRect)}) overlaps it ({Describe(row)})");
        }
        finally
        {
            editor.dcm.startRules.round = round;
            editor.dcm.startRules.lives = lives;
            BossMode.Options = BossMode.Options with {SpawnRounds = spawnRounds, BossEventRounds = eventRounds};
            editor.dcm.mode = mode;
            section.Tiers.Slider.value = tiers;
            section.BossHealth.Slider.value = bossHealth;
            section.HealthPerTier.Slider.value = healthPerTier;
            section.SpeedPerTier.Slider.value = speedPerTier;
            section.RulesChanged();
        }
    }

    /// <summary>
    /// The boss picker is on the screen, and its buttons and texts are inside it without covering each other
    /// </summary>
    private void CheckPicker(string format, BossEditorSection section)
    {
        var picker = AssertNotNull(section.OpenPicker(section.Strip.Cards.Count(card => card.Panel.gameObject.activeSelf)),
            $"{format}: the boss picker did not open")!;
        try
        {
            Canvas.ForceUpdateCanvases();
            var panel = ScreenRect(picker.Panel.RectTransform);
            Expect(Inside(panel, LayoutChecks.Screen), $"{format}: the boss picker ({Describe(panel)}) is not on the screen");
            var buttons = picker.Panel.GetComponentsInChildren<Selectable>().Where(control => control.gameObject != picker.Panel.gameObject).ToList();
            foreach (var control in buttons.Select(control => (Component) control)
                         .Concat(picker.Panel.GetComponentsInChildren<Il2CppTMPro.TMP_Text>().Select(text => (Component) text)))
            {
                var rect = ScreenRect(control);
                Expect(rect.width < 1 || Inside(rect, panel),
                    $"{format}: {NamePath(control.transform, picker.Root.transform)} ({Describe(rect)}) sticks out of the boss picker");
            }

            for (var i = 0; i < buttons.Count; i++)
            {
                for (var j = i + 1; j < buttons.Count; j++)
                {
                    if (buttons[j].transform.IsChildOf(buttons[i].transform) || buttons[i].transform.IsChildOf(buttons[j].transform)) continue;

                    // A slider row is the game's own: its arrows sit right against its slider, like in the editor
                    if (new[] {picker.Speed.Row, picker.Health.Row, picker.SkullCount.Row}.Any(row =>
                            buttons[i].transform.IsChildOf(row.transform) && buttons[j].transform.IsChildOf(row.transform))) continue;
                    Expect(!Overlap(ScreenRect(buttons[i]), ScreenRect(buttons[j])),
                        $"{format}: {buttons[i].name} overlaps {buttons[j].name} in the boss picker");
                }
            }

            var labels = picker.Choices.Values.Select(choice => choice.transform.Find("Label")?.GetComponent<Il2CppTMPro.TMP_Text>())
                .Where(label => label != null).Select(label => label!).ToList();
            foreach (var label in labels)
            {
                label.ForceMeshUpdate();
                var letters = LettersRect(label);
                Expect(letters.width < 1 || Inside(letters, panel), $"{format}: the boss name '{label.text}' sticks out of the picker");
            }

            for (var i = 0; i + 1 < labels.Count; i++)
            {
                Expect(!Overlap(LettersRect(labels[i]), LettersRect(labels[i + 1])),
                    $"{format}: the boss names '{labels[i].text}' and '{labels[i + 1].text}' run into each other in the picker");
            }
        }
        finally
        {
            picker.Hide();
        }
    }

    private void Expect(bool condition, string problem)
    {
        if (condition) return;
        problems.Add(problem);
        Log(problem);
    }

    private static string NamePath(Transform transform, Transform root)
    {
        var names = new List<string>();
        for (var current = transform; current != null && current != root; current = current.parent) names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
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
