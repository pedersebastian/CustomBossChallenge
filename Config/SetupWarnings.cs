using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// The parts of a setup the warnings look at, without the game's types so the checks can be unit tested (and ported
/// to the website). The mod fills it from the Challenge Editor's model and the boss models.
/// </summary>
public sealed record SetupFacts
{
    public BossOptions Options { get; init; } = new();

    /// <summary>The round the game starts on, as the player sees it (from 1)</summary>
    public int StartRound { get; init; } = 1;

    public string Difficulty { get; init; } = "Medium";
    public string Mode { get; init; } = "Standard";
    /// <summary>Below 0 for the difficulty's own, like the game</summary>
    public int Lives { get; init; } = -1;

    /// <summary>Below 0 for the difficulty's own, like the game</summary>
    public int Cash { get; init; } = -1;

    /// <summary>Below 0 for no limit, like the game</summary>
    public int MaxTowers { get; init; } = -1;

    public double BloonSpeed { get; init; } = 1;
    public double MoabSpeed { get; init; } = 1;
    public double BossSpeed { get; init; } = 1;
    public double BloonHealth { get; init; } = 1;
    public double MoabHealth { get; init; } = 1;
    public double BossHealth { get; init; } = 1;

    public bool NoSelling { get; init; }
    public bool NoKnowledge { get; init; }
    public bool NoPowers { get; init; }
    public bool AllCamo { get; init; }
    public bool AllRegrow { get; init; }
    public bool AllFortified { get; init; }

    /// <summary>
    /// Health and speed of the game's boss model each tier is made from (<see cref="BossOptions.SourceId"/>), by tier
    /// from 1; null where it isn't known, like for a Random tier or without the game
    /// </summary>
    public TierBase?[]? TierBases { get; init; }

    public TierBase? BaseOf(int tier) => TierBases != null && tier >= 1 && tier <= TierBases.Length ? TierBases[tier - 1] : null;
}

/// <summary>
/// Health and speed of a boss model as the game has it
/// </summary>
public readonly record struct TierBase(double Health, double Speed);

public enum SetupWarning
{
    /// <summary>The game starts only a few rounds before the first boss. Args: start round, rounds left, boss round.</summary>
    StartRoundClose,

    /// <summary>
    /// A tier comes only a few rounds after the one before, like rounds 80 and 81. Args: the rounds, like "80-81".
    /// </summary>
    TiersClose,

    /// <summary>Some tiers are held to the game's health limit. Args: the most health a tier can have.</summary>
    HealthCapped,

    /// <summary>Tiers in a row end up the same because of the limits. Args: the tiers, like "6-8".</summary>
    SameTiers,

    /// <summary>The rules together make a very hard challenge. No args.</summary>
    HardSetup
}

public readonly record struct Warning(SetupWarning Kind, params object[] Args);

/// <summary>
/// Things in a setup that are allowed but probably not what the player meant. They are shown before Play and never
/// block it; setups that can't be played are BossEditorSection.PlayProblem's job.
/// </summary>
public static class SetupWarnings
{
    /// <summary>
    /// Starting fewer rounds than this before the first boss, or a tier coming fewer rounds than this after the one
    /// before, leaves hardly any time
    /// </summary>
    public const int CloseRounds = 5;

    /// <summary>A setup scores at least this in <see cref="Difficulty"/> before it counts as very hard</summary>
    public const double HardThreshold = 5;

    private const double Same = 1e-6;

    /// <summary>
    /// The warnings for <paramref name="facts"/>, in the order of <see cref="SetupWarning"/>
    /// </summary>
    public static List<Warning> Check(SetupFacts facts)
    {
        var warnings = new List<Warning>();
        var options = facts.Options;
        var tiers = TierMath.ClampTiers(options.Tiers);

        var bossRound = options.EffectiveSpawnRounds[0];
        var left = bossRound - facts.StartRound;
        if (left > 0 && left < CloseRounds)
            warnings.Add(new Warning(SetupWarning.StartRoundClose, facts.StartRound, left, bossRound));

        var close = CloseTiers(options);
        if (close.Count > 0)
            warnings.Add(new Warning(SetupWarning.TiersClose, string.Join(", ", close.Select(pair => $"{pair.Before}-{pair.After}"))));

        var cap = TierMath.MaxModelHealth(facts.BossHealth);
        if (Enumerable.Range(1, tiers).Any(tier => ModelHealth(facts, tier) > cap))
            warnings.Add(new Warning(SetupWarning.HealthCapped, (double) cap * Math.Max(facts.BossHealth, 1)));

        var same = SameTiers(facts);
        if (same.Count > 0)
            warnings.Add(new Warning(SetupWarning.SameTiers,
                string.Join(", ", same.Select(run => $"{run.First}-{run.Last}"))));

        if (Difficulty(facts) >= HardThreshold) warnings.Add(new Warning(SetupWarning.HardSetup));
        return warnings;
    }

    /// <summary>
    /// The spawn rounds of tiers that come fewer than <see cref="CloseRounds"/> rounds after the tier before, with
    /// the round of the tier before. The last tier always gets 20 rounds, so only the gaps between tiers count.
    /// </summary>
    public static List<(int Before, int After)> CloseTiers(BossOptions options)
    {
        var rounds = options.EffectiveSpawnRounds;
        return rounds.Zip(rounds.Skip(1), (before, after) => (Before: before, After: after))
            .Where(pair => pair.After - pair.Before < CloseRounds)
            .ToList();
    }

