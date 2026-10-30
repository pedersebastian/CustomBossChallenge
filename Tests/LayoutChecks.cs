using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Where UI elements end up on the screen, to check that they don't overlap or stick out on any screen format
/// </summary>
internal static class LayoutChecks
{
    /// <summary>
    /// How far elements may overlap or stick out, in screen pixels, so rounding never fails a check
    /// </summary>
    public const float Tolerance = 1;

    private static readonly Il2CppStructArray<Vector3> Corners = new(4);

    /// <summary>
    /// The rectangle <paramref name="rect"/> covers on the screen, in pixels from the bottom left
    /// </summary>
    public static Rect ScreenRect(RectTransform rect)
    {
        rect.GetWorldCorners(Corners);
        var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
        var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var min = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(camera, Corners[2]);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    public static Rect ScreenRect(Component component) => ScreenRect(component.GetComponent<RectTransform>());

    public static Rect Screen => new(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height);

    public static bool Overlap(Rect a, Rect b) =>
        a.xMin < b.xMax - Tolerance && b.xMin < a.xMax - Tolerance &&
        a.yMin < b.yMax - Tolerance && b.yMin < a.yMax - Tolerance;

    public static bool Inside(Rect inner, Rect outer) =>
        inner.xMin >= outer.xMin - Tolerance && inner.xMax <= outer.xMax + Tolerance &&
        inner.yMin >= outer.yMin - Tolerance && inner.yMax <= outer.yMax + Tolerance;

    public static bool InsideHorizontally(Rect inner, Rect outer) =>
        inner.xMin >= outer.xMin - Tolerance && inner.xMax <= outer.xMax + Tolerance;

    public static string Describe(Rect rect) => $"x {rect.xMin:0}-{rect.xMax:0}, y {rect.yMin:0}-{rect.yMax:0}";

    /// <summary>
    /// Every visible control on the screen that isn't part of <paramref name="ours"/>, on any screen space canvas, so
    /// buttons other mods add on their own canvases count too. Full screen click blockers are left out.
    /// </summary>
    public static IEnumerable<(string Name, Rect Rect)> OtherControls(Transform ours)
    {
        foreach (var control in Object.FindObjectsOfType<UnityEngine.UI.Selectable>())
        {
            if (!control.isActiveAndEnabled || control.transform.IsChildOf(ours)) continue;

            var canvas = control.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;

            var rect = ScreenRect(control);
            if (rect.width * rect.height > Screen.width * Screen.height / 4) continue;

            yield return ($"{canvas.name}/{control.name}", rect);
        }
    }
}
