using System;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// Two tabs at the top of the Boss section, Standard and Advanced: the picked one has its name and a thick line under
/// it in orange, the other one is grey with a thin line. Advanced gives every tier a boss of its own.
/// </summary>
public sealed class ModeTabs
{
    public const float Height = 170;
    private const float LineHeight = 18;
    private const float ThinLineHeight = 6;

    public static readonly Color Picked = new(1f, 0.55f, 0.25f);
    public static readonly Color NotPicked = new(0.78f, 0.82f, 0.86f);

    public GameObject Row { get; }
    public Button Standard { get; }
    public Button Advanced { get; }

    /// <summary>
    /// Whether the Advanced tab is the picked one
    /// </summary>
    public bool AdvancedPicked { get; private set; }

    private readonly ModHelperImage standardLine;
    private readonly ModHelperImage advancedLine;

    /// <param name="row">An empty row of the section, as wide as the others</param>
    /// <param name="picked">Called with true when Advanced is clicked and false for Standard</param>
    public ModeTabs(GameObject row, Action<bool> picked)
    {
        Row = row;
        var rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, Height);

        // The info button stands at the row's left edge, so the tabs share the rest of it
        var left = 180f;
        var width = (rect.sizeDelta.x - left - 60) / 2;
        (Standard, standardLine) = Tab(row, "Standard", left + width / 2, width, Texts.ModeStandard.T(), () => picked(false));
        (Advanced, advancedLine) = Tab(row, "Advanced", left + width * 1.5f, width, Texts.ModeAdvanced.T(), () => picked(true));
        Show(false);
    }

    private static (Button, ModHelperImage) Tab(GameObject row, string name, float x, float width, string label,
        Action click)
    {
        var tab = ModHelperPanel.Create(new Info(name, x, 0, width, Height)
        {
            Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        });
        tab.transform.SetParent(row.transform, false);

        // Only the text and the line show: the image is see-through, there only to catch the click. A plain button,
        // since Mod Helper's buttons tint and scale their image themselves.
        var hit = tab.gameObject.AddComponent<Image>();
        hit.color = new Color(1, 1, 1, 0);
        var button = tab.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(new Action(click));

        var text = tab.AddText(new Info("Label", 0, LineHeight, width - 40, Height - LineHeight * 2), label, 80);
        text.Text.enableAutoSizing = true;
        text.Text.fontSizeMin = 40;
        text.Text.fontSizeMax = 80;
        text.Text.raycastTarget = false;

        var line = tab.AddImage(new Info("Line", 0, 0, width, LineHeight)
        {
            Anchor = new Vector2(0.5f, 0), Pivot = new Vector2(0.5f, 0)
        }, VanillaSprites.SmallSquareWhiteGradient);
        line.Image.raycastTarget = false;
        return (button, line);
    }

    /// <summary>
    /// Shows which tab is picked
    /// </summary>
    public void Show(bool advanced)
    {
        AdvancedPicked = advanced;
        Paint(Standard, standardLine, !advanced);
        Paint(Advanced, advancedLine, advanced);
    }

    private static void Paint(Button tab, ModHelperImage line, bool picked)
    {
        var color = picked ? Picked : NotPicked;
        tab.GetComponentInChildren<TMP_Text>().color = color;
        line.Image.color = color;
        line.RectTransform.sizeDelta = new Vector2(line.RectTransform.sizeDelta.x, picked ? LineHeight : ThinLineHeight);
    }

    /// <summary>
    /// A row with nothing in it, as wide as a slider row
    /// </summary>
    public static GameObject EmptyRow(Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor.ChallengeEditorSliderPanel template,
        Transform parent, string name)
    {
        var row = EditorRows.LabelRow(template, parent, name, "", VanillaSprites.BossChallenge);
        foreach (var child in new[] {"Lbl", "Image"})
        {
            var transform = row.transform.Find(child);
            if (transform != null) Object.DestroyImmediate(transform.gameObject);
        }

        return row;
    }
}
