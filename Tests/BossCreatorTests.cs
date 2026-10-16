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
            "30, 50, 70, 90, 110, 130, 150", "Spawn rounds");
        Assert(InGame.instance.GetGameModel().DoesBloonExist("VortexElite7"), "Tier 7 model");

        yield return ToMainMenu();
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
