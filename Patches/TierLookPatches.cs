using HarmonyLib;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.Races;

namespace CustomBossChallenge.Patches;

/// <summary>
/// The boss panel shows tiers above 5 with coloured stars and the tier's number (see <see cref="Visuals.TierStars"/>)
/// </summary>
[HarmonyPatch(typeof(BossUI), nameof(BossUI.ShowBossAliveUI))]
[OptionalPatch]
internal static class BossUI_ShowBossAliveUI
{
    [HarmonyPostfix]
    internal static void Postfix(BossUI __instance) => Visuals.TierStars.Show(__instance);
}
