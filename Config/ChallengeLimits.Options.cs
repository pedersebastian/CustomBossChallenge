using System.Collections.Generic;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// The part of <see cref="ChallengeLimits"/> that doesn't use the game, also built in Shared/ for the save format tests
/// </summary>
public static partial class ChallengeLimits
{
    public const int MaxNameLength = 60;

    /// <summary>
    /// The boss's speed and health multipliers, the range of Boss Speed and Boss Health. 25× is the fastest boss
    /// Ninja Kiwi has run (Bloonarius 61, #220). 50× health takes even the weakest Elite tier 5 (Dreadbloon's) past
    /// 500 million (#250); the game's health limit holds every tier below it (TierMath.MaxModelHealth).
    /// </summary>
    public const float MinBossSpeed = 0.1f;
    public const float MaxBossSpeed = 25;
    public const float MinBossHealth = 0.05f;
    public const float MaxBossHealth = 50;

    /// <summary>
    /// The boss options <see cref="BossOptions.Sanitized"/> changed, e.g. "Tiers 50 -> 10"
    /// </summary>
    public static List<string> OptionChanges(BossOptions before, BossOptions after) =>
        typeof(BossOptions).GetProperties()
            .Where(property => property.CanWrite)
            .Select(property => (property.Name, Before: property.GetValue(before), After: property.GetValue(after)))
            .Where(change => !Equals(change.Before, change.After))
            .Select(change => $"{change.Name} {change.Before} -> {change.After}")
            .ToList();

    /// <summary>
    /// A name without line breaks or other control characters, and not longer than <see cref="MaxNameLength"/>
    /// </summary>
    public static string CleanName(string? name, string fallback)
    {
        var clean = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length > MaxNameLength) clean = clean[..MaxNameLength].TrimEnd();
        return clean.Length > 0 ? clean : fallback;
    }
}
