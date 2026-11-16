using System.Collections;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The Past Events list (#146), offline like every headless run: the mod carries its own copy of the events, the
/// window opens from its row and filters them, and an event opens as normal or Elite with its own rules, as one Undo
/// step, and plays with them
/// </summary>
public class PastEventsTest : CustomBossTest
{
    private const string LychId = "Lych47_m5zq59de";

    protected override IEnumerator Run()
    {
        // The built-in copy only, never the player's cache, and nothing downloaded
        var store = PastEvents.Store;
        PastEvents.Store = new PastEventsStore(Path.Combine(TempFolder, "boss-events.json"),
            () => typeof(PastEvents).Assembly.GetManifestResourceStream(PastEventsFormat.ResourceName),
            message => Log(message));
        try
        {
            PastEvents.Reload();
            var file = AssertNotNull(PastEvents.Current, "The mod carries its own copy of the events")!;
            AssertEquals(PastEvents.Store.Source, PastEventsSource.BuiltIn, "Where the events come from");
            Assert(file.Events.Count >= 284, $"Events in the built-in copy: {file.Events.Count}");
            Assert(file.Events.Any(e => e.Id == LychId), "The built-in copy has Lych47");
            Assert(file.Events.All(e => e.DisplayName.Length > 0), "Every event has a name to show");

            yield return Browse(file);
        }
        finally
        {
            PastEvents.Store = store;
            PastEvents.Reload();
        }
    }

    private IEnumerator Browse(PastEventsFile file)
    {
        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitFor(() => (editor = Object.FindObjectOfType<ChallengeEditor>()) != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Challenge Editor");
        yield return new WaitForSecondsRealtime(1);
        var section = BossEditorSection.Current!;

        var label = section.BrowseEvents.GetComponentInChildren<Il2CppTMPro.TMP_Text>().text;
        Assert(label.Contains(file.Events.Count.ToString()), $"The row says how many events there are: {label}");

        section.BrowseEvents.Button.onClick.Invoke();
        yield return null;
        var browser = AssertNotNull(PastEventsBrowser.Current, "Browse opens the Past Events window")!;
        browser.SetQuery(new PastEventQuery());
        AssertEquals(browser.Shown.Count, file.Events.Count, "Every event is shown");
        AssertEquals(browser.Shown[0].Start, file.Events.Max(e => e.Start), "Newest first");

        // The list is virtual: only the rows in view exist, and scrolling gives them other events
        Assert(browser.Rows.Count < 15, $"Rows built: {browser.Rows.Count}");
        AssertEquals(browser.Rows[0].Event, browser.Shown[0], "The first row shows the newest event");
        var last = browser.Shown[^1];
        AssertNotNull(browser.ButtonFor(last, last.Standard == null), "The oldest event scrolls into view");
        Assert(browser.Rows.Any(row => row.Event == last && row.Row.activeSelf), "A row shows the oldest event");

        // The filters and the search pick the events
        browser.SetQuery(new PastEventQuery(Boss: "lych"));
        Assert(browser.Shown.Count > 0 && browser.Shown.All(e => e.BossKey == "lych"), "Only Lych events");
        Assert(browser.Rows.Where(row => row.Row.activeSelf).All(row => row.Event!.BossKey == "lych"), "Only Lych rows");
        browser.SetQuery(new PastEventQuery("no such event"));
        Assert(browser.Shown.Count == 0 && browser.Rows.All(row => !row.Row.activeSelf) && browser.Empty.gameObject.activeSelf,
            "Nothing matches");
        browser.SetQuery(new PastEventQuery("lych 47"));
        var lych = browser.Shown.Single(e => e.Id == LychId);

        // Normal: its own rules, the event's name, and one Undo step
        var undo = BossHistory.UndoCount;
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup popup = null!;
        AssertNotNull(browser.ButtonFor(lych, false), "Lych 47 opens as normal")!.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(lych.DisplayName), $"The popup names the event: {popup.body.text}");
        yield return ClosePopup(popup, true);
        yield return null;
        Assert(PastEventsBrowser.Current == null, "Loading the event closes the window");
        AssertEquals(BossHistory.UndoCount, undo + 1, "Opening an event is one Undo step");
        AssertEquals(BossMode.Options.Boss, BossType.Lych, "Boss");
        Assert(!BossMode.Options.Elite && BossMode.Options.BossEventRounds, "Normal, with the boss's event rounds");
        AssertEquals(editor!.dcm.map, "Tinkerton", "Map");
        AssertEquals(editor.dcm.startRules.cash, 2500, "Cash");
        AssertEquals(editor.dcm.startRules.lives, 200, "Lives");
        AssertEquals(editor.dcm.maxParagons, 4, "Max paragons");
        AssertEquals(editor.dcm.name, PastEventRules.SetupName(lych, false), $"The event's name and date: {editor.dcm.name} / {editor.chalName?.text}");
        AssertEquals(editor.dcm.towers.ToArray().First(t => t.tower == "DartMonkey").max, 0, "Towers the event didn't allow");

        // Elite, and played: the match has the event's rules
        section.OpenPastEvents();
        yield return null;
        browser = PastEventsBrowser.Current!;
        AssertNotNull(browser.ButtonFor(lych, true), "Lych 47 opens as Elite")!.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        yield return null;
        Assert(BossMode.Options.Elite, "Elite");
        AssertEquals(editor.dcm.startRules.cash, 3000, "Elite cash");
        AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), null, "The event can be played");

        section.PlayButton.onClick.Invoke();
        yield return WaitFor(() => InGame.instance != null && InGame.instance.bridge != null, "Loading the game", 60);
        yield return new WaitForSecondsRealtime(1);
        var data = InGameData.CurrentGame;
        AssertEquals(data.bossData.bossBloon, BossType.Lych, "Boss in the match");
        Assert(data.bossData.bossEliteMode, "Elite in the match");
        AssertEquals(data.selectedMap, "Tinkerton", "Map in the match");
        AssertEquals(data.dcModel.startRules.cash, 3000, "Cash in the match");
        AssertEquals(data.dcModel.maxParagons, 4, "Max paragons in the match");
        AssertEquals(data.dcModel.towers.ToArray().First(t => t.tower == "DartMonkey").max, 0, "Towers in the match");
        // Leaving a Boss Creator game goes back to the Boss Creator; from there, Back to the main menu
        InGame.instance.QuitToMainMenu();
        yield return WaitFor(() => InGame.instance == null && Object.FindObjectOfType<ChallengeEditor>() != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Back in the Boss Creator", 60);
        yield return new WaitForSecondsRealtime(1);
        MenuManager.instance.Back();
        yield return ToMainMenu();
    }
}
