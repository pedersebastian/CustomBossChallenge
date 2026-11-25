using CustomBossChallenge.Visuals;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;

namespace CustomBossChallenge.Patches;

/// <summary>
/// The victory and defeat screens of a game with Random tiers offer a code with the bosses Random picked (#270)
/// </summary>
[HarmonyPatch(typeof(BossVictoryScreen), nameof(BossVictoryScreen.Open))]
[OptionalPatch]
internal static class BossVictoryScreen_Open_Lineup
{
    [HarmonyPostfix]
    private static void Postfix(BossVictoryScreen __instance) => LineupButton.Show(__instance);
}

[HarmonyPatch(typeof(BossDefeatScreen), nameof(BossDefeatScreen.Open))]
[OptionalPatch]
internal static class BossDefeatScreen_Open_Lineup
{
    [HarmonyPostfix]
    private static void Postfix(BossDefeatScreen __instance) => LineupButton.Show(__instance);
}
