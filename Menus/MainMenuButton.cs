using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Persistence;
using CustomBossChallenge.Patches;
using HarmonyLib;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.Main;
using Il2CppAssets.Scripts.Unity.UI_New.Main.EventPanel;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The round "Boss Creator" button on the main menu, just below the column of event buttons on the right
/// </summary>
public static class MainMenuButton
{
    public const string Name = "CustomBossCreatorButton";
    public static string Label => Texts.BossCreator.T();
    public const float Size = 300;

    /// <summary>
    /// Space between the bottom of the event column and the top of the button
    /// </summary>
    public const float Gap = 20;

    /// <summary>
    /// Creates the button if it doesn't exist yet, and refreshes its icon
    /// </summary>
    public static GameObject? Create(MainMenu mainMenu)
    {
        var menu = mainMenu.transform;
        var existing = menu.Find(Name);
        var button = existing != null ? existing.gameObject : Build(mainMenu);
        if (button == null) return null;

        current = button.GetComponent<RectTransform>();
        column = mainMenu.GetComponentInChildren<MainMenuEventPanel>(true);
        current.anchorMin = current.anchorMax = new Vector2(1, 1);
        current.pivot = new Vector2(0.5f, 1);
        current.sizeDelta = new Vector2(Size, Size);
        UpdatePosition();

        button.GetComponentInChildren<Button>(true).image.SetSprite(Logo);
        return button;
    }

    private static RectTransform? current;
    private static MainMenuEventPanel? column;

    /// <summary>
    /// Reused by <see cref="UpdatePosition"/>, which runs every frame, so it doesn't make garbage each time
    /// </summary>
    private static Il2CppStructArray<Vector3>? corners;

    /// <summary>
    /// Keeps the button just below the lowest event button. The game fills the event column asynchronously and the
    /// number of events changes, so this runs every frame while the main menu is shown.
    /// </summary>
    public static void UpdatePosition()
    {
        if (current == null || column == null || !current.gameObject.activeInHierarchy) return;

        var corners = MainMenuButton.corners ??= new Il2CppStructArray<Vector3>(4);
        column.GetComponent<RectTransform>().GetWorldCorners(corners);
        var centerX = (corners[0].x + corners[2].x) / 2;
        var top = corners[1].y;

        var lowest = top;
        var icons = column.iconContainer;
        for (var i = 0; icons != null && i < icons.childCount; i++)
        {
            var icon = icons.GetChild(i);
            if (!icon.gameObject.activeInHierarchy || icon.GetComponent<RectTransform>() is not { } rect) continue;
            rect.GetWorldCorners(corners);
            lowest = Mathf.Min(lowest, corners[0].y);
        }

        var gap = lowest < top ? Gap * current.lossyScale.y : 0;
        current.position = new Vector3(centerX, lowest - gap, current.position.z);
    }

    /// <summary>
    /// The mod's own logo (Icon.png), so the button always looks the same whatever boss was chosen last
    /// </summary>
    public const string LogoName = "Icon";

    public static Il2CppNinjaKiwi.Common.ResourceUtils.SpriteReference Logo =>
        BTD_Mod_Helper.Api.ModContent.GetSpriteReference<CustomBossChallenge>(LogoName);

    private static GameObject? Build(MainMenu mainMenu)
    {
        // Copy a native round main menu button so the press animation and shadow match the rest of the menu
        var template = mainMenu.trophyStoreBtn != null ? mainMenu.trophyStoreBtn.transform.parent : null;
        if (template == null) return null;

        var button = Object.Instantiate(template.gameObject, mainMenu.transform);
        button.name = Name;
        button.SetActive(true);
        foreach (var child in button.GetComponentsInChildren<Transform>(true))
        {
            if (child != button.transform && child.name != "Button") child.gameObject.SetActive(false);
        }

        var unityButton = button.GetComponentInChildren<Button>(true);
        unityButton.gameObject.SetActive(true);

        // A fresh event also drops any listeners the template had, so the copy can't open the trophy store
        unityButton.onClick = new Button.ButtonClickedEvent();
        unityButton.onClick.AddListener(new System.Action(OnClick));

        var label = ModHelperText.Create(new Info("Label", 0, -Size / 2 - 10, Size + 120, 90)
        {
            Anchor = new Vector2(0.5f, 0.5f), Pivot = new Vector2(0.5f, 1)
        }, Label, 64, TextAlignmentOptions.Center);
        label.transform.SetParent(button.transform, false);
        label.Text.enableAutoSizing = true;
        label.Text.fontSizeMax = 64;

        return button;
    }

