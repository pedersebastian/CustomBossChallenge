using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Il2CppAssets.Scripts.Data.Boss;

namespace CustomBossChallenge.Config;

/// <summary>
/// Pure helpers for boss tiers and the rounds they spawn on
/// </summary>
public static class TierMath
{
    public const int VanillaTiers = 5;
    public const int MinTiers = 1;
    public const int MaxTiers = 10;
    public const int FirstSpawnRound = 40;
    public const int RoundsBetweenTiers = 20;
    public const int MaxRound = 1000;

    /// <summary>
    /// Boss models store health as an int, and boss health multipliers are applied on top of it
    /// </summary>
    public const int MaxModelHealth = int.MaxValue / 32;

    public static string BossId(BossType boss, bool elite, int tier) => $"{boss}{(elite ? "Elite" : "")}{tier}";

    /// <summary>
    /// The vanilla spawn rounds 40, 60, 80, 100, 120, continued every 20 rounds
    /// </summary>
    public static int[] DefaultSpawnRounds(int tiers) =>
        Enumerable.Range(0, ClampTiers(tiers)).Select(i => FirstSpawnRound + i * RoundsBetweenTiers).ToArray();

    public static int ClampTiers(int tiers) => Math.Clamp(tiers, MinTiers, MaxTiers);

    /// <summary>
    /// Exactly <paramref name="tiers"/> strictly ascending spawn rounds. Values from <paramref name="custom"/> are used
    /// where valid; missing ones continue 20 rounds after the previous tier.
    /// </summary>
    public static int[] SpawnRounds(int tiers, string? custom)
    {
        tiers = ClampTiers(tiers);
        var given = ParseRounds(custom);
        var result = new int[tiers];
        for (var i = 0; i < tiers; i++)
        {
            var previous = i == 0 ? 0 : result[i - 1];
            var value = i < given.Count ? given[i] : i == 0 ? FirstSpawnRound : previous + RoundsBetweenTiers;
            result[i] = Math.Min(Math.Max(value, previous + 1), MaxRound);
        }

        return result;
    }

    /// <summary>
    /// Positive whole numbers from a list like "40, 60;80", in the given order
    /// </summary>
    public static List<int> ParseRounds(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? new List<int>()
            : text.Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : 0)
                .Where(r => r > 0)
                .ToList();

    public static string FormatRounds(IEnumerable<int> rounds) => string.Join(", ", rounds);

    /// <summary>
    /// Health of a tier relative to tier 5: 1 up to tier 5, then multiplied by <paramref name="percentPerTier"/> per tier
    /// </summary>
    public static double HealthFactor(int tier, int percentPerTier) =>
        tier <= VanillaTiers ? 1 : Math.Pow(Math.Max(percentPerTier, 1) / 100.0, tier - VanillaTiers);

    public static int ScaleHealth(int health, double factor) =>
        (int) Math.Clamp(Math.Round(health * factor), 1, MaxModelHealth);
}
