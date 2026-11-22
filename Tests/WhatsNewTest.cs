using System;
using System.Collections;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using UnityEngine;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The What's new popup (#264), with a faked last seen version: after an update the Boss Creator shows the new
/// version's points once, not the next time, not on a fresh install and not with the setting off; Don't Show Again
/// turns the setting off
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

        // An update: the newest version before this one that is followed by points for players was seen last
        var current = Changelog.ReadVersion(WhatsNew.Current)!;
        var older = WhatsNew.Entries.Select(entry => Changelog.ReadVersion(entry.Version))
            .Where(version => version != null && version < current)
            .FirstOrDefault(version => WhatsNew.Points(version).Count > 0);
        older = AssertNotNull(older, "No version before this one has points after it")!;
        var expected = WhatsNew.Points(older);
        Log($"faking {older.ToString(3)} as last seen: {expected.Count} point(s) since");

        WhatsNew.Store.Save(older.ToString(3));
        var popup = default(Popup);
        yield return OpenBossCreator(found => popup = found);
        popup = AssertNotNull(popup, "No What's new popup after an update")!;
        var body = popup.body.text;
        Log($"text size {popup.body.fontSize:0.#}");
        Assert(body.Contains(Texts.WhatsNewIntro.Raw(WhatsNew.Current)), $"What's new says which version: {body}");
        foreach (var (_, point) in expected.Take(WhatsNew.MaxPoints))
        {
            Assert(body.Contains($"<b>{Changelog.ToRichText(point.Title)}</b>"), $"What's new must show '{point.Title}': {body}");
        }
        Assert(expected.All(item => !item.Point.ForContributors), "Points for contributors are not for players");
        Assert(!popup.body.isTextOverflowing, $"What's new doesn't fit its popup: {body}");
        AssertEquals(WhatsNew.Store.Load(), WhatsNew.Current, "The version seen after the popup");
        // Black text on a light blue card, which the popup doesn't keep for the game's other texts
        var card = AssertNotNull(popup.body.transform.parent.Find(WhatsNew.CardName), "No light blue card behind the text")!;
        AssertEquals(card.GetComponent<UnityEngine.UI.Image>().color, WhatsNew.CardColor, "The card's colour");
        AssertEquals(card.GetSiblingIndex() + 1, popup.body.transform.GetSiblingIndex(), "The card is right behind the text");
        AssertEquals(popup.body.color, WhatsNew.TextColor, "The text's colour");
        var cardRect = LayoutChecks.ScreenRect(card.GetComponent<RectTransform>());
        var textRect = LayoutChecks.ScreenRect(popup.body.rectTransform);
        Assert(cardRect.Contains(textRect.min) && cardRect.Contains(textRect.max),
            $"The card ({LayoutChecks.Describe(cardRect)}) must hold the text ({LayoutChecks.Describe(textRect)})");
        if (ScreenshotTour.Folder is {Length: > 0} folder) yield return ScreenshotTour.Capture(folder, "whats-new");
        var shown = popup;
        yield return ClosePopup(popup, true);
        if (shown != null && shown.body != null)
        {
            Assert(shown.body.transform.parent.Find(WhatsNew.CardName) == null, "The card stays on the popup after it closed");
            Assert(shown.body.color != WhatsNew.TextColor, "The popup's text keeps the black after it closed");
        }
        Assert(!turnedOff, "OK must leave Show What's New on");

        // Once per version: the next opening shows nothing. The other cases ask the Boss Creator's check again
        // without opening it every time, which keeps the test short.
        AssertEquals(WhatsNew.ShowIfNew(), false, "A popup the second time");

        // Don't Show Again turns the setting off
        WhatsNew.Store.Save(older.ToString(3));
        AssertEquals(WhatsNew.ShowIfNew(), true, "A popup after another update");
        yield return WaitForPopup(found => popup = found);
        yield return new WaitForSecondsRealtime(1);
        yield return ClosePopup(popup!, false);
        Assert(turnedOff, "Don't Show Again must turn Show What's New off");

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

        // An update from a version before the popup: the folder is there, the file isn't. This version's points show.
        File.Delete(WhatsNew.Store.FilePath);
        WhatsNew.OnGameStart(true);
        AssertEquals(WhatsNew.Store.Load(), null, "A file written for an update from before the popup");
        var own = WhatsNew.Points(null);
        AssertEquals(WhatsNew.ShowIfNew(), own.Count > 0, "A popup after an update from before the popup");
        if (own.Count > 0)
        {
            yield return WaitForPopup(found => popup = found);
            Assert(popup!.body.text.Contains($"<b>{Changelog.ToRichText(own[0].Point.Title)}</b>"),
                $"What's new must show this version's points: {popup.body.text}");
            yield return new WaitForSecondsRealtime(1);
            yield return ClosePopup(popup, true);
        }
        AssertEquals(WhatsNew.Store.Load(), WhatsNew.Current, "The version seen after an update from before the popup");
        AssertEquals(PopupScreen.instance.GetFirstActivePopup(), null, "A popup left open");

        // The most the popup ever shows fits it in letters nearly as big as the Boss Creator's info popups, which
        // players read already: a point with a long title and a first sentence cut at its longest, or many
        // points with long titles
        PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup($"<b>{Texts.BossTiers.T()}</b>\n\n{Texts.BossTiersInfo.T()}", null));
        yield return WaitForPopup(found => popup = found);
        yield return new WaitForSecondsRealtime(1);
        var normal = popup!.body.fontSize;
        yield return ClosePopup(popup, true);
        var sentence = string.Join(' ', Enumerable.Repeat("Boss Creator", 20)) + ".";
        foreach (var (name, count) in new[] {("one", WhatsNew.MaxWithText), ("many", WhatsNew.MaxPoints + 2)})
        {
            var points = Enumerable.Range(1, count).Select(i => (new ChangelogEntry("9.0.0", "", []),
                new ChangelogPoint($"A long title for point number {i} of the list", "", sentence))).ToList();
            WhatsNew.Show(WhatsNew.Body(points));
            yield return WaitForPopup(found => popup = found);
            yield return new WaitForSecondsRealtime(1);
            Log($"{name}: text size {popup!.body.fontSize:0.#}, the info popup's {normal:0.#}");
            if (ScreenshotTour.Folder is {Length: > 0} shots) yield return ScreenshotTour.Capture(shots, $"whats-new-{name}");
            Assert(!popup.body.isTextOverflowing, $"What's new with {name} points doesn't fit its popup: {popup.body.text}");
            Assert(popup.body.fontSize >= normal * 0.85f,
                $"What's new with {name} points is too small to read: {popup.body.fontSize:0.#} of {normal:0.#}");
            yield return ClosePopup(popup, true);
        }
        yield return ToMainMenu();
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
        found(popup);
        // Its opening animation done
        if (popup != null) yield return new WaitForSecondsRealtime(1);
    }
}
