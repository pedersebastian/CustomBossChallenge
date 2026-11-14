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

    /// <summary>
    /// Moves one step left or right, like the row's arrow buttons
    /// </summary>
    public void Step(int direction) => Set(Value + direction * step);

    /// <summary>
    /// Goes back to the default, like the row's reset button
    /// </summary>
    public void Reset() => Set(Default);

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
            // Dragging lands on the step too, like the arrows, Random and the share codes (#171)
            var snapped = Mathf.Clamp(min + Mathf.Round((value - min) / step) * step, min, max);
            if (!Mathf.Approximately(snapped, value)) slider.SetValueWithoutNotify(snapped);
            valueText.text = format(snapped);
            changed(snapped);
        }));

        left.onClick = new Button.ButtonClickedEvent();
        left.onClick.AddListener(new Action(() => result.Set(result.Value - step)));
        right.onClick = new Button.ButtonClickedEvent();
        right.onClick.AddListener(new Action(() => result.Set(result.Value + step)));
        reset.onClick = new Button.ButtonClickedEvent();
        reset.onClick.AddListener(new Action(result.Reset));

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
    /// A round reset button at the end of a row, where the slider rows have theirs
    /// </summary>
    public static ModHelperButton ResetButton(GameObject row, string name, Action action)
    {
        var button = ModHelperButton.Create(new Info(name, -132, 0, 180, 180)
        {
            Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.RestartBtn, action);
        button.transform.SetParent(row.transform, false);
        return button;
    }

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

    /// <summary>How big the i next to a choice is</summary>
    public const float ChoiceInfoSize = 120;

    /// <summary>
    /// Adds an info button on <paramref name="row"/> at <paramref name="x"/> (from the row's right edge, like
    /// <see cref="ControlInfo"/>) that explains a choice of the row's control in more detail than the row's own i,
    /// like what each CHIMPS round set tests (#157)
    /// </summary>
    public static ModHelperButton AddChoiceInfo(GameObject row, string name, float x, string titleKey, string textKey)
    {
        var button = ModHelperButton.Create(new Info(name, x, 0, ChoiceInfoSize, ChoiceInfoSize)
        {
            Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.InfoBtn2, new Action(() => ShowInfo(titleKey, textKey)));
        button.transform.SetParent(row.transform, false);
        return button;
    }

    /// <summary>The width of the frame around an opened dropdown list</summary>
    public const float ListFrame = 8;

    /// <summary>The radius of the opened list's rounded corners</summary>
    public const int ListFrameRadius = 28;

    /// <summary>The name of the frame's object, the last child of the dropdown's list</summary>
    public const string ListFrameName = "ListFrame";

    private static Sprite? listFillSprite;
    private static Sprite? listFrameSprite;

    /// <summary>
    /// Rounded corners and a white frame for the dropdown's opened list, so it stands out from the rows behind it. The
    /// list masks its entries with its own shape, so the entries get the rounded corners too and the frame lies just
    /// inside the edge.
    /// </summary>
    public static void FrameList(ModHelperDropdown dropdown)
    {
        var list = dropdown.TemplatePanel.gameObject;
        // The game's canvas counts far fewer pixels per unit than a sprite's 100, which would shrink the corners to
        // a few units: this gives them their size in units
        var unitScale = (list.GetComponentInParent<Canvas>()?.referencePixelsPerUnit ?? 100) / 100f;
        var background = list.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = listFillSprite ??= RoundedSprite(false);
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = unitScale;
        }

        var frame = new GameObject(ListFrameName);
        frame.transform.SetParent(list.transform, false);
        var rect = frame.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = frame.AddComponent<Image>();
        image.sprite = listFrameSprite ??= RoundedSprite(true);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = unitScale;
        image.color = Color.white;
        image.raycastTarget = false;
        frame.transform.SetAsLastSibling();

        // The entries keep clear of the frame
        var text = dropdown.Dropdown.itemText;
        if (text != null) text.margin = new Vector4(ListFrame * 3, 0, ListFrame * 3, 0);
    }

    /// <summary>
    /// A white square with rounded corners, or only its <see cref="ListFrame"/> wide edge, sliced so the corners keep
    /// their shape at any size
    /// </summary>
    private static Sprite RoundedSprite(bool ring)
    {
        const int size = ListFrameRadius * 2 + 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        var half = size / 2f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var point = new Vector2(x + 0.5f - half, y + 0.5f - half);
            var alpha = Mathf.Clamp01(0.5f - RoundedBox(point, half, ListFrameRadius));
            if (ring) alpha -= Mathf.Clamp01(0.5f - RoundedBox(point, half - ListFrame, ListFrameRadius - ListFrame));
            texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(alpha)));
        }
        texture.Apply();
        var border = ListFrameRadius + 1;
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = ring ? "CbcListFrame" : "CbcListFill";
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    /// <summary>How far <paramref name="point"/> is outside a square of half size <paramref name="half"/> with rounded corners</summary>
    private static float RoundedBox(Vector2 point, float half, float radius)
    {
        var qx = Mathf.Abs(point.x) - half + radius;
        var qy = Mathf.Abs(point.y) - half + radius;
        return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
    }

    /// <summary>Where the right edge of a control made with <see cref="ControlInfo"/> and no offset is</summary>
    public static float ControlRight(float width) => ControlX - (ControlWidth - width) / 2 + width / 2;

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
