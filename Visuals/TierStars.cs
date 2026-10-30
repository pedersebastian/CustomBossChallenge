using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.Races;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The boss panel shows the tier with 5 stars. For tiers above 5, the first (tier - 5) stars take the tier's colour
/// (tier 7: two coloured stars and three white ones) and the tier's number stands next to them.
/// <br/>
/// The panel is reused for the next game, so the stars and the number are put back for every other boss.
/// </summary>
public static class TierStars
{
    public const string LabelName = "CustomBossTierNumber";

    /// <summary>
    /// Updates the panel to the boss that is shown now
    /// </summary>
    public static void Show(BossUI ui)
    {
        var stars = ui.stars;
        if (stars == null || stars.Length == 0) return;

        var tier = CustomBossState.IsActive ? InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager?.CurrentBossTier ?? 0 : 0;
        var label = Label(ui, stars[stars.Length - 1], TierLook.HasLook(tier));
        if (!TierLook.HasLook(tier))
        {
            if (label == null) return;

            // Back to the game's look, which it doesn't always set again itself
            label.gameObject.SetActive(false);
            var shown = System.Math.Clamp(tier == 0 ? stars.Length : tier, 0, stars.Length);
            for (var i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null) stars[i].color = i < shown ? ui.starColorOn : ui.starColorOff;
            }

            return;
        }

        var color = TierLook.Color(CustomBossState.ActiveOptions!.Boss.ToString(), tier);
        var unity = new Color(color.R, color.G, color.B, 1);
        for (var i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null) stars[i].color = i < TierLook.Step(tier) ? unity : ui.starColorOn;
        }

        label!.gameObject.SetActive(true);
        label.text = tier.ToString();
        label.color = unity;
    }

    /// <summary>
    /// The number next to the stars, made from the panel's own health text the first time it is needed
    /// </summary>
    private static TMP_Text? Label(BossUI ui, Image lastStar, bool create)
    {
        var parent = lastStar.transform.parent;
        var existing = parent.Find(LabelName);
        if (existing != null) return existing.GetComponent<TMP_Text>();
        if (!create || ui.bossHealthTxt == null) return null;

        var copy = Object.Instantiate(ui.bossHealthTxt.gameObject, parent, false);
        copy.name = LabelName;
        var layout = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        var star = lastStar.rectTransform;
        var rect = copy.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = star.anchorMin;
        rect.pivot = new Vector2(0, 0.5f);
        rect.sizeDelta = new Vector2(star.rect.height * 2, star.rect.height * 1.4f);
        rect.anchoredPosition = star.anchoredPosition + new Vector2(star.rect.width * (1 - star.pivot.x) + star.rect.width * 0.15f, 0);

        var text = copy.GetComponent<TMP_Text>();
        text.enableAutoSizing = false;
        text.fontSize = star.rect.height * 1.1f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
}
