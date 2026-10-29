using System;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Patches;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The Boss Creator's keyboard: Ctrl+C / Ctrl+V for share codes, Ctrl+S to save and Ctrl+Enter to play, and Tab
/// through the Boss section's controls, where the arrows change the one with the frame and Enter or Space press it.
/// Esc in a text field cancels the edit. Only the Boss Creator reacts, never the normal Challenge Editor.
/// </summary>
public static class BossCreatorKeys
{
    /// <summary>
    /// Whether Esc was pressed this frame; tests replace it, since key presses can't be faked
    /// </summary>
    public static Func<bool> EscapePressed { get; set; } = () => Input.GetKeyDown(KeyCode.Escape);

    /// <summary>
    /// The control with the keyboard focus frame, or null
    /// </summary>
    public static FocusStop? Current { get; private set; }

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
        if (section == null)
        {
            ClearFocus();
            return;
        }

        // The mouse takes over from the keyboard
        if (Input.GetMouseButtonDown(0)) ClearFocus();

        var focused = FocusedField();
        if (focused != null)
        {
            fieldFocusedFrame = Time.frameCount;
            Show(section.FocusStops.FirstOrDefault(stop => stop.Field != null && stop.Field.Pointer == focused.Pointer));
        }
        else if (Current is {Field: not null} || Current is {Available: false})
        {
            // The field's edit ended, or the control was hidden
            if (pendingFocus == null) ClearFocus();
        }

        var press = ReadKeys();

        // Enter in a text field already moved on to the next control; it must not press that one too
        var fromField = focused == null && Time.frameCount - fieldFocusedFrame <= 1;
        if (fromField && press.Key is ShortcutKey.Enter or ShortcutKey.Space && !press.Ctrl) return;

        if (press.Key != ShortcutKey.Other) Handle(press);
    }

    /// <summary>
    /// Does what a key press means in the open Boss Creator, and says what that was
    /// </summary>
    public static KeyAction Handle(KeyPress press)
    {
        var section = OpenSection();
        var focused = FocusedField();
        var stop = section == null ? null : StopOf(section, focused) ?? Current;
        var context = new KeyContext(section != null, focused != null, section != null && BlockingUiOpen(section),
            stop != null && stop.Field == null);
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
                MoveFocus(section!, stop, 1, wrap: true);
                break;
            case KeyAction.PreviousField:
                MoveFocus(section!, stop, -1, wrap: true);
                break;
            case KeyAction.Decrease:
                stop!.Step?.Invoke(-1);
                break;
            case KeyAction.Increase:
                stop!.Step?.Invoke(1);
                break;
            case KeyAction.Activate:
                stop!.Activate?.Invoke();
                break;
        }

        return action;
    }

    /// <summary>
    /// Enter in one of the section's text fields applies it and moves on to the next control
    /// </summary>
    public static void Submitted(BossEditorSection section, TMP_InputField field) =>
        MoveFocus(section, StopOf(section, field), 1, wrap: false);

    /// <summary>
    /// Moves the keyboard from <paramref name="from"/> to the next or previous control of the section. A text field that
    /// had the keyboard ends its edit first, which applies its value. Without a control yet, Tab starts at the first one
    /// and Shift+Tab at the last one.
    /// </summary>
    public static void MoveFocus(BossEditorSection section, FocusStop? from, int step, bool wrap)
    {
        var stops = section.FocusStops;
        if (stops.Count == 0) return;

        var index = from == null ? -1 : stops.ToList().FindIndex(stop => stop.Name == from.Name);
        var next = index < 0 ? (step > 0 ? 0 : stops.Count - 1) : index + step;
        if (wrap) next = (next % stops.Count + stops.Count) % stops.Count;

        FocusedField()?.DeactivateInputField();
        EventSystem.current?.SetSelectedGameObject(null);
        if (next < 0 || next >= stops.Count)
        {
            ClearFocus();
            return;
        }

        var target = stops[next];
        Show(target);
        ScrollIntoView(target.Target());

        // A field that just ended its edit on Enter finishes deactivating after this, so focus the next one a frame later
        pendingFocus = target.Field;
    }

    /// <summary>
    /// Whether Esc should only cancel the edit of a text field or take the keyboard frame away, instead of closing the
    /// Boss Creator. Focus may already be gone when the game asks, so a field focused in the frame before counts too.
    /// </summary>
    public static bool EscapeEndsFocus()
    {
        if (!EscapePressed() || FocusedField() == null && Time.frameCount - fieldFocusedFrame > 1 && Current == null)
        {
            return false;
        }

        ClearFocus();
        return true;
    }

    private static void Show(FocusStop? stop)
    {
        Current = stop;
        if (stop == null) FocusFrame.Hide();
        else FocusFrame.Show(stop.Target());
    }

    public static void ClearFocus()
    {
        Current = null;
        FocusFrame.Hide();
    }

    /// <summary>
    /// The Boss Creator's section while it is on the screen
    /// </summary>
    private static BossEditorSection? OpenSection()
    {
        var section = BossEditorSection.Current;
        return BossMode.Active && section != null && section.Section.activeInHierarchy ? section : null;
    }

    private static FocusStop? StopOf(BossEditorSection section, TMP_InputField? field) =>
        field == null ? null : section.FocusStops.FirstOrDefault(stop => stop.Field != null && stop.Field.Pointer == field.Pointer);

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

    private static void Click(Button button)
    {
        if (button != null && button.interactable && button.gameObject.activeInHierarchy) button.onClick.Invoke();
    }

    /// <summary>
    /// Scrolls the editor so the control with the keyboard is on the screen
    /// </summary>
    private static void ScrollIntoView(GameObject target)
    {
        var scroll = target.GetComponentInParent<ScrollRect>();
        var rect = target.GetComponent<RectTransform>();
        if (scroll == null || rect == null || scroll.content == null) return;

        var viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();
        var center = viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center)).y;
        var half = rect.rect.height / 2 + 40;
        var view = viewport.rect;
        var shift = center + half > view.yMax ? center + half - view.yMax
            : center - half < view.yMin ? center - half - view.yMin
            : 0;
        if (shift == 0) return;

        scroll.StopMovement();
        var content = scroll.content;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, content.anchoredPosition.y - shift);
    }

    private static KeyPress ReadKeys()
    {
        var key = Input.GetKeyDown(KeyCode.C) ? ShortcutKey.C
            : Input.GetKeyDown(KeyCode.V) ? ShortcutKey.V
            : Input.GetKeyDown(KeyCode.S) ? ShortcutKey.S
            : Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ? ShortcutKey.Enter
            : Input.GetKeyDown(KeyCode.Tab) ? ShortcutKey.Tab
            : Input.GetKeyDown(KeyCode.Space) ? ShortcutKey.Space
            : Input.GetKeyDown(KeyCode.LeftArrow) ? ShortcutKey.Left
            : Input.GetKeyDown(KeyCode.RightArrow) ? ShortcutKey.Right
            : Input.GetKeyDown(KeyCode.UpArrow) ? ShortcutKey.Up
            : Input.GetKeyDown(KeyCode.DownArrow) ? ShortcutKey.Down
            : ShortcutKey.Other;
        var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        return new KeyPress(key, ctrl, shift);
    }
}
