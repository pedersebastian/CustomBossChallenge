using System.Collections;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.RightMenu.Powers;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// A past event's rules that the game's model has but a share code doesn't (#212): Insta Monkeys turned off and only
/// some powers allowed. No event in the archive has them yet, so the test makes one up from Lych 47. They are set on
/// the model, not listed as left out, survive the Challenge Editor, show in the Tier Preview, are left out by Copy, and
/// the match has them.
/// </summary>
public class PastEventRulesTest : CustomBossTest
{
    private const string LychId = "Lych47_m5zq59de";

    protected override IEnumerator Run()
    {
        // The built-in copy only, never the player's cache, and nothing downloaded
        var store = PastEvents.Store;
        PastEvents.Store = new PastEventsStore(System.IO.Path.Combine(TempFolder, "boss-events.json"),
            () => typeof(PastEvents).Assembly.GetManifestResourceStream(PastEventsFormat.ResourceName),
            message => Log(message));
        try
        {
            PastEvents.Reload();
            yield return Open();
        }
        finally
        {
            PastEvents.Store = store;
            PastEvents.Reload();
        }
    }

    private IEnumerator Open()
    {
        var lych = AssertNotNull(PastEvents.Current?.Events.FirstOrDefault(e => e.Id == LychId), "Lych 47 is in the events")!;
        var rules = (JObject) lych.Standard!.DeepClone();
        rules["disableInstas"] = true;
        rules["_powers"] = new JArray(
            new JObject {["power"] = "MonkeyBoost", ["max"] = 1},
            new JObject {["power"] = "CashDrop", ["max"] = 2});
        var pastEvent = lych with {Id = "MadeUpInstasAndPowers", Standard = rules};

        Assert(PastEventSetups.TryBuild(pastEvent, false, out var setup, out var dcm) && dcm != null, "The event opens");
        AssertEquals(string.Join(", ", setup.Unsupported), "", "Nothing is left out");
        Assert(dcm!.disableInstas, "Insta Monkeys are off on the model");
        AssertEquals(Max(dcm, "MonkeyBoost"), 1, "Monkey Boost is allowed once");
        AssertEquals(Max(dcm, "CashDrop"), 2, "Cash Drop is allowed twice");
        AssertEquals(Max(dcm, "Thrive"), 0, "A power the event doesn't list isn't allowed");
        Assert(dcm.displayIncludedPowers, "The game shows the allowed powers");
        AssertEquals(PastEventSetups.EventOnlyRules(dcm), (true, true), "The rules only an event sets");

        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitFor(() => (editor = Object.FindObjectOfType<ChallengeEditor>()) != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Challenge Editor");
        yield return new WaitForSecondsRealtime(1);
        var section = BossEditorSection.Current!;

        BossMode.Replace(editor!, setup.Options, dcm);
        section.Load(editor!.dcm, BossMode.Options);
        yield return null;
        // The editor writes its own fields into the model; it has no control for these, which must stay
        BossHistory.WatchRules(editor, now: true);
        Assert(editor.dcm.disableInstas, "Insta Monkeys stay off through the Challenge Editor");
        AssertEquals(Max(editor.dcm, "MonkeyBoost"), 1, "The powers stay through the Challenge Editor");

        var index = section.Strip.Warnings.FindIndex(w => w.Kind == SetupWarning.EventRules);
        Assert(index >= 0 && section.Strip.Notes[index].gameObject.activeSelf, "The Tier Preview says the event's rules are on");
        AssertEquals(section.Strip.Notes[index].Text.Shown(), ScreenText.Plain(Texts.EventNoInstasPowers.T()), "What the Tier Preview says");

        // Copy (and Save, which keeps the code) leaves them out, as the Tier Preview says
        Assert(SetupCode.TryDecode(SetupCode.Encode(BossMode.Options, editor.dcm), out _, out var copied, out _),
            "The copied code reads");
        Assert(!copied!.disableInstas && PastEventSetups.EventOnlyRules(copied) == (false, false), "Copy leaves them out");

        // The last setup keeps the whole model
        BossMode.Save();
        Assert(BossMode.Store.TryLoad(out _, out var last) && last!.disableInstas && Max(last, "CashDrop") == 2,
            "The last setup keeps them");

        section.PlayButton.onClick.Invoke();
        yield return WaitFor(() => InGame.instance != null && InGame.instance.bridge != null, "Loading the game", 60);
        yield return new WaitForSecondsRealtime(1);
        var data = InGameData.CurrentGame;
        Assert(data.dcModel.disableInstas, "Insta Monkeys are off in the match");
        AssertEquals(Max(data.dcModel, "MonkeyBoost"), 1, "Monkey Boost in the match");
        AssertEquals(Max(data.dcModel, "Thrive"), 0, "Thrive in the match");

        var menu = Object.FindObjectOfType<PowersMenu>(true);
        if (menu != null)
        {
            Log($"Powers menu: instasDisabled {menu.instasDisabled}");
            Assert(menu.instasDisabled, "The powers menu has Insta Monkeys turned off");
        }
        else Log("No powers menu in the match to check");

        InGame.instance.QuitToMainMenu();
        yield return WaitFor(() => InGame.instance == null && Object.FindObjectOfType<ChallengeEditor>() != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Back in the Boss Creator", 60);
        yield return new WaitForSecondsRealtime(1);
        Assert(BossEditorSection.Current?.Editor.dcm.disableInstas == true, "Back in the Boss Creator, they are still on");
        MenuManager.instance.Back();
        yield return ToMainMenu();
    }

    private static int Max(Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel dcm, string power)
    {
        foreach (var details in dcm.powers)
        {
            if (details.power == power) return details.max;
        }

        return int.MinValue;
    }
}
