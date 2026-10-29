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
/// The Boss Creator's keyboard: Ctrl shortcuts for share codes, saving and Play, Tab, the arrows, Enter and Space
/// through the Boss section's controls, Esc in a field only cancelling the edit, and nothing of it in the normal
/// Challenge Editor.
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
            yield return Controls();
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

        // Tab applies the typed value and moves on to the next control; Shift+Tab goes back
        yield return Focus(rounds);
        rounds.text = "30, 50";
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab)), KeyAction.NextField, "Tab");
        yield return WaitFor(() => !rounds.isFocused, "Tab leaving the spawn rounds");
        AssertEquals(BossCreatorKeys.Current?.Name, "ResetRounds", "Tab after the spawn rounds");
        AssertEquals(BossMode.Options.SpawnRounds, "30, 50", "Tab must apply the spawn rounds");
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab, Shift: true)), KeyAction.PreviousField, "Shift+Tab");
        yield return WaitFor(() => rounds.isFocused, "Shift+Tab focusing the spawn rounds");

        // Tab never types a tab character
        AssertEquals(rounds.onValidateInput.Invoke(rounds.text, 0, '\t'), '\0', "Tab character");
        AssertEquals(rounds.onValidateInput.Invoke(rounds.text, 0, '7'), '7', "Other characters");

        // Tab goes from the skull mode on to the skull field, which takes the keyboard
        yield return TabTo("SkullMode");
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab)), KeyAction.NextField, "Tab after the skull mode");
        yield return WaitFor(() => skulls.isFocused, "Tab focusing the skull field");

        // Enter applies the value and moves on to the next control
        skulls.text = "80, 20";
        skulls.onSubmit.Invoke(skulls.text);
        yield return WaitFor(() => !skulls.isFocused, "Enter ending the skull field's edit");
        AssertEquals(BossMode.Options.CustomSkulls, "80, 20", "Enter must apply the skulls");
        Assert(BossCreatorKeys.Current != null && BossCreatorKeys.Current.Name != "CustomSkulls",
            $"Enter must move on, not stay on {BossCreatorKeys.Current?.Name}");
        yield return Unfocus();

        yield return Leave();
    }

    /// <summary>
    /// Tab through all the controls, with a frame around the one that has the keyboard, the arrows changing it and
    /// Enter or Space pressing it
    /// </summary>
    private IEnumerator Controls()
    {
        BossMode.Enter();
        yield return WaitForEditor();
        var section = BossEditorSection.Current!;
        yield return Unfocus();

        // Tab starts at the first boss, Shift+Tab at Play
        var bosses = Enum.GetValues<BossType>();
        var first = $"Boss{bosses[0]}";
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab)), KeyAction.NextField, "Tab without a control");
        AssertEquals(BossCreatorKeys.Current?.Name, first, "First control");
        var frame = AssertNotNull(FocusFrame.Current, "No keyboard frame")!;
        AssertEquals(frame.transform.parent.gameObject.name, section.BossButtons[bosses[0]].gameObject.name,
            "The frame must be around the first boss");
        yield return Unfocus();
        BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab, Shift: true));
        AssertEquals(BossCreatorKeys.Current?.Name, "Play", "Shift+Tab without a control");
        BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab));
        AssertEquals(BossCreatorKeys.Current?.Name, first, "Tab after Play wraps to the start");

        // Every boss is a control of its own, picked with Enter or Space
        var boss = BossMode.Options.Boss;
        var other = bosses.First(b => b != boss);
        yield return TabTo($"Boss{other}");
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Enter)), KeyAction.Activate, "Enter on a boss");
        AssertEquals(BossMode.Options.Boss, other, "Enter must pick the boss with the frame");
        yield return TabTo($"Boss{boss}");
        BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Space));
        AssertEquals(BossMode.Options.Boss, boss, "Space must pick the boss with the frame");

        // Space and Enter toggle Elite
        yield return TabTo("Elite");
        var elite = BossMode.Options.Elite;
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Space)), KeyAction.Activate, "Space on Elite");
        AssertEquals(BossMode.Options.Elite, !elite, "Space must toggle Elite");
        BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Enter));
        AssertEquals(BossMode.Options.Elite, elite, "Enter must toggle Elite back");

        // The arrows move a slider by its step, Up and Down move between controls
        BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab));
        AssertEquals(BossCreatorKeys.Current?.Name, "BossSpeed", "Tab after Elite");

        // The frame goes around everything the row shows, its info button too
        yield return null;
        var frameRect = FocusFrame.Current!.GetComponent<RectTransform>();
        var info = section.BossSpeed.Row.GetComponentsInChildren<RectTransform>().First(r => r.name == EditorRows.InfoName);
        Vector2 infoCenter = frameRect.InverseTransformPoint(info.TransformPoint(info.rect.center));
        Assert(frameRect.rect.Contains(infoCenter), "The frame must go around the row's info button");

        var speed = section.BossSpeed.Value;
        BossCreatorKeys.Handle(new KeyPress(speed < section.BossSpeed.Slider.maxValue ? ShortcutKey.Right : ShortcutKey.Left));
        var changed = section.BossSpeed.Value;
        AssertEquals(Math.Abs(changed - speed), 10f, "An arrow must move Boss Speed by one step");
        section.Editor.ApplyUiSettingsToDCM();
        Assert(Math.Abs(section.Editor.dcm.bloonModifiers.bossSpeedMultiplier - changed / 100f) < 0.0001f,
            "Boss Speed in the challenge");
        section.BossSpeed.Step(speed > changed ? 1 : -1);
        BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Down));
        AssertEquals(BossCreatorKeys.Current?.Name, "BossHealth", "Down after Boss Speed");
        BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Up));
        AssertEquals(BossCreatorKeys.Current?.Name, "BossSpeed", "Up after Boss Health");

        // The arrows pick the next skull mode, which shows its own rows
        yield return TabTo("SkullMode");
        var mode = BossMode.Options.SkullMode;
        BossCreatorKeys.Handle(new KeyPress(mode == SkullMode.Custom ? ShortcutKey.Left : ShortcutKey.Right));
        yield return null;
        Assert(BossMode.Options.SkullMode != mode, "An arrow must change the skull mode");
        AssertEquals(section.FocusStops.Any(stop => stop.Name == "SkullCount"),
            BossMode.Options.SkullMode == SkullMode.EvenlySpaced, "Skull Count is a control only while it is shown");
        section.SkullMode.Dropdown.value = (int) mode;
        yield return null;

        // Enter presses a button
        yield return TabTo("CopyCode");
        clipboard = "";
        AssertEquals(BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Enter)), KeyAction.Activate, "Enter on Copy");
        Popup popup = null!;
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        Assert(clipboard.StartsWith("CBC"), $"Enter on Copy must copy a code: {clipboard}");

        // Esc takes the frame away instead of leaving the Boss Creator
        BossCreatorKeys.EscapePressed = () => true;
        MenuManager.instance.Back();
        yield return new WaitForSecondsRealtime(1);
        BossCreatorKeys.EscapePressed = () => Input.GetKeyDown(KeyCode.Escape);
        Assert(BossMode.Active, "Esc with the keyboard frame left the Boss Creator");
        AssertEquals(BossCreatorKeys.Current, null, "Esc must take the keyboard frame away");
        AssertEquals(FocusFrame.Current, null, "Esc must hide the keyboard frame");

        yield return Leave();
    }

    /// <summary>
    /// Presses Tab until <paramref name="name"/> has the keyboard
    /// </summary>
    private IEnumerator TabTo(string name)
    {
        for (var i = 0; i < 40 && BossCreatorKeys.Current?.Name != name; i++)
        {
            BossCreatorKeys.Handle(new KeyPress(ShortcutKey.Tab));
            yield return null;
        }

        AssertEquals(BossCreatorKeys.Current?.Name, name, $"Tabbing to {name}");
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
        BossCreatorKeys.ClearFocus();
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
