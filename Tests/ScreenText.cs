using System;
using CustomBossChallenge.Config;
using Il2CppTMPro;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Texts the way the game shows them, for tests that pass in any language (#291). Never compare
/// <see cref="TMP_Text.text"/> directly: the game keeps a right-to-left text like Arabic reversed, so <c>40</c> is
/// <c>04</c> in it, and the mod leaves the tags out of its popups and lists in Arabic
/// (<see cref="Menus.LanguageText.ForGameText"/>). Compare <see cref="Shown"/> with <see cref="Plain"/> of the mod's
/// own text (<c>Texts.X.T()</c>), never with English written into the test.
/// </summary>
public static class ScreenText
{
    /// <summary>
    /// What <paramref name="text"/> shows, without tags, in the order it was written. The game says itself whether it
    /// lays the text out right to left.
    /// </summary>
    public static string Shown(this TMP_Text text) => RichText.Visible(text.text, text.isRightToLeftText);

    /// <summary>
    /// A text as it shows, to compare with <see cref="Shown"/>: without its tags. Also for texts the game sets
    /// itself, like a dropdown's options.
    /// </summary>
    public static string Plain(string? text) => RichText.Strip(text);

    /// <summary>
    /// A mod text that the game or Mod Helper looks up itself, by its key (a modifier's title, Mod Settings and their
    /// info popups): it shows as the translation is written, without the shaping <c>Texts.X.T()</c> gives Arabic (#292)
    /// </summary>
    public static string ByGame(string key) => Plain(BTD_Mod_Helper.Extensions.StringExt.Localize(key));

    /// <summary>Whether <paramref name="text"/> shows <paramref name="part"/></summary>
    public static bool Shows(this TMP_Text text, string part) =>
        text.Shown().Contains(Plain(part), StringComparison.Ordinal);

    /// <summary>Whether <paramref name="text"/> shows exactly <paramref name="expected"/></summary>
    public static bool ShowsExactly(this TMP_Text text, string expected) => text.Shown() == Plain(expected);
}
