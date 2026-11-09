using System;
using System.Collections.Generic;
using System.Globalization;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomBossChallenge.Menus;

/// <summary>
/// A row of small cards, one per boss tier, showing the round each tier arrives on and the speed and health it will
/// have. Values above the normal game are orange with ▲, values below it blue with ▼, so the effect of every boss
/// option shows at a glance, also to players who can't tell the colours apart (issue #18).
/// <br/>
/// In Advanced Mode, every card shows its tier's boss and how many skulls it has, and is a button that opens the
/// <see cref="TierBossPicker"/>.
/// </summary>
public sealed class TierStrip
{
    public const float Height = 350;
    public const float CardHeight = 320;
    public const float NoteHeight = 46;

    /// <summary>
    /// The row's height while it also shows one warning line, like the health cap line
    /// </summary>
    public const float CappedHeight = 430;

    /// <summary>
    /// What each further warning line adds to the row's height
    /// </summary>
    public const float NoteStep = 50;

    /// <summary>
    /// One line for each kind of <see cref="SetupWarning"/>
    /// </summary>
    public const int MaxNotes = 4;

    public const string NoteName = "Warning";
    public const float MaxCardWidth = 330;
    public const float CardGap = 12;
    public const string CardName = "Tier";

    public static readonly Color Higher = new(1f, 0.62f, 0.2f);
    public static readonly Color Lower = new(0.45f, 0.8f, 1f);
    public static readonly Color Same = Color.white;

    public const string EditMarker = "Edit";
    public const string SkullsMarker = "Skulls";

    /// <summary>
    /// After a value above or below the normal game, like the website's Tier Preview, so it doesn't rely on colour.
    /// The game's font has ▲ but no ▼, so ▼ is the same triangle turned upside down.
    /// </summary>
    public const string HigherMark = "▲";
    public const string LowerMark = "<rotate=180>▲</rotate>";

    public GameObject Row { get; }

    /// <summary>
    /// The cards, for tiers 1 to 10
    /// </summary>
    public List<Card> Cards { get; } = new();

    /// <summary>
    /// The warning lines under the cards (issue #70), like the most health a tier can have while a tier is held to
    /// it. Only as many as there are warnings are shown, top to bottom in the order of <see cref="SetupWarning"/>.
    /// </summary>
    public List<ModHelperText> Notes { get; } = new();

    /// <summary>
    /// The warnings the lines show now
    /// </summary>
    public List<Warning> Warnings { get; private set; } = new();

    private readonly ModHelperPanel strip;

    /// <summary>
    /// A card was clicked while the tiers can have bosses of their own, with its tier
    /// </summary>
    public Action<int>? CardClicked { get; set; }

    public sealed class Card
    {
        public ModHelperPanel Panel = null!;
        public ModHelperText Tier = null!;
        public ModHelperImage Portrait = null!;
        public ModHelperText Round = null!;
        public ModHelperText Speed = null!;
        public ModHelperText Health = null!;

        /// <summary>
        /// Opens the tier's boss picker; only usable while the tiers can have bosses of their own
        /// </summary>
        public Button Button = null!;

        /// <summary>
        /// The small pencil in the corner that shows the card can be clicked
        /// </summary>
        public ModHelperImage Edit = null!;

        /// <summary>
        /// A skull with the number of skulls the tier's boss has under it, at the card's left; only in Advanced Mode,
        /// where the bosses and so their skulls differ between tiers
        /// </summary>
        public ModHelperPanel Skulls = null!;
        public ModHelperText SkullCount = null!;
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

