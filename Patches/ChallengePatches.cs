using BTD_Mod_Helper;
using HarmonyLib;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.Player;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;

namespace CustomBossChallenge.Patches;

/// <summary>
/// Backstop for every Boss Challenge setup: custom games are forced unranked and never saved
/// </summary>
[HarmonyPatch(typeof(InGameData), nameof(InGameData.SetupBossChallenge))]
internal static class InGameData_SetupBossChallenge
{
    [HarmonyPrefix]
    internal static void Prefix(InGameData __instance, ref bool isRanked, DailyChallengeModel dcm)
    {
        var coop = __instance.selectedCoopMode || __instance.selectedCouchMode;
        if (!CustomBossState.OnSetupBossChallenge(dcm, coop)) return;

        if (isRanked)
        {
            ModHelper.Msg<CustomBossChallenge>("Custom Boss Challenges are always unranked, disabling ranked mode");
            isRanked = false;
        }
    }

    [HarmonyPostfix]
    internal static void Postfix(InGameData __instance)
    {
        // Saves are already disabled through dcm.disableSaves, which InGameData.disableSaves reads from
        if (CustomBossState.ActiveConfig == null) return;

        if (__instance.bossData != null)
        {
            __instance.bossData.bossRankedMode = false;
        }
    }
}

/// <summary>
/// Custom games must never count towards the player's Boss Challenge records
/// </summary>
[HarmonyPatch(typeof(Btd6Player), nameof(Btd6Player.SetBossChallengeRecord))]
internal static class Btd6Player_SetBossChallengeRecord
{
    [HarmonyPrefix]
    internal static bool Prefix(BossType bossType, BossScoreComponents score)
    {
        if (!CustomBossState.IsActive) return true;

        ModHelper.Msg<CustomBossChallenge>($"Not saving a {bossType} record for a custom boss challenge");
        return false;
    }
}
