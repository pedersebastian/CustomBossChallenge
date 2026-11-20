using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// Mod Helper makes an empty, nameless <see cref="TextAsset"/> for every component it creates (a panel, text, image,
/// button and so on) and never destroys it, so it stays after the component is gone: about 260 more each time the
/// Boss Creator opened (#235). The components work the same without them. Wrap code that builds Mod Helper components
/// in a <c>using</c> of this: at the end the empty text assets made meanwhile are destroyed. Text assets that were
/// there before, or have a name or text, are left alone.
/// </summary>
internal sealed class ModHelperLeftovers : IDisposable
{
    private readonly HashSet<IntPtr> before;

    /// <summary>How many were destroyed in this session, for the tests</summary>
    internal static int Destroyed { get; private set; }

    public ModHelperLeftovers()
    {
        before = Resources.FindObjectsOfTypeAll<TextAsset>().Select(text => text.Pointer).ToHashSet();
    }

    public void Dispose()
    {
        foreach (var text in Resources.FindObjectsOfTypeAll<TextAsset>())
        {
            if (before.Contains(text.Pointer) || !IsLeftover(text)) continue;
            Object.Destroy(text);
            Destroyed++;
        }
    }

    private static bool IsLeftover(TextAsset text) =>
        string.IsNullOrEmpty(text.name) && text.dataSize == 0;
}
