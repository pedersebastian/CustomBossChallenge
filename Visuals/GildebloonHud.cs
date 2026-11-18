using System;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
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

    /// <summary>The Vault panel while it is shown, for tests</summary>
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
        }

        if (!GildebloonBrain.Active)
        {
            if (Panel != null) Panel.gameObject.SetActive(false);
            return;
        }

        if (Panel == null)
        {
            var ui = Object.FindObjectOfType<BossUI>();
            if (ui == null) return;
            Build(ui.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>());
        }

        Panel!.gameObject.SetActive(true);
        amount!.SetText($"${GildebloonBrain.Stolen:N0}");

        var age = Time.unscaledTime - changeStart;
        change!.gameObject.SetActive(age < ChangeSeconds);
        if (age < ChangeSeconds) change.Text.alpha = Math.Min(1, (ChangeSeconds - age) / 0.6f);
    }

    private static void OnCashTaken(double cash)
    {
        if (change == null) return;
        change.SetText(cash >= 0 ? $"-${cash:N0}" : $"+${-cash:N0}");
        change.Text.color = cash >= 0 ? new Color(1f, 0.35f, 0.3f) : new Color(0.45f, 1f, 0.4f);
        changeStart = Time.unscaledTime;
    }

    private static void Build(RectTransform canvas)
    {
        var height = canvas.rect.height * 0.055f;
        var width = height * 4.2f;
        panel = ModHelperPanel.Create(new Info(Name, 0, -canvas.rect.height * 0.17f, width, height)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.MainBGPanelBlue);
        panel.transform.SetParent(canvas, false);
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
    }
}
