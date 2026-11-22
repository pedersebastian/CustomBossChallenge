using System;
using System.Collections;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using UnityEngine;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The What's new popup (#264), with a faked last seen version: after an update the Boss Creator shows the newest
/// release's points, whole and in a list that scrolls, once: not the next time, not on a fresh install and not with
/// the setting off; Don't Show Again turns the setting off
/// </summary>
public class WhatsNewTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var turnOff = WhatsNew.TurnOff;
        var usedBefore = WhatsNew.UsedBefore;
        try
        {
            yield return PopupShowsOnce();
        }
        finally
        {
            WhatsNew.TurnOff = turnOff;
            WhatsNew.UsedBefore = usedBefore;
        }
    }

    private IEnumerator PopupShowsOnce()
    {
        AssertEquals(Settings.ShowWhatsNew.GetDefaultValue(), true, "Show What's New is on by default");
        Assert(WhatsNew.Entries.Count > 0, "The mod was built without CHANGELOG.md");
        WhatsNew.Suppressed = false;
        WhatsNew.EnabledOverride = true;
        var turnedOff = false;
        WhatsNew.TurnOff = () => turnedOff = true;

        // An update: an older version was seen last. Only this version's points show, not those of versions between.
        var current = Changelog.ReadVersion(WhatsNew.Current)!;
        var older = Changelog.ReadVersion(WhatsNew.Entries.Select(entry => entry.Version)
            .FirstOrDefault(version => Changelog.ReadVersion(version) is { } read && read < current));
        older = AssertNotNull(older, "No version before this one in CHANGELOG.md")!;
        var expected = WhatsNew.Points();
        Assert(expected.All(item => item.Entry.Version == current.ToString(3)), "Only this version's points");
        Assert(expected.All(item => !item.Point.ForContributors), "Points for contributors are not for players");
        Log($"faking {older.ToString(3)} as last seen: {expected.Count} point(s) in {current.ToString(3)}");

        WhatsNew.Store.Save(older.ToString(3));
        var popup = default(Popup);
        yield return OpenBossCreator(found => popup = found);
        AssertEquals(WhatsNew.Store.Load(), WhatsNew.Current, "The version seen after the popup");
        if (expected.Count == 0)
        {
            // A release with only fixes has nothing to show
            AssertEquals(popup, null, "A popup for a version without new points");
            yield return ToMainMenu();
            yield break;
        }

        popup = AssertNotNull(popup, "No What's new popup after an update")!;
        var text = ScrollText(popup);
        Assert(text.text.Contains(Texts.WhatsNewIntro.Raw(WhatsNew.Current)), $"What's new says which version: {text.text}");
        foreach (var (_, point) in expected)
        {
            // Every point whole, not cut short
            Assert(text.text.Contains($"<b>{Changelog.ToRichText(point.Title)}</b>: {Changelog.ToRichText(point.Text)}"),
                $"What's new must show '{point.Title}' whole: {text.text}");
        }
        if (ScreenshotTour.Folder is {Length: > 0} folder) yield return ScreenshotTour.Capture(folder, "whats-new");
        yield return ClosePopup(popup, true);
        AssertRestored(popup);
        Assert(!turnedOff, "OK must leave Show What's New on");

        // Once per version: the next time shows nothing. The other cases ask the Boss Creator's check again without
        // opening it every time, which keeps the test short.
        AssertEquals(WhatsNew.ShowIfNew(), false, "A popup the second time");

        // Don't Show Again turns the setting off
        WhatsNew.Store.Save(older.ToString(3));
        AssertEquals(WhatsNew.ShowIfNew(), true, "A popup after another update");
        yield return WaitForList(found => popup = found);
        // Its own button, as a player clicks it: cancelling the popup is also what Esc does
        popup!.cancelObj.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);
        AssertRestored(popup);
        Assert(turnedOff, "Don't Show Again must turn Show What's New off");

        // Esc (the game's back) only closes it: the setting stays on
        turnedOff = false;
        WhatsNew.Store.Save(older.ToString(3));
        AssertEquals(WhatsNew.ShowIfNew(), true, "A popup after another update");
        yield return WaitForList(found => popup = found);
        Il2CppAssets.Scripts.Unity.Menu.MenuManager.instance.ProcessEscape();
        yield return new WaitForSecondsRealtime(1);
        Assert(!turnedOff, "Esc must not turn Show What's New off");
        AssertEquals(PopupScreen.instance.GetFirstActivePopup(), null, "Esc must close What's new");
        AssertRestored(popup!);

        // With the setting off nothing shows, but the version still counts as seen
        WhatsNew.EnabledOverride = false;
        WhatsNew.Store.Save(older.ToString(3));
        AssertEquals(WhatsNew.ShowIfNew(), false, "A popup with Show What's New off");
        AssertEquals(WhatsNew.Store.Load(), WhatsNew.Current, "The version seen with the setting off");
        WhatsNew.EnabledOverride = true;

        // A fresh install has no folder of the mod's when the game starts, and never shows the popup
        File.Delete(WhatsNew.Store.FilePath);
        WhatsNew.OnGameStart(false);
        AssertEquals(WhatsNew.Store.Load(), WhatsNew.Current, "The version seen on a fresh install");
        AssertEquals(WhatsNew.ShowIfNew(), false, "A popup on a fresh install");

        // An update from a version before the popup: the folder is there, the file isn't
        File.Delete(WhatsNew.Store.FilePath);
        WhatsNew.OnGameStart(true);
        AssertEquals(WhatsNew.Store.Load(), null, "A file written for an update from before the popup");
        AssertEquals(WhatsNew.ShowIfNew(), true, "A popup after an update from before the popup");
        yield return WaitForList(found => popup = found);
        yield return ClosePopup(popup!, true);
        AssertEquals(WhatsNew.Store.Load(), WhatsNew.Current, "The version seen after an update from before the popup");

        // A long release scrolls instead of shrinking its letters
        var sentence = string.Join(' ', Enumerable.Repeat("The Boss Creator shows something new.", 8));
        var many = Enumerable.Range(1, 8).Select(i => (new ChangelogEntry(WhatsNew.Current, "", []),
            new ChangelogPoint($"A long title for point number {i} of the list", "", sentence))).ToList();
        WhatsNew.Show(WhatsNew.Body(many));
        yield return WaitForList(found => popup = found);
        var scroll = WhatsNew.ScrollOf(popup!)!;
        var longText = ScrollText(popup!);
        AssertEquals(longText.fontSize, WhatsNew.FontSize, "The size of a long text");
        Assert(scroll.content.rect.height > scroll.viewport.rect.height * 2,
            $"A long text must be taller than its view: {scroll.content.rect.height:0} in {scroll.viewport.rect.height:0}");
        Assert(scroll.verticalScrollbar.gameObject.activeInHierarchy, "A long text has a scroll bar");
        Assert(Mathf.Abs(scroll.content.anchoredPosition.y) < 1, "A long text starts at its top");
        if (ScreenshotTour.Folder is {Length: > 0} top) yield return ScreenshotTour.Capture(top, "whats-new-long");
        scroll.verticalNormalizedPosition = 0;
        yield return null;
        Assert(scroll.content.anchoredPosition.y > scroll.content.rect.height / 2,
            $"Scrolling to the end must move the text up: {scroll.content.anchoredPosition.y:0}");
        yield return ClosePopup(popup!, true);
        AssertRestored(popup!);

        AssertEquals(PopupScreen.instance.GetFirstActivePopup(), null, "A popup left open");
        yield return ToMainMenu();
    }

    /// <summary>The popup's text as the list shows it, with the popup's own text hidden behind it</summary>
    private TMP_Text ScrollText(Popup popup)
    {
        var scroll = AssertNotNull(WhatsNew.ScrollOf(popup), "What's new has no list that scrolls")!;
        Assert(!popup.body.enabled, "The popup's own text must be hidden behind the list");
        var text = AssertNotNull(scroll.content.GetComponent<TMP_Text>(), "The list has no text")!;
        Assert(text.fontSize >= WhatsNew.FontSize, $"The list's text is too small: {text.fontSize}");
        AssertEquals(text.color, popup.body.color, "The list's text has the popup's colour");
        var list = LayoutChecks.ScreenRect(scroll.GetComponent<RectTransform>());
        var own = LayoutChecks.ScreenRect(popup.body.rectTransform);
        Assert(Mathf.Abs(list.center.x - own.center.x) < 2 && Mathf.Abs(list.center.y - own.center.y) < 2,
            $"The list ({LayoutChecks.Describe(list)}) must be where the text is ({LayoutChecks.Describe(own)})");
        return text;
    }

    /// <summary>The game uses the popup again for other texts: they must not get the list</summary>
    private void AssertRestored(Popup popup)
    {
        if (popup == null || popup.body == null) return;
        Assert(popup.body.enabled, "The popup's own text must come back when it closes");
        AssertEquals(WhatsNew.ScrollOf(popup), null, "The list must go when the popup closes");
    }

    /// <summary>Waits for the popup <see cref="WhatsNew.Show"/> opened and its list</summary>
    private IEnumerator WaitForList(Action<Popup?> found)
    {
        Popup? popup = null;
        yield return WaitFor(() => (popup = PopupScreen.instance.GetFirstActivePopup()) != null && WhatsNew.ScrollOf(popup) != null,
            "Showing What's new");
        // Its opening animation done
        yield return new WaitForSecondsRealtime(1);
        found(popup);
    }

    /// <summary>
    /// Opens the Boss Creator from the main menu and finds the popup it shows, or null after a moment without one
    /// </summary>
    private IEnumerator OpenBossCreator(Action<Popup?> found)
    {
        yield return ToMainMenu();
        ClickBossCreator();
        yield return WaitForBossCreator();
        var start = Time.realtimeSinceStartup;
        Popup? popup = null;
        while ((popup = PopupScreen.instance.GetFirstActivePopup()) == null && Time.realtimeSinceStartup - start < 2)
        {
            yield return new WaitForSecondsRealtime(0.25f);
        }
        // Its opening animation done
        if (popup != null) yield return new WaitForSecondsRealtime(1);
        found(popup);
    }
}
