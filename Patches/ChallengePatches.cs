using System;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using HarmonyLib;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.Player;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Utils;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.UI;

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
/// The defeat screen says every tier has 20 rounds; custom games tell the rounds their tiers really had
/// </summary>
[HarmonyPatch(typeof(BossDefeatScreen), nameof(BossDefeatScreen.Open))]
internal static class BossDefeatScreen_Open
{
    [HarmonyPostfix]
    internal static void Postfix(BossDefeatScreen __instance)
    {
        var options = CustomBossState.ActiveOptions;
        if (!CustomBossState.IsActive || options == null || InGame.instance?.bridge == null) return;

        var rounds = options.EffectiveSpawnRounds;
        var lostRound = LostRound;
        var (tier, between) = TierMath.TimeLimit(rounds, lostRound);
        __instance.ranOutOfTimeSubTxt.text = TierMath.EvenlySpaced(rounds)
            ? Texts.OutOfTimeEachTier.T(between)
            : Texts.OutOfTimeTier.T(tier, between);

        ShowRetries(__instance, rounds, lostRound);
    }

    /// <summary>
    /// The game's retry buttons need its map saves, which custom games don't write; they use the mod's own
    /// snapshots instead and cost nothing
    /// </summary>
    private static void ShowRetries(BossDefeatScreen screen, int[] spawnRounds, int lostRound)
    {
        var canRetry = CustomBossRetries.HasLastRound(lostRound);
        screen.canRetry = canRetry;
        screen.retryMapSave = CustomBossRetries.SaveFor(lostRound);
        screen.retryButton.gameObject.SetActive(canRetry);
        screen.retryButton.interactable = canRetry;
        screen.retryCostTxt.text = Texts.Free.T();

        var checkpoint = CustomBossRetries.CheckpointRound(spawnRounds, lostRound);
        screen.canContinue = checkpoint != null;
        screen.continueMapSave = checkpoint == null ? null : CustomBossRetries.SaveFor(checkpoint.Value);
        screen.continueButton.gameObject.SetActive(checkpoint != null);
        screen.continueButton.interactable = checkpoint != null;
        screen.continueCostTxt.text = Texts.Free.T();
        if (checkpoint != null) screen.continueDescTxt.text = Texts.RetryFromRound.T(checkpoint.Value);

        OnClick(screen.retryButton, () => CustomBossRetries.RetryFrom(lostRound, true));
        OnClick(screen.continueButton, () =>
        {
            if (checkpoint != null) CustomBossRetries.RetryFrom(checkpoint.Value, false);
        });
    }

    /// <summary>
    /// The game's own click handlers skip retrying without its map saves. The screen may be reused for a normal Boss
    /// Challenge later, so outside custom games the click still goes to the game's handlers.
    /// </summary>
    private static void OnClick(Button button, Action retry)
    {
        var original = button.onClick;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(new Action(() =>
        {
            if (CustomBossState.IsActive) retry();
            else original.Invoke();
        }));
    }

    private static int LostRound => InGame.instance.bridge.GetCurrentRound() + 1;
}

/// <summary>
/// Custom games retry for free; backstops for anything else that asks what a retry costs
/// </summary>
[HarmonyPatch(typeof(BossDefeatScreen), nameof(BossDefeatScreen.GetRetryMmCost), new System.Type[0])]
internal static class BossDefeatScreen_GetRetryMmCost
{
    [HarmonyPostfix]
    internal static void Postfix(ref KonFuze __result)
    {
        if (CustomBossState.IsActive) __result = new KonFuze(0);
    }
}

[HarmonyPatch(typeof(CheckpointDefeatScreen), nameof(CheckpointDefeatScreen.GetContinueMmCost))]
internal static class CheckpointDefeatScreen_GetContinueMmCost
{
    [HarmonyPostfix]
    internal static void Postfix(ref KonFuze __result)
    {
        if (CustomBossState.IsActive) __result = new KonFuze(0);
    }
}

/// <summary>
/// Every completed round of a custom game is snapshotted for the defeat screen's retries
/// </summary>
[HarmonyPatch(typeof(InGame), nameof(InGame.RoundEnd))]
internal static class InGame_RoundEnd
{
    [HarmonyPostfix]
    internal static void Postfix(InGame __instance, int highestCompletedRound)
    {
        if (CustomBossState.IsActive) CustomBossRetries.OnRoundEnd(__instance, highestCompletedRound);
    }
}

[HarmonyPatch(typeof(InGame), nameof(InGame.RoundStart))]
internal static class InGame_RoundStart
{
    [HarmonyPostfix]
    internal static void Postfix()
    {
        if (CustomBossState.IsActive) CustomBossState.OnRoundStart();
    }
}

[HarmonyPatch(typeof(InGame), nameof(InGame.RunRestart))]
internal static class InGame_RunRestart
{
    [HarmonyPrefix]
    internal static void Prefix()
    {
        if (CustomBossState.IsActive) CustomBossState.OnRestart();
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
    internal static readonly string[] MethodNames =
    {
        nameof(Btd6Player.DefeatedBossTier),
        nameof(Btd6Player.CompleteBossEvent),
        nameof(Btd6Player.AddBossBadges),
        nameof(Btd6Player.BossEventBestRound)
    };

    /// <summary>
    /// A method a game update renamed or removed is skipped, so the others are still guarded; <see cref="PatchCheck"/>
    /// then keeps the Boss Creator closed
    /// </summary>
    [HarmonyTargetMethods]
    internal static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        MethodNames.Select(name => PatchCheck.Target(typeof(Btd6Player), name)).Where(method => method != null)!;

    [HarmonyPrefix]
    internal static bool Prefix() => !CustomBossState.IsActive;
}
