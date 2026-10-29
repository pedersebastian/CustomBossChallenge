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
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The Boss Creator's keyboard: Ctrl shortcuts for share codes, saving and Play, Tab and Enter between the Boss
/// section's text fields, Esc in a field only cancelling the edit, and nothing of it in the normal Challenge Editor.
/// Key presses can't be faked, so the tests hand <see cref="BossCreatorKeys.Handle"/> the key press it would read.
/// </summary>
public class KeyboardTest : CustomBossTest
{
    private string clipboard = "";

    protected override IEnumerator Run()
    {
        var store = BossMode.Store;
        var saved = BossMode.Saved;
        BossMode.Store = new LastSetupStore(Path.Combine(TempFolder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(TempFolder, "SavedSetups.json"));
        Clipboard.Read = () => clipboard;
        Clipboard.Write = text => clipboard = text;
        try
        {
            yield return Shortcuts();
            yield return TabAndEnter();
            yield return EscapeCancelsTheEdit();
            yield return NormalEditorIgnoresKeys();
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
            Clipboard.Reset();
            BossCreatorKeys.EscapePressed = () => Input.GetKeyDown(KeyCode.Escape);
        }
    }

    private static KeyPress Ctrl(ShortcutKey key) => new(key, Ctrl: true);

    private IEnumerator Shortcuts()
    {
        BossMode.Enter();
        yield return WaitForEditor();
        var section = BossEditorSection.Current!;

        // Ctrl+C copies the setup
        AssertEquals(BossCreatorKeys.Handle(Ctrl(ShortcutKey.C)), KeyAction.Copy, "Ctrl+C");
        Popup popup = null!;
        yield return WaitForPopup(p => popup = p);

        // Nothing reacts while a popup is open
        var copied = clipboard;
        AssertEquals(BossCreatorKeys.Handle(Ctrl(ShortcutKey.V)), KeyAction.None, "Ctrl+V with a popup open");
        yield return ClosePopup(popup, true);
        Assert(SetupCode.TryDecode(copied, out var options, out _, out _) && options == BossMode.Options,
            $"Ctrl+C must copy the setup: {copied}");

        // Ctrl+V asks before loading a shared setup
        var theirDcm = Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel.CreateDefaultEditorModel();
        theirDcm.name = "Keyboard Lych";
        clipboard = SetupCode.Encode(new BossOptions {Boss = BossType.Lych}, theirDcm);
        AssertEquals(BossCreatorKeys.Handle(Ctrl(ShortcutKey.V)), KeyAction.Paste, "Ctrl+V");
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains("Keyboard Lych"), $"Paste popup: {popup.body.text}");
        yield return ClosePopup(popup, false);

        // Ctrl+S saves the setup under the challenge's name
        AssertEquals(BossCreatorKeys.Handle(Ctrl(ShortcutKey.S)), KeyAction.Save, "Ctrl+S");
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        section.Editor.ApplyUiSettingsToDCM();
        Assert(BossMode.Saved.All.Any(entry => entry.Name == SavedSetupsStore.NameOf(BossMode.Options, section.Editor.dcm)),
            "Ctrl+S must save the setup");

        // Ctrl+Enter presses Play, which explains a setup it can't play instead of starting it
        var mine = BossMode.Options;
        BossMode.Options = mine with {Boss = (BossType) 99};
        AssertEquals(BossCreatorKeys.Handle(Ctrl(ShortcutKey.Enter)), KeyAction.Play, "Ctrl+Enter");
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(Texts.BossUnavailable.T(BossMode.Options.BossId(1))), $"Play popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        BossMode.Options = mine;
        Assert(BossMode.Active, "Ctrl+Enter must not start a game it can't play");

        // Shortcuts leave a focused text field alone, so copy and paste work in it
        yield return Focus(section.SpawnRounds.InputField);
        clipboard = "untouched";
        AssertEquals(BossCreatorKeys.Handle(Ctrl(ShortcutKey.C)), KeyAction.None, "Ctrl+C in a text field");
        AssertEquals(clipboard, "untouched", "Ctrl+C in a text field must not copy a share code");
        yield return Unfocus();

        yield return Leave();
    }

