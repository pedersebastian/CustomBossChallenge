using System;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Patches;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The Boss Creator's keyboard: Ctrl+C / Ctrl+V for share codes, Ctrl+S to save, Ctrl+Enter to play, and Tab, Enter and
/// Esc in the Boss section's text fields. Only the Boss Creator reacts, never the normal Challenge Editor.
/// </summary>
public static class BossCreatorKeys
{
    /// <summary>
    /// Whether Esc was pressed this frame; tests replace it, since key presses can't be faked
    /// </summary>
    public static Func<bool> EscapePressed { get; set; } = () => Input.GetKeyDown(KeyCode.Escape);

    private static int fieldFocusedFrame = -10;
    private static TMP_InputField? pendingFocus;

    public static void Update()
    {
        if (pendingFocus != null)
        {
            var field = pendingFocus;
            pendingFocus = null;
            if (field != null && field.gameObject.activeInHierarchy) Focus(field);
        }

        var section = OpenSection();
        if (section == null) return;
        if (FocusedField() != null) fieldFocusedFrame = Time.frameCount;

        var press = ReadKeys();
        if (press.Key != ShortcutKey.Other) Handle(press);
    }

    /// <summary>
    /// Does what a key press means in the open Boss Creator, and says what that was
    /// </summary>
    public static KeyAction Handle(KeyPress press)
    {
        var section = OpenSection();
        var focused = FocusedField();
        var context = new KeyContext(section != null, focused != null, section != null && BlockingUiOpen(section));
        var action = KeyboardShortcuts.Decide(press, context);

        switch (action)
        {
            case KeyAction.Copy:
                Click(section!.CopyCode.Button);
                break;
            case KeyAction.Paste:
                Click(section!.PasteCode.Button);
                break;
            case KeyAction.Save:
                Click(section!.SaveSetup.Button);
                break;
            case KeyAction.Play:
                Click(section!.PlayButton);
                break;
            case KeyAction.NextField:
                MoveFocus(section!, focused!, 1, wrap: true);
                break;
            case KeyAction.PreviousField:
                MoveFocus(section!, focused!, -1, wrap: true);
                break;
        }

        return action;
    }

    /// <summary>
    /// Ends the edit of <paramref name="from"/>, which applies its value, and moves on to the next or previous field of
    /// the section. Without <paramref name="wrap"/>, the last field just ends its edit. Fields outside the section are
    /// left alone.
    /// </summary>
    public static void MoveFocus(BossEditorSection section, TMP_InputField from, int step, bool wrap = false)
    {
        var fields = section.InputFields;
        var index = fields.ToList().FindIndex(field => field.Pointer == from.Pointer);
        if (index < 0) return;

        var next = index + step;
        if (wrap) next = (next % fields.Count + fields.Count) % fields.Count;
        from.DeactivateInputField();
        EventSystem.current?.SetSelectedGameObject(null);
        if (next < 0 || next >= fields.Count) return;

        // A field that just ended its edit on Enter finishes deactivating after this, so focus the next one a frame later
        pendingFocus = fields[next];
    }

    /// <summary>
    /// Whether Esc should only cancel the edit of a text field instead of closing the Boss Creator. Focus may already
    /// be gone when the game asks, so a field focused in the frame before counts too.
    /// </summary>
    public static bool EscapeCancelsEdit() =>
        EscapePressed() && (FocusedField() != null || Time.frameCount - fieldFocusedFrame <= 1);

    /// <summary>
    /// The Boss Creator's section while it is on the screen
    /// </summary>
    private static BossEditorSection? OpenSection()
    {
        var section = BossEditorSection.Current;
        return BossMode.Active && section != null && section.Section.activeInHierarchy ? section : null;
    }

    /// <summary>
    /// The text field that has the keyboard, the section's or one of the editor's own
    /// </summary>
    private static TMP_InputField? FocusedField()
    {
        var selected = EventSystem.current?.currentSelectedGameObject;
        var field = selected == null ? null : selected.GetComponent<TMP_InputField>();
        return field != null && field.isFocused ? field : null;
    }

    private static bool BlockingUiOpen(BossEditorSection section) =>
        PopupScreen.instance != null && PopupScreen.instance.GetFirstActivePopup() != null ||
        ChallengeEditor_OverrideClose.OpenPanel(section.Editor);

    private static void Focus(TMP_InputField field)
    {
        EventSystem.current?.SetSelectedGameObject(field.gameObject);
        field.ActivateInputField();
    }

    private static void Click(UnityEngine.UI.Button button)
    {
        if (button != null && button.interactable && button.gameObject.activeInHierarchy) button.onClick.Invoke();
    }

    private static KeyPress ReadKeys()
    {
        var key = Input.GetKeyDown(KeyCode.C) ? ShortcutKey.C
            : Input.GetKeyDown(KeyCode.V) ? ShortcutKey.V
            : Input.GetKeyDown(KeyCode.S) ? ShortcutKey.S
            : Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ? ShortcutKey.Enter
            : Input.GetKeyDown(KeyCode.Tab) ? ShortcutKey.Tab
            : ShortcutKey.Other;
        var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        return new KeyPress(key, ctrl, shift);
    }
}
