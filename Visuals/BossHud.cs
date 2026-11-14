using System;
using System.Collections.Generic;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Patches;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Simulation.Track;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.Races;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// What a game with a different boss per tier shows besides the game's own boss panel: the panel's portraits follow
/// the tier's boss, a line-up of every tier's boss (defeated ones greyed and ticked, the one alive framed) that can be
/// folded away, and a banner that names each boss when it arrives
/// </summary>
public static class BossHud
{
    public const string LineupName = "CustomBossLineup";
    public const string BannerName = "CustomBossArrival";
    public const float CheckInterval = 0.25f;
    public const float BannerSeconds = 3.5f;
    private const float FadeSeconds = 0.4f;

    public static readonly Color Defeated = new(0.35f, 0.35f, 0.35f, 0.85f);
    public static readonly Color CurrentFrame = new(1f, 0.82f, 0.15f);

    /// <summary>
    /// Whether the line-up is unfolded; remembered between games in MelonLoader's preferences
    /// </summary>
    private static MelonPreferences_Entry<bool>? showLineup;

    public static bool LineupShown
    {
        get => (showLineup ??= Preferences()).Value;
        set => (showLineup ??= Preferences()).Value = value;
    }

    private static MelonPreferences_Entry<bool> Preferences() =>
        MelonPreferences.CreateCategory(nameof(CustomBossChallenge)).CreateEntry("ShowBossLineup", true);

    public sealed class Cell
    {
        public ModHelperPanel Panel = null!;
        public ModHelperImage Frame = null!;
        public ModHelperImage Portrait = null!;
        public ModHelperImage Tick = null!;
        public ModHelperText Tier = null!;
    }

    /// <summary>
    /// The line-up, or null outside a game with a different boss per tier
    /// </summary>
    public static ModHelperPanel? Lineup => lineup != null && lineup.gameObject != null ? lineup : null;

    public static List<Cell> Cells { get; } = new();
    public static ModHelperButton? Fold { get; private set; }
    public static ModHelperPanel? Banner => banner != null && banner.gameObject != null ? banner : null;
    public static ModHelperText? BannerText { get; private set; }

    private static ModHelperPanel? lineup;
    private static ModHelperPanel? banner;
    private static ModHelperImage? bannerPortrait;
    private static float bannerStart = -100;
    private static float nextCheck;
    private static int lineupTiers;

    static BossHud() => MixedBosses.Spawned += OnSpawned;

    /// <summary>
    /// The small boss portrait the game's boss panel uses, like "LychBtn"
    /// </summary>
    public static string HudIcon(BossType boss) => boss switch
    {
        BossType.Bloonarius => VanillaSprites.BloonariusBtn,
        BossType.Lych => VanillaSprites.LychBtn,
        BossType.Vortex => VanillaSprites.VortexBtn,
        BossType.Dreadbloon => VanillaSprites.DreadbloonBtn,
        BossType.Phayze => VanillaSprites.PhayzeBtn,
        BossType.Blastapopoulos => VanillaSprites.BlastapopoulosBtn,
        BossType.Diamondback => VanillaSprites.DiamondbackBtn,
        _ => Menus.BossEditorSection.MissingSprite(boss, "HUD icon")
    };

    /// <summary>
    /// A boss's name for the banner, e.g. "Lych Elite"
    /// </summary>
    public static string Name(BossType boss, bool elite) => elite ? $"{boss} {Texts.Elite.T()}" : boss.ToString();

    private static BossOptions? MixedOptions =>
        CustomBossState.IsActive && CustomBossState.ActiveOptions is {Mixed: true} options ? options : null;

    private static BossBloonManager? Manager => InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager;

    /// <summary>
    /// The boss panel shows the portrait of the tier's boss: the one alive, or between bosses the next one, or a die
    /// while that one is a secret
    /// </summary>
    public static void ShowPortraits(BossUI ui)
    {
        var options = MixedOptions;
        var manager = Manager;
        if (options == null || manager == null) return;

        var tier = MixedBosses.ShownTier(manager, options);
        var icon = MixedBosses.IsSecret(manager, options, tier) ? VanillaSprites.RandomIcon : HudIcon(options.BossOf(tier).Boss);
        if (ui.bossImg != null) ui.bossImg.SetSprite(icon);
        if (ui.arriveBossImg != null) ui.arriveBossImg.SetSprite(icon);
    }

