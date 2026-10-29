using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using HarmonyLib;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;
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
        if (BossMode.Active) BossMode.OnEditorOpened();

        BossEditorSection.Show(__instance, BossMode.Active);
    }
}

/// <summary>
/// The editor's Undo goes back to the model it was opened with; the boss options go back with it
/// </summary>
[HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.Undo))]
internal static class ChallengeEditor_Undo
{
    [HarmonyPostfix]
    internal static void Postfix(ChallengeEditor __instance)
    {
        if (!BossMode.Active || __instance.dcm == null) return;

        BossMode.Options = BossMode.OpenedOptions;
        BossEditorSection.Current?.Load(__instance.dcm, BossMode.Options);
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

/// <summary>
/// The editor's Random button also randomizes the boss options, and keeps the rest playable as a boss game
/// </summary>
[HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.Randomize))]
internal static class ChallengeEditor_Randomize
{
    [HarmonyPostfix]
    internal static void Postfix(ChallengeEditor __instance)
    {
        if (!BossMode.Active || __instance.dcm == null) return;

        __instance.ApplyUiSettingsToDCM();
        BossMode.Options = BossRandomizer.Randomize(__instance.dcm, BossEditorSection.UnsupportedModes);
        __instance.ApplyDcmSettingsToUI();
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
        __instance.dcm.maxParagons = ParagonMath.NoLimit;
        var settings = FortifiedModifier.Settings(__instance);
        if (settings != null) FortifiedModifier.Show(settings, __instance.dcm);

        // Like a new setup: the first boss, Bloonarius, with its default challenge name
        BossMode.Options = new BossOptions();
        __instance.dcm.name = BossMode.DefaultName(BossMode.Options.Boss);
        if (__instance.chalName != null) __instance.chalName.text = __instance.dcm.name;
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

        // Esc in a text field only cancels its edit, and Esc in one of the editor's own panels closes that panel, not
        // the whole Boss Creator
        if (BossMode.Active && (BossCreatorKeys.EscapeCancelsEdit() || ClosePanel(__instance)))
        {
            __result = true;
            return false;
        }

        if (BossMode.Active)
        {
            __instance.ApplyUiSettingsToDCM();
            BossMode.Exit();
        }

        BossEditorSection.Hide(__instance);
        __result = false;
        return false;
    }

    /// <summary>
    /// Closes the editor's open panel (tower restrictions, tower select, map select or modifiers), if any
    /// </summary>
    internal static bool ClosePanel(ChallengeEditor editor)
    {
        if (editor.towerRestrictionMenu != null && editor.towerRestrictionMenu.activeInHierarchy)
            editor.TowerRestrictionBackClicked();
        else if (editor.towerSelectObject != null && editor.towerSelectObject.activeInHierarchy)
            editor.CloseTowerSelect();
        else if (editor.mapSelectPanel != null && editor.mapSelectPanel.gameObject.activeInHierarchy)
            editor.CloseMapSelect();
        else if (editor.modifierSettings != null && editor.modifierSettings.gameObject.activeInHierarchy)
            editor.modifierSettings.CloseEditPopup();
        else
            return false;

        return true;
    }

    /// <summary>
    /// Whether one of the editor's panels that <see cref="ClosePanel"/> closes is open
    /// </summary>
    internal static bool OpenPanel(ChallengeEditor editor) =>
        editor.towerRestrictionMenu != null && editor.towerRestrictionMenu.activeInHierarchy ||
        editor.towerSelectObject != null && editor.towerSelectObject.activeInHierarchy ||
        editor.mapSelectPanel != null && editor.mapSelectPanel.gameObject.activeInHierarchy ||
        editor.modifierSettings != null && editor.modifierSettings.gameObject.activeInHierarchy;
}

/// <summary>
/// The editor's map picker shows which maps the selected boss has been defeated on
/// </summary>
[HarmonyPatch(typeof(MapSelectPanel), nameof(MapSelectPanel.Open))]
[OptionalPatch]
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

/// <summary>
/// The Boss Creator's All Fortified button among the editor's modifiers shows and sets the challenge's value; the game
/// only knows its own modifiers
/// </summary>
[HarmonyPatch(typeof(ChallengeEditorModifierSettings), nameof(ChallengeEditorModifierSettings.ApplyDCMtoUI))]
[OptionalPatch]
internal static class ChallengeEditorModifierSettings_ApplyDCMtoUI
{
    [HarmonyPostfix]
    internal static void Postfix(ChallengeEditorModifierSettings __instance, DailyChallengeModel dcm) =>
        FortifiedModifier.Show(__instance, dcm);
}

[HarmonyPatch(typeof(ChallengeEditorModifierSettings), nameof(ChallengeEditorModifierSettings.ApplyUIToDCM))]
[OptionalPatch]
internal static class ChallengeEditorModifierSettings_ApplyUIToDCM
{
    [HarmonyPostfix]
    internal static void Postfix(ChallengeEditorModifierSettings __instance, DailyChallengeModel dcm) =>
        FortifiedModifier.Save(__instance, dcm);
}
