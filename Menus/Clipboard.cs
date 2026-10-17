using System;
using UnityEngine;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The system clipboard. Tests replace it, since the clipboard isn't reliable in a game run with -batchmode.
/// </summary>
public static class Clipboard
{
    public static Func<string> Read { get; set; } = SystemRead;
    public static Action<string> Write { get; set; } = SystemWrite;

    public static void Reset()
    {
        Read = SystemRead;
        Write = SystemWrite;
    }

    private static string SystemRead() => GUIUtility.systemCopyBuffer ?? "";

    private static void SystemWrite(string text) => GUIUtility.systemCopyBuffer = text;
}
