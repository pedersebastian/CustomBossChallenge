using System;
using System.Globalization;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// Pure helpers for turning skull settings into health percentage thresholds
/// </summary>
public static class SkullMath
{
    public const int MaxSkulls = 10;

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
}
