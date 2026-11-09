using System;
using System.Collections.Generic;
using System.Linq;
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
    public static TimeSpan? BossTime => Manager is { } manager
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
    public static TimeSpan PenaltyTime => Manager is { } manager ? TimeSpanOf(manager.GetPenaltyTime()) : TimeSpan.Zero;

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
    /// Builds, refreshes or removes the panel; called every frame
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
        }

        FollowCard();
        if (Panel == null) return;
        var lines = PanelLines();
        for (var i = 0; i < lines.Count && i < Values.Count; i++) Values[i].Text.text = lines[i].Value;
    }

    /// <summary>
    /// The game's lives and cash at the top left, which the panel sits under
    /// </summary>
    private const string LeftHudPath = "InGame/UIRect/MainHudLeftAlign(Clone)/LeftGroup";

    private static RectTransform? LeftHud()
    {
        var hud = GameObject.Find(LeftHudPath);
        return hud != null ? hud.GetComponent<RectTransform>() : null;
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
    }

    /// <summary>
    /// Puts the panel just under the lives and cash, wherever the game's layout has them: under the lowest of their
    /// visible parts, as their texts reach below the group's own frame
    /// </summary>
    private static void Place(ModHelperPanel ours, RectTransform hud)
    {
        var corners = new Il2CppStructArray<Vector3>(4);
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
    public static Rect? CardBounds(RectTransform card)
    {
        var corners = new Il2CppStructArray<Vector3>(4);
        Rect? bounds = null;
        foreach (var graphic in card.GetComponentsInChildren<UnityEngine.UI.Graphic>())
        {
            if (!graphic.isActiveAndEnabled) continue;
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
        if (Summary == null || summaryCard == null || CardBounds(summaryCard) is not { } card) return;
        var canvas = Canvas(Summary);
        var gap = canvas.rect.height * 0.02f * canvas.lossyScale.x;
        Summary.transform.position = new Vector3(card.xMax + gap, card.center.y, Summary.transform.position.z);
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
