using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using HarmonyLib;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.DailyChallenge;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Patches;

/// <summary>
/// Shows the Boss section and Play button only while the Challenge Editor is used as the Boss Creator
/// </summary>
[HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.Open))]
internal static class ChallengeEditor_Open
{
    [HarmonyPostfix]
    internal static void Postfix(ChallengeEditor __instance)
    {
        // After a custom game the game returns to the editor it was started from: carry on in the Boss Creator
        if (!BossMode.Active && BossEditorSection.IsBossEditor(__instance)) BossMode.Resume();

        BossEditorSection.Show(__instance, BossMode.Active);
    }
}

[HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.ApplyDcmSettingsToUI))]
internal static class ChallengeEditor_ApplyDcmSettingsToUI
{
    [HarmonyPostfix]
    internal static void Postfix(ChallengeEditor __instance)
    {
        if (!BossMode.Active) return;
        BossEditorSection.Current?.Load(__instance.dcm, BossMode.Options);
    }
}

/// <summary>
/// The vanilla editor rebuilds parts of its model from the UI, which would drop the boss values
/// </summary>
[HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.ApplyUiSettingsToDCM))]
internal static class ChallengeEditor_ApplyUiSettingsToDCM
{
    [HarmonyPostfix]
    internal static void Postfix(ChallengeEditor __instance)
    {
        if (!BossMode.Active || __instance.dcm == null) return;
        BossEditorSection.Current?.Save(__instance.dcm);
    }
}

[HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.Clear))]
internal static class ChallengeEditor_Clear
{
    [HarmonyPostfix]
    internal static void Postfix(ChallengeEditor __instance)
    {
        if (!BossMode.Active) return;

        // The vanilla reset doesn't know about the boss values in the model
        var modifiers = __instance.dcm.bloonModifiers;
        modifiers.bossSpeedMultiplier = 1;
        modifiers.healthMultipliers.boss = 1;
        modifiers.allFortified = false;
        BossMode.Options = new BossOptions {Boss = BossMode.Options.Boss};
        BossEditorSection.Current?.Load(__instance.dcm, BossMode.Options);
    }
}

/// <summary>
/// Leaving the Boss Creator saves the setup and goes straight back, instead of on to the challenge preview
/// </summary>
[HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.OverrideClose))]
internal static class ChallengeEditor_OverrideClose
{
    [HarmonyPrefix]
    internal static bool Prefix(ChallengeEditor __instance, ref bool __result)
    {
        // After Play the editor stays on the menu stack in boss configuration; closing it then must not run the
        // vanilla draft handling on the boss setup either
        if (!BossMode.Active && !BossEditorSection.IsBossEditor(__instance)) return true;

        if (BossMode.Active)
        {
            __instance.ApplyUiSettingsToDCM();
            BossMode.Exit();
        }

        BossEditorSection.Hide(__instance);
        __result = false;
        return false;
    }
}

/// <summary>
/// The editor's map picker shows which maps the selected boss has been defeated on
/// </summary>
[HarmonyPatch(typeof(MapSelectPanel), nameof(MapSelectPanel.Open))]
internal static class MapSelectPanel_Open
{
    [HarmonyPrefix]
    internal static void Prefix(MapSelectPanel __instance, ref BossType bossType, ref bool showBossVisuals)
    {
        if (!BossMode.Active || __instance.GetComponentInParent<ChallengeEditor>() == null) return;

        bossType = BossMode.Options.Boss;
        showBossVisuals = true;
    }
}
