using System;
using System.Collections.Generic;

namespace CustomBossChallenge.Config;

/// <summary>
/// A short history of changes for Undo and Redo (issue #36). Each step is the state from before a change. Changes to
/// the same control in quick succession, like dragging a slider, are one step. Free of the game's types, so the unit
/// tests can use it.
/// </summary>
/// <typeparam name="T">A snapshot of what a change can touch</typeparam>
public sealed class UndoHistory<T>
{
    public const int DefaultCapacity = 20;

    /// <summary>
    /// Changes to the same control closer together than this are merged into one step
    /// </summary>
    public static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(1);

    private readonly LinkedList<T> undo = new();
    private readonly Stack<T> redo = new();
    private string? lastKey;
    private DateTime lastChange;

    public UndoHistory(int capacity = DefaultCapacity) => Capacity = Math.Max(capacity, 1);

    /// <summary>
    /// The most steps kept; the oldest is dropped when there are more
    /// </summary>
    public int Capacity { get; }

    public int UndoCount => undo.Count;
    public int RedoCount => redo.Count;

    /// <summary>
    /// Remembers <paramref name="before"/> as the state to go back to, unless the change continues the last one: the
    /// same <paramref name="mergeKey"/> within <see cref="MergeWindow"/> of it. A new change always clears Redo.
    /// </summary>
    /// <param name="mergeKey">The control that changed, or null for a change that is always a step of its own</param>
    public void Record(T before, string? mergeKey, DateTime now)
    {
        redo.Clear();
        var continues = mergeKey != null && mergeKey == lastKey && undo.Count > 0 && now - lastChange <= MergeWindow;
        lastKey = mergeKey;
        lastChange = now;
        if (continues) return;

        undo.AddLast(before);
        if (undo.Count > Capacity) undo.RemoveFirst();
    }

    /// <summary>
    /// Takes the last step back. <paramref name="current"/> captures the state as it is now, of the same kind as the
    /// step that is undone, so Redo can bring it back.
    /// </summary>
    public bool TryUndo(Func<T, T> current, out T restore)
    {
        restore = default!;
        if (undo.Last == null) return false;

        restore = undo.Last.Value;
        undo.RemoveLast();
        redo.Push(current(restore));
        lastKey = null;
        return true;
    }

    /// <summary>
    /// Brings back the last undone step. <paramref name="current"/> captures the state as it is now, so Undo can take
    /// it back again.
    /// </summary>
    public bool TryRedo(Func<T, T> current, out T restore)
    {
        restore = default!;
        if (redo.Count == 0) return false;

        restore = redo.Pop();
        undo.AddLast(current(restore));
        if (undo.Count > Capacity) undo.RemoveFirst();
        lastKey = null;
        return true;
    }

    public void Clear()
    {
        undo.Clear();
        redo.Clear();
        lastKey = null;
    }
}
