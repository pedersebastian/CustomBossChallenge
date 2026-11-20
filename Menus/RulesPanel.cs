using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Extensions;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The boss options of a custom game in the game's Challenge Rules panel, which the pause screen's Rules button opens
/// (#140). The Boss Creator's sliders are rows of its Modifiers list (#141). Its Rules box shows the rest with the game's
/// look (#147): the boss, Boss Event Rounds and No changes as tiles after the game's own rule tiles, then the tiers as
/// the Tier Preview's cards and the skulls as the Skull Preview's bar. <see cref="RulesSummary.View"/> says what is
/// shown; the game's own rules (towers, cash, lives, rounds, modifiers) stay as they are.
/// </summary>
public static class RulesPanel
{
    /// <summary>The name of the part under the Rules box's grid that holds the tier cards and the skull bar</summary>
    public const string Name = "CbcRules";

    /// <summary>The name of the tiles the mod adds to the Rules box's grid</summary>
    public const string TileName = "CbcRule";

    /// <summary>The name of the rows the mod adds to the Modifiers list</summary>
    public const string ModifierName = "CbcModifier";

    public const string TierStripName = "TierStripRow";
    public const string SkullStripName = "SkullStripRow";

    private const float Padding = 30;

    /// <summary>The Tier Preview's and Skull Preview's width, with a little room on both sides</summary>
    private const float StripWidth = EditorRows.ControlWidth + 20;

    private const float TileLabelWidth = 640;

    private static bool logged;

    /// <summary>The tier cards the Rules box shows now, for the tests; null when it shows none</summary>
    public static TierStrip? Tiers { get; private set; }

    /// <summary>The skull bar the Rules box shows now, for the tests; null when it shows none</summary>
    public static SkullStrip? Skulls { get; private set; }

    /// <summary>
    /// The highest tier whose boss has arrived in the game being played, like the HUD counts it
    /// </summary>
    public static int ArrivedTier()
    {
        var manager = Il2CppAssets.Scripts.Unity.UI_New.InGame.InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager;
        return manager != null ? Patches.MixedBosses.ArrivedTier(manager) : 0;
    }

    /// <summary>
    /// The rows the Modifiers list gets for <paramref name="options"/>, after the game's own: the Boss Creator's sliders
    /// </summary>
    public static List<(string Icon, string Label, string Value)> ModifierRows(BossOptions options) =>
        RulesSummary.Modifiers(options).Select(line => (Icon(line), Texts.ModifierLabel(line), Texts.ModifierValue(line))).ToList();

    /// <summary>The Boss Creator's icon for a slider</summary>
    private static string Icon(SummaryLine line) => line.Kind switch
    {
        RuleLine.Tiers => VanillaSprites.BossTiersIconSmall,
        RuleLine.SpeedPerTier => VanillaSprites.FasterBossIcon,
        RuleLine.HealthPerTier => VanillaSprites.BossBoostIcon,
        _ => VanillaSprites.CrossboneSkull
    };

    /// <summary>
    /// The tiles the Rules box gets for <paramref name="view"/>, after the game's own: the boss with its portrait
    /// (Elite's own when Elite), Boss Event Rounds with the Boss Creator's icon, and No changes
    /// </summary>
    public static List<(string Icon, string Label)> Tiles(RulesView view) => view.Tiles.Select(Tile).ToList();

    private static (string Icon, string Label) Tile(SummaryLine line) => line.Kind switch
    {
        RuleLine.Boss => (BossEditorSection.Portrait((BossType) line.Args[0], (bool) line.Args[1]),
            Visuals.BossHud.Name((BossType) line.Args[0], (bool) line.Args[1])),
        RuleLine.BossEventRounds => (VanillaSprites.BossChallenge, Texts.BossEventRounds.T()),
        _ => (VanillaSprites.TickGreenIcon, Texts.RulesNoChanges.T())
    };

    public static void Show(ChallengeRulesScreen screen)
    {
        var content = screen.gameRuleContent;
        if (content == null) return;

        // The panel is reused between games: take back what an earlier custom game added
        for (var i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (child.name is Name or TileName) Object.DestroyImmediate(child.gameObject);
        }
        Tiers = null;
        Skulls = null;
        var grid = content.GetComponent<GridLayoutGroup>();
        var id = content.GetInstanceID();
        if (grid != null && GridPadding.TryGetValue(id, out var bottom)) grid.padding.bottom = bottom;

        var modifiers = screen.modifierContent;
        if (modifiers != null)
        {
            for (var i = modifiers.childCount - 1; i >= 0; i--)
            {
                var child = modifiers.GetChild(i);
                if (child.name == ModifierName) Object.DestroyImmediate(child.gameObject);
            }
        }

        if (!CustomBossState.IsActive || !ConfigApplier.IsMarked(screen.dcm) ||
            CustomBossState.ActiveOptions is not { } options) return;

        ShowModifiers(screen, options);
        LogLayout(content.gameObject);

        var view = RulesSummary.View(options, ArrivedTier());
        ShowTiles(screen, view);
        ShowStrips(screen, options, view);
    }

