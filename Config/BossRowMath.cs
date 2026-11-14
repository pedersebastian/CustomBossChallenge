using System;

namespace CustomBossChallenge.Config;

/// <summary>
/// Where the Boss Type row puts its boss buttons, the Elite tick and its label, measured from the left edge of the
/// row's control area. Seven bosses get the full size; when a game update adds more, the buttons get smaller so the
/// Elite label still ends inside the control area, before the row's reset button (#170).
/// </summary>
public static class BossRowMath
{
    /// <summary>The first boss button's center</summary>
    public const float FirstX = 100;

    /// <summary>From one boss button's center to the next at full size</summary>
    public const float MaxStep = 215;

    /// <summary>A boss button's size as a share of the step, so the gap between them shrinks with them</summary>
    public const float ButtonShare = 190f / MaxStep;

    /// <summary>The Elite tick's center and the Elite label's center, after the last boss button's step</summary>
    public const float EliteX = 30;
    public const float LabelX = 230;
    public const float LabelWidth = 260;

    /// <summary>From one boss button's center to the next, for <paramref name="bosses"/> bosses in a row this wide</summary>
    public static float Step(int bosses, float width) =>
        Math.Min(MaxStep, (width - FirstX - LabelX - LabelWidth / 2) / Math.Max(bosses, 1));

    /// <summary>A boss button's width and height</summary>
    public static float ButtonSize(int bosses, float width) => Step(bosses, width) * ButtonShare;

    /// <summary>The boss button's center for boss number <paramref name="index"/>, counting from 0</summary>
    public static float ButtonX(int index, int bosses, float width) => FirstX + index * Step(bosses, width);

    /// <summary>Where the Elite tick's center is</summary>
    public static float EliteTickX(int bosses, float width) => ButtonX(bosses, bosses, width) + EliteX;

    /// <summary>Where the Elite label's center is</summary>
    public static float EliteLabelX(int bosses, float width) => ButtonX(bosses, bosses, width) + LabelX;

    /// <summary>Where the Elite label ends on the right</summary>
    public static float RightEnd(int bosses, float width) => EliteLabelX(bosses, width) + LabelWidth / 2;
}
