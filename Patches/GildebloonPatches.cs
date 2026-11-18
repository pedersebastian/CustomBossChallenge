using HarmonyLib;
using Il2CppAssets.Scripts.Simulation.Bloons;
using Il2CppAssets.Scripts.Simulation.Track;

namespace CustomBossChallenge.Patches;

/// <summary>
/// Runs the mod's own boss Gildebloon once per simulation frame (#25). Optional: without it Gildebloon still plays,
/// only without its cash stealing.
/// </summary>
[OptionalPatch]
[HarmonyPatch(typeof(BossBloonManager), nameof(BossBloonManager.Process))]
internal static class BossBloonManager_Process_Gildebloon
{
    [HarmonyPostfix]
    internal static void Postfix(BossBloonManager __instance) => Config.GildebloonBrain.Tick(__instance);
}

/// <summary>
/// A Gildebloon Tax Collector that leaks steals cash instead of lives (#25)
/// </summary>
[OptionalPatch]
[HarmonyPatch(typeof(Bloon), nameof(Bloon.Leaked))]
internal static class Bloon_Leaked
{
    [HarmonyPrefix]
    internal static void Prefix(Bloon __instance)
    {
        if (Config.Gildebloon.IsCollector(__instance.bloonModel?.id)) Config.GildebloonBrain.CollectorLeaked(__instance);
    }
}
