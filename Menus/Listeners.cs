using System;
using System.Collections.Generic;

namespace CustomBossChallenge.Menus;

/// <summary>
/// Il2CppInterop never frees a C# delegate that is handed to the game, like a button's click listener: it stays alive
/// after its button is destroyed, and so does everything it holds. A listener of the Boss Creator holds the whole
/// section, and through it every object of the editor, so each time the Boss Creator opened the game kept its old
/// one, about 1.7 MB (#235). The listeners of a window are made through this instead: the game keeps only a small box,
/// which <see cref="Release"/> empties when the window is gone.
/// </summary>
internal sealed class Listeners
{
    private readonly List<Box> boxes = new();

    /// <summary>The window being built, whose listeners <see cref="Wrap(Action)"/> makes</summary>
    private static Listeners? building;

    /// <summary>
    /// Makes the listeners of helpers, like a slider row's or a tier card's, through these until the scope ends
    /// </summary>
    public BuildScope Build()
    {
        var scope = new BuildScope(building);
        building = this;
        return scope;
    }

    public readonly struct BuildScope : IDisposable
    {
        private readonly Listeners? previous;
        internal BuildScope(Listeners? previous) => this.previous = previous;
        public void Dispose() => building = previous;
    }

    /// <summary>A listener of the window being built, or <paramref name="action"/> itself outside one</summary>
    public static Action Wrap(Action action) => building?.Of(action) ?? action;

    /// <inheritdoc cref="Wrap(Action)"/>
    public static Action<T> Wrap<T>(Action<T> action) => building?.Of(action) ?? action;

    /// <summary>Whether the window is gone; its listeners do nothing from then on</summary>
    public bool Released { get; private set; }

    /// <summary>How many listeners were let go in this session, for the tests</summary>
    internal static int ReleasedCount { get; private set; }

    public Action Of(Action action)
    {
        var box = Add(action);
        return () => (box.Target as Action)?.Invoke();
    }

    public Action<T> Of<T>(Action<T> action)
    {
        var box = Add(action);
        return value => (box.Target as Action<T>)?.Invoke(value);
    }

    public Func<T1, T2, T3, TResult> Of<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> func, TResult released)
    {
        var box = Add(func);
        return (a, b, c) => box.Target is Func<T1, T2, T3, TResult> target ? target(a, b, c) : released;
    }

    /// <summary>
    /// Lets go of what the listeners hold, once their window is gone. Listeners made after this do nothing.
    /// </summary>
    public void Release()
    {
        Released = true;
        foreach (var box in boxes) box.Target = null;
        ReleasedCount += boxes.Count;
        boxes.Clear();
    }

    private Box Add(Delegate target)
    {
        var box = new Box {Target = Released ? null : target};
        if (!Released) boxes.Add(box);
        return box;
    }

    private sealed class Box
    {
        public Delegate? Target;
    }
}
