using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Simulation.Track;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Ranked practice (#83): with <see cref="Settings.ShowRankedStats"/> on, a custom game shows the time as the ranked boss
/// events score it (the bosses' time plus the penalty for slow rounds without a boss), with the upgrade tiers and cash
/// used that Least Tiers and Least Cash events count, and the victory screen shows the time of every tier. The game counts all of it itself, also across free retries and Continue (it
/// is in its map saves); the mod only shows it. The game's own ranked timer can't be used: outside of ranked it sits
/// off screen between bosses and behind the top bar while a boss is alive.
/// </summary>
public static class RankedHud
{
    public const string PanelName = "CustomBossRankedTimer";
    public const string SummaryName = "CustomBossRankedSummary";

    /// <summary>
    /// How often the panel is looked at, in seconds; its texts change every frame
    /// </summary>
    private const float CheckInterval = 0.25f;

    private static float nextCheck;

    private static ModHelperPanel? panel;
    private static readonly List<ModHelperText> Values = new();

    /// <summary>
    /// The times the panel shows now, in hundredths of a second as they are written, so a time is only written again
    /// when it shows something else (#183)
    /// </summary>
    private static long shownBossTime = -1;
    private static long shownPenalty = -1;

    /// <summary>
    /// The game's lives and cash, found once per match: looking for them by their path searches the whole scene
    /// </summary>
    private static RectTransform? leftHud;

    /// <summary>
    /// Looks in a row that didn't find the lives and cash, to say once when a game update moved them
    /// </summary>
    private static int missedLookups;

    private static bool toldNoHud;

    /// <summary>
    /// How often the lives and cash were looked for in the scene this session, for the tests (#183)
    /// </summary>
    internal static int HudLookups { get; private set; }

    /// <summary>
    /// Reused for every look at a frame's corners, so following the HUD and the card makes no garbage
    /// </summary>
    private static Il2CppStructArray<Vector3>? corners;

    private static Il2CppStructArray<Vector3> Corners => corners ??= new Il2CppStructArray<Vector3>(4);

    /// <summary>
    /// The panel in the game, or null; Unity's own null check, as it is destroyed with the match
    /// </summary>
    public static ModHelperPanel? Panel => panel != null ? panel : null;

    /// <summary>
    /// Tests only: the setting as if the player had chosen it, null for the player's setting
    /// </summary>
    internal static bool? ShowRankedStatsOverride { get; set; }

    public static bool Enabled => ShowRankedStatsOverride ?? Settings.ShowRankedStats;

    public static bool Visible => Enabled && CustomBossState.IsActive;

    private static BossBloonManager? Manager => InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager;

    /// <summary>
    /// The time every boss was alive so far, added up
    /// </summary>
    public static TimeSpan? BossTime => BossTimeOf(Manager);

    private static TimeSpan? BossTimeOf(BossBloonManager? manager) => manager != null
        ? TimeSpanOf(manager.GetBossTotalCompletionTime()) - TimeSpanOf(manager.GetPenaltyTime())
        : null;

    /// <summary>
    /// The ranked time so far: the bosses' time plus the penalty, as the events score it
    /// </summary>
    public static TimeSpan? TotalTime => Manager is { } manager ? TimeSpanOf(manager.GetBossTotalCompletionTime()) : null;

    /// <summary>
    /// The time each tier's boss was alive, null for tiers not defeated yet
    /// </summary>
    public static IReadOnlyList<TimeSpan?> TierTimes()
    {
        var manager = Manager;
        var options = CustomBossState.ActiveOptions;
        if (manager?.bossTimes == null || options == null) return Array.Empty<TimeSpan?>();
        var frames = Enumerable.Range(0, manager.bossTimes.Length).Select(i => (double) (manager.bossTimes[i]?.Value ?? 0)).ToList();
        return RankedTime.TierTimes(frames, TierMath.ClampTiers(options.Tiers), CustomBossState.HighestDefeatedTier);
    }

    /// <summary>
    /// The penalty for time spent on a round without a boss after its bloons are all out, past the events' grace
    /// time of 10 s + 0.25 s per round. The game counts it in custom games too and keeps it in its map saves.
    /// </summary>
    public static TimeSpan PenaltyTime => PenaltyOf(Manager);

    private static TimeSpan PenaltyOf(BossBloonManager? manager) =>
        manager != null ? TimeSpanOf(manager.GetPenaltyTime()) : TimeSpan.Zero;

    /// <summary>
    /// Upgrade tiers used, as the Least Tiers events count them
    /// </summary>
    public static int TiersUsed => InGame.instance?.bridge?.Simulation?.GetTiersUsed() ?? 0;

    /// <summary>
    /// Cash used, as the Least Cash events count it
    /// </summary>
    public static int CashUsed =>
        InGame.instance?.bridge?.Simulation?.cashSpent is { } spent ? (int) Math.Round(spent.Value) : 0;

    private static TimeSpan TimeSpanOf(Il2CppSystem.TimeSpan time) => TimeSpan.FromTicks(time.Ticks);

    /// <summary>
    /// The panel's lines while playing: the bosses' time and the penalty, as the events show them, then the upgrade
    /// tiers and cash used
    /// </summary>
    public static List<(string Label, string Value)> PanelLines() => new()
    {
        (Texts.RankedTime.T(), RankedTime.Format(BossTime ?? TimeSpan.Zero)),
        (Texts.PenaltyTime.T(), "+" + RankedTime.Format(PenaltyTime)),
        (Texts.TiersUsed.T(), TiersUsed.ToString("N0")),
        (Texts.CashUsed.T(), "$" + CashUsed.ToString("N0"))
    };

    /// <summary>
    /// Builds, refreshes or removes the panel; called every frame. The times count in hundredths, so they are looked at
    /// every frame and written when they change; the upgrade tiers and cash only change with what the player does, and
    /// are looked at every <see cref="CheckInterval"/> (#183).
    /// </summary>
    /// <param name="now">Look now, not only every <see cref="CheckInterval"/></param>
    public static void Update(bool now = false)
    {
        if (now || Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + CheckInterval;
            var hud = Visible ? LeftHud() : null;
            if (hud == null)
            {
                if (Panel != null) Object.Destroy(Panel.gameObject);
                panel = null;
                return;
            }

            if (Panel == null) Build(hud);
            Place(Panel!, hud);
            ShowUsed();
        }

        FollowCard();
        if (Panel == null) return;
        ShowTimes(Manager);
    }

    /// <summary>
    /// The bosses' time and the penalty, each written only when it shows another hundredth
    /// </summary>
    private static void ShowTimes(BossBloonManager? manager)
    {
        if (Values.Count < 2) return;
        var bossTime = BossTimeOf(manager) ?? TimeSpan.Zero;
        var boss = Hundredths(bossTime);
        if (boss != shownBossTime)
        {
            shownBossTime = boss;
            Values[0].Text.text = RankedTime.Format(bossTime);
        }

        var penalty = PenaltyOf(manager);
        var shown = Hundredths(penalty);
        if (shown != shownPenalty)
        {
            shownPenalty = shown;
            Values[1].Text.text = "+" + RankedTime.Format(penalty);
        }
    }

    private static long Hundredths(TimeSpan time) => time.Ticks / (TimeSpan.TicksPerMillisecond * 10);

    /// <summary>
    /// The upgrade tiers and cash used
    /// </summary>
    private static void ShowUsed()
    {
        if (Values.Count < 4) return;
        Values[2].Text.text = TiersUsed.ToString("N0");
        Values[3].Text.text = "$" + CashUsed.ToString("N0");
    }

    /// <summary>
    /// The game's lives and cash at the top left, which the panel sits under
    /// </summary>
    private const string LeftHudPath = "InGame/UIRect/MainHudLeftAlign(Clone)/LeftGroup";

    /// <summary>
    /// How many looks in a row may miss the lives and cash before the log says so: a match that is still loading
    /// doesn't have them yet
    /// </summary>
    private const int MissedLookupsToTell = 40;

    private static RectTransform? LeftHud()
    {
        if (leftHud != null) return leftHud;

        HudLookups++;
        var hud = GameObject.Find(LeftHudPath);
        leftHud = hud != null ? hud.GetComponent<RectTransform>() : null;
        if (leftHud != null)
        {
            missedLookups = 0;
            return leftHud;
        }

        // A game update that moved the lives and cash would only hide the panel; say so once
        if (++missedLookups >= MissedLookupsToTell && !toldNoHud)
        {
            toldNoHud = true;
            ModHelper.Warning<CustomBossChallenge>($"{LeftHudPath} not found, the ranked stats panel can't be shown");
        }

        return null;
    }

    private static RectTransform Canvas(Component near) =>
        near.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();

    private static void Build(RectTransform hud)
    {
        var canvas = Canvas(hud);
        var lineHeight = canvas.rect.height * 0.034f;
        var width = lineHeight * 9f;
        var lines = PanelLines();
        panel = ModHelperPanel.Create(new Info(PanelName, 0, 0, width, lineHeight * (lines.Count + 0.5f))
        {
            Anchor = new Vector2(0.5f, 0.5f), Pivot = new Vector2(0, 1)
        }, VanillaSprites.BlueInsertPanelRound);
        panel.transform.SetParent(canvas, false);
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Values.Clear();
        Values.AddRange(AddLines(panel, lines, width, lineHeight));
        shownBossTime = shownPenalty = -1;
    }

    /// <summary>
    /// Puts the panel just under the lives and cash, wherever the game's layout has them: under the lowest of their
    /// visible parts, as their texts reach below the group's own frame
    /// </summary>
    private static void Place(ModHelperPanel ours, RectTransform hud)
    {
        var corners = Corners;
        hud.GetWorldCorners(corners);
        var left = corners[0].x;
        var bottom = corners[0].y;
        foreach (var graphic in hud.GetComponentsInChildren<UnityEngine.UI.Graphic>())
        {
            if (!graphic.isActiveAndEnabled) continue;
            graphic.rectTransform.GetWorldCorners(corners);
            bottom = Math.Min(bottom, corners[0].y);
        }

        var canvas = Canvas(hud);
        var gap = canvas.rect.height * 0.01f * canvas.lossyScale.y;
        ours.transform.position = new Vector3(left, bottom - gap, ours.transform.position.z);
    }

    /// <summary>
    /// Adds a row per line, the label on the left and the value on the right
    /// </summary>
    /// <returns>The value texts</returns>
    private static List<ModHelperText> AddLines(ModHelperPanel parent, List<(string Label, string Value)> lines,
        float width, float lineHeight)
    {
        var values = new List<ModHelperText>();
        for (var i = 0; i < lines.Count; i++)
        {
            var y = -lineHeight * (i + 0.75f);
            var (label, value) = lines[i];
            var name = parent.AddText(new Info($"Label{i}", width * 0.06f, y, width * 0.56f, lineHeight)
            {
                Anchor = new Vector2(0, 1), Pivot = new Vector2(0, 0.5f)
            }, "", lineHeight * 0.62f, TextAlignmentOptions.MidlineLeft);
            var text = parent.AddText(new Info($"Value{i}", -width * 0.06f, y, width * 0.4f, lineHeight)
            {
                Anchor = new Vector2(1, 1), Pivot = new Vector2(1, 0.5f)
            }, "", lineHeight * 0.62f, TextAlignmentOptions.MidlineRight);
            // Without the game's localizer, which would turn "Time" into its own "Time:"
            EditorRows.SetText(name.Text, label);
            EditorRows.SetText(text.Text, value);
            foreach (var part in new[] {name.Text, text.Text})
            {
                part.enableAutoSizing = true;
                part.fontSizeMin = lineHeight * 0.3f;
                part.fontSizeMax = lineHeight * 0.62f;
                part.textWrappingMode = TextWrappingModes.NoWrap;
                part.raycastTarget = false;
            }
            values.Add(text);
        }
        return values;
    }

    private static ModHelperPanel? summary;
    private static RectTransform? summaryCard;

    /// <summary>
    /// What the card shows and the gap to keep from it, looked up every <see cref="CheckInterval"/> while the summary
    /// follows the card, not every frame (#183)
    /// </summary>
    private static readonly List<UnityEngine.UI.Graphic> CardGraphics = new();
    private static float cardGap;
    private static float nextCardCheck;

    /// <summary>
    /// The victory summary, or null
    /// </summary>
    public static ModHelperPanel? Summary => summary != null ? summary : null;

    /// <summary>
    /// Adds the time of every tier, the total and the upgrade tiers and cash used next to the victory screen's stats
    /// card. The card moves while the screen animates and is flipped while it turns, so the summary sits on the
    /// screen itself and follows the card (<see cref="FollowCard"/>).
    /// </summary>
    public static void ShowSummary(BossVictoryScreen screen)
    {
        if (!Visible || TotalTime is not { } total) return;

        if (Summary != null) Object.Destroy(Summary.gameObject);
        var canvas = Canvas(screen);
        var lines = SummaryLines(total);
        var height = canvas.rect.height;
        var lineHeight = height * 0.042f;
        var width = height * 0.42f;
        summary = ModHelperPanel.Create(new Info(SummaryName, 0, 0, width, lineHeight * (lines.Count + 0.5f))
        {
            Anchor = new Vector2(0.5f, 0.5f), Pivot = new Vector2(0, 0.5f)
        }, VanillaSprites.BlueInsertPanelRound);
        summary.transform.SetParent(screen.transform, false);
        summary.transform.SetAsLastSibling();
        AddLines(summary, lines, width, lineHeight);
        summaryCard = SummaryCard(screen);
        nextCardCheck = 0;
        FollowCard();
    }

    /// <summary>
    /// The victory screen's stats card, which the summary is put next to
    /// </summary>
    public static RectTransform? SummaryCard(BossVictoryScreen screen) =>
        screen.statsBackground != null ? screen.statsBackground.TryCast<RectTransform>() : null;

    /// <summary>
    /// What the card shows on the screen, in world space: its texts and pictures reach past its own frame
    /// </summary>
    public static Rect? CardBounds(RectTransform card) => Bounds(card.GetComponentsInChildren<UnityEngine.UI.Graphic>());

    private static Rect? Bounds(IEnumerable<UnityEngine.UI.Graphic> graphics)
    {
        var corners = Corners;
        Rect? bounds = null;
        foreach (var graphic in graphics)
        {
            if (graphic == null || !graphic.isActiveAndEnabled) continue;
            graphic.rectTransform.GetWorldCorners(corners);
            for (var i = 0; i < 4; i++)
            {
                var point = corners[i];
                bounds = bounds is { } b
                    ? Rect.MinMaxRect(Math.Min(b.xMin, point.x), Math.Min(b.yMin, point.y), Math.Max(b.xMax, point.x), Math.Max(b.yMax, point.y))
                    : new Rect(point.x, point.y, 0, 0);
            }
        }
        return bounds;
    }

    /// <summary>
    /// Keeps the summary just right of the card, vertically centred on it; called every frame
    /// </summary>
    private static void FollowCard()
    {
        if (Summary == null || summaryCard == null) return;
        if (Time.unscaledTime >= nextCardCheck)
        {
            nextCardCheck = Time.unscaledTime + CheckInterval;
            CardGraphics.Clear();
            foreach (var graphic in summaryCard.GetComponentsInChildren<UnityEngine.UI.Graphic>()) CardGraphics.Add(graphic);
            var canvas = Canvas(Summary);
            cardGap = canvas.rect.height * 0.02f * canvas.lossyScale.x;
        }

        if (Bounds(CardGraphics) is not { } card) return;
        Summary.transform.position = new Vector3(card.xMax + cardGap, card.center.y, Summary.transform.position.z);
    }

    /// <summary>
    /// The victory summary's lines: the time of every tier, any penalty, the total, then the upgrade tiers and cash
    /// used
    /// </summary>
    public static List<(string Label, string Value)> SummaryLines(TimeSpan total)
    {
        var lines = TierTimes()
            .Select((time, i) => (Texts.TierTime.T(i + 1), time is { } t ? RankedTime.Format(t) : "-"))
            .ToList();
        if (PenaltyTime > TimeSpan.Zero) lines.Add((Texts.PenaltyTime.T(), "+" + RankedTime.Format(PenaltyTime)));
        lines.Add((Texts.TotalTime.T(), RankedTime.Format(total)));
        lines.Add((Texts.TiersUsed.T(), TiersUsed.ToString("N0")));
        lines.Add((Texts.CashUsed.T(), "$" + CashUsed.ToString("N0")));
        return lines;
    }
}
