using System;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomBossChallenge.Menus;

/// <summary>
/// One control of the Boss section that the keyboard can move to: a text field gets the keyboard itself, the others
/// are changed with the arrows (<see cref="Step"/>) or pressed with Enter or Space (<see cref="Activate"/>)
/// </summary>
public sealed class FocusStop
{
    public string Name { get; }

    /// <summary>
    /// What the focus frame goes around; the stop is skipped while it is hidden
    /// </summary>
    public Func<GameObject> Target { get; }

    public TMP_InputField? Field { get; init; }
    public Action<int>? Step { get; init; }
    public Action? Activate { get; init; }

    /// <summary>
    /// Whether the stop can be used, e.g. Load without saved setups can't
    /// </summary>
    public Func<bool> Enabled { get; init; } = () => true;

    public FocusStop(string name, Func<GameObject> target)
    {
        Name = name;
        Target = target;
    }

    public bool Available
    {
        get
        {
            var target = Target();
            return target != null && target.activeInHierarchy && Enabled();
        }
    }

    public static FocusStop ForSlider(string name, SliderRow row) =>
        new(name, () => row.Row) {Step = row.Step};

    public static FocusStop ForButton(string name, Button button) =>
        new(name, () => button.gameObject)
        {
            Activate = () => button.onClick.Invoke(),
            Enabled = () => button.interactable
        };

    public static FocusStop ForField(string name, TMP_InputField field) =>
        new(name, () => field.gameObject) {Field = field};

    /// <summary>
    /// Left and right pick the previous or next option, without opening the list
    /// </summary>
    public static FocusStop ForDropdown(string name, TMP_Dropdown dropdown) =>
        new(name, () => dropdown.gameObject)
        {
            Step = direction => dropdown.value = Math.Clamp(dropdown.value + direction, 0, Math.Max(dropdown.options.Count - 1, 0)),
            Enabled = () => dropdown.interactable
        };
}

/// <summary>
/// The frame around the control that has the keyboard. It is made of four bars so it needs no sprite, and it is put
/// inside the control so it moves and hides with it. It goes around everything the control shows, such as a slider
/// row's value above it and its info button, not just the control's own box.
/// </summary>
public static class FocusFrame
{
    public const string Name = "CustomBossKeyboardFocus";
    private const float Thickness = 10;
    private const float Margin = 24;
    private static readonly Color Color = new(1f, 0.82f, 0.15f);

    private static GameObject? frame;

    public static GameObject? Current => frame != null && frame.activeSelf ? frame : null;

    public static void Show(GameObject target)
    {
        if (frame == null) frame = Build();
        frame.transform.SetParent(target.transform, false);
        frame.transform.SetAsLastSibling();
        frame.SetActive(true);
        Fit();
    }

    public static void Hide()
    {
        if (frame != null) frame.SetActive(false);
    }

    /// <summary>
    /// Sizes the frame to what its control shows now; values and texts change width while it is shown
    /// </summary>
    public static void Fit()
    {
        var target = Current?.transform.parent?.GetComponent<RectTransform>();
        if (target == null) return;

        var (min, max) = Bounds(target, target) ?? (target.rect.min, target.rect.max);
        var top = max.y + Margin;
        var bottom = min.y - Margin;

        // A slider's value stands in the gap above its row, so the room around the frame goes at most halfway to the
        // rows above and below, and frames of neighbouring rows meet in the middle instead of covering each other
        var row = RowOf(target);
        if (row != null)
        {
            if (Bounds(Neighbour(row, -1), target) is var (_, above)) top = Math.Min(top, (max.y + above.y) / 2);
            if (Bounds(Neighbour(row, 1), target) is var (below, _)) bottom = Math.Max(bottom, (min.y + below.y) / 2);
        }

        var frameRect = frame!.GetComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = target.pivot;
        frameRect.offsetMin = new Vector2(min.x - Margin, bottom);
        frameRect.offsetMax = new Vector2(max.x + Margin, top);
    }

    /// <summary>
    /// The box around everything <paramref name="root"/> shows, in <paramref name="space"/>'s coordinates, or null
    /// when it shows nothing. The frame itself doesn't count.
    /// </summary>
    private static (Vector2 Min, Vector2 Max)? Bounds(Transform? root, RectTransform space)
    {
        if (root == null) return null;

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var graphic in root.GetComponentsInChildren<Graphic>())
        {
            if (!graphic.enabled || graphic.color.a < 0.01f || frame != null && graphic.transform.IsChildOf(frame.transform)) continue;

            // A text's box is much taller than its letters, so its letters count instead
            var rect = graphic.rectTransform;
            var text = graphic.TryCast<TMP_Text>();
            var box = text != null && text.textBounds.size.x > 0
                ? new Rect(text.textBounds.min, text.textBounds.size)
                : rect.rect;
            foreach (var corner in new[] {box.min, box.max, new Vector2(box.xMin, box.yMax), new Vector2(box.xMax, box.yMin)})
            {
                Vector2 local = space.InverseTransformPoint(rect.TransformPoint(corner));
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
        }

        return min.x > max.x ? null : (min, max);
    }

    /// <summary>
    /// The row of the Boss section that <paramref name="target"/> is in or is, or null outside the rows (Play)
    /// </summary>
    private static Transform? RowOf(Transform target)
    {
        for (var current = target; current.parent != null; current = current.parent)
        {
            if (current.parent.name == BossEditorSection.RowsName) return current;
        }

        return null;
    }

    /// <summary>
    /// The shown row before (-1) or after (1) <paramref name="row"/>
    /// </summary>
    private static Transform? Neighbour(Transform row, int direction)
    {
        var parent = row.parent;
        for (var i = row.GetSiblingIndex() + direction; i >= 0 && i < parent.childCount; i += direction)
        {
            var sibling = parent.GetChild(i);
            if (sibling.gameObject.activeSelf) return sibling;
        }

        return null;
    }

    private static GameObject Build()
    {
        var root = new GameObject(Name, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        root.AddComponent<LayoutElement>().ignoreLayout = true;

        Bar(root, "Top", new Vector2(0, 1), Vector2.one, new Vector2(0, -Thickness), Vector2.zero);
        Bar(root, "Bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, Thickness));
        Bar(root, "Left", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(Thickness, 0));
        Bar(root, "Right", new Vector2(1, 0), Vector2.one, new Vector2(-Thickness, 0), Vector2.zero);
        return root;
    }

    private static void Bar(GameObject root, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var bar = new GameObject(name, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        bar.transform.SetParent(root.transform, false);
        Stretch(bar.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        var image = bar.AddComponent<Image>();
        image.color = Color;
        image.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
