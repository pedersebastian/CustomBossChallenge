using CustomBossChallenge.Visuals;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;

namespace CustomBossChallenge.Patches;

/// <summary>
/// Ranked practice (#83): the victory screen of a custom game shows the ranked time, the time of every tier and the
/// tiers and cash used, when the Show Event Timer setting is on
/// </summary>
[HarmonyPatch(typeof(BossVictoryScreen), nameof(BossVictoryScreen.Open))]
[OptionalPatch]
internal static class BossVictoryScreen_Open
{
    [HarmonyPostfix]
    private static void Postfix(BossVictoryScreen __instance) => RankedHud.ShowSummary(__instance);
}
