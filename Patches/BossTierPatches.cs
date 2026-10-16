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
