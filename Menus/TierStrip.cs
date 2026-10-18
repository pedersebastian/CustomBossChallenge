using System;
using System.Collections.Generic;
using System.Globalization;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity;
using Il2CppTMPro;
using UnityEngine;

namespace CustomBossChallenge.Menus;

/// <summary>
/// A row of small cards, one per boss tier, showing the round each tier arrives on and the speed and health it will
/// have. Values above the normal game are orange, values below it blue, so the effect of every boss option shows at
/// a glance.
/// </summary>
public sealed class TierStrip
{
    public const float Height = 300;
    public const float CardHeight = 270;
    public const float NoteHeight = 46;

    /// <summary>
    /// The row's height while it also shows the health cap line
    /// </summary>
    public const float CappedHeight = 380;
    public const float MaxCardWidth = 330;
    public const float CardGap = 12;
    public const string CardName = "Tier";

    public static readonly Color Higher = new(1f, 0.62f, 0.2f);
    public static readonly Color Lower = new(0.45f, 0.8f, 1f);
    public static readonly Color Same = Color.white;

    public GameObject Row { get; }

    /// <summary>
    /// The cards, for tiers 1 to 10
    /// </summary>
    public List<Card> Cards { get; } = new();

    /// <summary>
    /// The line under the cards with the most health a tier can have, only shown while a tier is held to it
    /// </summary>
    public ModHelperText Note { get; }

    private readonly ModHelperPanel strip;

    public sealed class Card
    {
        public ModHelperPanel Panel = null!;
        public ModHelperImage Portrait = null!;
        public ModHelperText Round = null!;
        public ModHelperText Speed = null!;
        public ModHelperText Health = null!;
    }

    public TierStrip(GameObject row)
    {
        Row = row;
        var rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, Height);

        strip = ModHelperPanel.Create(EditorRows.ControlInfo("TierStrip", EditorRows.ControlWidth, Height - 10),
            VanillaSprites.BlueInsertPanelRound);
        strip.transform.SetParent(row.transform, false);

        for (var tier = 1; tier <= TierMath.MaxTiers; tier++)
        {
            var card = new Card
            {
                Panel = ModHelperPanel.Create(new Info($"{CardName}{tier}", 0, 0, MaxCardWidth, CardHeight)
                {
                    Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
                }, VanillaSprites.BlueInsertPanelRound)
            };
            card.Panel.transform.SetParent(strip.transform, false);

            // The tier in the top left corner and its round in the top right one
            card.Panel.AddText(new Info("Tier", 16, -6, 120, 70)
            {
                Anchor = new Vector2(0, 1), Pivot = new Vector2(0, 1)
            }, $"T{tier}", 52, TextAlignmentOptions.TopLeft);
            card.Round = card.Panel.AddText(new Info("Round", -16, -10, 160, 70)
            {
                Anchor = new Vector2(1, 1), Pivot = new Vector2(1, 1)
            }, "", 42, TextAlignmentOptions.TopRight);

            // Later tiers get a slightly bigger portrait, so the strip reads as a climb
            var size = 84 + tier * 6;
            card.Portrait = card.Panel.AddImage(new Info("Portrait", 0, 22, size, size), VanillaSprites.BossTiersIconSmall);
            card.Speed = card.Panel.AddText(new Info("Speed", 0, -62, 300, 64), "", 54);
            card.Health = card.Panel.AddText(new Info("Health", 0, -112, 300, 64), "", 54);
            Cards.Add(card);
        }

