namespace CustomBossChallenge.Config;

/// <summary>
/// The keys the Boss Creator reacts to, as far as it cares about them
/// </summary>
public enum ShortcutKey
{
    Other,
    C,
    V,
    S,
    Enter,
    Tab,
    Space,
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// What a key press in the Boss Creator does
/// </summary>
public enum KeyAction
{
    None,
    Copy,
    Paste,
    Save,
    Play,
    NextField,
    PreviousField,
    Decrease,
    Increase,
    Activate
}

/// <summary>
/// A key pressed this frame. Ctrl is either Control key, or Command on macOS.
/// </summary>
public readonly record struct KeyPress(ShortcutKey Key, bool Ctrl = false, bool Shift = false);

/// <summary>
/// What is on the screen when a key is pressed
/// </summary>
/// <param name="BossCreatorOpen">The Boss Creator is open, not the normal Challenge Editor</param>
/// <param name="FieldFocused">A text field has the keyboard, the Boss section's or one of the editor's own</param>
/// <param name="BlockingUiOpen">A popup or one of the editor's panels (towers, maps, modifiers) is open</param>
/// <param name="StopFocused">One of the Boss section's other controls (a slider, button, ...) has the keyboard</param>
public readonly record struct KeyContext(bool BossCreatorOpen, bool FieldFocused, bool BlockingUiOpen, bool StopFocused = false);

/// <summary>
/// Pure rules for the Boss Creator's keyboard. Shortcuts only work while no text field has the keyboard, so copy and
/// paste in the fields stay normal. Tab moves through the Boss section's controls, and once one of them has the
/// keyboard, the arrows change it or move on, and Enter or Space press it.
/// </summary>
public static class KeyboardShortcuts
{
    public static KeyAction Decide(KeyPress press, KeyContext context)
    {
        if (!context.BossCreatorOpen || context.BlockingUiOpen) return KeyAction.None;

        if (press.Key == ShortcutKey.Tab && !press.Ctrl)
        {
            return press.Shift ? KeyAction.PreviousField : KeyAction.NextField;
        }

        // A text field keeps every other key for itself
        if (context.FieldFocused) return KeyAction.None;

        if (press.Ctrl)
        {
            if (press.Shift) return KeyAction.None;
            return press.Key switch
            {
                ShortcutKey.C => KeyAction.Copy,
                ShortcutKey.V => KeyAction.Paste,
                ShortcutKey.S => KeyAction.Save,
                ShortcutKey.Enter => KeyAction.Play,
                _ => KeyAction.None
            };
        }

        if (!context.StopFocused) return KeyAction.None;

        return press.Key switch
        {
            ShortcutKey.Left => KeyAction.Decrease,
            ShortcutKey.Right => KeyAction.Increase,
            ShortcutKey.Up => KeyAction.PreviousField,
            ShortcutKey.Down => KeyAction.NextField,
            ShortcutKey.Enter or ShortcutKey.Space => KeyAction.Activate,
            _ => KeyAction.None
        };
    }
}
