using System;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2Cpp;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// A copy of one of the Challenge Editor's slider rows, fully owned by this mod: the vanilla row component is removed
/// and all events are replaced, so the copy never writes to the editor's own settings
/// </summary>
public sealed class SliderRow
{
    public GameObject Row { get; }
    public Slider Slider { get; }
    public float Default { get; }

    private readonly TMP_Text valueText;
    private readonly float step;
    private readonly Func<float, string> format;
    private readonly Action<float> changed;

    private SliderRow(GameObject row, Slider slider, TMP_Text valueText, float step, float defaultValue,
        Func<float, string> format, Action<float> changed)
    {
        Row = row;
        Slider = slider;
        this.valueText = valueText;
        this.step = step;
        Default = defaultValue;
        this.format = format;
        this.changed = changed;
    }

    public float Value => Slider.value;

    /// <summary>
    /// Shows a value without reporting it as a change
    /// </summary>
    public void Show(float value)
    {
        Slider.SetValueWithoutNotify(Mathf.Clamp(value, Slider.minValue, Slider.maxValue));
        valueText.text = format(Slider.value);
    }

    private void Set(float value)
    {
        Show(value);
        changed(Slider.value);
    }

    public static SliderRow Clone(ChallengeEditorSliderPanel template, Transform parent, string name, string label,
        string icon, float min, float max, float step, float defaultValue, Func<float, string> format,
        Action<float> changed)
    {
        var row = EditorRows.CloneRow(template, parent, name, label, icon, out var panel);
        var slider = panel.slider;
        var valueText = panel.valueTxt;
        var left = panel.leftBtn;
        var right = panel.rightBtn;
        var reset = panel.defaultBtn;
        var notch = panel.defNotch;
        Object.DestroyImmediate(panel);

        slider.onValueChanged = new Slider.SliderEvent();
        slider.wholeNumbers = true;
        slider.minValue = min;
        slider.maxValue = max;

        var result = new SliderRow(row, slider, valueText, step, defaultValue, format, changed);
        slider.onValueChanged.AddListener(new Action<float>(value =>
        {
            valueText.text = format(value);
            changed(value);
        }));

        left.onClick = new Button.ButtonClickedEvent();
        left.onClick.AddListener(new Action(() => result.Set(result.Value - step)));
        right.onClick = new Button.ButtonClickedEvent();
        right.onClick.AddListener(new Action(() => result.Set(result.Value + step)));
        reset.onClick = new Button.ButtonClickedEvent();
        reset.onClick.AddListener(new Action(() => result.Set(defaultValue)));

        if (notch != null)
        {
            var notchRect = notch.GetComponent<RectTransform>();
            var normalized = (defaultValue - min) / (max - min);
            notchRect.anchorMin = notchRect.anchorMax = new Vector2(normalized, 0.5f);
            notchRect.anchoredPosition = new Vector2(0, notchRect.anchoredPosition.y);
        }

        result.Show(defaultValue);
        return result;
    }
}

/// <summary>
/// Helpers for building rows that look like the Challenge Editor's own
/// </summary>
public static class EditorRows
{
    /// <summary>
    /// Where the slider sits in a row, used to place other controls in the same column
    /// </summary>
    public const float ControlX = -1328;
    public const float ControlWidth = 1990;
    public const float RowHeight = 200;

    /// <summary>
    /// Copies a slider row, relabels it and gives it a new icon. The vanilla row component is returned so the caller
    /// can take what it needs before removing it.
    /// </summary>
    internal static GameObject CloneRow(ChallengeEditorSliderPanel template, Transform parent, string name, string label,
        string icon, out ChallengeEditorSliderPanel panel)
    {
        var row = Object.Instantiate(template.gameObject, parent, false);
        row.name = name;
        row.SetActive(true);
        panel = row.GetComponent<ChallengeEditorSliderPanel>();

        SetText(row.transform.Find("Lbl")?.GetComponent<TMP_Text>(), label);
        row.transform.Find("Image")?.GetComponent<Image>()?.SetSprite(icon);
        return row;
    }

    /// <summary>
    /// A row with only the label and icon of a slider row, for custom controls
    /// </summary>
    public static GameObject LabelRow(ChallengeEditorSliderPanel template, Transform parent, string name, string label,
        string icon)
    {
        var row = CloneRow(template, parent, name, label, icon, out var panel);
        Object.DestroyImmediate(panel);
        foreach (var child in new[] {"LeftBtn", "RightBtn", "DefaultBtn", "Slider"})
        {
            var transform = row.transform.Find(child);
            if (transform != null) Object.DestroyImmediate(transform.gameObject);
        }

        return row;
    }

    /// <summary>
    /// Sets the text of a (possibly localized) game text without the localizer changing it back
    /// </summary>
    public static void SetText(TMP_Text? text, string value)
    {
        if (text == null) return;
        var nkText = text.TryCast<NK_TextMeshProUGUI>();
        if (nkText != null)
        {
            nkText.AutoLocalize = false;
            nkText.localizeKey = "";
        }

        text.text = value;
    }

    /// <summary>
    /// Adds a small info button on the row's icon that explains the option in a popup. Clicking the icon works too.
    /// </summary>
    public static ModHelperButton AddInfo(GameObject row, string titleKey, string textKey)
    {
        var action = new Action(() => ShowInfo(titleKey, textKey));
        var icon = row.transform.Find("Image");
        var button = ModHelperButton.Create(new Info(InfoName, 0, 0, 100, 100)
        {
            Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.InfoBtn2, action);
        button.transform.SetParent(icon != null ? icon : row.transform, false);
        if (icon != null)
        {
            // Top right corner of the icon
            var rect = button.RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-15, -15);
            var iconButton = icon.gameObject.GetComponent<Button>() ?? icon.gameObject.AddComponent<Button>();
            iconButton.onClick = new Button.ButtonClickedEvent();
            iconButton.onClick.AddListener(action);
        }

        return button;
    }

    public const string InfoName = "Info";

    public static void ShowInfo(string titleKey, string textKey) =>
        PopupScreen.instance.SafelyQueue(screen =>
            screen.ShowOkPopup($"<b>{titleKey.T()}</b>\n\n{textKey.T()}", null));

    /// <summary>
    /// Info for a control placed where a row's slider would be
    /// </summary>
    public static Info ControlInfo(string name, float width = ControlWidth, float height = 160, float offset = 0) =>
        new(name, ControlX - (ControlWidth - width) / 2 + offset, 0, width, height)
        {
            Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        };
}
