using System;
using System.Globalization;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// How the skull thresholds of the boss should be determined
/// </summary>
public enum SkullMode
{
    /// <summary>Keep the skulls the boss normally has</summary>
    Vanilla,

    /// <summary>Place a count of skulls evenly across the health bar</summary>
    EvenlySpaced,

    /// <summary>Use the comma separated percentages in a list</summary>
    Custom
}

/// <summary>
/// Pure helpers for turning skull settings into health percentage thresholds
/// </summary>
public static class SkullMath
{
    public const int MaxSkulls = 10;

    /// <summary>
    /// The skulls a new setup has for Evenly Spaced and Custom
    /// </summary>
    public const int DefaultCount = 3;
    public const string DefaultCustom = "75, 50, 25";

    /// <summary>
    /// The thresholds a skull mode gives, or null for <see cref="SkullMode.Vanilla"/> (and for a Custom list without a
    /// single valid value), which keeps the boss's own
    /// </summary>
    public static float[]? Percents(SkullMode mode, int count, string? custom) => mode switch
    {
        SkullMode.EvenlySpaced => EvenlySpaced(count),
        SkullMode.Custom => ParseCustom(custom),
        _ => null
    };

    /// <summary>
    /// Evenly spaced thresholds, e.g. 3 -> [0.75, 0.5, 0.25]. 0 skulls gives an empty array.
    /// </summary>
    public static float[] EvenlySpaced(int count)
    {
        count = Math.Clamp(count, 0, MaxSkulls);
        return Enumerable.Range(1, count)
            .Select(i => (float) Math.Round(1 - i / (double) (count + 1), 4))
            .ToArray();
    }

    /// <summary>
    /// Parses a list like "75, 50,25" into descending thresholds [0.75, 0.5, 0.25].
    /// Values outside 1-99, duplicates and junk are ignored. Returns null when nothing valid remains.
    /// </summary>
    public static float[]? ParseCustom(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // A list split by ; may use decimal commas, as typed in Norwegian or German: "33,3; 66,6" is 33.3 and 66.6 (#187)
        if (text.Contains(';')) text = DecimalComma.Replace(text, ".");

        var values = text
            .Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().TrimEnd('%'))
            .Select(s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : float.NaN)
            .Where(f => !float.IsNaN(f) && f >= 1 && f <= 99)
            .Select(f => (float) Math.Round(f / 100.0, 4))
            .Distinct()
            .OrderByDescending(f => f)
            .Take(MaxSkulls)
            .ToArray();

        return values.Length == 0 ? null : values;
    }

    /// <summary>A comma with a digit on both sides</summary>
    private static readonly System.Text.RegularExpressions.Regex DecimalComma =
        new("(?<=[0-9]),(?=[0-9])", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Thresholds as the player writes them, e.g. [0.5, 0.335] -> "50, 33.5"
    /// </summary>
    public static string FormatCustom(float[] thresholds) =>
        string.Join(", ", thresholds.Select(f => Math.Round(f * 100.0, 2).ToString("0.##", CultureInfo.InvariantCulture)));

    /// <summary>
    /// The list as it is understood, e.g. "25,25,50" -> "50, 25", so the player sees duplicates and junk dropped.
    /// A list without a single valid value gives <paramref name="fallback"/> instead.
    /// </summary>
    public static string NormalizeCustom(string? text, string fallback) =>
        ParseCustom(text) is { } values ? FormatCustom(values) : fallback;

    /// <summary>
    /// A boss's values that go round, made long enough for <paramref name="count"/>: the values again from the start
    /// for every entry past the end, e.g. Dreadbloon's immunities Primary, Military, Magic, Support, Primary, ...
    /// Never shorter than the boss's own list, and empty stays empty.
    /// </summary>
    public static T[] Cycle<T>(T[] values, int count) =>
        values.Length == 0 ? values : Enumerable.Range(0, Math.Max(count, values.Length)).Select(i => values[i % values.Length]).ToArray();

    /// <summary>
    /// A boss's values that grow by a step per skull, made long enough for <paramref name="count"/>: past the end
    /// they keep growing by the last step, e.g. Phayze's 1.1, 1.15, 1.2 go on 1.25, 1.3, as on Elite Phayze. Never
    /// shorter than the boss's own list; a single value is repeated, and empty stays empty.
    /// </summary>
    public static float[] ContinueSteps(float[] values, int count)
    {
        if (values.Length == 0 || count <= values.Length) return values;
        var step = values.Length > 1 ? values[^1] - values[^2] : 0;
        return values.Concat(Enumerable.Range(1, count - values.Length)
            .Select(i => (float) Math.Round(values[^1] + step * i, 4))).ToArray();
    }

    /// <summary>
    /// A skull's health percentage, e.g. 0.3333 -> "33.33%"
    /// </summary>
    public static string FormatPercent(float percent) =>
        Math.Round(percent * 100.0, 2).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
