using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The boss options of a custom game in the Rules box of the game's Challenge Rules panel, which the pause screen's
/// Rules button opens (#140). The game's own rules (towers, cash, lives, rounds, modifiers) stay as they are.
/// </summary>
public static class RulesPanel
{
    /// <summary>The name of the text the mod adds to the Rules box</summary>
    public const string Name = "CbcRules";

    private const float FontSize = 72;
    private const float Padding = 30;

    private static bool logged;

    /// <summary>
    /// The lines the panel shows for <paramref name="options"/>: a header, then <see cref="RulesSummary.Lines"/>
    /// </summary>
    public static List<string> Lines(BossOptions options) =>
        new[] {$"<size=125%>{Texts.RulesHeader.T()}</size>"}.Concat(RulesSummary.Lines(options).Select(Texts.Summary)).ToList();

    public static void Show(ChallengeRulesScreen screen)
    {
        var content = screen.gameRuleContent;
        if (content == null) return;

        // The panel is reused between games: take back what an earlier custom game added
        var old = content.Find(Name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var grid = content.GetComponent<GridLayoutGroup>();
        var id = content.GetInstanceID();
        if (grid != null && GridPadding.TryGetValue(id, out var bottom)) grid.padding.bottom = bottom;

        if (!CustomBossState.IsActive || !ConfigApplier.IsMarked(screen.dcm) ||
            CustomBossState.ActiveOptions is not { } options) return;

        LogLayout(content.gameObject);

        var rect = content.GetComponent<RectTransform>();
        var left = grid != null ? grid.padding.left : Padding;
        var right = grid != null ? grid.padding.right : Padding;
        var width = Mathf.Max(rect.rect.width - left - right - Padding * 2, 400);
        var text = ModHelperText.Create(new Info(Name, 0, 0, width, 100)
        {
            Anchor = new Vector2(0, 1), Pivot = new Vector2(0, 1)
        }, string.Join("\n", Lines(options)), FontSize, TextAlignmentOptions.TopLeft);
        text.transform.SetParent(content, false);
        text.transform.SetAsLastSibling();
        text.Text.enableWordWrapping = true;
        text.Text.raycastTarget = false;

        var height = text.Text.GetPreferredValues(text.Text.text, width, 0).y;
        var textRect = text.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(width, height);

        // The box is a grid of the game's own rules. The text stays out of the grid, under its last row, and the
        // grid's bottom padding makes room for it so the box scrolls to it like to the game's rules.
        var top = Padding;
        if (grid != null)
        {
            text.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var cells = 0;
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (child != textRect.transform && child.gameObject.activeSelf) cells++;
            }
            var rows = cells == 0 ? 0 : Mathf.CeilToInt(cells / (float) Columns(grid, rect.rect.width));
            top = grid.padding.top + rows * (grid.cellSize.y + grid.spacing.y);
            if (!GridPadding.ContainsKey(id)) GridPadding[id] = grid.padding.bottom;
            grid.padding.bottom = GridPadding[id] + Mathf.CeilToInt(top + height + Padding);
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        textRect.anchoredPosition = new Vector2(left + Padding, -top);
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
