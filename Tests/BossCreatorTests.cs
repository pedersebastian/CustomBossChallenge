using System;
using System.Collections;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The whole Boss Creator flow in the real Challenge Editor: the boss section edits the setup and the editor's
/// model, leaving saves the setup without touching the player's own editor draft, the normal editor shows no boss
/// section, and Play starts the custom game
/// </summary>
public class BossCreatorTest : CustomBossTest
{
    private string folder = "";

    protected override IEnumerator Run()
    {
        folder = Path.Combine(Path.GetTempPath(), "CustomBossChallengeTest-" + Guid.NewGuid().ToString("N"));
        var store = BossMode.Store;
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
        try
        {
            yield return EditSetup();
            yield return ShareCodes();
            yield return NormalEditorHasNoBossSection();
            yield return PlayLaunchesTheGame();
        }
        finally
        {
            BossMode.Store = store;
            Menus.Clipboard.Reset();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private IEnumerator EditSetup()
    {
        var userDraft = Game.Player.Data.challengeEditorModel;

        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitForEditor(e => editor = e);
        Assert(BossMode.Active, "Boss mode is not active");

        var section = AssertNotNull(BossEditorSection.Current, "No boss section")!;
        Assert(section.Section.activeInHierarchy, "The boss section is hidden");
        Assert(section.Play.activeInHierarchy, "The Play button is hidden");
        AssertEquals(section.BossButtons.Count, Enum.GetValues<BossType>().Length, "Boss buttons");
        AssertEquals(editor!.chalName.text, BossMode.DefaultName(BossType.Bloonarius), "Default challenge name");

        // Every boss option explains itself through an info button
        var infoButtons = 0;
        foreach (var info in section.Section.GetComponentsInChildren<Button>(true))
        {
            if (info.name == EditorRows.InfoName) infoButtons++;
        }
        AssertEquals(infoButtons, 14, "Info buttons in the boss section");

        section.Tiers.Row.transform.Find($"Image/{EditorRows.InfoName}").GetComponent<Button>().onClick.Invoke();
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitFor(() => (popup = Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()) != null,
            "Opening the info popup");
        Assert(popup!.body.text.Contains(Texts.BossTiersInfo.T()), $"Info popup text: {popup.body.text}");
        popup.TryHidePop();
        yield return new WaitForSecondsRealtime(1);

        AssertEquals(section.MaxParagons.Value, section.MaxParagons.Slider.maxValue, "Max paragons must default to unlimited");
        AssertEquals(editor.dcm.maxParagons, ParagonMath.NoLimit, "No paragon limit in a new setup");

        section.BossButtons[BossType.Vortex].Button.onClick.Invoke();
        section.Elite.SetChecked(true, true);
        section.BossSpeed.Slider.value = 150;
        section.BossHealth.Slider.value = 300;
        section.AllFortified.SetChecked(true, true);
        section.MaxParagons.Slider.value = 1;
        section.Tiers.Slider.value = 7;
        section.SpeedPerTier.Slider.value = 20;
        section.HealthPerTier.Slider.value = 50;
        section.SkullMode.Dropdown.value = (int) SkullMode.EvenlySpaced;
        section.SkullCount.Slider.value = 5;
        section.SpawnRounds.InputField.text = "30, 50";
        section.SpawnRounds.InputField.onEndEdit.Invoke("30, 50");
        yield return null;

        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Vortex), "The name must follow the boss");
        // The tier preview shows every tier with its round, speed and health, colored against the normal game
        var cards = section.Strip.Cards;
        AssertEquals(cards.Count(card => card.Panel.gameObject.activeSelf), 7, "Tier cards shown");
        AssertEquals(cards[0].Round.Text.text, "R30", "Round of tier 1");
        AssertEquals(cards[0].Speed.Text.text, "×1.5", "Speed of tier 1 (Boss Speed 150 %)");
        AssertEquals(cards[0].Health.Text.text, "125k", "Health of Vortex Elite tier 1 at 300 %");
        AssertEquals(cards[2].Speed.Text.text, "×2.1", "Speed of tier 3 (+20 % per tier)");
        AssertEquals(cards[6].Speed.Text.text, "×3.3", "Speed of tier 7");
        AssertEquals(cards[6].Health.Text.text, "201M", "Health of tier 7, capped like in the game");
        AssertEquals(cards[0].Speed.Text.color, TierStrip.Higher, "Faster than the normal game is orange");
        section.BossSpeed.Slider.value = 50;
        AssertEquals(cards[0].Speed.Text.text, "×0.5", "The preview follows Boss Speed");
        AssertEquals(cards[0].Speed.Text.color, TierStrip.Lower, "Slower than the normal game is blue");
        section.BossSpeed.Slider.value = 150;
        section.Elite.SetChecked(false, true);
        AssertEquals(cards[0].Health.Text.text, "60k", "The preview follows Elite");
        section.Elite.SetChecked(true, true);
        Assert(section.SkullCount.Row.activeSelf, "Skull count must show for evenly spaced skulls");
        AssertEquals(section.SpawnRounds.InputField.text, "30, 50, 70, 90, 110, 130, 150", "Normalized spawn rounds");

        var expected = new BossOptions
        {
            Boss = BossType.Vortex, Elite = true, Tiers = 7, SpeedPerTier = 20, HealthPerTier = 50,
            SkullMode = SkullMode.EvenlySpaced, SkullCount = 5, SpawnRounds = "30, 50"
        };
        AssertEquals(BossMode.Options, expected, "Options from the boss section");

        editor.ApplyUiSettingsToDCM();
        var dcm = editor.dcm;
        Assert(Approximately(dcm.bloonModifiers.bossSpeedMultiplier, 1.5f), "Boss speed in the editor's model");
        Assert(Approximately(dcm.bloonModifiers.healthMultipliers.boss, 3), "Boss health in the editor's model");
        Assert(dcm.bloonModifiers.allFortified, "All fortified in the editor's model");
        AssertEquals(dcm.maxParagons, 1, "Max paragons in the editor's model");

        section.BossSpeed.Show(100);
        editor.ApplyDcmSettingsToUI();
        AssertEquals(section.BossSpeed.Value, 150f, "The section must show the model's boss speed");

        // Loading a setup with another boss also follows it in the name, unless the player named the challenge
        editor.chalName.text = "My boss";
        section.Load(editor.dcm, BossMode.Options with {Boss = BossType.Lych});
        AssertEquals(editor.chalName.text, "My boss", "A loaded boss must keep the player's own name");
        editor.chalName.text = BossMode.DefaultName(BossType.Vortex);
        section.Load(editor.dcm, BossMode.Options with {Boss = BossType.Lych});
        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Lych), "The name must follow a loaded boss");
        section.BossButtons[BossType.Vortex].Button.onClick.Invoke();
        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Vortex), "The name must follow the boss back");

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return WaitFor(() => !BossMode.Active && MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name != nameof(ChallengeEditor),
            "Leaving the Boss Creator");

        var draft = Game.Player.Data.challengeEditorModel;
        Assert(draft == null && userDraft == null || draft != null && userDraft != null && draft.Pointer == userDraft.Pointer,
            "The player's own Challenge Editor draft was replaced");

        Assert(BossMode.Store.TryLoad(out var saved, out var savedDcm), "The setup was not saved");
        AssertEquals(saved, expected, "Saved options");
        Assert(Approximately(savedDcm!.bloonModifiers.bossSpeedMultiplier, 1.5f), "Saved boss speed");
        AssertEquals(savedDcm.maxParagons, 1, "Saved max paragons");
        yield return ToMainMenu();
    }

    /// <summary>
    /// Copy puts the setup on the clipboard, and Paste loads a shared setup after asking, or explains what is wrong
    /// </summary>
    private IEnumerator ShareCodes()
    {
        var clipboard = "";
        Clipboard.Read = () => clipboard;
        Clipboard.Write = text => clipboard = text;

        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitForEditor(e => editor = e);
        var section = BossEditorSection.Current!;
        var mine = BossMode.Options;

        section.CopyCode.Button.onClick.Invoke();
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup popup = null!;
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(clipboard.Length.ToString()), $"Copied popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        var myCode = clipboard;
        Assert(SetupCode.TryDecode(myCode, out var copied, out var copiedDcm, out _), $"Copied code: {myCode}");
        AssertEquals(copied, mine, "Copied options");
        AssertEquals(copiedDcm!.maxParagons, 1, "Copied max paragons");

        // A setup from someone else
        var theirs = new BossOptions {Boss = BossType.Lych, Tiers = 3};
        var theirDcm = Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel.CreateDefaultEditorModel();
        theirDcm.name = "Shared Lych";
        theirDcm.map = "Logs";
        theirDcm.bloonModifiers.bossSpeedMultiplier = 2.5f;
        clipboard = SetupCode.Encode(theirs, theirDcm);

        // Cancel keeps the current setup
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains("Shared Lych"), $"Paste popup: {popup.body.text}");
        yield return ClosePopup(popup, false);
        AssertEquals(BossMode.Options, mine, "Cancel must keep the options");
        AssertEquals(editor!.dcm.name, BossMode.DefaultName(BossType.Vortex), "Cancel must keep the challenge");

        // Load replaces it, in the section, the editor and the saved setup
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        AssertEquals(BossMode.Options, theirs, "Loaded options");
        Assert(BossMode.Dcm != null && editor.dcm.Pointer == BossMode.Dcm.Pointer, "The editor must edit the loaded setup");
        AssertEquals(editor.dcm.name, "Shared Lych", "Loaded challenge name");
        AssertEquals(editor.dcm.map, "Logs", "Loaded map");
        AssertEquals(section.BossSpeed.Value, 250f, "Loaded boss speed shown");
        AssertEquals(section.Tiers.Value, 3f, "Loaded tiers shown");
        AssertEquals(section.MaxParagons.Value, section.MaxParagons.Slider.maxValue, "Loaded max paragons shown");
        Assert(BossMode.Store.TryLoad(out var saved, out _) && saved == theirs, "The loaded setup was not saved");

        // A broken code and one from a newer version explain themselves
        clipboard = "not a code";
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(Texts.CodeInvalid.T()), $"Invalid code popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        clipboard = "CBC7:abc";
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(Texts.CodeTooNew.T()), $"Newer code popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        AssertEquals(BossMode.Options, theirs, "A bad code must not change the setup");

        // The editor's Random button randomizes the boss options too, into a setup that can be played
        var random = BossRandomizer.Random;
        BossRandomizer.Random = new System.Random(7);
        try
        {
            var before = BossMode.Options;
            for (var i = 0; i < 10 && BossMode.Options == before; i++)
            {
                editor.randomBtn.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);
            }

            Assert(BossMode.Options != before, "Random must change the boss options");
            AssertEquals(section.Tiers.Value, (float) BossMode.Options.Tiers, "Random tiers shown");
            AssertEquals(section.SpeedPerTier.Value, (float) BossMode.Options.SpeedPerTier, "Random speed per tier shown");
            AssertEquals(section.HealthPerTier.Value, (float) BossMode.Options.HealthPerTier, "Random health per tier shown");
            AssertEquals(section.SkullMode.Dropdown.value, (int) BossMode.Options.SkullMode, "Random skull mode shown");
            editor.ApplyUiSettingsToDCM();
            AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), null, "A random setup must be playable");
            Assert(Approximately(editor.dcm.bloonModifiers.bossSpeedMultiplier, section.BossSpeed.Value / 100f),
                "Random boss speed shown");
        }
        finally
        {
            BossRandomizer.Random = random;
        }

        // Back to the setup from before, for the rest of the test
        clipboard = myCode;
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        AssertEquals(BossMode.Options, mine, "Options pasted back");
        AssertEquals(editor.dcm.maxParagons, 1, "Max paragons pasted back");

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return WaitFor(() => !BossMode.Active, "Leaving the Boss Creator");
        yield return ToMainMenu();
    }

    private IEnumerator WaitForPopup(Action<Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup> found)
    {
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitFor(() => (popup = Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()) != null,
            "Showing a popup");
        found(popup!);
    }

    private static IEnumerator ClosePopup(Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup popup, bool ok)
    {
        if (ok) popup.OKClicked();
        else popup.CancelClicked();
        yield return new WaitForSecondsRealtime(1);
    }

    private IEnumerator NormalEditorHasNoBossSection()
    {
        var model = Game.Player.Data.challengeEditorModel ??
                    Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel.CreateDefaultEditorModel();
        MenuManager.instance.OpenMenu(SceneNames.ChallengeEditor, model);
        yield return WaitForEditor(_ => { });

        Assert(!BossMode.Active, "Boss mode must be off in the normal editor");
        var editor = Object.FindObjectOfType<ChallengeEditor>();
        Assert(!BossEditorSection.IsShowing(editor), "The normal Challenge Editor shows the boss section");
        var play = editor.transform.Find(BossEditorSection.PlayName);
        Assert(play == null || !play.gameObject.activeSelf, "The normal Challenge Editor shows the boss Play button");
        var title = editor.transform.Find("TopInfo/Title")?.GetComponent<TMP_Text>();
        Assert(title == null || title.text != BossEditorSection.Title, "The normal Challenge Editor is titled Boss Creator");

        MenuManager.instance.Back();
        yield return ToMainMenu();
    }

    private IEnumerator PlayLaunchesTheGame()
    {
        BossMode.Enter();
        yield return WaitForEditor(_ => { });
        Assert(BossMode.Options.Boss == BossType.Vortex, "The last setup was not loaded");

        var play = AssertNotNull(BossEditorSection.Current, "No boss section")!.Play.GetComponentInChildren<Button>();
        yield return PlayIsBlocked(play);
        play.onClick.Invoke();
        yield return WaitFor(() => InGame.instance != null && InGame.instance.bridge != null, "Loading the game", 60);
        yield return new WaitForSecondsRealtime(1);

        Assert(!BossMode.Active, "Boss mode must end when the game starts");
        Assert(CustomBossState.IsActive, "The game is not a custom boss game");
        var data = InGameData.CurrentGame;
        AssertEquals(data.bossData.bossBloon, BossType.Vortex, "Boss");
        Assert(data.bossData.bossEliteMode, "Elite");
        Assert(!data.bossData.bossRankedMode, "Ranked");
        AssertEquals(data.dcModel.maxParagons, 1, "Max paragons in the game");
        AssertEquals(TierMath.FormatRounds(InGame.instance.bridge.Simulation.map.spawner.bossBloonManager.spawnRounds),
            "29, 49, 69, 89, 109, 129, 149", "Spawn rounds (the game counts rounds from 0)");
        Assert(InGame.instance.GetGameModel().DoesBloonExist("VortexElite7"), "Tier 7 model");

        // Leaving the game returns to the editor it was started from, which must still be the Boss Creator
        InGame.instance.QuitToMainMenu();
        yield return WaitForEditor(_ => { });
        Assert(BossMode.Active, "Returning from the game must continue the Boss Creator");
        var editor = Object.FindObjectOfType<ChallengeEditor>();
        Assert(BossEditorSection.IsShowing(editor), "The boss section is missing after the game");
        AssertEquals(BossMode.Options.Boss, BossType.Vortex, "Boss after the game");
        AssertEquals(BossEditorSection.Current!.Tiers.Value, 7f, "Tiers shown after the game");

        yield return ResetClearsBossOptions(editor);

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return ToMainMenu();
    }

    /// <summary>
    /// Play explains instead of starting a game when the mode can't be used or the boss doesn't exist
    /// </summary>
    private IEnumerator PlayIsBlocked(Button play)
    {
        var editor = Object.FindObjectOfType<ChallengeEditor>();
        var mode = editor.dcm.mode;
        foreach (var unsupported in BossEditorSection.UnsupportedModes)
        {
            editor.dcm.mode = unsupported;
            AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), Texts.UnsupportedMode.T(unsupported),
                $"Playing {unsupported}");
        }
        editor.dcm.mode = mode;
        AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), null, "Playing the setup");

        // The unsupported modes the editor offers for this difficulty, through the real mode dropdown
        var original = editor.gameModeDropdown.value;
        foreach (var unsupported in BossEditorSection.UnsupportedModes)
        {
            var index = editor.gameModes.IndexOf(unsupported);
            if (index < 0) continue;

            editor.gameModeDropdown.value = index;
            yield return PlayShowsPopup(play, Texts.UnsupportedMode.T(unsupported));
        }
        editor.gameModeDropdown.value = original;
        editor.ApplyUiSettingsToDCM();

        var options = BossMode.Options;
        BossMode.Options = options with {Boss = (BossType) 99};
        yield return PlayShowsPopup(play, Texts.BossUnavailable.T(BossMode.Options.BossId(1)));
        BossMode.Options = options;
    }

    private IEnumerator PlayShowsPopup(Button play, string text)
    {
        play.onClick.Invoke();
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitFor(() => (popup = Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()) != null,
            $"Showing \"{text}\"");
        Assert(popup!.body.text.Contains(text), $"Popup text: {popup.body.text}");
        popup.TryHidePop();
        yield return new WaitForSecondsRealtime(1);
        Assert(InGame.instance == null && BossMode.Active, "Play must not start a game it can't play");
    }

    /// <summary>
    /// The editor's reset button also resets the boss options, back to a new setup with Bloonarius
    /// </summary>
    private IEnumerator ResetClearsBossOptions(ChallengeEditor editor)
    {
        var section = BossEditorSection.Current!;
        section.AllFortified.SetChecked(true, true);
        section.MaxParagons.Slider.value = 2;
        section.SkullMode.Dropdown.value = (int) SkullMode.Custom;

        // The skull field shows what it understood, and a list without valid values keeps the previous skulls
        section.CustomSkulls.InputField.text = "25,25,50";
        section.CustomSkulls.InputField.onEndEdit.Invoke("25,25,50");
        AssertEquals(section.CustomSkulls.InputField.text, "50, 25", "Skulls shown after editing");
        AssertEquals(BossMode.Options.CustomSkulls, "50, 25", "Skulls in the setup");
        section.CustomSkulls.InputField.text = "junk";
        section.CustomSkulls.InputField.onEndEdit.Invoke("junk");
        AssertEquals(section.CustomSkulls.InputField.text, "50, 25", "Skulls shown after invalid input");
        AssertEquals(BossMode.Options.CustomSkulls, "50, 25", "Skulls in the setup after invalid input");

        editor.clearBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);

        AssertEquals(BossMode.Options, new BossOptions(), "Options after reset");
        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Bloonarius), "Name after reset");
        Assert(section.BossButtons[BossType.Bloonarius].transform.Find(BossEditorSection.SelectedMarker).gameObject.activeSelf,
            "Bloonarius must be shown as chosen after reset");
        Assert(!section.BossButtons[BossType.Vortex].transform.Find(BossEditorSection.SelectedMarker).gameObject.activeSelf,
            "The previous boss must not be shown as chosen after reset");
        AssertEquals(section.SpeedPerTier.Value, 0f, "Speed per tier shown after reset");
        AssertEquals(section.BossSpeed.Value, 100f, "Boss speed shown after reset");
        AssertEquals(section.BossHealth.Value, 100f, "Boss health shown after reset");
        AssertEquals(section.Tiers.Value, 5f, "Tiers shown after reset");
        AssertEquals(section.CustomSkulls.InputField.text, new BossOptions().CustomSkulls, "Skulls shown after reset");
        Assert(!section.AllFortified.CurrentValue, "All fortified shown after reset");
        Assert(Approximately(editor.dcm.bloonModifiers.bossSpeedMultiplier, 1), "Boss speed in the model after reset");
        Assert(Approximately(editor.dcm.bloonModifiers.healthMultipliers.boss, 1), "Boss health in the model after reset");
        Assert(!editor.dcm.bloonModifiers.allFortified, "All fortified in the model after reset");
        AssertEquals(section.MaxParagons.Value, section.MaxParagons.Slider.maxValue, "Max paragons shown after reset");
        AssertEquals(editor.dcm.maxParagons, ParagonMath.NoLimit, "Max paragons in the model after reset");
    }

    private IEnumerator WaitForEditor(Action<ChallengeEditor> found)
    {
        ChallengeEditor? editor = null;
        yield return WaitFor(() => (editor = Object.FindObjectOfType<ChallengeEditor>()) != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Challenge Editor");
        yield return new WaitForSecondsRealtime(1);
        found(editor!);
    }

    private IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 20)
    {
        var start = Time.realtimeSinceStartup;
        while (!condition())
        {
            if (Time.realtimeSinceStartup - start > seconds) Fail($"{what} took more than {seconds}s");
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    private static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;
}
