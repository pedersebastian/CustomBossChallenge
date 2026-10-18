using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using UnityEngine;
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
}