    /// <summary>
    /// Rows like the game's Boss Speed and Boss Health in the Modifiers list, made from the game's own row
    /// </summary>
    private static void ShowModifiers(ChallengeRulesScreen screen, BossOptions options)
    {
        if (screen.modifierContent == null || screen.modifierPrefab == null) return;
        foreach (var (icon, label, value) in ModifierRows(options))
        {
            var row = Object.Instantiate(screen.modifierPrefab, screen.modifierContent);
            row.name = ModifierName;
            row.SetActive(true);
            row.transform.SetAsLastSibling();
            row.transform.Find("Icon")?.GetComponent<Image>()?.SetSprite(icon);
            SetText(row.transform.Find("Layout/Lbl"), label);
            SetText(row.transform.Find("Layout/Value"), value);
        }
        LayoutRebuilder.MarkLayoutForRebuild(screen.modifierContent.Cast<RectTransform>());
    }

    /// <summary>
    /// Tiles like the game's own rules (No Selling, Monkey Knowledge Disabled), which are the same template as the
    /// Modifiers list's rows: an icon and a label, without a value. They come after the game's own, and the grid gives
    /// them the size of its other cells.
    /// </summary>
    private static void ShowTiles(ChallengeRulesScreen screen, RulesView view)
    {
        if (screen.modifierPrefab == null) return;
        foreach (var (icon, label) in Tiles(view))
        {
            var tile = Object.Instantiate(screen.modifierPrefab, screen.gameRuleContent);
            tile.name = TileName;
            tile.SetActive(true);
            tile.transform.SetAsLastSibling();
            tile.transform.Find("Icon")?.GetComponent<Image>()?.SetSprite(icon);

            var value = tile.transform.Find("Layout/Value");
            if (value != null) value.gameObject.SetActive(false);
            var text = tile.transform.Find("Layout/Lbl");
            SetText(text, label);
            if (text != null)
            {
                var element = text.GetComponent<LayoutElement>();
                if (element != null) element.preferredWidth = TileLabelWidth;
                var tmp = text.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    // A long name in a long language shrinks rather than runs out of the tile
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMax = tmp.fontSize;
                    tmp.fontSizeMin = 30;
                }
            }

            // Nothing in the panel takes the focus: a controller stays on the game's own buttons
            foreach (var button in tile.GetComponentsInChildren<Button>(true)) button.enabled = false;
        }
    }

    /// <summary>
    /// The tier cards and the skull bar, under the grid's last row. The grid's bottom padding makes room for them, so
    /// the box scrolls to them like to the game's rules.
    /// </summary>
    private static void ShowStrips(ChallengeRulesScreen screen, BossOptions options, RulesView view)
    {
        // Mod Helper leaves an empty text asset behind for every component (#235)
        using var leftovers = new ModHelperLeftovers();
        if (!view.TierStrip && view.Skulls == null) return;

        var content = screen.gameRuleContent;
        var rect = content.Cast<RectTransform>();
        var grid = content.GetComponent<GridLayoutGroup>();
        var left = grid != null ? grid.padding.left : Padding;
        var right = grid != null ? grid.padding.right : Padding;
        var width = Mathf.Max(rect.rect.width - left - right, 400);
        // Narrower than the Boss Creator's rows, the strips get smaller instead of running out of the box
        var scale = Mathf.Min(1, width / StripWidth);

        var part = ModHelperPanel.Create(new Info(Name, 0, 0, width, 100)
        {
            Anchor = new Vector2(0, 1), Pivot = new Vector2(0, 1)
        });
        part.transform.SetParent(content, false);
        part.transform.SetAsLastSibling();

        var y = 0f;
        if (view.TierStrip)
        {
            var row = Row(part, TierStripName);
            Tiers = new TierStrip(row, readOnly: true);
            Tiers.Show(options, screen.dcm, view.SecretTiers.Contains);
            y = Place(row, Tiers.Panel, y, scale);
        }
        if (view.Skulls != null)
        {
            var row = Row(part, SkullStripName);
            Skulls = new SkullStrip(row);
            Skulls.Show(options);
            if (y > 0) y += Padding;
            y = Place(row, Skulls.Panel, y, scale);
        }

        var height = y;
        part.RectTransform.sizeDelta = new Vector2(width, height);

        var top = Padding;
        if (grid != null)
        {
            part.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var cells = 0;
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child != part.transform && child.gameObject.activeSelf) cells++;
            }
            var rows = cells == 0 ? 0 : Mathf.CeilToInt(cells / (float) Columns(grid, rect.rect.width));
            top = grid.padding.top + rows * (grid.cellSize.y + grid.spacing.y);
            var id = content.GetInstanceID();
            if (!GridPadding.ContainsKey(id)) GridPadding[id] = grid.padding.bottom;
            grid.padding.bottom = GridPadding[id] + Mathf.CeilToInt(top + height + Padding);
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        part.RectTransform.anchoredPosition = new Vector2(left, -top);

        // Nothing in the panel takes the focus or a click, like the game's own rules
        foreach (var graphic in part.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    /// <summary>A row across <paramref name="part"/> for a strip, which sets the row's height itself</summary>
    private static GameObject Row(ModHelperPanel part, string name)
    {
        var row = ModHelperPanel.Create(new Info(name, 0, 0, 0, 100)
        {
            AnchorMin = new Vector2(0, 1), AnchorMax = new Vector2(1, 1), Pivot = new Vector2(0.5f, 1)
        });
        row.transform.SetParent(part.transform, false);
        return row.gameObject;
    }

    /// <summary>
    /// Puts <paramref name="row"/> at <paramref name="y"/> from the top with its strip in the middle, scaled, and
    /// returns where the next one goes
    /// </summary>
    private static float Place(GameObject row, ModHelperPanel strip, float y, float scale)
    {
        var rowRect = row.GetComponent<RectTransform>();
        var height = rowRect.sizeDelta.y * scale;
        rowRect.sizeDelta = new Vector2(0, height);
        rowRect.anchoredPosition = new Vector2(0, -y);

        var stripRect = strip.RectTransform;
        stripRect.anchorMin = stripRect.anchorMax = new Vector2(0.5f, 0.5f);
        stripRect.pivot = new Vector2(0.5f, 0.5f);
        stripRect.anchoredPosition = Vector2.zero;
        stripRect.localScale = new Vector3(scale, scale, 1);
        return y + height;
    }

    private static void SetText(Transform? text, string value)
    {
        var tmp = text != null ? text.GetComponent<TMP_Text>() : null;
        if (tmp != null) EditorRows.SetText(tmp, value);
    }

    /// <summary>The grid's own bottom padding, by the Rules box's instance id, before the mod made room in it</summary>
    private static readonly Dictionary<int, int> GridPadding = new();

    private static int Columns(GridLayoutGroup grid, float width)
    {
        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount) return Mathf.Max(grid.constraintCount, 1);
        var room = width - grid.padding.left - grid.padding.right + grid.spacing.x;
        return Mathf.Max(Mathf.FloorToInt(room / (grid.cellSize.x + grid.spacing.x)), 1);
    }

    /// <summary>
    /// Writes the Rules box's layout to the log once, to see how the game builds it after a game update
    /// </summary>
    private static void LogLayout(GameObject content)
    {
        if (logged) return;
        logged = true;
        var rect = content.GetComponent<RectTransform>();
        var components = string.Join(", ", content.GetComponents<Component>().Select(c => c.GetIl2CppType().Name));
        var children = string.Join(", ", Enumerable.Range(0, content.transform.childCount)
            .Select(i => content.transform.GetChild(i)).Select(c => $"{c.name}{(c.gameObject.activeSelf ? "" : " (off)")}"));
        var scroll = content.GetComponentInParent<ScrollRect>();
        var grid = content.GetComponent<GridLayoutGroup>();
        var cells = grid == null ? "" : $", cell {grid.cellSize}, spacing {grid.spacing}, padding {grid.padding.left}/" +
                                        $"{grid.padding.right}/{grid.padding.top}/{grid.padding.bottom}, {grid.constraint} {grid.constraintCount}";
        var view = scroll != null && scroll.viewport != null ? $", viewport {scroll.viewport.rect.width:0}x{scroll.viewport.rect.height:0}" : "";
        ModHelper.Msg<CustomBossChallenge>(
            $"Rules box: {rect.rect.width:0}x{rect.rect.height:0}, [{components}], scroll {scroll != null}{view}{cells}, children [{children}]");
    }
}
