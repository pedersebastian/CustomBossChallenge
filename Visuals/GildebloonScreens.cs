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
    }

    /// <summary>The defeat screen shows the boss that was alive, or else the next one</summary>
    public static void Defeat(BossDefeatScreen screen)
    {
        if (CustomBossState.ActiveOptions is not { } options || !CustomBossState.IsActive) return;
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
