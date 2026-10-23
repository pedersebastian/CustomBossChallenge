using System;
using System.Collections.Generic;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// An All Fortified button among the Challenge Editor's own modifiers (All Camo, All Regrow, ...), only while the
/// editor is the Boss Creator. It is a copy of the All Camo button, and the game moves it between Excluded and
/// Included, lists it and shows its info like its own; only reading and writing the challenge is up to the mod.
/// </summary>
public static class FortifiedModifier
{
    public const string ButtonName = "AllFortifiedToggle";

    public static ChallengeEditorModifierSettings? Settings(ChallengeEditor editor) =>
        editor.GetComponentInChildren<ChallengeEditorModifierSettings>(true);

    /// <summary>
    /// The button, or null outside of the Boss Creator
    /// </summary>
    public static ModifierSettingsButton? Find(ChallengeEditorModifierSettings? settings)
    {
        if (settings == null) return null;
        var button = settings.excludedContainer.Find(ButtonName) ?? settings.includedContainer.Find(ButtonName);
        return button != null ? button.GetComponent<ModifierSettingsButton>() : null;
    }

    /// <summary>
    /// Adds the button if it isn't there yet, showing <paramref name="dcm"/>'s value
    /// </summary>
    public static void Add(ChallengeEditor editor, DailyChallengeModel? dcm)
    {
        var settings = Settings(editor);
        if (settings == null || settings.allCamoToggle == null) return;

        var button = Find(settings);
        if (button == null)
        {
            button = Object.Instantiate(settings.allCamoToggle.gameObject, settings.excludedContainer, false)
                .GetComponent<ModifierSettingsButton>();
            button.name = ButtonName;
            button.titleLocKey = Texts.AllFortified;
            button.descriptionLocKey = Texts.AllFortifiedInfo;
            button.parentPanel = settings;
            button.icon.SetSprite(VanillaSprites.FortifiedBloonsIcon);

            // The label was localized when the All Camo button was made; it must not localize back to All Camo
            foreach (var text in button.GetComponentsInChildren<Il2CppTMPro.TMP_Text>(true))
            {
                EditorRows.SetText(text, Texts.AllFortified.T());
            }

            // Like the game's own buttons, it starts excluded; first in the list, next to the camo and regrow ones
            button.transform.SetSiblingIndex(0);
            button.SetValue(false);
        }

        Enlarge(settings, true);
        if (dcm?.bloonModifiers != null) button.SetValue(dcm.bloonModifiers.allFortified);
        settings.FillMsbLists();
        settings.SetDisplayIcons();
    }

    /// <summary>
    /// Takes the button out again, so the normal Challenge Editor never has it
    /// </summary>
    public static void Remove(ChallengeEditor editor)
    {
        var settings = Settings(editor);
        var button = Find(settings);
        if (settings == null || button == null) return;

        // Out of its container right away, so the game's lists don't pick it up before it is destroyed
        button.transform.SetParent(null, false);
        Object.Destroy(button.gameObject);
        Enlarge(settings, false);
        settings.FillMsbLists();
        settings.SetDisplayIcons();
    }

    /// <summary>
    /// Shows the challenge's value on the button, if there is one
    /// </summary>
    public static void Show(ChallengeEditorModifierSettings settings, DailyChallengeModel dcm)
    {
        var button = Find(settings);
        if (button != null && dcm.bloonModifiers != null) button.SetValue(dcm.bloonModifiers.allFortified);
    }

    /// <summary>
    /// Writes the button's value into the challenge, if there is a button
    /// </summary>
    public static void Save(ChallengeEditorModifierSettings settings, DailyChallengeModel dcm)
    {
        var button = Find(settings);
        if (button == null) return;
        dcm.bloonModifiers ??= new BloonModifiers();
        dcm.bloonModifiers.allFortified = button.includeToggle.isOn;
    }

    /// <summary>
    /// The panels that are enlarged. Holding the panels themselves, not just their addresses, keeps a destroyed panel's
    /// address from being given to a new one that would then count as enlarged.
    /// </summary>
    private static readonly List<ChallengeEditorModifierSettings> Enlarged = new();

    private static bool IsEnlarged(ChallengeEditorModifierSettings settings)
    {
        // Panels destroyed with their editor (e.g. after Play) are forgotten
        Enlarged.RemoveAll(panel => panel == null);
        return Enlarged.Exists(panel => panel.Pointer == settings.Pointer);
    }

    /// <summary>
    /// How much taller the modifier panels are than the game makes them: one row while the button is there, so all
    /// nine modifiers fit without scrolling
    /// </summary>
    public static float ExtraHeight(ChallengeEditorModifierSettings settings) =>
        IsEnlarged(settings) ? RowHeight(settings) : 0;

    private static float RowHeight(ChallengeEditorModifierSettings settings)
    {
        var grid = settings.excludedContainer.GetComponent<GridLayoutGroup>();
        return grid != null ? grid.cellSize.y + grid.spacing.y : 245;
    }

    /// <summary>
    /// The game sized the panels and their scroll area for its own eight modifiers. Grows them by a row upwards (there
    /// is no room below, where the info box is) or shrinks them back exactly, so the normal editor is unchanged.
    /// </summary>
    private static void Enlarge(ChallengeEditorModifierSettings settings, bool enlarge)
    {
        if (enlarge == IsEnlarged(settings)) return;

        var scrollContent = settings.excludedContainer.parent;
        var scroll = scrollContent?.parent;
        var panel = scroll?.parent;
        if (scrollContent == null || scroll == null || panel == null) return;

        var delta = RowHeight(settings) * (enlarge ? 1 : -1);
        Grow(scroll, delta, delta);
        Grow(scrollContent, delta, 0);
        foreach (var name in new[] {"ExcludedBg", "IncludedBg"}) Grow(panel.Find(name), delta, delta / 2);
        foreach (var name in new[] {"ExcludedBanner", "IncludedBanner"}) Grow(panel.Find(name), 0, delta);

        if (enlarge) Enlarged.Add(settings);
        else Enlarged.RemoveAll(panel => panel.Pointer == settings.Pointer);
    }

    private static void Grow(Transform? transform, float height, float up)
    {
        var rect = transform?.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.sizeDelta += new Vector2(0, height);
        rect.anchoredPosition += new Vector2(0, up);
    }
}
