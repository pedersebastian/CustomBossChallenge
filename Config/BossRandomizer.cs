using System;
using System.Linq;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Config;

/// <summary>
/// What the Challenge Editor's Random button does to the boss options. It works like the game's own randomizer: each
/// option has a chance to change and otherwise keeps its default, and a changed value is spread evenly over a range
/// that stays playable.
/// </summary>
public static class BossRandomizer
{
    public const double EliteChance = 0.3;
    public const double SpeedChance = 0.4;
    public const double HealthChance = 0.4;
    public const double FortifiedChance = 0.15;
    public const double ParagonChance = 0.2;
    public const double TiersChance = 0.5;
    public const double ExtraTierHealthChance = 0.5;
    public const double SkullsChance = 0.3;

    /// <summary>
    /// Boss speed and health range in percent, like the game's 50-200 % for bloon speed and health
    /// </summary>
    public const int MinPercent = 50;
    public const int MaxPercent = 200;

    public const int MaxRandomParagons = 3;
    public const int MaxRandomExtraTierHealth = 300;

    /// <summary>
    /// The random source; tests set a seeded one
    /// </summary>
    public static Random Random { get; set; } = new();

    /// <summary>
    /// New random boss options, with the boss values in the challenge randomized to match. Fixes what the game's
    /// randomizer picks that a boss game can't use: unsupported game modes and a start round after the first tier.
    /// </summary>
    public static BossOptions Randomize(DailyChallengeModel dcm, string[] unsupportedModes)
    {
        var bosses = Enum.GetValues<BossType>();
        var options = new BossOptions
        {
            Boss = bosses[Random.Next(bosses.Length)],
            Elite = Chance(EliteChance),
            Tiers = Chance(TiersChance) ? Random.Next(TierMath.MinTiers, TierMath.MaxTiers + 1) : TierMath.VanillaTiers
        };

        if (options.Tiers > TierMath.VanillaTiers && Chance(ExtraTierHealthChance))
        {
            options = options with {ExtraTierHealth = Step(TierMath.MinExtraTierHealth + 10, MaxRandomExtraTierHealth, 10)};
        }

        if (Chance(SkullsChance))
        {
            options = Random.Next(2) == 0
                ? options with {SkullMode = SkullMode.EvenlySpaced, SkullCount = Random.Next(0, 7)}
                : options with {SkullMode = SkullMode.Custom, CustomSkulls = RandomSkulls()};
        }

        var modifiers = dcm.bloonModifiers ??= new BloonModifiers();
        var health = modifiers.healthMultipliers ??= new BloonHealthMultipliers();
        modifiers.bossSpeedMultiplier = Chance(SpeedChance) ? Step(MinPercent, MaxPercent, 10) / 100f : 1;
        health.boss = Chance(HealthChance) ? Step(MinPercent, MaxPercent, 10) / 100f : 1;
        modifiers.allFortified = Chance(FortifiedChance);
        dcm.maxParagons = Chance(ParagonChance) ? Random.Next(0, MaxRandomParagons + 1) : ParagonMath.NoLimit;

        if (unsupportedModes.Contains(dcm.mode)) dcm.mode = "Standard";

        // Starting after the first tier's round would skip it
        var rules = dcm.startRules ??= new StartRules();
        var firstSpawn = options.EffectiveSpawnRounds[0];
        if (rules.round >= firstSpawn) rules.round = Random.Next(1, firstSpawn);

        return options.Sanitized();
    }

    private static bool Chance(double chance) => Random.NextDouble() < chance;

    /// <summary>
    /// A random multiple of <paramref name="step"/> from <paramref name="min"/> to <paramref name="max"/>
    /// </summary>
    private static int Step(int min, int max, int step) => min + Random.Next((max - min) / step + 1) * step;

    /// <summary>
    /// Two to four different skulls between 10 and 90 %, e.g. "85, 50, 15"
    /// </summary>
    private static string RandomSkulls()
    {
        var count = Random.Next(2, 5);
        var skulls = Enumerable.Range(2, 17).Select(i => i * 5).OrderBy(_ => Random.Next()).Take(count)
            .OrderByDescending(p => p);
        return string.Join(", ", skulls);
    }
}