    /// <summary>
    /// Builds, refreshes or removes the line-up and fades the banner; called every frame
    /// </summary>
    /// <param name="now">Look now, not only every <see cref="CheckInterval"/></param>
    public static void Update(bool now = false)
    {
        UpdateBanner();
        if (!now && Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckInterval;

        var options = MixedOptions;
        var manager = Manager;
        var ui = options != null && manager != null ? BossUi() : null;
        if (ui == null || options == null || manager == null)
        {
            if (Lineup != null) Object.Destroy(Lineup.gameObject);
            if (Banner != null) Object.Destroy(Banner.gameObject);
            lineup = null;
            banner = null;
            return;
        }

        var tiers = TierMath.ClampTiers(options.Tiers);
        if (Lineup == null || lineupTiers != tiers)
        {
            BuildLineup(Canvas(ui), tiers);
            shownState = null;
        }

        // Only what changed since the last look is drawn again (#183)
        var state = new LineupState(options, tiers, LineupShown, manager.CurrentBoss != null ? MixedBosses.ShownTier(manager, options) : 0,
            CustomBossState.HighestDefeatedTier, MixedBosses.ArrivedTier(manager));
        if (state == shownState) return;
        shownState = state;
        Refresh(options, manager);
    }

    /// <summary>
    /// What the line-up shows: the setup, whether it is unfolded, the tier alive, the tiers beaten and the highest
    /// that has arrived, which decides the secret ones
    /// </summary>
    private readonly record struct LineupState(BossOptions Options, int Tiers, bool Shown, int Alive, int Defeated, int Arrived);

    private static LineupState? shownState;

    /// <summary>
    /// How often the line-up was drawn again this session, for the tests (#183)
    /// </summary>
    internal static int Refreshes { get; private set; }

    /// <summary>
    /// The game's boss panel, looked for in the scene only when the one found before is gone with its match
    /// </summary>
    private static BossUI? bossUi;

    private static BossUI? BossUi()
    {
        if (bossUi == null) bossUi = Object.FindObjectOfType<BossUI>();
        return bossUi != null ? bossUi : null;
    }

    /// <summary>
    /// The canvas the game's boss panel is on, so the line-up scales with the rest of the game's interface
    /// </summary>
    private static RectTransform Canvas(BossUI ui) =>
        ui.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();

    /// <summary>
    /// The size of one tier's cell, so even 10 tiers stay well within the screen's height
    /// </summary>
    private static float CellSize(RectTransform canvas, int tiers) =>
        Math.Min(canvas.rect.height * 0.075f, canvas.rect.height * 0.62f / tiers);

    private static void BuildLineup(RectTransform canvas, int tiers)
    {
        if (Lineup != null) Object.Destroy(Lineup.gameObject);
        Cells.Clear();
        lineupTiers = tiers;

        var cell = CellSize(canvas, tiers);
        var header = cell * 0.55f;
        var width = cell * 1.5f;
        lineup = ModHelperPanel.Create(new Info(LineupName, cell * 0.25f, 0, width, header + cell * tiers + cell * 0.2f)
        {
            Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0, 0.5f)
        }, VanillaSprites.BlueInsertPanelRound);
        lineup.transform.SetParent(canvas, false);

        Fold = lineup.AddButton(new Info("Fold", 0, 0, width, header)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1)
        }, VanillaSprites.BlueInsertPanelRound, new Action(() =>
        {
            LineupShown = !LineupShown;
            nextCheck = 0;
        }));
        var label = Fold.AddText(new Info("Label", InfoPreset.FillParent), "", header * 0.5f);
        label.Text.enableAutoSizing = true;
        label.Text.fontSizeMin = header * 0.25f;
        label.Text.fontSizeMax = header * 0.5f;

        for (var tier = 1; tier <= tiers; tier++)
        {
            var y = -header - (tier - 0.5f) * cell;
            var panel = lineup.AddPanel(new Info($"Tier{tier}", 0, y, width, cell)
            {
                Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
            });
            var frame = panel.AddImage(new Info("Frame", cell * 0.18f, 0, cell * 0.98f, cell * 0.98f), VanillaSprites.SmallSquareWhiteGradient);
            frame.Image.color = CurrentFrame;
            var portrait = panel.AddImage(new Info("Portrait", cell * 0.18f, 0, cell * 0.86f, cell * 0.86f), VanillaSprites.BossChallenge);
            var tick = panel.AddImage(new Info("Tick", cell * 0.45f, -cell * 0.25f, cell * 0.45f, cell * 0.45f), VanillaSprites.TickGreenIcon);
            var text = panel.AddText(new Info("Tier", -width / 2 + cell * 0.24f, 0, cell * 0.5f, cell * 0.6f), $"{tier}", cell * 0.4f);
            foreach (var image in new[] {frame.Image, portrait.Image, tick.Image}) image.raycastTarget = false;
            text.Text.raycastTarget = false;
            Cells.Add(new Cell {Panel = panel, Frame = frame, Portrait = portrait, Tick = tick, Tier = text});
        }
    }

    private static void Refresh(BossOptions options, BossBloonManager manager)
    {
        Refreshes++;
        var shown = LineupShown;
        var rect = Lineup!.RectTransform;
        var cell = Cells.Count > 0 ? Cells[0].Panel.RectTransform.sizeDelta.y : 0;
        var header = Fold!.RectTransform.sizeDelta.y;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, shown ? header + cell * Cells.Count + cell * 0.2f : header);
        Fold.GetComponentInChildren<TMP_Text>().text = $"{Texts.BossLineup.T()} {(shown ? TierStrip.HigherMark : TierStrip.LowerMark)}";

        var alive = manager.CurrentBoss != null ? MixedBosses.ShownTier(manager, options) : 0;
        for (var i = 0; i < Cells.Count; i++)
        {
            var tier = i + 1;
            var cellParts = Cells[i];
            cellParts.Panel.gameObject.SetActive(shown);
            if (!shown) continue;

            var (boss, elite) = options.BossOf(tier);
            var defeated = tier <= CustomBossState.HighestDefeatedTier;
            var secret = MixedBosses.IsSecret(manager, options, tier);
            cellParts.Portrait.Image.SetSprite(secret ? VanillaSprites.RandomIcon : BossEditorSection.Portrait(boss, elite));
            BossEditorSection.ShowEliteGlow(cellParts.Portrait, secret && elite);
            cellParts.Portrait.Image.color = defeated ? Defeated : Color.white;
            cellParts.Tick.gameObject.SetActive(defeated);
            cellParts.Frame.gameObject.SetActive(tier == alive);
            cellParts.Tier.Text.color = tier == alive ? CurrentFrame : Color.white;
        }
    }

    private static void OnSpawned(int tier)
    {
        var options = MixedOptions;
        if (options == null || CustomBossState.Resuming) return;

        var ui = BossUi();
        if (ui == null) return;
        if (Banner == null) BuildBanner(Canvas(ui));

        var (boss, elite) = options.BossOf(tier);
        BannerText!.SetText(Texts.TierArrives.T(tier, Name(boss, elite)));
        bannerPortrait!.Image.SetSprite(BossEditorSection.Portrait(boss, elite));
        bannerStart = Time.unscaledTime;
        Banner!.gameObject.SetActive(true);
        UpdateBanner();
    }

    private static void BuildBanner(RectTransform canvas)
    {
        var height = canvas.rect.height * 0.1f;
        var width = height * 7;
        banner = ModHelperPanel.Create(new Info(BannerName, 0, -canvas.rect.height * 0.24f, width, height)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.MainBGPanelBlue);
        banner.transform.SetParent(canvas, false);
        var group = banner.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        bannerPortrait = banner.AddImage(new Info("Portrait", -width / 2 + height * 0.65f, 0, height * 0.9f, height * 0.9f),
            VanillaSprites.BossChallenge);
        BannerText = banner.AddText(new Info("Text", height * 0.6f, 0, width - height * 1.6f, height * 0.8f), "", height * 0.42f);
        BannerText.Text.enableAutoSizing = true;
        BannerText.Text.fontSizeMin = height * 0.2f;
        BannerText.Text.fontSizeMax = height * 0.42f;
        BannerText.Text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    /// <summary>
    /// Fades the banner in, holds it and fades it out again
    /// </summary>
    private static void UpdateBanner()
    {
        if (Banner == null || !Banner.gameObject.activeSelf) return;
        var age = Time.unscaledTime - bannerStart;
        if (age > BannerSeconds)
        {
            Banner.gameObject.SetActive(false);
            return;
        }

        var alpha = Math.Min(Math.Min(age / FadeSeconds, (BannerSeconds - age) / FadeSeconds), 1);
        Banner.GetComponent<CanvasGroup>().alpha = alpha;
    }
}
