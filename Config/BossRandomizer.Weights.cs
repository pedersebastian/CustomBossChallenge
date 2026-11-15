using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// The part of <see cref="BossRandomizer"/> that doesn't use the game: the chances, the weights and the bosses per tier.
/// Also built in Shared/, so the unit tests check it against web/test/random-cases.json, which the website's Random
/// (web/src/studio/random.ts) reads too: given the same random numbers, both make the same values.
/// </summary>
public static partial class BossRandomizer
{
    public const double EliteChance = 0.3;
    public const double SpeedChance = 0.4;
    public const double HealthChance = 0.4;
    public const double FortifiedChance = 0.15;
    public const double ParagonChance = 0.2;
    public const double SpeedPerTierChance = 0.4;
    public const double HealthPerTierChance = 0.4;
    public const double SkullsChance = 0.3;

    /// <summary>
    /// Boss speed and health range in percent. Values near 100 % are the most likely, see <see cref="PercentWeight"/>.
    /// </summary>
    public const int MinPercent = 50;
    public const int MaxPercent = 400;
    public const int PercentStep = 10;

    /// <summary>
    /// How far from 100 % a boss speed or health still is about half as likely as 100 % itself
    /// </summary>
    public const double PercentSpread = 40;

    /// <summary>
    /// The spread of the number of tiers around the normal 5, like a bell curve: 4 and 6 come up almost as often as
    /// 5, 1 and 10 only now and then
    /// </summary>
    public const double TierSpread = 2;

    /// <summary>
    /// In Advanced Mode: the chance that a tier is Random, that it keeps the tier before it's boss, and that tier 1 or
    /// tier 10 is Elite (the tiers between them in steps)
    /// </summary>
    public const double RandomTierChance = 0.1;
    public const double SameBossChance = 0.2;
    public const double FirstTierEliteChance = 0.15;
    public const double LastTierEliteChance = 0.5;

    public const int MaxRandomParagons = 3;
    public const int MaxRandomSpeedPerTier = 25;
    public const int MaxRandomHealthPerTier = 50;

    /// <summary>
    /// The random source; tests set a seeded one, or one that gives listed numbers
    /// </summary>
    public static Random Random { get; set; } = new();

    private static bool Chance(double chance) => Random.NextDouble() < chance;

    /// <summary>
    /// How likely a boss speed or health is, compared with 100 %: 1 at 100 %, about 0.94 at 110 % and 0.02 at 400 %
    /// </summary>
    public static double PercentWeight(int percent)
    {
        var distance = (percent - 100) / PercentSpread;
        return 1 / (1 + distance * distance);
    }

    /// <summary>
    /// A boss speed or health from <see cref="MinPercent"/> to <see cref="MaxPercent"/> in steps of
    /// <see cref="PercentStep"/>, picked by <see cref="PercentWeight"/>
    /// </summary>
    public static int WeightedPercent() =>
        Weighted(Enumerable.Range(0, (MaxPercent - MinPercent) / PercentStep + 1).Select(i => MinPercent + i * PercentStep), PercentWeight);

    /// <summary>
    /// How likely a number of tiers is, compared with the normal 5: 1 at 5, about 0.88 at 4 and 6, 0.04 at 10
    /// </summary>
    public static double TierWeight(int tiers)
    {
        var distance = (tiers - TierMath.VanillaTiers) / TierSpread;
        return Math.Exp(-distance * distance / 2);
    }

    /// <summary>
    /// A number of tiers from <see cref="TierMath.MinTiers"/> to <see cref="TierMath.MaxTiers"/>, picked by
    /// <see cref="TierWeight"/>
    /// </summary>
    public static int WeightedTiers() =>
        Weighted(Enumerable.Range(TierMath.MinTiers, TierMath.MaxTiers - TierMath.MinTiers + 1), TierWeight);

    /// <summary>
    /// One of <paramref name="values"/>, each as likely as its <paramref name="weight"/>
    /// </summary>
    private static int Weighted(IEnumerable<int> values, Func<int, double> weight)
    {
        var list = values.ToList();
        var pick = Random.NextDouble() * list.Sum(weight);
        foreach (var value in list)
        {
            pick -= weight(value);
            if (pick < 0) return value;
        }

        return list[^1];
    }

    /// <summary>
    /// A boss for each of the 10 tiers, so changing the number of tiers later keeps them. A tier is Random now and
    /// then, rarely has the same boss as the tier before it, and is Elite more often the later it comes. Each tier's
    /// speed, health and skulls change as often as Boss Speed, Boss Health and Skulls do in Standard, with values near
    /// 100 % the most likely.
    /// </summary>
    public static string RandomTierBosses()
    {
        var tiers = new List<TierBoss>();
        string? previous = null;
        for (var tier = 1; tier <= TierMath.MaxTiers; tier++)
        {
            var eliteChance = FirstTierEliteChance +
                              (LastTierEliteChance - FirstTierEliteChance) * (tier - 1) / (TierMath.MaxTiers - 1);
            var elite = Chance(eliteChance);
            var speed = Chance(SpeedChance) ? WeightedPercent() : TierBossMath.Normal;
            var health = Chance(HealthChance) ? WeightedPercent() : TierBossMath.Normal;
            var values = RandomSkullsOf(TierBoss.Follows with {Elite = elite, Speed = speed, Health = health});
            if (Chance(RandomTierChance))
            {
                tiers.Add(values with {Boss = TierBossMath.Random});
                continue;
            }

            var others = TierBossMath.Bosses.Where(boss => boss != previous).ToArray();
            var boss = previous != null && Chance(SameBossChance) ? previous : others[Random.Next(others.Length)];
            tiers.Add(values with {Boss = boss});
            previous = boss;
        }

        return TierBossMath.Format(tiers);
    }

    /// <summary>
    /// A random multiple of <paramref name="step"/> from <paramref name="min"/> to <paramref name="max"/>
    /// </summary>
    private static int Step(int min, int max, int step) => min + Random.Next((max - min) / step + 1) * step;

    /// <summary>
    /// <paramref name="tier"/> with its own skulls now and then: evenly spaced or a custom list, as likely as each other
    /// </summary>
    private static TierBoss RandomSkullsOf(TierBoss tier)
    {
        if (!Chance(SkullsChance)) return tier;
        return Random.Next(2) == 0
            ? tier with {Skulls = SkullMode.EvenlySpaced, SkullCount = Random.Next(0, 7)}
            : tier with {Skulls = SkullMode.Custom, CustomSkulls = RandomSkulls()};
    }

    /// <summary>
    /// Two to four different skulls between 10 and 90 %, e.g. "85, 50, 15". The pool is shuffled like the website does
    /// it (Fisher-Yates), so the same random numbers give the same skulls on both.
    /// </summary>
    private static string RandomSkulls()
    {
        var count = Random.Next(2, 5);
        var pool = Enumerable.Range(2, 17).Select(i => i * 5).ToArray();
        for (var i = pool.Length - 1; i > 0; i--)
        {
            var j = Random.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return string.Join(", ", pool.Take(count).OrderByDescending(p => p));
    }
}
