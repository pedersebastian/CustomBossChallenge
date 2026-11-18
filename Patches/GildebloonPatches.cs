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

/// <summary>
/// Gildebloon's Tax Collectors don't get the boss skin the game dresses bloons a boss sends in (#25): it is
/// Bloonarius's, the boss Gildebloon is made from, and the Tax Collectors have their own look. Only looked at in a
/// match with Gildebloon.
/// </summary>
[OptionalPatch]
[HarmonyPatch(typeof(Bloon), nameof(Bloon.AddMutator))]
internal static class Bloon_AddMutator_Gildebloon
{
    [HarmonyPrefix]
    internal static bool Prefix(Bloon __instance, Il2CppAssets.Scripts.Simulation.Objects.BehaviorMutator mutator)
    {
        if (!Config.Experimental.Gildebloon || CustomBossState.ActiveOptions is not {UsesGildebloon: true} || !CustomBossState.IsActive) return true;
        return !(mutator?.id == Visuals.GildebloonLook.BossSkinMutator && Config.Gildebloon.IsCollector(__instance.bloonModel?.id));
    }
}
