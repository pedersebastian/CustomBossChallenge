using System;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The victory and defeat screens name and show the boss of the game's boss data, which for Gildebloon is Bloonarius,
/// the boss it is made from (#25). When the boss in question is Gildebloon, its name and portrait take their place.
/// </summary>
public static class GildebloonScreens
{
    /// <summary>The victory screen shows the last tier's boss, the one just beaten</summary>
    public static void Victory(BossVictoryScreen screen)
    {
        if (CustomBossState.ActiveOptions is not { } options || !CustomBossState.IsActive) return;
        var (boss, elite) = options.BossOf(Math.Max(1, CustomBossState.HighestDefeatedTier));
        Show(screen.gameObject, screen.bossIcon, boss, elite);
        if (BossList.IsModBoss(boss) && screen.defeatedBossTxt != null) FillName(screen.defeatedBossTxt, BossList.Name(boss));
    }

    /// <summary>The defeat screen shows the boss that was alive, or else the next one</summary>
    public static void Defeat(BossDefeatScreen screen)
    {
        if (CustomBossState.ActiveOptions is not { } options || !CustomBossState.IsActive) return;
        // A boss Random picked that hasn't arrived stays a die, its name unsaid, even when it is Gildebloon (#283)
        if (BossHud.DefeatSecretTier() > 0) return;
        var (boss, elite) = options.BossOf(CustomBossState.HighestDefeatedTier + 1);
        Show(screen.gameObject, screen.bossIcon, boss, elite);
    }

    private static void Show(GameObject screen, Image? icon, Il2CppAssets.Scripts.Data.Boss.BossType boss, bool elite)
    {
        if (!BossList.IsModBoss(boss)) return;
        try
        {
            var host = BossList.Name(BossList.Host(boss));
            foreach (var text in screen.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text == null || text.text.IndexOf(host, StringComparison.OrdinalIgnoreCase) < 0) continue;
                text.text = ReplaceName(text.text, host, BossList.Name(boss));
            }

            if (icon != null) icon.SetSprite(Menus.BossEditorSection.Portrait(boss, elite));
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Couldn't show {BossList.Name(boss)} on the game's end screen: {e.Message}");
        }
    }

    /// <summary>
    /// In Arabic the game leaves the boss's place in its victory line empty, as {0} (#291): Gildebloon's name goes
    /// there, written forward. The game turns the line around for right-to-left once more after this, and laying it out
    /// right to left turns it back, so the name reads the right way.
    /// </summary>
    private static void FillName(TMP_Text text, string name)
    {
        if (string.IsNullOrEmpty(text.text)) return;
        var filled = FillPlaceholder(text.text, name);
        if (filled != text.text) text.text = filled;
    }

    /// <summary>The text with {0}, also the way a right-to-left text holds it reversed, replaced by <paramref name="name"/></summary>
    public static string FillPlaceholder(string text, string name) => Placeholder.Replace(text, name);

    private static readonly System.Text.RegularExpressions.Regex Placeholder = new(@"\{0\}|\}0\{|0\{\}|\}\{0");

    /// <summary>The text with the host's name replaced, in any case, by the boss's</summary>
    public static string ReplaceName(string text, string host, string name)
    {
        var at = text.IndexOf(host, StringComparison.OrdinalIgnoreCase);
        while (at >= 0)
        {
            var found = text.Substring(at, host.Length);
            var replacement = found == found.ToUpperInvariant() ? name.ToUpperInvariant() : name;
            text = text[..at] + replacement + text[(at + host.Length)..];
            at = text.IndexOf(host, at + replacement.Length, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }
}