        Note = strip.AddText(new Info("HealthCap", 0, 18, EditorRows.ControlWidth - 40, NoteHeight)
        {
            Anchor = new Vector2(0.5f, 0), Pivot = new Vector2(0.5f, 0)
        }, "", 40, TextAlignmentOptions.Center);
        Note.Text.color = Higher;
    }

    /// <summary>
    /// Cards share the strip's width, centered, but never get wider than <see cref="MaxCardWidth"/>. With the health
    /// cap line the row grows and the cards stay at its top, above the line.
    /// </summary>
    private void LayOut(int tiers, bool capped)
    {
        var height = capped ? CappedHeight : Height;
        var rowRect = Row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, height);
        strip.RectTransform.sizeDelta = new Vector2(strip.RectTransform.sizeDelta.x, height - 10);
        Note.gameObject.SetActive(capped);

        var y = capped ? (height - 10 - CardHeight) / 2 - 8 : 0;
        var slot = Math.Min(EditorRows.ControlWidth / tiers, MaxCardWidth + CardGap);
        var left = (EditorRows.ControlWidth - slot * tiers) / 2;
        for (var i = 0; i < tiers; i++)
        {
            var cardRect = Cards[i].Panel.RectTransform;
            cardRect.anchoredPosition = new Vector2(left + (i + 0.5f) * slot, y);
            cardRect.sizeDelta = new Vector2(slot - CardGap, CardHeight);
        }
    }

    /// <summary>
    /// Shows the tiers <paramref name="options"/> asks for, with the boss multipliers from <paramref name="dcm"/>.
    /// Returns whether the row changed height, so the section must be laid out again.
    /// </summary>
    public bool Show(BossOptions options, DailyChallengeModel? dcm)
    {
        var height = Row.GetComponent<RectTransform>().sizeDelta.y;
        var tiers = TierMath.ClampTiers(options.Tiers);
        var rounds = options.EffectiveSpawnRounds;
        var bossSpeed = dcm?.bloonModifiers?.bossSpeedMultiplier ?? 1;
        var bossHealth = dcm?.bloonModifiers?.healthMultipliers?.boss ?? 1;
        var portrait = BossEditorSection.Portrait(options.Boss, options.Elite);
        var game = Game.instance?.model;
        var capped = false;

        for (var i = 0; i < Cards.Count; i++)
        {
            var tier = i + 1;
            var card = Cards[i];
            card.Panel.gameObject.SetActive(tier <= tiers);
            if (tier > tiers) continue;

            card.Portrait.Image.SetSprite(portrait);
            card.Round.SetText($"R{rounds[i]}");

            var speed = TierMath.SpeedFactor(tier, options.SpeedPerTier) * bossSpeed;
            Paint(card.Speed, $"×{speed.ToString("0.##", CultureInfo.InvariantCulture)}", speed);

            var source = options.BossId(Math.Min(tier, TierMath.VanillaTiers));
            var health = TierMath.HealthFactor(tier, options.HealthPerTier) * bossHealth;
            var shown = "?";
            if (game != null && game.DoesBloonExist(source))
            {
                var model = game.GetBloon(source).maxHealth * TierMath.ExtraTierFactor(tier) *
                            TierMath.HealthFactor(tier, options.HealthPerTier);
                capped |= model > TierMath.MaxModelHealth;
                shown = FormatHealth(Math.Min(model, TierMath.MaxModelHealth) * bossHealth);
            }
            Paint(card.Health, shown, health);
        }

        // The cap is on the boss model; Boss Health is applied on top of it, like in the game
        EditorRows.SetText(Note.Text, Texts.HealthCap.T(FormatHealth(TierMath.MaxModelHealth * bossHealth)));
        LayOut(tiers, capped);
        return !Mathf.Approximately(height, Row.GetComponent<RectTransform>().sizeDelta.y);
    }

    private static void Paint(ModHelperText text, string value, double factor)
    {
        text.SetText(value);
        text.Text.color = factor > 1.0001 ? Higher : factor < 0.9999 ? Lower : Same;
    }

    /// <summary>
    /// Health the way the game shortens big numbers, e.g. 950, 7.5k, 112k, 3.4M, 1.2B
    /// </summary>
    public static string FormatHealth(double health)
    {
        string Short(double value, string unit) =>
            (value < 10 ? value.ToString("0.#", CultureInfo.InvariantCulture) : value.ToString("0", CultureInfo.InvariantCulture)) + unit;

        return health switch
        {
            >= 1e9 => Short(health / 1e9, "B"),
            >= 1e6 => Short(health / 1e6, "M"),
            >= 1e3 => Short(health / 1e3, "k"),
            _ => Math.Round(health).ToString(CultureInfo.InvariantCulture)
        };
    }
}
