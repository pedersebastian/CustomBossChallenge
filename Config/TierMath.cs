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
    public const int MinSpeedPerTier = -10;
    public const int MaxSpeedPerTier = 50;
    public const int MinHealthPerTier = -10;
    public const int MaxHealthPerTier = 200;
    public const int PerTierStep = 5;

    /// <summary>
    /// The game only has tiers 1-5; each tier above that has this much more health than the one before it
    /// </summary>
    public const double ExtraTierStep = 1.5;

    /// <summary>
    /// No tier gets more than this much faster than the game makes it, however many tiers there are
    /// </summary>
    public const double MaxSpeedFactor = 5;

    /// <summary>
    /// Later tiers never drop below this share of the game's speed or health, however low the setting
    /// </summary>
    public const double MinPerTierFactor = 0.1;

    /// <summary>
    /// The most health a boss can have in a game. Boss models store health as an int that the game multiplies by Boss
    /// Health (through a float) when the match starts; above about 2.147 billion the boss gets 1 health or less.
    /// BossHealthLimitTest checks that this much still works.
    /// </summary>
    public const int MaxGameHealth = 2_000_000_000;

    /// <summary>
    /// The most health a boss model can have so that Boss Health doesn't take it past <see cref="MaxGameHealth"/>
    /// </summary>
    public static int MaxModelHealth(double bossHealth) =>
        (int) Math.Floor(MaxGameHealth / Math.Max(bossHealth, 1));

    public static string BossId(BossType boss, bool elite, int tier) => $"{boss}{(elite ? "Elite" : "")}{tier}";

    /// <summary>
    /// The tier in a boss bloon id like "VortexElite7", or 0 if it has none
    /// </summary>
    public static int TierOf(string? bossId)
    {
        if (string.IsNullOrEmpty(bossId)) return 0;
        var digits = new string(bossId.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var tier) ? tier : 0;
    }

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
            // Leave a round for every later tier below the cap, so no two tiers share a round
            var latest = MaxRound - (tiers - 1 - i);
            result[i] = Math.Min(Math.Max(value, previous + 1), latest);
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

    /// <summary>
    /// Converts rounds as the player sees them (starting at 1) into the game's internal rounds (starting at 0)
    /// </summary>
    public static int[] ToGameRounds(IEnumerable<int> rounds) => rounds.Select(r => Math.Max(r - 1, 0)).ToArray();

    public static string FormatRounds(IEnumerable<int> rounds) => string.Join(", ", rounds);

    /// <summary>
    /// How much faster a tier is than the game makes it: <paramref name="percentPerTier"/> of its speed added for
    /// every tier after the first, so +20 % gives tier 3 ×1.4. At most <see cref="MaxSpeedFactor"/>.
    /// </summary>
    public static double SpeedFactor(int tier, int percentPerTier) =>
        Math.Min(PerTierFactor(tier, percentPerTier), MaxSpeedFactor);

    /// <summary>
    /// How much more health a tier has than the game would give it: <paramref name="percentPerTier"/> of its health
    /// added for every tier after the first, so +100 % gives tier 5 ×5
    /// </summary>
    public static double HealthFactor(int tier, int percentPerTier) => PerTierFactor(tier, percentPerTier);

    /// <summary>
    /// 1 + percent × (tier - 1), never below <see cref="MinPerTierFactor"/>
    /// </summary>
    private static double PerTierFactor(int tier, int percentPerTier) =>
        Math.Max(1 + percentPerTier / 100.0 * Math.Max(tier - 1, 0), MinPerTierFactor);

    /// <summary>
    /// Health of a tier relative to the game's tier of the same number, or to tier 5 above that: tiers above 5 get
    /// <see cref="ExtraTierStep"/> per tier, like the game's own steps
    /// </summary>
    public static double ExtraTierFactor(int tier) => tier <= VanillaTiers ? 1 : Math.Pow(ExtraTierStep, tier - VanillaTiers);

    public static int ClampSpeedPerTier(int percent) => Math.Clamp(percent, MinSpeedPerTier, MaxSpeedPerTier);

    public static int ClampHealthPerTier(int percent) => Math.Clamp(percent, MinHealthPerTier, MaxHealthPerTier);

    /// <summary>
    /// The Health per tier that gives tier 6 the health the old Extra Tier Health setting gave it, rounded to the
    /// slider's step. Tiers 2-5 now get it too, which the old setting didn't do.
    /// </summary>
    public static int HealthPerTierFromExtraTierHealth(int extraTierHealth)
    {
        // Tier 6 used to be tier 5 x Extra Tier Health; now it is tier 5 x 1.5 x (1 + 5 x Health per tier)
        var percent = (extraTierHealth / (ExtraTierStep * 100) - 1) / VanillaTiers * 100;
        return ClampHealthPerTier((int) Math.Round(percent / PerTierStep) * PerTierStep);
    }

    /// <summary>
    /// <paramref name="health"/> times <paramref name="factor"/>, held to what the game can take with
    /// <paramref name="bossHealth"/> on top
    /// </summary>
    public static int ScaleHealth(int health, double factor, double bossHealth = 1) =>
        (int) Math.Clamp(Math.Round(health * factor), 1, MaxModelHealth(bossHealth));
}
