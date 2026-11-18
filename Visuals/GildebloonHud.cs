using System;
using System.Linq;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.Races;
using Il2CppTMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Gildebloon's Vault under the game's boss panel (#25): a gold coin with what it has stolen, and the last theft or
/// payout fading out next to it. Numbers only, so nothing to translate.
/// </summary>
public static class GildebloonHud
{
    public const string Name = "GildebloonVault";

    private const float ChangeSeconds = 2.5f;

    private static ModHelperPanel? panel;
    private static ModHelperText? amount;
    private static ModHelperText? change;
    private static float changeStart = -100;
    private static bool subscribed;

    /// <summary>The Vault panel while it is shown, for tests (hidden while a tower is selected)</summary>
    public static ModHelperPanel? Panel => panel != null && panel.gameObject != null ? panel : null;

    /// <summary>The amount it shows, for tests</summary>
    public static string? Shown => amount != null ? amount.Text.text : null;

    /// <summary>Called every frame</summary>
    public static void Update()
    {
        if (!subscribed)
        {
            subscribed = true;
            GildebloonBrain.CashTaken += OnCashTaken;
            GildebloonBrain.Repossessed += OnRepossessed;
        }

        if (!GildebloonBrain.Active)
        {
            if (Panel != null) Panel.gameObject.SetActive(false);
            if (seizedLine != null) seizedLine.gameObject.SetActive(false);
            return;
        }

        if (Panel == null)
        {
            var ui = Object.FindObjectOfType<BossUI>();
            if (ui == null) return;
            Build(ui);
        }

        // The tower menu covers the boss panel's right end, where the Vault is: the Vault makes way while it is open
        var selected = Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu.TowerSelectionMenu.instance?.selectedTower != null;
        Panel!.gameObject.SetActive(!selected);
        amount!.SetText($"${GildebloonBrain.Stolen:N0}");

        var age = Time.unscaledTime - changeStart;
        change!.gameObject.SetActive(age < ChangeSeconds);
        if (age < ChangeSeconds) change.Text.alpha = Math.Min(1, (ChangeSeconds - age) / 0.6f);

        UpdateSeizedLine();

        var shown = Time.unscaledTime - bannerStart;
        banner!.gameObject.SetActive(shown < BannerSeconds);
        if (shown < BannerSeconds)
        {
            banner.Text.alpha = Math.Min(1, (BannerSeconds - shown) / 0.5f);
            banner.transform.localScale = Vector3.one * (1 + 0.15f * Math.Max(0, 1 - shown * 4));
        }
    }

    /// <summary>The Repossession at a skull, shown big in the middle of the screen for a moment</summary>
    private static void OnRepossessed(GildebloonBrain.Repossession repossession)
    {
        if (Panel == null || banner == null) return;
        banner.SetText(BannerText(repossession));
        bannerStart = Time.unscaledTime;
        Repossessions++;
    }

    /// <summary>
    /// "Repossession! Gildebloon seized $2,400 and froze 3 towers for 3 s", with the Banana Farms it locked on a line
    /// of their own
    /// </summary>
    public static string BannerText(GildebloonBrain.Repossession repossession)
    {
        var cash = $"{repossession.Cash:N0}";
        var text = repossession.Frozen switch
        {
            0 => Texts.GildebloonRepossessionCash.T(cash),
            1 => Texts.GildebloonRepossession.T(cash, Texts.GildebloonTowerOne.T(), GildebloonBrain.FreezeSeconds),
            _ => Texts.GildebloonRepossession.T(cash, Texts.GildebloonTowers.T(repossession.Frozen), GildebloonBrain.FreezeSeconds)
        };
        if (repossession.Farms > 0) text += "\n" + Texts.GildebloonFarmsLocked.T(repossession.Farms, GildebloonBrain.SeizeSeconds);
        return text;
    }

    /// <summary>The line under the Vault while a seized tower is selected, for tests</summary>
    public static string? SeizedLine => seizedLine != null && seizedLine.gameObject.activeSelf ? seizedLine.Text.text : null;

    private static ModHelperText? seizedLine;

    /// <summary>A seized tower selected: how long until it is let go</summary>
    private static void UpdateSeizedLine()
    {
        if (seizedLine == null) return;
        var selected = Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu.TowerSelectionMenu.instance?.selectedTower?.tower;
        var seized = selected == null ? null
            : GildebloonBrain.SeizedNow().Where(entry => entry.Tower != null && entry.Tower.Pointer == selected.Pointer)
                .Select(entry => (float?) entry.SecondsLeft).FirstOrDefault();
        seizedLine.gameObject.SetActive(seized != null);
        if (seized is { } left) seizedLine.SetText(Texts.GildebloonSeizedFor.T(Math.Ceiling(left)));
    }

    /// <summary>How many Repossessions the HUD showed, for tests</summary>
    public static int Repossessions { get; private set; }

    /// <summary>The Repossession banner while it shows, for tests</summary>
    public static string? Banner => banner != null && banner.gameObject.activeSelf ? banner.Text.text : null;

    private const float BannerSeconds = 2.5f;
    private static ModHelperText? banner;
    private static float bannerStart = -100;

    private static void OnCashTaken(double cash)
    {
        if (change == null) return;
        change.SetText(cash >= 0 ? $"-${cash:N0}" : $"+${-cash:N0}");
        change.Text.color = cash >= 0 ? new Color(1f, 0.35f, 0.3f) : new Color(0.45f, 1f, 0.4f);
        changeStart = Time.unscaledTime;
    }

    private static void Build(BossUI ui)
    {
        var canvas = ui.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
        var height = canvas.rect.height * 0.045f;
        var width = height * 4.2f;
        panel = ModHelperPanel.Create(new Info(Name, canvas.rect.height * 0.42f, -canvas.rect.height * 0.12f, width, height)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.MainBGPanelBlue);
        panel.transform.SetParent(canvas, false);

        // Right after the boss panel among the canvas's children, so the tower menus and popups drawn after it cover
        // the Vault like they cover the boss panel, instead of the Vault lying on top of them
        var above = ui.transform;
        while (above.parent != null && above.parent != canvas.transform) above = above.parent;
        if (above.parent == canvas.transform) panel.transform.SetSiblingIndex(above.GetSiblingIndex() + 1);
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        panel.AddImage(new Info("Coin", -width / 2 + height * 0.6f, 0, height * 0.85f, height * 0.85f), VanillaSprites.GoldenBloonIcon);
        amount = panel.AddText(new Info("Amount", height * 0.45f, 0, width - height * 1.3f, height * 0.8f), "", height * 0.5f);
        amount.Text.color = new Color(1f, 0.85f, 0.3f);
        amount.Text.textWrappingMode = TextWrappingModes.NoWrap;
        change = panel.AddText(new Info("Change", width * 0.5f + height * 1.2f, 0, height * 2.6f, height * 0.8f), "", height * 0.45f);
        change.Text.textWrappingMode = TextWrappingModes.NoWrap;
        change.gameObject.SetActive(false);

        // The Repossession banner, under the boss panel's middle, in gold
        banner = ModHelperText.Create(new Info("Repossession", -canvas.rect.height * 0.42f, -canvas.rect.height * 0.12f,
            canvas.rect.height * 1.4f, height * 2.8f), "", height * 0.75f);
        banner.transform.SetParent(panel.transform, false);
        banner.Text.color = new Color(1f, 0.82f, 0.25f);
        banner.Text.textWrappingMode = TextWrappingModes.NoWrap;
        banner.Text.outlineWidth = 0.25f;
        banner.Text.outlineColor = new Color32(70, 40, 0, 255);
        banner.gameObject.SetActive(false);

        // A seized tower's time left, in the middle under the boss panel, where the tower menu doesn't cover it
        seizedLine = ModHelperText.Create(new Info("GildebloonSeized", 0, -canvas.rect.height * 0.2f, canvas.rect.height * 0.8f, height)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
        }, "", height * 0.6f);
        seizedLine.transform.SetParent(canvas, false);
        seizedLine.transform.SetSiblingIndex(panel.transform.GetSiblingIndex() + 1);
        seizedLine.Text.color = new Color(1f, 0.82f, 0.25f);
        seizedLine.Text.textWrappingMode = TextWrappingModes.NoWrap;
        seizedLine.Text.outlineWidth = 0.2f;
        seizedLine.Text.outlineColor = new Color32(70, 40, 0, 255);
        seizedLine.gameObject.SetActive(false);
    }
}
