using System;
using System.Collections;
using System.IO;
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
            yield return NormalEditorHasNoBossSection();
            yield return PlayLaunchesTheGame();
        }
        finally
        {
            BossMode.Store = store;
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
        AssertEquals(infoButtons, 10, "Info buttons in the boss section");

        section.Tiers.Row.transform.Find($"Image/{EditorRows.InfoName}").GetComponent<Button>().onClick.Invoke();
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitFor(() => (popup = Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()) != null,
            "Opening the info popup");
        Assert(popup!.body.text.Contains(Texts.BossTiersInfo.T()), $"Info popup text: {popup.body.text}");
        popup.TryHidePop();
        yield return new WaitForSecondsRealtime(1);

        section.BossButtons[BossType.Vortex].Button.onClick.Invoke();
        section.Elite.SetChecked(true, true);
        section.BossSpeed.Slider.value = 150;
        section.BossHealth.Slider.value = 300;
        section.AllFortified.SetChecked(true, true);
        section.Tiers.Slider.value = 7;
        section.ExtraTierHealth.Slider.value = 200;
        section.SkullMode.Dropdown.value = (int) SkullMode.EvenlySpaced;
        section.SkullCount.Slider.value = 5;
        section.SpawnRounds.InputField.text = "30, 50";
        section.SpawnRounds.InputField.onEndEdit.Invoke("30, 50");
        yield return null;

        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Vortex), "The name must follow the boss");
        Assert(section.ExtraTierHealth.Row.activeSelf, "Extra tier health must show above 5 tiers");
        Assert(section.SkullCount.Row.activeSelf, "Skull count must show for evenly spaced skulls");
        AssertEquals(section.SpawnRounds.InputField.text, "30, 50, 70, 90, 110, 130, 150", "Normalized spawn rounds");

        var expected = new BossOptions
        {
            Boss = BossType.Vortex, Elite = true, Tiers = 7, ExtraTierHealth = 200,
            SkullMode = SkullMode.EvenlySpaced, SkullCount = 5, SpawnRounds = "30, 50"
        };
        AssertEquals(BossMode.Options, expected, "Options from the boss section");

        editor.ApplyUiSettingsToDCM();
        var dcm = editor.dcm;
        Assert(Approximately(dcm.bloonModifiers.bossSpeedMultiplier, 1.5f), "Boss speed in the editor's model");
        Assert(Approximately(dcm.bloonModifiers.healthMultipliers.boss, 3), "Boss health in the editor's model");
        Assert(dcm.bloonModifiers.allFortified, "All fortified in the editor's model");

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
        yield return ToMainMenu();
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
    /// The editor's reset button also resets the boss options, except for the chosen boss
    /// </summary>
    private IEnumerator ResetClearsBossOptions(ChallengeEditor editor)
    {
        var section = BossEditorSection.Current!;
        section.AllFortified.SetChecked(true, true);
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

        AssertEquals(BossMode.Options, new BossOptions {Boss = BossType.Vortex}, "Options after reset");
        AssertEquals(section.BossSpeed.Value, 100f, "Boss speed shown after reset");
        AssertEquals(section.BossHealth.Value, 100f, "Boss health shown after reset");
        AssertEquals(section.Tiers.Value, 5f, "Tiers shown after reset");
        AssertEquals(section.CustomSkulls.InputField.text, new BossOptions().CustomSkulls, "Skulls shown after reset");
        Assert(!section.AllFortified.CurrentValue, "All fortified shown after reset");
        Assert(Approximately(editor.dcm.bloonModifiers.bossSpeedMultiplier, 1), "Boss speed in the model after reset");
        Assert(Approximately(editor.dcm.bloonModifiers.healthMultipliers.boss, 1), "Boss health in the model after reset");
        Assert(!editor.dcm.bloonModifiers.allFortified, "All fortified in the model after reset");
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
