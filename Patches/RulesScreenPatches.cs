using CustomBossChallenge.Menus;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;

namespace CustomBossChallenge.Patches;

/// <summary>
/// The Challenge Rules panel of a custom game, opened with the pause screen's Rules button, also lists the boss
/// options (#140)
/// </summary>
[HarmonyPatch(typeof(ChallengeRulesScreen), nameof(ChallengeRulesScreen.SetupUIs))]
[OptionalPatch]
internal static class ChallengeRulesScreen_SetupUIs
{
    [HarmonyPostfix]
    private static void Postfix(ChallengeRulesScreen __instance) => RulesPanel.Show(__instance);
}
