using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Every way a setup gets into the Boss Creator (by hand, pasted, saved, example, Random, Past Event, Undo): after its
/// game is quit, the player is back in the Boss Creator with the setup they played, and Back reaches the main menu
/// without an error in the log (#229)
/// </summary>
[FullSuiteOnly("plays a game from every way into the Boss Creator (about 1.5 min)")]
public class BackToCreatorTest : CustomBossTest
{
    private static readonly string[] Entries = {"by hand", "pasted", "saved", "example", "random", "past event", "undo"};
    private string clipboard = "";

    protected override IEnumerator Run()
    {
        Clipboard.Read = () => clipboard;
        Clipboard.Write = text => clipboard = text;
        var store = PastEvents.Store;
        PastEvents.Store = new PastEventsStore(Path.Combine(TempFolder, "boss-events.json"),
            () => typeof(PastEvents).Assembly.GetManifestResourceStream(PastEventsFormat.ResourceName), message => Log(message));
        PastEvents.Reload();
        var errors = new List<string>();
        void Logged(string section, string message) => errors.Add($"{section}: {message.Split('\n')[0]}");
        MelonLoader.MelonLogger.ErrorCallbackHandler += Logged;
        try
        {
            foreach (var entry in Entries)
            {
                errors.Clear();
                yield return PlayAndQuit(entry);
                AssertEquals(string.Join(" | ", errors), "", $"{entry}: errors logged");
            }
        }
        finally
        {
            MelonLoader.MelonLogger.ErrorCallbackHandler -= Logged;
            Clipboard.Reset();
            PastEvents.Store = store;
            PastEvents.Reload();
        }
    }

    private IEnumerator PlayAndQuit(string entry)
    {
        Log(entry);
        yield return ToMainMenu();
        BossMode.Enter();
        yield return WaitFor(() => BossEditorSection.Current?.Editor != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Boss Creator", 30);
        yield return new WaitForSecondsRealtime(1);

        yield return Enter(entry);
        var section = BossEditorSection.Current!;
        var editor = section.Editor;
        editor.dcm.disableSaves = true;
        var options = BossMode.Options;
        var name = editor.dcm.name;
        AssertEquals(BossEditorSection.PlayProblem(editor.dcm, options), null, $"{entry}: the setup can be played");

        section.PlayButton.onClick.Invoke();
        yield return WaitFor(() => InGame.instance?.bridge?.Simulation != null && !InGame.instance.quitting &&
                                   MenuManager.instance?.IsTransitioning == false, "Loading the game", 60);
        yield return new WaitForSecondsRealtime(1.5f);

        InGame.instance.QuitToMainMenu();
        yield return WaitFor(() => InGame.instance == null && Object.FindObjectOfType<ChallengeEditor>() != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor) &&
                                   !MenuManager.instance.IsTransitioning, "Back in the editor", 60);
        yield return new WaitForSecondsRealtime(1);

        editor = Object.FindObjectOfType<ChallengeEditor>();
        Assert(BossMode.Active, $"{entry}: back in the Boss Creator, not the plain Challenge Editor");
        AssertNotNull(BossEditorSection.Current, $"{entry}: the Boss section is there");
        Assert(BossEditorSection.IsShowing(editor), $"{entry}: the Boss section is shown");
        Assert(BossMode.Dcm != null && editor.dcm.Pointer == BossMode.Dcm.Pointer, $"{entry}: the editor edits the Boss Creator's setup");
        AssertEquals(BossMode.Options, options, $"{entry}: the boss options played");
        AssertEquals(editor.dcm.name, name, $"{entry}: the challenge played");

        Object.FindObjectOfType<CommonForegroundScreen>()?.backBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1.5f);
        yield return AnswerPopups();
        yield return WaitFor(() => MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == "MainMenu",
            "Back to the main menu", 15);
        Assert(!BossMode.Active, $"{entry}: Back leaves the Boss Creator");
    }

    private static IEnumerator AnswerPopups()
    {
        for (var i = 0; i < 3 && PopupScreen.instance.GetFirstActivePopup() is { } popup; i++)
        {
            popup.OKClicked();
            yield return new WaitForSecondsRealtime(0.7f);
        }
    }

    private IEnumerator Enter(string entry)
    {
        var section = BossEditorSection.Current!;
        var editor = section.Editor;
        switch (entry)
        {
            case "by hand":
                // With the boss buttons, as the player does: the name follows the boss
                section.BossButtons[BossType.Lych].Button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.3f);
                AssertEquals(BossMode.Options.Boss, BossType.Lych, "The Lych button picks Lych");
                AssertEquals(editor.dcm.name, BossMode.DefaultName(BossType.Lych), "The name follows the boss");
                break;
            case "pasted":
            {
                var dcm = TestUtils.BaseDcm();
                dcm.name = "Pasted Back";
                clipboard = SetupCode.Encode(new BossOptions {Boss = BossType.Vortex, Tiers = 2, SpawnRounds = "40, 41"}, dcm);
                section.PasteCode.Button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return AnswerPopups();
                AssertEquals(editor.dcm.name, "Pasted Back", "The pasted setup is loaded");
                break;
            }
            case "saved":
            {
                var dcm = TestUtils.BaseDcm();
                dcm.name = "Saved Back";
                BossMode.Saved.Save(new BossOptions {Boss = BossType.Phayze, Tiers = 2, SpawnRounds = "40, 41"}, dcm);
                yield return LoadFromList(section, text => text.Contains("Saved Back"));
                AssertEquals(editor.dcm.name, "Saved Back", "The saved setup is loaded");
                break;
            }
            case "example":
            {
                var example = ExampleSetups.All.First();
                var line = BossEditorSection.ExampleLine(example);
                yield return LoadFromList(section, text => text == line);
                Assert(ExampleSetups.Named(editor.dcm.name) != null, $"The example is loaded: {editor.dcm.name}");
                break;
            }
            case "random":
                editor.randomBtn.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return AnswerPopups();
                break;
            case "past event":
            {
                section.OpenPastEvents();
                yield return null;
                var browser = PastEventsBrowser.Current!;
                browser.SetQuery(new PastEventQuery());
                var pastEvent = browser.Shown.First(e => PastEventSetups.TryBuild(e, false, out _, out _));
                browser.ButtonFor(pastEvent, false)!.Button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return AnswerPopups();
                Assert(PastEventsBrowser.Current == null, "The Past Event is loaded");
                break;
            }
            case "undo":
            {
                // A pasted setup, then Undo back to the one before it
                var dcm = TestUtils.BaseDcm();
                dcm.name = "Undone";
                clipboard = SetupCode.Encode(new BossOptions {Boss = BossType.Dreadbloon}, dcm);
                section.PasteCode.Button.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return AnswerPopups();
                BossHistory.Undo(editor);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert(editor.dcm.name != "Undone", "Undo goes back to the setup before the paste");
                break;
            }
        }
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private IEnumerator LoadFromList(BossEditorSection section, System.Func<string, bool> pick)
    {
        section.ShowSavedSetups();
        yield return new WaitForSecondsRealtime(0.3f);
        var index = section.SavedList.Dropdown.options.ToArray().ToList().FindIndex(option => pick(option.text));
        Assert(index >= 0, "The setup is in the list");
        section.SavedList.Dropdown.value = index;
        section.SavedList.Dropdown.onValueChanged.Invoke(index);
        yield return new WaitForSecondsRealtime(0.3f);
        section.LoadSetup.Button.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        yield return AnswerPopups();
    }
}