    private IEnumerator TabAndEnter()
    {
        BossMode.Enter();
        yield return WaitForEditor();
        var section = BossEditorSection.Current!;
        var rounds = section.SpawnRounds.InputField;
        var skulls = section.CustomSkulls.InputField;

        section.SkullMode.Dropdown.value = (int) SkullMode.Custom;
        yield return null;
        AssertEquals(section.InputFields.Count, 2, "Text fields with Custom skulls");

        // Tab applies the typed value and moves on; Shift+Tab goes back
        yield return Focus(rounds);
        rounds.text = "30, 50";
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab)), KeyAction.NextField, "Tab");
        yield return WaitFor(() => skulls.isFocused, "Tab focusing the skull field");
        AssertEquals(BossMode.Options.SpawnRounds, "30, 50", "Tab must apply the spawn rounds");
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab, Shift: true)), KeyAction.PreviousField, "Shift+Tab");
        yield return WaitFor(() => rounds.isFocused, "Shift+Tab focusing the spawn rounds");

        // Tab never types a tab character
        AssertEquals(rounds.onValidateInput.Invoke(rounds.text, 0, '\t'), '\0', "Tab character");
        AssertEquals(rounds.onValidateInput.Invoke(rounds.text, 0, '7'), '7', "Other characters");

        // Enter applies the value and moves on, and ends the edit on the last field
        rounds.text = "35, 55";
        rounds.onSubmit.Invoke(rounds.text);
        yield return WaitFor(() => skulls.isFocused, "Enter focusing the skull field");
        skulls.text = "80, 20";
        skulls.onSubmit.Invoke(skulls.text);
        yield return WaitFor(() => !skulls.isFocused, "Enter on the last field ending its edit");
        AssertEquals(BossMode.Options.CustomSkulls, "80, 20", "Enter must apply the skulls");
        yield return new WaitForSecondsRealtime(0.2f);
        Assert(!rounds.isFocused, "Enter on the last field must not go back to the first");

        // With the skull field hidden, Tab stays on the only field
        section.SkullMode.Dropdown.value = (int) SkullMode.Vanilla;
        yield return null;
        AssertEquals(section.InputFields.Count, 1, "Text fields with vanilla skulls");
        yield return Focus(rounds);
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab)), KeyAction.NextField, "Tab on the only field");
        yield return WaitFor(() => rounds.isFocused, "Tab wrapping to the spawn rounds");
        yield return Unfocus();

        yield return Leave();
    }

    private IEnumerator EscapeCancelsTheEdit()
    {
        BossMode.Enter();
        yield return WaitForEditor();
        var section = BossEditorSection.Current!;

        // Esc while typing only ends the edit
        BossCreatorKeys.EscapePressed = () => true;
        yield return Focus(section.SpawnRounds.InputField);
        MenuManager.instance.Back();
        yield return new WaitForSecondsRealtime(1);
        Assert(BossMode.Active && MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Esc in a text field left the Boss Creator");

        // Without a field it leaves as before
        yield return Unfocus();
        yield return new WaitForSecondsRealtime(0.2f);
        MenuManager.instance.Back();
        yield return WaitFor(() => !BossMode.Active, "Esc leaving the Boss Creator");
        BossCreatorKeys.EscapePressed = () => Input.GetKeyDown(KeyCode.Escape);
        yield return ToMainMenu();
    }

    private IEnumerator NormalEditorIgnoresKeys()
    {
        var model = Game.Player.Data.challengeEditorModel ??
                    Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel.CreateDefaultEditorModel();
        MenuManager.instance.OpenMenu(SceneNames.ChallengeEditor, model);
        yield return WaitForEditor();

        clipboard = "untouched";
        AssertEquals(BossCreatorKeys.Handle(Ctrl(ShortcutKey.C)), KeyAction.None, "Ctrl+C in the normal editor");
        AssertEquals(BossCreatorKeys.Handle(Ctrl(ShortcutKey.S)), KeyAction.None, "Ctrl+S in the normal editor");
        yield return new WaitForSecondsRealtime(0.5f);
        AssertEquals(clipboard, "untouched", "The normal editor must not copy a share code");
        AssertEquals(PopupScreen.instance.GetFirstActivePopup(), null, "The normal editor must not show a popup");

        MenuManager.instance.Back();
        yield return ToMainMenu();
    }

    private IEnumerator Focus(TMP_InputField field)
    {
        EventSystem.current.SetSelectedGameObject(field.gameObject);
        field.ActivateInputField();
        yield return WaitFor(() => field.isFocused, $"Focusing {field.name}");
    }

    private static IEnumerator Unfocus()
    {
        EventSystem.current.SetSelectedGameObject(null);
        yield return new WaitForSecondsRealtime(0.2f);
    }

    private IEnumerator Leave()
    {
        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return WaitFor(() => !BossMode.Active, "Leaving the Boss Creator");
        yield return ToMainMenu();
    }

    private IEnumerator WaitForEditor()
    {
        yield return WaitFor(() => Object.FindObjectOfType<ChallengeEditor>() != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Challenge Editor");
        yield return new WaitForSecondsRealtime(1);
    }
}
