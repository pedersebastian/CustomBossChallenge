using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using Il2CppTMPro;
using UnityEngine;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// A button on the victory and defeat screens of a game with Random tiers that copies a code with the bosses Random
/// picked (#270). Playing them again needs nothing new there: the game's own Restart keeps them.
/// </summary>
public static class LineupButton
{
    public const string Name = "CopyTheseBosses";

    /// <summary>
    /// Puts the button on <paramref name="screen"/>, or hides it outside a custom game with Random tiers. Screens are
    /// reused, so the button is made once per screen.
    /// </summary>
    public static ModHelperButton? Show(Component screen)
    {
        var existing = screen.transform.Find(Name);
        var lineup = CustomBossState.IsActive ? LastGame.Lineup(CustomBossState.ActiveOptions) : null;
        if (lineup == null)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return null;
        }

        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing.GetComponent<ModHelperButton>();
        }

        // Mod Helper leaves an empty text asset behind for every component (#235)
        using var leftovers = new ModHelperLeftovers();
        var height = screen.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>().rect.height;
        var button = ModHelperButton.Create(new Info(Name, height * 0.04f, height * 0.04f, height * 0.5f, height * 0.09f)
        {
            Anchor = new Vector2(0, 0), Pivot = new Vector2(0, 0)
        }, VanillaSprites.BlueBtnLong, Listeners.Wrap(Copy));
        button.transform.SetParent(screen.transform, false);
        button.transform.SetAsLastSibling();
        var label = button.AddText(new Info("Label", InfoPreset.FillParent), Texts.CopyTheseBosses.T(), height * 0.035f);
        label.Text.enableAutoSizing = true;
        label.Text.fontSizeMax = height * 0.035f;
        label.Text.fontSizeMin = height * 0.02f;
        label.Text.textWrappingMode = TextWrappingModes.Normal;
        label.Text.margin = new Vector4(height * 0.02f, 0, height * 0.02f, 0);
        return button;
    }

    /// <summary>
    /// Copies the code of the game being played, with its bosses; its Random tiers still show as Random
    /// </summary>
    public static void Copy()
    {
        if (LastGame.Lineup(CustomBossState.ActiveOptions) is not var (options, dcm)) return;
        ModHelper.Msg<CustomBossChallenge>("Copying the bosses of this game");
        BossEditorSection.CopyCodeOf(options, dcm, () => BossEditorSection.CopyLinkOf(options, dcm));
    }
}
