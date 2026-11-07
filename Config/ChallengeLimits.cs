using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BTD_Mod_Helper;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Config;

/// <summary>
/// Keeps a challenge read from a shared code or a file within values the game can play. The Challenge Editor's own
/// controls never go outside these limits; a hand made code could, and could then hang or crash the game.
/// </summary>
public static partial class ChallengeLimits
{
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
    public const int MaxTiersUsed = 100_000;

    /// <summary>A path has five upgrade tiers, which can all be blocked</summary>
    public const int MaxBlockedTiers = 5;

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
    /// <paramref name="defaults"/>. Returns what was changed, e.g. "boss speed 1E+30 -> 10", for the log.
    /// </summary>
    public static List<string> Sanitize(DailyChallengeModel dcm, DailyChallengeModel defaults)
    {
        var changes = new List<string>();

        var name = CleanName(dcm.name, defaults.name);
        if (name != dcm.name) changes.Add($"name cleaned to '{name}'");
        dcm.name = name;
        dcm.map = Known(changes, "map", dcm.map, IsKnownMap(dcm.map), defaults.map);
        dcm.difficulty = Known(changes, "difficulty", dcm.difficulty, Difficulties.Contains(dcm.difficulty),
            defaults.difficulty);
        dcm.mode = Known(changes, "mode", dcm.mode, Modes.Contains(dcm.mode), defaults.mode);

        var modifiers = dcm.bloonModifiers ??= new BloonModifiers();
        modifiers.speedMultiplier = Clamp(changes, "bloon speed", modifiers.speedMultiplier, MinMultiplier, MaxMultiplier);
        modifiers.moabSpeedMultiplier =
            Clamp(changes, "MOAB speed", modifiers.moabSpeedMultiplier, MinMultiplier, MaxMultiplier);
        modifiers.regrowRateMultiplier =
            Clamp(changes, "regrow rate", modifiers.regrowRateMultiplier, MinMultiplier, MaxMultiplier);
        modifiers.bossSpeedMultiplier =
            Clamp(changes, "boss speed", modifiers.bossSpeedMultiplier, MinBossSpeed, MaxBossSpeed);
        var health = modifiers.healthMultipliers ??= new BloonHealthMultipliers();
        health.bloons = Clamp(changes, "bloon health", health.bloons, MinMultiplier, MaxMultiplier);
        health.moabs = Clamp(changes, "MOAB health", health.moabs, MinMultiplier, MaxMultiplier);
        health.boss = Clamp(changes, "boss health", health.boss, MinBossHealth, MaxBossHealth);

        var rules = dcm.startRules ??= new StartRules();
        rules.cash = OrDefault(changes, "cash", rules.cash, 0, MaxCash);
        rules.lives = OrDefault(changes, "lives", rules.lives, 1, MaxLives);
        rules.maxLives = OrDefault(changes, "max lives", rules.maxLives, 1, MaxLives);
        rules.revives = OrDefault(changes, "revives", rules.revives, 0, MaxRevives);
        rules.round = OrDefault(changes, "start round", rules.round, 1, TierMath.MaxRound);
        rules.endRound = OrDefault(changes, "end round", rules.endRound, 1, TierMath.MaxRound + TierMath.RoundsBetweenTiers);

        dcm.maxTowers = OrDefault(changes, "max towers", dcm.maxTowers, 0, MaxTowerCount);
        dcm.maxParagons = OrDefault(changes, "max paragons", dcm.maxParagons, 0, MaxTowerCount);
        dcm.leastCashUsed = OrDefault(changes, "least cash", dcm.leastCashUsed, 0, MaxCash);
        dcm.leastTiersUsed = OrDefault(changes, "least tiers", dcm.leastTiersUsed, 0, MaxTiersUsed);
        dcm.maxPowers = OrDefault(changes, "max powers", dcm.maxPowers, 0, MaxTowerCount);
        dcm.removeableCostMultiplier =
            Clamp(changes, "removable cost", dcm.removeableCostMultiplier, 0, MaxMultiplier);
        dcm.abilityCooldownReductionMultiplier =
            Clamp(changes, "ability cooldown", dcm.abilityCooldownReductionMultiplier, 0, MaxMultiplier);
        if (dcm.towers != null)
        {
            foreach (var tower in dcm.towers)
            {
                tower.max = OrDefault(changes, $"{tower.tower} limit", tower.max, 0, MaxTowerCount);
                tower.path1NumBlockedTiers = Range(changes, $"{tower.tower} path 1", tower.path1NumBlockedTiers, 0, MaxBlockedTiers);
                tower.path2NumBlockedTiers = Range(changes, $"{tower.tower} path 2", tower.path2NumBlockedTiers, 0, MaxBlockedTiers);
                tower.path3NumBlockedTiers = Range(changes, $"{tower.tower} path 3", tower.path3NumBlockedTiers, 0, MaxBlockedTiers);
            }
        }

        return changes;
    }


    /// <summary>
    /// Writes what was brought into range to the log, so a shared setup that looks different can be explained
    /// </summary>
    public static void Report(string source, List<string> changes)
    {
        if (changes.Count == 0) return;
        ModHelper.Msg<CustomBossChallenge>($"{source}: brought into range: {string.Join(", ", changes)}");
    }
    private static bool IsKnownMap(string? map)
    {
        if (string.IsNullOrEmpty(map)) return false;
        var maps = GameData.Instance?.mapSet;
        return maps == null || maps.TryGetMapDetails(map, out _);
    }

    private static string Known(List<string> changes, string what, string? value, bool known, string fallback)
    {
        if (known) return value!;
        changes.Add($"unknown {what} '{value}' -> {fallback}");
        return fallback;
    }

    /// <summary>
    /// A finite value within the limits; NaN becomes the lower limit
    /// </summary>
    private static float Clamp(List<string> changes, string what, float value, float min, float max)
    {
        var clamped = float.IsNaN(value) ? min : Math.Clamp(value, min, max);
        if (!clamped.Equals(value))
        {
            changes.Add($"{what} {value.ToString(CultureInfo.InvariantCulture)} -> {clamped.ToString(CultureInfo.InvariantCulture)}");
        }

        return clamped;
    }

    /// <summary>
    /// A value within the limits
    /// </summary>
    private static int Range(List<string> changes, string what, int value, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        if (clamped != value) changes.Add($"{what} {value} -> {clamped}");
        return clamped;
    }

    /// <summary>
    /// The game uses negative values for "default"; anything else is kept within the limits
    /// </summary>
    private static int OrDefault(List<string> changes, string what, int value, int min, int max)
    {
        var clamped = value < 0 ? -1 : Math.Clamp(value, min, max);
        if (clamped != value) changes.Add($"{what} {value} -> {clamped}");
        return clamped;
    }
}
