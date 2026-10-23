using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Unity;
using Il2CppTMPro;
using UnityEngine;

namespace CustomBossChallenge.Menus;

/// <summary>
/// A health bar like the boss's own, full on the left and empty on the right, with a skull at every health percentage
/// where the boss uses its special ability. Shows the skulls the chosen skull settings give, or the boss's own skulls
/// for Vanilla.
/// </summary>
public sealed class SkullStrip
{
    public const float Height = 370;
    public const float BarWidth = 1540;
    public const float BarHeight = 56;
    public const float MarkerWidth = 200;
    public const string MarkerName = "Skull";

    /// <summary>
    /// Percentages closer than this on the bar go on the lower line, so they don't cover each other
    /// </summary>
    public const float MinTextGap = 195;
    public const float TextY = -84;
    public const float LowerTextY = -148;

    public static readonly Color Health = new(0.47f, 0.84f, 0.13f);
    public static readonly Color Track = new(0.24f, 0.15f, 0.08f);

    public GameObject Row { get; }

    /// <summary>
    /// The skulls, up to <see cref="SkullMath.MaxSkulls"/>, from full health to empty
    /// </summary>
    public List<Marker> Markers { get; } = new();

    /// <summary>
    /// Shown on the bar while the boss has no skulls
    /// </summary>
    public ModHelperText Empty { get; }

    /// <summary>
    /// Names the tier the bar shows while the boss's own skulls differ between tiers
    /// </summary>
    public ModHelperText Tier { get; }

    public ModHelperPanel Bar { get; }

    public sealed class Marker
    {
        public ModHelperPanel Panel = null!;
        public ModHelperImage Line = null!;
        public ModHelperImage Icon = null!;
        public ModHelperText Percent = null!;
    }

    public SkullStrip(GameObject row)
    {
        Row = row;
        var rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, Height);

        var strip = ModHelperPanel.Create(EditorRows.ControlInfo("SkullStrip", EditorRows.ControlWidth, Height - 10),
            VanillaSprites.BlueInsertPanelRound);
        strip.transform.SetParent(row.transform, false);

        // The bar sits a little above the middle, leaving room for the percentages below it
        Bar = strip.AddPanel(new Info("Bar", 0, 34, BarWidth, BarHeight));
        Solid(Bar.AddImage(new Info("Track", InfoPreset.FillParent), VanillaSprites.SmallSquareWhiteGradient), Track);
        Solid(Bar.AddImage(new Info("Health", 4, 0, BarWidth - 8, BarHeight - 10)
        {
            Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0, 0.5f)
        }, VanillaSprites.SmallSquareWhiteGradient), Health);

        OneLine(strip.AddText(new Info("Full", -(BarWidth / 2 + 115), 34, 210, 80), "100%", 60));
        OneLine(strip.AddText(new Info("None", BarWidth / 2 + 85, 34, 150, 80), "0%", 60));

        for (var i = 0; i < SkullMath.MaxSkulls; i++)
        {
            var marker = new Marker
            {
                Panel = Bar.AddPanel(new Info($"{MarkerName}{i + 1}", 0, 0, MarkerWidth, BarHeight)
                {
                    Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
                })
            };
            marker.Line = Solid(marker.Panel.AddImage(new Info("Line", 0, 0, 8, BarHeight + 28),
                VanillaSprites.SmallSquareWhiteGradient), Color.white);
            marker.Icon = marker.Panel.AddImage(new Info("Icon", 0, 84, 116, 116), VanillaSprites.CrossboneSkull);
            marker.Percent = OneLine(marker.Panel.AddText(new Info("Percent", 0, TextY, MarkerWidth, 70), "", 62));
            Markers.Add(marker);
        }

        Empty = Bar.AddText(new Info("Empty", InfoPreset.FillParent), "", 40);
        Tier = strip.AddText(new Info("Tier", 24, -14, 120, 50)
        {
            Anchor = new Vector2(0, 1), Pivot = new Vector2(0, 1)
        }, "", 40, TextAlignmentOptions.TopLeft);
    }

    /// <summary>
    /// Always the whole text on one line, never wrapped or cut off with "..."
    /// </summary>
    private static ModHelperText OneLine(ModHelperText text)
    {
        text.Text.textWrappingMode = TextWrappingModes.NoWrap;
        text.Text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static ModHelperImage Solid(ModHelperImage image, Color color)
    {
        image.Image.sprite = null;
        image.Image.color = color;
        return image;
    }

    /// <summary>
    /// Shows the skulls <paramref name="options"/> gives the boss
    /// </summary>
    public void Show(BossOptions options)
    {
        var (percents, tier) = Skulls(options);
        var lastUpper = float.NegativeInfinity;
        for (var i = 0; i < Markers.Count; i++)
        {
            var marker = Markers[i];
            marker.Panel.gameObject.SetActive(i < percents.Length);
            if (i >= percents.Length) continue;

            var x = (1 - percents[i]) * BarWidth;
            marker.Panel.RectTransform.anchoredPosition = new Vector2(x, 0);
            marker.Percent.SetText(Format(percents[i]));

            // A percentage too close to the one before it on the upper line goes on the lower line
            var upper = x - lastUpper >= MinTextGap;
            if (upper) lastUpper = x;
            marker.Percent.RectTransform.anchoredPosition = new Vector2(0, upper ? TextY : LowerTextY);
        }

        EditorRows.SetText(Empty.Text, percents.Length == 0 ? Texts.NoSkulls.T() : "");
        Tier.SetText(tier is { } shown ? $"T{shown}" : "");
    }

    /// <summary>
    /// The skull percentages (0-1, from full health down) the preview shows, and the tier they belong to when the
    /// boss's own skulls differ between its tiers
    /// </summary>
    public static (float[] Percents, int? Tier) Skulls(BossOptions options)
    {
        if (options.SkullPercents is { } percents) return (percents, null);

        var game = Game.instance?.model;
        if (game == null) return (Array.Empty<float>(), null);

        var tiers = Enumerable.Range(1, Math.Min(TierMath.ClampTiers(options.Tiers), TierMath.VanillaTiers))
            .Select(tier => options.BossId(tier))
            .Where(game.DoesBloonExist)
            .Select(id => SkullModifier.GetSkulls(game.GetBloon(id)) ?? Array.Empty<float>())
            .ToList();
        if (tiers.Count == 0) return (Array.Empty<float>(), null);

        var first = tiers[0].OrderByDescending(p => p).ToArray();
        var same = tiers.All(skulls => skulls.OrderByDescending(p => p).SequenceEqual(first));
        return (first, same ? null : 1);
    }

    /// <summary>
    /// A skull's health percentage, e.g. 0.3333 -> "33.33%"
    /// </summary>
    public static string Format(float percent) =>
        Math.Round(percent * 100.0, 2).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