    /// <summary>
    /// A tier's model health before the game's limit, or null when its boss model isn't known
    /// </summary>
    public static double? ModelHealth(SetupFacts facts, int tier) =>
        facts.BaseOf(tier) is { } source && !facts.Options.EntryOf(tier).IsRandom
            ? source.Health * TierMath.ExtraTierFactor(tier) * TierMath.HealthFactor(tier, facts.Options.HealthPerTier) *
              facts.Options.TierHealthFactor(tier)
            : null;

    /// <summary>
    /// Runs of tiers in a row that get the same health and speed from the same boss although the settings ask for a
    /// change from tier to tier: the speed limit, the lowest share of the game's values or the health limit made them
    /// equal
    /// </summary>
    public static List<(int First, int Last)> SameTiers(SetupFacts facts)
    {
        var options = facts.Options;
        var tiers = TierMath.ClampTiers(options.Tiers);
        var cap = TierMath.MaxModelHealth(facts.BossHealth);
        var runs = new List<(int First, int Last)>();
        int? start = null;
        for (var tier = 1; tier < tiers; tier++)
        {
            var next = tier + 1;
            var isSame = Values(facts, tier, cap) is { } a && Values(facts, next, cap) is { } b &&
                         options.BossOf(tier) == options.BossOf(next) &&
                         Equal(a.Health, b.Health) && Equal(a.Speed, b.Speed) &&
                         (!Equal(a.AskedHealth, b.AskedHealth) || !Equal(a.AskedSpeed, b.AskedSpeed));
            if (isSame)
            {
                start ??= tier;
                continue;
            }

            if (start != null) runs.Add((start.Value, tier));
            start = null;
        }

        if (start != null) runs.Add((start.Value, tiers));
        return runs;
    }

    private static (double Health, double Speed, double AskedHealth, double AskedSpeed)? Values(SetupFacts facts, int tier, int cap)
    {
        if (facts.BaseOf(tier) is not { } source || ModelHealth(facts, tier) is not { } health) return null;

        var options = facts.Options;
        var tierSpeed = options.TierSpeedFactor(tier);
        var tierHealth = options.TierHealthFactor(tier);
        // What the settings ask for, without the limits
        var askedHealth = source.Health * TierMath.ExtraTierFactor(tier) * Unlimited(tier, options.HealthPerTier) * tierHealth;
        var askedSpeed = source.Speed * Unlimited(tier, options.SpeedPerTier) * tierSpeed;
        return (Math.Min(health, cap), source.Speed * TierMath.SpeedFactor(tier, options.SpeedPerTier) * tierSpeed,
            askedHealth, askedSpeed);
    }

    private static double Unlimited(int tier, int percentPerTier) => 1 + percentPerTier / 100.0 * Math.Max(tier - 1, 0);

    private static bool Equal(double a, double b) => Math.Abs(a - b) <= Same * Math.Max(Math.Abs(a), Math.Abs(b));

    /// <summary>
    /// How hard the setup is compared with a normal boss game, roughly in doublings: 0 for the game's own bosses
    /// with the editor's default rules. Twice the boss's health and speed or twice the bloons' health adds 1, and so do
    /// the rules that take a big tool away from the player.
    /// </summary>
    public static double Difficulty(SetupFacts facts)
    {
        var options = facts.Options;
        var last = TierMath.ClampTiers(options.Tiers);

        // The last tier compared with the game's own tier of that number. The game's steps for tiers above 5 are left
        // out: more tiers is what the setup is for, and their health is in the Tier Preview.
        var boss = TierMath.HealthFactor(last, options.HealthPerTier) * options.TierHealthFactor(last) *
                   Math.Max(facts.BossHealth, 0.01) *
                   TierMath.SpeedFactor(last, options.SpeedPerTier) * options.TierSpeedFactor(last) *
                   Math.Max(facts.BossSpeed, 0.01);
        var elite = Enumerable.Range(1, last).Any(tier => options.BossOf(tier).Elite || options.EntryOf(tier).IsRandom && options.EntryOf(tier).Elite);

        var score = Log(boss) + (elite ? 1.5 : 0) +
                    Log(facts.BloonHealth) + Log(facts.MoabHealth) + Log(facts.BloonSpeed) + Log(facts.MoabSpeed);

        score += facts.Lives switch
        {
            < 0 => 0,
            <= 1 => 2,
            <= 10 => 1,
            <= 50 => 0.5,
            _ => 0
        };
        score += facts.Cash is >= 0 and < 400 ? 1 : 0;
        score += facts.MaxTowers is >= 0 and <= 5 ? 1.5 : facts.MaxTowers is > 5 and <= 15 ? 0.5 : 0;
        score += (facts.NoSelling ? 1 : 0) + (facts.NoKnowledge ? 0.5 : 0) + (facts.NoPowers ? 0.25 : 0);
        score += (facts.AllCamo ? 0.5 : 0) + (facts.AllRegrow ? 0.5 : 0) + (facts.AllFortified ? 1 : 0);
        score += facts.Difficulty == "Hard" ? 0.5 : 0;
        score += facts.Mode switch
        {
            "Impoppable" => 1.5,
            "HalfCash" or "DoubleMoabHealth" => 1,
            "Apopalypse" => 0.5,
            _ => 0
        };
        return score;
    }

    private static double Log(double factor) => Math.Log2(Math.Max(factor, 0.01));
}
