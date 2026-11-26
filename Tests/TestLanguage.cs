using System;
using System.Collections;
using Il2CppNinjaKiwi.Localization;
using UnityEngine;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The tests must pass in whatever language the game is set to. <c>CBC_LANGUAGE=Arabic</c> runs every test in that
/// language: the game switches to it before each test and back to the player's own language after, so a run that
/// stops halfway leaves the player's language as it was at most one test later.
/// </summary>
public static class TestLanguage
{
    /// <summary>
    /// The language the tests run in, from <c>CBC_LANGUAGE</c>, or null for the player's own. <c>Monkey</c> is the
    /// game's joke language, <see cref="Language.MonkeyCipher"/>.
    /// </summary>
    public static Language? Wanted => Parse(Environment.GetEnvironmentVariable("CBC_LANGUAGE"));

    /// <summary>A language by its name in <see cref="Language"/>, or <c>Monkey</c>; null for none</summary>
    public static Language? Parse(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        if (name.Equals("Monkey", StringComparison.OrdinalIgnoreCase) || name.Equals("Monklish", StringComparison.OrdinalIgnoreCase))
            return Language.MonkeyCipher;
        return Enum.TryParse<Language>(name, true, out var language) && Enum.IsDefined(language)
            ? language
            : throw new InvalidOperationException($"CBC_LANGUAGE={name} is not one of the game's languages");
    }

    /// <summary>The player's own language while a test runs in <see cref="Wanted"/></summary>
    private static Language? players;

    /// <summary>Whether the game is in <see cref="Wanted"/> for this test, so the player's language must come back</summary>
    public static bool Switched => players != null;

    /// <summary>Switches the game to <see cref="Wanted"/>, at the main menu, before a test</summary>
    public static IEnumerator Switch()
    {
        if (Wanted is not { } wanted) yield break;
        var manager = LocalizationManager.Instance;
        if (manager.ActiveLanguage == wanted) yield break;
        players ??= manager.ActiveLanguage;
        yield return To(wanted);
    }

    /// <summary>Puts the player's own language back after a test</summary>
    public static IEnumerator Restore()
    {
        if (players is not { } language) yield break;
        players = null;
        if (LocalizationManager.Instance.ActiveLanguage != language) yield return To(language);
    }

    private static IEnumerator To(Language language)
    {
        var task = LocalizationManager.Instance.UpdateLanguageAsync(language);
        var until = Time.realtimeSinceStartup + 30;
        while (!task.IsCompleted && Time.realtimeSinceStartup < until) yield return null;
        if (LocalizationManager.Instance.ActiveLanguage != language)
            throw new InvalidOperationException($"The game didn't switch to {language}");
        // Texts on screen follow a moment later
        yield return new WaitForSecondsRealtime(0.5f);
    }
}
