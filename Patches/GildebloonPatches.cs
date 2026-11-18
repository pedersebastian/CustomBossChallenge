using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using BTD_Mod_Helper.Extensions;
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
/// A Gildebloon Tax Collector that leaks steals cash instead of lives (#25). The game still takes what the bloons inside
/// it would cost (a MOAB's four Ceramics, whatever the model's own leak damage says), so the lives are held up while it
/// leaks, so it can't end the game, and put back after.
/// </summary>
[OptionalPatch]
[HarmonyPatch(typeof(Bloon), nameof(Bloon.Leaked))]
internal static class Bloon_Leaked
{
    /// <summary>More than any leak can take</summary>
    private const double Cushion = 1e9;

    [HarmonyPrefix]
    internal static void Prefix(Bloon __instance, out double __state)
    {
        __state = -1;
        if (!Config.Gildebloon.IsCollector(__instance.bloonModel?.id) || InGame.instance == null) return;
        Config.GildebloonBrain.CollectorLeaked(__instance);
        __state = InGame.instance.GetHealth();
        InGame.instance.SetHealth(__state + Cushion);
    }

    [HarmonyPostfix]
    internal static void Postfix(double __state)
    {
        if (__state >= 0 && InGame.instance != null) InGame.instance.SetHealth(__state);
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

