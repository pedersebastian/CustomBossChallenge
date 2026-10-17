using System;
using System.Linq;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Config;

/// <summary>
/// Keeps a challenge read from a shared code or a file within values the game can play. The Challenge Editor's own
/// controls never go outside these limits; a hand made code could, and could then hang or crash the game.
/// </summary>
public static class ChallengeLimits
{
    public const int MaxNameLength = 60;
    public const float MinMultiplier = 0.01f;
    public const float MaxMultiplier = 100;
    public const float MinBossSpeed = 0.1f;
    public const float MaxBossSpeed = 10;
    public const float MinBossHealth = 0.1f;
    public const float MaxBossHealth = 20;
    public const int MaxCash = 100_000_000;
    public const int MaxLives = 1_000_000;
    public const int MaxRevives = 100;
    public const int MaxTowerCount = 1000;

    public static readonly string[] Difficulties = {"Easy", "Medium", "Hard"};

    /// <summary>
    /// The game modes the Challenge Editor offers
    /// </summary>
    public static readonly string[] Modes =
    {
        "Standard", "PrimaryOnly", "Deflation", "MilitaryOnly", "Apopalypse", "Reverse", "MagicOnly",
        "DoubleMoabHealth", "HalfCash", "AlternateBloonsRounds", "Impoppable", "Clicks"
    };

    /// <summary>
    /// Brings every value into range. Text that isn't a known map, difficulty or mode falls back to
    /// <paramref name="defaults"/>.
    /// </summary>
    public static void Sanitize(DailyChallengeModel dcm, DailyChallengeModel defaults)
    {
        dcm.name = CleanName(dcm.name, defaults.name);
        if (!IsKnownMap(dcm.map)) dcm.map = defaults.map;
        if (!Difficulties.Contains(dcm.difficulty)) dcm.difficulty = defaults.difficulty;
        if (!Modes.Contains(dcm.mode)) dcm.mode = defaults.mode;

        var modifiers = dcm.bloonModifiers ??= new BloonModifiers();
        modifiers.speedMultiplier = Clamp(modifiers.speedMultiplier, MinMultiplier, MaxMultiplier);
        modifiers.moabSpeedMultiplier = Clamp(modifiers.moabSpeedMultiplier, MinMultiplier, MaxMultiplier);
        modifiers.regrowRateMultiplier = Clamp(modifiers.regrowRateMultiplier, MinMultiplier, MaxMultiplier);
        modifiers.bossSpeedMultiplier = Clamp(modifiers.bossSpeedMultiplier, MinBossSpeed, MaxBossSpeed);
        var health = modifiers.healthMultipliers ??= new BloonHealthMultipliers();
        health.bloons = Clamp(health.bloons, MinMultiplier, MaxMultiplier);
        health.moabs = Clamp(health.moabs, MinMultiplier, MaxMultiplier);
        health.boss = Clamp(health.boss, MinBossHealth, MaxBossHealth);

        var rules = dcm.startRules ??= new StartRules();
        rules.cash = OrDefault(rules.cash, 0, MaxCash);
        rules.lives = OrDefault(rules.lives, 1, MaxLives);
        rules.maxLives = OrDefault(rules.maxLives, 1, MaxLives);
        rules.revives = OrDefault(rules.revives, 0, MaxRevives);
        rules.round = OrDefault(rules.round, 1, TierMath.MaxRound);
        rules.endRound = OrDefault(rules.endRound, 1, TierMath.MaxRound + TierMath.RoundsBetweenTiers);

        dcm.maxTowers = OrDefault(dcm.maxTowers, 0, MaxTowerCount);
        dcm.maxPowers = OrDefault(dcm.maxPowers, 0, MaxTowerCount);
        dcm.removeableCostMultiplier = Clamp(dcm.removeableCostMultiplier, 0, MaxMultiplier);
        dcm.abilityCooldownReductionMultiplier = Clamp(dcm.abilityCooldownReductionMultiplier, 0, MaxMultiplier);
        if (dcm.towers != null)
        {
            foreach (var tower in dcm.towers)
            {
                tower.max = OrDefault(tower.max, 0, MaxTowerCount);
            }
        }
    }

    /// <summary>
    /// A name without line breaks or other control characters, and not longer than <see cref="MaxNameLength"/>
    /// </summary>
    public static string CleanName(string? name, string fallback)
    {
        var clean = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length > MaxNameLength) clean = clean[..MaxNameLength].TrimEnd();
        return clean.Length > 0 ? clean : fallback;
    }

    private static bool IsKnownMap(string? map)
    {
        if (string.IsNullOrEmpty(map)) return false;
        var maps = GameData.Instance?.mapSet;
        return maps == null || maps.TryGetMapDetails(map, out _);
    }

    /// <summary>
    /// A finite value within the limits; NaN becomes the lower limit
    /// </summary>
    private static float Clamp(float value, float min, float max) =>
        float.IsNaN(value) ? min : Math.Clamp(value, min, max);

    /// <summary>
    /// The game uses negative values for "default"; anything else is kept within the limits
    /// </summary>
    private static int OrDefault(int value, int min, int max) => value < 0 ? -1 : Math.Clamp(value, min, max);
}
