using BTD_Mod_Helper;
using HarmonyLib;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.Player;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

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
        if (CustomBossState.ActiveOptions == null) return;

        if (__instance.bossData != null)
        {
            __instance.bossData.bossRankedMode = false;
            __instance.bossData.spawnRounds =
                new Il2CppStructArray<int>(CustomBossState.ActiveOptions.GameSpawnRounds);
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

/// <summary>
/// Custom games must not award boss tier progress, badges or event completion either
/// </summary>
[HarmonyPatch]
internal static class Btd6Player_BossProgress
{
    [HarmonyTargetMethods]
    internal static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods() => new[]
    {
        AccessTools.Method(typeof(Btd6Player), nameof(Btd6Player.DefeatedBossTier)),
        AccessTools.Method(typeof(Btd6Player), nameof(Btd6Player.CompleteBossEvent)),
        AccessTools.Method(typeof(Btd6Player), nameof(Btd6Player.AddBossBadges)),
        AccessTools.Method(typeof(Btd6Player), nameof(Btd6Player.BossEventBestRound))
    };

    [HarmonyPrefix]
    internal static bool Prefix() => !CustomBossState.IsActive;
}
