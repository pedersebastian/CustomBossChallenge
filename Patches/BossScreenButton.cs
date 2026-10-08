using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.UI.Menus;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.UI_New.DailyChallenge;
using Il2CppTMPro;
using UnityEngine;

namespace CustomBossChallenge.Patches;

/// <summary>
/// Adds the "Customize" button to the Boss Challenge part of the Boss Events screen. It opens this mod's settings,
/// and shows whether custom rules are currently enabled.
/// </summary>
public static class BossScreenButton
{
    public const string ButtonName = "CustomBossChallengeButton";
    public const int Width = 520;
    public const int Height = 170;

    /// <summary>
    /// Position relative to the Start button of the Boss Challenge panel
    /// </summary>
    public static readonly Vector2 Offset = new(0, 230);

    public static string Label => Settings.Enabled ? "Custom Rules: ON" : "Customize";

    public static string Sprite => Settings.Enabled ? VanillaSprites.GreenBtnLong : VanillaSprites.BlueBtnLong;

    /// <summary>
    /// Creates the button, or refreshes it if it already exists
    /// </summary>
    public static ModHelperButton? CreateOrUpdate(BossEventScreen screen) =>
        screen.startBossChallenge == null ? null : CreateOrUpdate(screen.startBossChallenge.GetComponent<RectTransform>());

    /// <summary>
    /// Creates the button next to the given Start button, or refreshes it if it already exists
    /// </summary>
    public static ModHelperButton CreateOrUpdate(RectTransform start)
    {
        var parent = start.parent;
        var existing = parent.Find(ButtonName);
        var button = existing != null ? existing.GetComponent<ModHelperButton>() : null;

        if (button == null)
        {
            button = parent.gameObject.AddButton(new Info(ButtonName, Width, Height), Sprite, new System.Action(Open));
            button.AddText(new Info("Text", InfoPreset.FillParent), Label, 54, TextAlignmentOptions.Center);
            var rect = button.RectTransform;
            rect.anchorMin = start.anchorMin;
            rect.anchorMax = start.anchorMax;
            rect.anchoredPosition = start.anchoredPosition + Offset;
        }

        button.Image.SetSprite(Sprite);
        var text = button.GetComponentInChildren<ModHelperText>();
        if (text != null) text.Text.text = Label;
        return button;
    }

    private static void Open() => ModSettingsMenu.Open(ModContent.GetInstance<CustomBossChallenge>());
}

[HarmonyPatch(typeof(BossEventScreen), nameof(BossEventScreen.Open))]
internal static class BossEventScreen_Open
{
    [HarmonyPostfix]
    internal static void Postfix(BossEventScreen __instance) => BossScreenButton.CreateOrUpdate(__instance);
}

[HarmonyPatch(typeof(BossEventScreen), nameof(BossEventScreen.ReOpen))]
internal static class BossEventScreen_ReOpen
{
    [HarmonyPostfix]
    internal static void Postfix(BossEventScreen __instance) => BossScreenButton.CreateOrUpdate(__instance);
}