    private static void OnClick()
    {
        MenuManager.instance.buttonClickSound?.Play("ClickSounds");

        if (!PatchCheck.AllInPlace)
        {
            var missing = string.Join(", ", PatchCheck.Missing!);
            PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(Texts.BossCreatorOff.T(missing), null));
            return;
        }

        var load = CustomBossRetries.Store.Load();
        var game = load.Game;
        if (load.Problem == ContinueProblem.Unusable)
        {
            ShowUnusableGame(load.Lost);
            return;
        }
        if (game == null)
        {
            BossMode.Enter();
            return;
        }

        // New Game keeps the unfinished game until a new one is actually played
        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            Texts.UnfinishedGame.T(), UnfinishedGameText(game),
            new System.Action(() => BossGameLauncher.Continue(game)), Texts.ContinueGame.T(),
            new System.Action(BossMode.Enter), Texts.NewGame.T(), Popup.TransitionAnim.Scale));
    }

    /// <summary>
    /// Tells the player, once, that the unfinished game can't be continued: the file has already been moved aside.
    /// When its setup could still be read, it can be loaded to play the game again from the start.
    /// </summary>
    private static void ShowUnusableGame(LostGame? lost)
    {
        if (lost == null)
        {
            var file = System.IO.Path.GetFileName(CustomBossRetries.Store.BrokenPath);
            PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(Texts.UnfinishedGameBroken.T(file),
                new System.Action(BossMode.Enter)));
            return;
        }

        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            Texts.UnfinishedGame.T(), UnusableGameText(lost),
            new System.Action(() => BossMode.Enter(lost.Options, ConfigApplier.Unprepare(lost.Dcm))), Texts.LoadGameSetup.T(),
            new System.Action(BossMode.Enter), Texts.NewGame.T(), Popup.TransitionAnim.Scale));
    }

    /// <summary>
    /// The name of the unfinished game's challenge, the round it continues on and its tiers
    /// </summary>
    public static string UnfinishedGameText(ContinueGame game) =>
        Texts.UnfinishedGameQuestion.T(GameName(game.Dcm), game.Latest.Snapshot.Round, game.Options.Tiers);

    public static string UnusableGameText(LostGame lost) =>
        Texts.UnfinishedGameLost.T(GameName(lost.Dcm), lost.Round, lost.Options.Tiers);

    /// <summary>
    /// The challenge's name as the player gave it, in bold
    /// </summary>
    private static string GameName(DailyChallengeModel dcm)
    {
        var name = dcm.name ?? "";
        if (ConfigApplier.IsMarked(dcm)) name = name[..^ConfigApplier.Marker.Length];
        return $"<b>{BossEditorSection.PlainText(name)}</b>";
    }
}

[HarmonyPatch(typeof(MainMenu), nameof(MainMenu.Open))]
[OptionalPatch]
internal static class MainMenu_Open
{
    [HarmonyPostfix]
    internal static void Postfix(MainMenu __instance)
    {
        BossMode.Exit();
        MainMenuButton.Create(__instance);
    }
}

[HarmonyPatch(typeof(MainMenu), nameof(MainMenu.ReOpen))]
[OptionalPatch]
internal static class MainMenu_ReOpen
{
    [HarmonyPostfix]
    internal static void Postfix(MainMenu __instance)
    {
        BossMode.Exit();
        MainMenuButton.Create(__instance);
    }
}
