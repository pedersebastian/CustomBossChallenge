using System;
using System.Linq;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Config;

/// <summary>
/// What the Challenge Editor's Random button does to the boss options. It works like the game's own randomizer: each
/// option has a chance to change and otherwise keeps its default. A changed value is spread over a range that stays
/// playable, with values close to the normal game more likely than extreme ones (110 % far more often than 400 %).
/// In Advanced Mode every tier gets a boss of its own, and the setup stays in Advanced Mode; Standard stays Standard.
/// </summary>
public static class BossRandomizer
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
    /// The random source; tests set a seeded one
    /// </summary>
    public static Random Random { get; set; } = new();

    /// <summary>
    /// New random boss options, with the boss values in the challenge randomized to match. Fixes what the game's
    /// randomizer picks that a boss game can't use: unsupported game modes and a start round after the first tier.
    /// </summary>
    /// <param name="advanced">Whether the setup is in Advanced Mode, which random setups then are too</param>
    public static BossOptions Randomize(DailyChallengeModel dcm, string[] unsupportedModes, bool advanced = false)
    {
        var bosses = Enum.GetValues<BossType>();
        var options = new BossOptions
        {
            Boss = bosses[Random.Next(bosses.Length)],
            Elite = Chance(EliteChance),
            Tiers = WeightedTiers()
        };

        if (advanced) options = options with {TierBosses = RandomTierBosses()};

        if (Chance(SpeedPerTierChance))
        {
            options = options with {SpeedPerTier = Step(TierMath.PerTierStep, MaxRandomSpeedPerTier, TierMath.PerTierStep)};
        }

        if (Chance(HealthPerTierChance))
        {
            options = options with {HealthPerTier = Step(TierMath.PerTierStep, MaxRandomHealthPerTier, TierMath.PerTierStep)};
        }

        if (Chance(SkullsChance))
        {
            options = Random.Next(2) == 0
                ? options with {SkullMode = SkullMode.EvenlySpaced, SkullCount = Random.Next(0, 7)}
                : options with {SkullMode = SkullMode.Custom, CustomSkulls = RandomSkulls()};
        }

        var modifiers = dcm.bloonModifiers ??= new BloonModifiers();
        var health = modifiers.healthMultipliers ??= new BloonHealthMultipliers();
        modifiers.bossSpeedMultiplier = Chance(SpeedChance) ? WeightedPercent() / 100f : 1;
        health.boss = Chance(HealthChance) ? WeightedPercent() / 100f : 1;
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
    private static int Weighted(System.Collections.Generic.IEnumerable<int> values, Func<int, double> weight)
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
    /// then, rarely has the same boss as the tier before it, and is Elite more often the later it comes.
    /// </summary>
    public static string RandomTierBosses()
    {
        var tiers = new System.Collections.Generic.List<TierBoss>();
        string? previous = null;
        for (var tier = 1; tier <= TierMath.MaxTiers; tier++)
        {
            var eliteChance = FirstTierEliteChance +
                              (LastTierEliteChance - FirstTierEliteChance) * (tier - 1) / (TierMath.MaxTiers - 1);
            var elite = Chance(eliteChance);
            if (Chance(RandomTierChance))
            {
                tiers.Add(new TierBoss(TierBossMath.Random, elite));
                continue;
            }

            var others = TierBossMath.Bosses.Where(boss => boss != previous).ToArray();
            var boss = previous != null && Chance(SameBossChance) ? previous : others[Random.Next(others.Length)];
            tiers.Add(new TierBoss(boss, elite));
            previous = boss;
        }

        return TierBossMath.Format(tiers);
    }

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
