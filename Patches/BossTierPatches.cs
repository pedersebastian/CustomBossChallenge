using HarmonyLib;
using Il2CppAssets.Scripts.Simulation.Track;
using Il2CppAssets.Scripts.Utils;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CustomBossChallenge.Patches;

/// <summary>
/// The boss manager tracks a completion time per tier in an array that always has 5 entries. With more than 5 tiers
/// it would index past the end every frame once tier 6 spawns, so the array is grown to the number of tiers.
/// <br/>
/// The number of tiers itself comes from the length of the spawn rounds, and the game already ends in victory when
/// the last of them is defeated.
/// </summary>
internal static class BossTierPatches
{
    internal static void EnsureBossTimes(BossBloonManager manager)
    {
        var rounds = manager.spawnRounds;
        var times = manager.bossTimes;
        if (rounds == null || times == null || times.Length >= rounds.Length) return;

        var grown = new Il2CppReferenceArray<KonFuze>(rounds.Length);
        for (var i = 0; i < rounds.Length; i++)
        {
            grown[i] = i < times.Length ? times[i] : new KonFuze(0);
        }

        manager.bossTimes = grown;
    }
}

[HarmonyPatch(typeof(BossBloonManager), nameof(BossBloonManager.Init))]
internal static class BossBloonManager_Init
{
    [HarmonyPostfix]
    internal static void Postfix(BossBloonManager __instance) => BossTierPatches.EnsureBossTimes(__instance);
}

[HarmonyPatch(typeof(BossBloonManager), nameof(BossBloonManager.MatchReady))]
internal static class BossBloonManager_MatchReady
{
    [HarmonyPostfix]
    internal static void Postfix(BossBloonManager __instance) => BossTierPatches.EnsureBossTimes(__instance);
}

[HarmonyPatch(typeof(BossBloonManager), nameof(BossBloonManager.SpawnBoss))]
internal static class BossBloonManager_SpawnBoss
{
    [HarmonyPrefix]
    internal static void Prefix(BossBloonManager __instance) => BossTierPatches.EnsureBossTimes(__instance);
}

/// <summary>
/// A Boss Challenge set up from code is won as soon as the first boss is defeated. Custom games are only won once
/// the last chosen tier is defeated, so earlier victories are held back.
/// </summary>
internal static class BossVictoryPatches
{
    internal static void TrackDefeat(BossBloonManager manager, Il2CppSystem.Object? obj)
    {
        if (!CustomBossState.IsActive) return;

        var bloon = obj?.TryCast<Il2CppAssets.Scripts.Simulation.Bloons.Bloon>();
        var model = bloon?.bloonModel;
        if (model == null || !model.isBoss || model.isBossSegment) return;

        CustomBossState.OnBossDefeated(Config.TierMath.TierOf(model.id));
        GuardVictory(manager);
    }

    internal static void GuardVictory(BossBloonManager manager)
    {
        if (manager.checkForVictory && CustomBossState.IsActive && !CustomBossState.AllTiersDefeated)
        {
            manager.checkForVictory = false;
        }
    }
}

[HarmonyPatch(typeof(BossBloonManager), nameof(BossBloonManager.BloonDestroyed))]
internal static class BossBloonManager_BloonDestroyed
{
    [HarmonyPostfix]
    internal static void Postfix(BossBloonManager __instance, Il2CppSystem.Object obj) =>
        BossVictoryPatches.TrackDefeat(__instance, obj);
}

[HarmonyPatch(typeof(BossBloonManager), nameof(BossBloonManager.Process))]
internal static class BossBloonManager_Process
{
    [HarmonyPrefix]
    internal static void Prefix(BossBloonManager __instance) => BossVictoryPatches.GuardVictory(__instance);
}

[HarmonyPatch(typeof(BossBloonManager), nameof(BossBloonManager.OnRoundEnd))]
internal static class BossBloonManager_OnRoundEnd
{
    [HarmonyPrefix]
    internal static void Prefix(BossBloonManager __instance) => BossVictoryPatches.GuardVictory(__instance);
}
