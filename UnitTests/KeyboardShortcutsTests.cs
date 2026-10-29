using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Which key press does what in the Boss Creator
/// </summary>
public class KeyboardShortcutsTests
{
    private static readonly KeyContext Editing = new(BossCreatorOpen: true, FieldFocused: false, BlockingUiOpen: false);
    private static readonly KeyContext InField = Editing with {FieldFocused = true};

    [Theory]
    [InlineData(ShortcutKey.C, KeyAction.Copy)]
    [InlineData(ShortcutKey.V, KeyAction.Paste)]
    [InlineData(ShortcutKey.S, KeyAction.Save)]
    [InlineData(ShortcutKey.Enter, KeyAction.Play)]
    [InlineData(ShortcutKey.Tab, KeyAction.None)]
    [InlineData(ShortcutKey.Other, KeyAction.None)]
    public void Ctrl_shortcuts_work_without_a_focused_field(ShortcutKey key, KeyAction expected) =>
        Assert.Equal(expected, KeyboardShortcuts.Decide(new KeyPress(key, Ctrl: true), Editing));

    [Theory]
    [InlineData(ShortcutKey.C)]
    [InlineData(ShortcutKey.V)]
    [InlineData(ShortcutKey.S)]
    [InlineData(ShortcutKey.Enter)]
    public void Keys_without_Ctrl_or_with_Shift_do_nothing(ShortcutKey key)
    {
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(key), Editing));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(key, Ctrl: true, Shift: true), Editing));
    }

    [Theory]
    [InlineData(ShortcutKey.C)]
    [InlineData(ShortcutKey.V)]
    [InlineData(ShortcutKey.S)]
    [InlineData(ShortcutKey.Enter)]
    public void Shortcuts_leave_a_focused_field_alone(ShortcutKey key) =>
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(key, Ctrl: true), InField));

    [Fact]
    public void Tab_moves_between_fields_only_while_one_is_focused()
    {
        Assert.Equal(KeyAction.NextField, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab), InField));
        Assert.Equal(KeyAction.PreviousField, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab, Shift: true), InField));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab, Ctrl: true), InField));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab), Editing));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void Nothing_happens_outside_the_Boss_Creator_or_with_a_popup_or_panel_open(bool open, bool blocked)
    {
        var context = new KeyContext(open, FieldFocused: false, blocked);
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.C, Ctrl: true), context));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab), context with {FieldFocused = true}));
    }
}