            // The tier at the top and its round on the line below, so even T10 R1000 fits a narrow card
            // The boxes are taller than the font, so auto-sizing only shrinks the text for width, on narrow cards
            card.Tier = card.Panel.AddText(new Info("Tier", 0, -4, MaxCardWidth, 70)
            {
                Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1)
            }, $"T{tier}", 52, TextAlignmentOptions.Top);
            card.Round = card.Panel.AddText(new Info("Round", 0, -56, MaxCardWidth, 60)
            {
                Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1)
            }, "", 40, TextAlignmentOptions.Top);
            foreach (var text in new[] { card.Tier, card.Round })
            {
                text.Text.enableAutoSizing = true;
                text.Text.fontSizeMin = 24;
                text.Text.fontSizeMax = text.Text.fontSize;
                text.Text.textWrappingMode = TextWrappingModes.NoWrap;
            }

            // Later tiers get a slightly bigger portrait, so the strip reads as a climb
            var size = 84 + tier * 6;
            card.Portrait = card.Panel.AddImage(new Info("Portrait", 0, -3, size, size), VanillaSprites.BossTiersIconSmall);
            // Taller than the font like the tier and round, so auto-sizing only shrinks them for width
            card.Speed = card.Panel.AddText(new Info("Speed", 0, -87, 300, 80), "", 54);
            card.Health = card.Panel.AddText(new Info("Health", 0, -137, 300, 80), "", 54);
            foreach (var text in new[] { card.Speed, card.Health })
            {
                text.Text.enableAutoSizing = true;
                text.Text.fontSizeMin = 30;
                text.Text.fontSizeMax = text.Text.fontSize;
                text.Text.textWrappingMode = TextWrappingModes.NoWrap;
            }

            var cardTier = tier;
            card.Button = card.Panel.gameObject.AddComponent<Button>();
            card.Button.targetGraphic = card.Panel.GetComponent<Image>();
            card.Button.onClick.AddListener(new Action(() => CardClicked?.Invoke(cardTier)));
            card.Edit = card.Panel.AddImage(new Info(EditMarker, -6, -6, 64, 64)
            {
                Anchor = new Vector2(1, 1), Pivot = new Vector2(1, 1)
            }, VanillaSprites.EditBtn);
            card.Edit.Image.raycastTarget = false;

            card.Skulls = card.Panel.AddPanel(new Info(SkullsMarker, 6, -8, 56, 104)
            {
                Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0, 0.5f)
            });
            card.Skulls.AddImage(new Info("Icon", 0, 26, 50, 50), VanillaSprites.CrossboneSkull).Image.raycastTarget = false;
            card.SkullCount = card.Skulls.AddText(new Info("Count", 0, -26, 56, 54), "", 44);
            card.SkullCount.Text.raycastTarget = false;

            Cards.Add(card);
        }

        for (var i = 0; i < MaxNotes; i++)
        {
            var note = strip.AddText(new Info(NoteName + i, 0, 18, EditorRows.ControlWidth - 40, NoteHeight)
            {
                Anchor = new Vector2(0.5f, 0), Pivot = new Vector2(0.5f, 0)
            }, "", 40, TextAlignmentOptions.Center);
            note.Text.color = Higher;
            // Long translations shrink to the line instead of running into the next one
            note.Text.enableAutoSizing = true;
            note.Text.fontSizeMin = 24;
            note.Text.fontSizeMax = 40;
            note.Text.textWrappingMode = TextWrappingModes.NoWrap;
            note.gameObject.SetActive(false);
            Notes.Add(note);
        }
    }

    /// <summary>
    /// The row's height with <paramref name="lines"/> warning lines
    /// </summary>
    public static float HeightFor(int lines) => lines <= 0 ? Height : CappedHeight + (lines - 1) * NoteStep;

    /// <summary>
    /// Cards share the strip's width, centered, but never get wider than <see cref="MaxCardWidth"/>. With the health
    /// warning lines the row grows and the cards stay at its top, above the lines.
    /// </summary>
    private void LayOut(int tiers, int lines)
    {
        var height = HeightFor(lines);
        var rowRect = Row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, height);
        strip.RectTransform.sizeDelta = new Vector2(strip.RectTransform.sizeDelta.x, height - 10);
        for (var i = 0; i < Notes.Count; i++)
        {
            Notes[i].gameObject.SetActive(i < lines);
            Notes[i].RectTransform.anchoredPosition = new Vector2(0, 18 + (lines - 1 - i) * NoteStep);
        }

        var y = lines > 0 ? (height - 10 - CardHeight) / 2 - 8 : 0;
        var slot = Math.Min(EditorRows.ControlWidth / tiers, MaxCardWidth + CardGap);
        var left = (EditorRows.ControlWidth - slot * tiers) / 2;
        for (var i = 0; i < tiers; i++)
        {
            var cardRect = Cards[i].Panel.RectTransform;
            cardRect.anchoredPosition = new Vector2(left + (i + 0.5f) * slot, y);
            cardRect.sizeDelta = new Vector2(slot - CardGap, CardHeight);

            // The text shrinks to the card instead of running into the next one
            foreach (var text in new[] { Cards[i].Tier, Cards[i].Round, Cards[i].Speed, Cards[i].Health })
            {
                text.RectTransform.sizeDelta = new Vector2(slot - CardGap - 16, text.RectTransform.sizeDelta.y);
            }
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
        var game = Game.instance?.model;
        var values = Values(options, dcm, game);

        for (var i = 0; i < Cards.Count; i++)
        {
            var card = Cards[i];
            card.Panel.gameObject.SetActive(i < tiers);
            if (i >= tiers) continue;

            var value = values[i];
            var entry = options.EntryOf(i + 1);
            var (boss, elite) = options.BossOf(i + 1);
            card.Portrait.Image.SetSprite(entry.IsRandom ? VanillaSprites.RandomIcon : BossEditorSection.Portrait(boss, elite));
            BossEditorSection.ShowEliteGlow(card.Portrait, entry.IsRandom && entry.Elite);
            card.Button.interactable = options.Mixed;
            card.Edit.gameObject.SetActive(options.Mixed);
            card.Skulls.gameObject.SetActive(options.Mixed);
            card.SkullCount.SetText(value.Skulls?.ToString(CultureInfo.InvariantCulture) ?? "?");
            card.Round.SetText($"R{value.Round}");
            Paint(card.Speed, $"×{value.Speed.ToString("0.##", CultureInfo.InvariantCulture)}", value.Speed);
            Paint(card.Health, value.Shown, value.HealthFactor);
        }

        Warnings = SetupWarnings.Check(Facts(options, dcm, game));
        for (var i = 0; i < Warnings.Count && i < Notes.Count; i++) EditorRows.SetText(Notes[i].Text, Texts.Warning(Warnings[i]));
        LayOut(tiers, Math.Min(Warnings.Count, Notes.Count));
        return !Mathf.Approximately(height, Row.GetComponent<RectTransform>().sizeDelta.y);
    }

    /// <summary>
    /// What a tier's card shows: the round it arrives on, its speed and health compared with the normal game (above 1
    /// is harder), and its health shortened like the game does, or "?" without the boss's model or for a Random tier.
    /// Also how many skulls it has, null for a Random tier.
    /// </summary>
    public readonly record struct CardValues(int Tier, int Round, double Speed, double HealthFactor, string Shown, bool Capped,
        int? Skulls);

    /// <summary>
    /// The cards for <paramref name="options"/> with the boss multipliers from <paramref name="dcm"/>. The website
    /// shows the same numbers, see WebDataTest.
    /// </summary>
    public static List<CardValues> Values(BossOptions options, DailyChallengeModel? dcm, GameModel? game)
    {
        var rounds = options.EffectiveSpawnRounds;
        var bossSpeed = dcm?.bloonModifiers?.bossSpeedMultiplier ?? 1;
        var bossHealth = dcm?.bloonModifiers?.healthMultipliers?.boss ?? 1;
        var cap = TierMath.MaxModelHealth(bossHealth);
        var facts = Facts(options, dcm, game);
        var values = new List<CardValues>();
        for (var tier = 1; tier <= TierMath.ClampTiers(options.Tiers); tier++)
        {
            // In Advanced Mode each tier has its own speed and health on top
            var tierSpeed = options.TierSpeedFactor(tier);
            var tierHealth = options.TierHealthFactor(tier);
            var speed = TierMath.SpeedFactor(tier, options.SpeedPerTier) * tierSpeed * bossSpeed;
            var health = TierMath.HealthFactor(tier, options.HealthPerTier) * tierHealth * bossHealth;
            var shown = "?";
            var capped = false;
            // The same health the warnings check against the game's limit
            if (SetupWarnings.ModelHealth(facts, tier) is { } model)
            {
                capped = model > cap;
                shown = FormatHealth(Math.Min(model, cap) * bossHealth);
            }

            values.Add(new CardValues(tier, rounds[tier - 1], speed, health, shown, capped,
                SkullStrip.TierSkulls(options, tier, game)?.Length));
        }

        return values;
    }

    private static void Paint(ModHelperText text, string value, double factor)
    {
        text.SetText(value + Mark(factor));
        text.Text.color = factor > 1.0001 ? Higher : factor < 0.9999 ? Lower : Same;
    }

    /// <summary>
    /// ▲ for a value above the normal game, ▼ below it, nothing for the same
    /// </summary>
    public static string Mark(double factor) => factor > 1.0001 ? HigherMark : factor < 0.9999 ? LowerMark : "";

    /// <summary>
    /// What <see cref="SetupWarnings"/> needs from <paramref name="dcm"/> and the boss models in
    /// <paramref name="game"/>; without them, the editor's defaults and no model values
    /// </summary>
    public static SetupFacts Facts(BossOptions options, DailyChallengeModel? dcm, GameModel? game)
    {
        var bases = new TierBase?[TierMath.ClampTiers(options.Tiers)];
        for (var tier = 1; tier <= bases.Length; tier++)
        {
            var source = options.SourceId(tier);
            if (game == null || !game.DoesBloonExist(source) || options.EntryOf(tier).IsRandom) continue;
            var model = game.GetBloon(source);
            bases[tier - 1] = new TierBase(model.maxHealth, model.speed);
        }

        var facts = new SetupFacts {Options = options, TierBases = bases};
        if (dcm == null) return facts;

        var modifiers = dcm.bloonModifiers;
        var health = modifiers?.healthMultipliers;
        return facts with
        {
            StartRound = dcm.startRules?.round ?? facts.StartRound,
            Difficulty = dcm.difficulty ?? facts.Difficulty,
            Mode = dcm.mode ?? facts.Mode,
            Lives = dcm.startRules?.lives ?? facts.Lives,
            Cash = dcm.startRules?.cash ?? facts.Cash,
            MaxTowers = dcm.maxTowers,
            BloonSpeed = modifiers?.speedMultiplier ?? 1,
            MoabSpeed = modifiers?.moabSpeedMultiplier ?? 1,
            BossSpeed = modifiers?.bossSpeedMultiplier ?? 1,
            BloonHealth = health?.bloons ?? 1,
            MoabHealth = health?.moabs ?? 1,
            BossHealth = health?.boss ?? 1,
            NoSelling = dcm.disableSelling,
            NoKnowledge = dcm.disableMK,
            NoPowers = dcm.disablePowers,
            AllCamo = modifiers?.allCamo ?? false,
            AllRegrow = modifiers?.allRegen ?? false,
            AllFortified = modifiers?.allFortified ?? false
        };
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
