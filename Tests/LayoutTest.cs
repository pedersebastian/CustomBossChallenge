using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // The other round buttons on the main menu; full screen click blockers are left out
        foreach (var other in mainMenu.GetComponentsInChildren<Button>())
        {
            if (other.transform.IsChildOf(button)) continue;
            var otherRect = ScreenRect(other);
            if (otherRect.width * otherRect.height > LayoutChecks.Screen.width * LayoutChecks.Screen.height / 4) continue;
            Expect(!Overlap(rect, otherRect), $"{format}: the Boss Creator button overlaps {other.name} ({Describe(otherRect)})");
        }
    }

    private void CheckEditor(string format, ChallengeEditor editor)
    {
        var section = AssertNotNull(BossEditorSection.Current, $"{format}: no Boss section")!;
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
        Log($"{format}: Play {Describe(play)}, Reset {Describe(reset)}, section {Describe(sectionRect)}, {checkedControls} controls checked");
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
