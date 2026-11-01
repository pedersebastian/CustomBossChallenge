using System;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The Boss Creator's Undo and Redo history (issue #36)
/// </summary>
public class UndoHistoryTests
{
    private static readonly DateTime Start = new(2026, 11, 1, 12, 0, 0);

    /// <summary>
    /// A state with a kind, like the mod's boss-only and whole-setup steps
    /// </summary>
    private readonly record struct State(string Kind, int Value);

    private int now = 7;

    /// <summary>
    /// Captures the state as it is now, in the kind of the step being undone or redone
    /// </summary>
    private State Current(State step) => step with {Value = now};

    [Fact]
    public void Undo_goes_back_step_by_step_and_redo_forward()
    {
        var history = new UndoHistory<State>();
        history.Record(new State("boss", 1), "Tiers", Start);
        history.Record(new State("boss", 2), "Elite", Start.AddSeconds(1));
        now = 3;

        Assert.True(history.TryUndo(Current, out var back));
        Assert.Equal(2, back.Value);
        now = 2;
        Assert.True(history.TryUndo(Current, out back));
        Assert.Equal(1, back.Value);
        now = 1;
        Assert.False(history.TryUndo(Current, out _));

        Assert.True(history.TryRedo(Current, out var forward));
        Assert.Equal(2, forward.Value);
        now = 2;
        Assert.True(history.TryRedo(Current, out forward));
        Assert.Equal(3, forward.Value);
        Assert.False(history.TryRedo(Current, out _));
        Assert.Equal(2, history.UndoCount);
    }

    [Fact]
    public void Undo_and_redo_keep_the_kind_of_step()
    {
        var history = new UndoHistory<State>();
        history.Record(new State("whole", 1), null, Start);
        now = 5;

        Assert.True(history.TryUndo(Current, out var back));
        Assert.Equal(new State("whole", 1), back);
        Assert.True(history.TryRedo(Current, out var forward));
        Assert.Equal(new State("whole", 5), forward);
    }

    [Fact]
    public void Keeps_the_newest_steps_up_to_its_capacity()
    {
        var history = new UndoHistory<State>();
        for (var i = 1; i <= 25; i++) history.Record(new State("boss", i), null, Start.AddSeconds(i));

        Assert.Equal(UndoHistory<State>.DefaultCapacity, history.UndoCount);
        var oldest = 0;
        while (history.TryUndo(Current, out var back)) oldest = back.Value;
        Assert.Equal(6, oldest);
    }

    [Fact]
    public void Quick_changes_to_the_same_control_are_one_step()
    {
        var history = new UndoHistory<State>();
        // A slider dragged from 1 to 4, a tick every 200 ms for longer than the merge window in total
        for (var i = 1; i <= 8; i++) history.Record(new State("boss", i), "BossSpeed", Start.AddMilliseconds(200 * i));

        Assert.Equal(1, history.UndoCount);
        Assert.True(history.TryUndo(Current, out var back));
        Assert.Equal(1, back.Value);
    }

    [Fact]
    public void Another_control_a_pause_or_an_undo_starts_a_new_step()
    {
        var history = new UndoHistory<State>();
        history.Record(new State("boss", 1), "BossSpeed", Start);
        history.Record(new State("boss", 2), "BossHealth", Start.AddMilliseconds(100));
        history.Record(new State("boss", 3), "BossHealth", Start.AddSeconds(3));
        history.Record(new State("boss", 4), null, Start.AddSeconds(3.1));
        history.Record(new State("boss", 5), null, Start.AddSeconds(3.2));
        Assert.Equal(5, history.UndoCount);

        history.TryUndo(Current, out _);
        history.Record(new State("boss", 6), "BossHealth", Start.AddSeconds(3.3));
        history.Record(new State("boss", 7), "BossHealth", Start.AddSeconds(3.4));
        Assert.Equal(5, history.UndoCount);
    }

    [Fact]
    public void A_new_change_clears_redo()
    {
        var history = new UndoHistory<State>();
        history.Record(new State("boss", 1), null, Start);
        history.TryUndo(Current, out _);
        Assert.Equal(1, history.RedoCount);

        history.Record(new State("boss", 2), "Tiers", Start.AddSeconds(5));
        Assert.Equal(0, history.RedoCount);
        Assert.False(history.TryRedo(Current, out _));
    }

    [Fact]
    public void Clear_forgets_everything()
    {
        var history = new UndoHistory<State>();
        history.Record(new State("boss", 1), null, Start);
        history.Record(new State("boss", 2), null, Start);
        history.TryUndo(Current, out _);
        history.Clear();

        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
    }
}
