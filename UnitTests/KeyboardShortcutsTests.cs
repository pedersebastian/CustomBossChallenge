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
    private static readonly KeyContext OnControl = Editing with {StopFocused = true};

    [Theory]
    [InlineData(ShortcutKey.C, KeyAction.Copy)]
    [InlineData(ShortcutKey.V, KeyAction.Paste)]
    [InlineData(ShortcutKey.S, KeyAction.Save)]
    [InlineData(ShortcutKey.Enter, KeyAction.Play)]
    [InlineData(ShortcutKey.Tab, KeyAction.None)]
    [InlineData(ShortcutKey.Left, KeyAction.None)]
    [InlineData(ShortcutKey.Other, KeyAction.None)]
    public void Ctrl_shortcuts_work_without_a_focused_field(ShortcutKey key, KeyAction expected)
    {
        Assert.Equal(expected, KeyboardShortcuts.Decide(new KeyPress(key, Ctrl: true), Editing));
        Assert.Equal(expected, KeyboardShortcuts.Decide(new KeyPress(key, Ctrl: true), OnControl));
    }

    [Theory]
    [InlineData(ShortcutKey.C)]
    [InlineData(ShortcutKey.V)]
    [InlineData(ShortcutKey.S)]
    [InlineData(ShortcutKey.Enter)]
    public void Keys_without_Ctrl_or_with_Shift_do_nothing_without_a_focused_control(ShortcutKey key)
    {
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(key), Editing));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(key, Ctrl: true, Shift: true), Editing));
    }

    [Theory]
    [InlineData(ShortcutKey.C)]
    [InlineData(ShortcutKey.V)]
    [InlineData(ShortcutKey.S)]
    [InlineData(ShortcutKey.Enter)]
    [InlineData(ShortcutKey.Left)]
    [InlineData(ShortcutKey.Down)]
    [InlineData(ShortcutKey.Space)]
    public void A_focused_field_keeps_its_keys(ShortcutKey key)
    {
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(key, Ctrl: true), InField));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(key), InField));
    }

    [Fact]
    public void Tab_moves_through_the_controls_with_or_without_one_focused()
    {
        foreach (var context in new[] {Editing, InField, OnControl})
        {
            Assert.Equal(KeyAction.NextField, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab), context));
            Assert.Equal(KeyAction.PreviousField, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab, Shift: true), context));
            Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab, Ctrl: true), context));
        }
    }

    [Theory]
    [InlineData(ShortcutKey.Left, KeyAction.Decrease)]
    [InlineData(ShortcutKey.Right, KeyAction.Increase)]
    [InlineData(ShortcutKey.Up, KeyAction.PreviousField)]
    [InlineData(ShortcutKey.Down, KeyAction.NextField)]
    [InlineData(ShortcutKey.Enter, KeyAction.Activate)]
    [InlineData(ShortcutKey.Space, KeyAction.Activate)]
    [InlineData(ShortcutKey.C, KeyAction.None)]
    public void A_focused_control_reacts_to_arrows_Enter_and_Space(ShortcutKey key, KeyAction expected)
    {
        Assert.Equal(expected, KeyboardShortcuts.Decide(new KeyPress(key), OnControl));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(key), Editing));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void Nothing_happens_outside_the_Boss_Creator_or_with_a_popup_or_panel_open(bool open, bool blocked)
    {
        var context = new KeyContext(open, FieldFocused: false, blocked, StopFocused: true);
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.C, Ctrl: true), context));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Tab), context));
        Assert.Equal(KeyAction.None, KeyboardShortcuts.Decide(new KeyPress(ShortcutKey.Right), context));
    }
}
