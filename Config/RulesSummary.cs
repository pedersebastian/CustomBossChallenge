using System.Collections.Generic;
using System.Linq;
using Il2CppAssets.Scripts.Data.Boss;

namespace CustomBossChallenge.Config;

public enum RuleLine
{
    /// <summary>The boss every tier has. Args: the <see cref="BossType"/>, whether it is Elite.</summary>
    Boss,

    /// <summary>The number of tiers, when not the game's 5. Args: the tiers.</summary>
    Tiers,

    /// <summary>The rounds the tiers come on, when not the defaults. Args: the rounds, like "40, 50, 60".</summary>
    SpawnRounds,

    /// <summary>The game is played with the boss event's own rounds. No args.</summary>
    BossEventRounds,

    /// <summary>Each tier faster than the game makes it. Args: the percent per tier, can be below 0.</summary>
    SpeedPerTier,

    /// <summary>Each tier with more health than the game gives it. Args: the percent per tier, can be below 0.</summary>
    HealthPerTier,

    /// <summary>
    /// Skulls other than the boss's own. Args: the <see cref="SkullMode"/>, then the skull count for Evenly Spaced or
    /// the thresholds as written, like "75, 50, 25", for Custom.
    /// </summary>
    Skulls,

    /// <summary>Nothing besides the boss differs from a normal boss game. No args.</summary>
    NoChanges
}

public readonly record struct SummaryLine(RuleLine Kind, params object[] Args)
{
    /// <summary>
    /// Whether the line is a slider of the Boss Creator, which the panel shows as a row of its Modifiers list like the
    /// game's Boss Speed and Boss Health, instead of a line of text in its Rules box
    /// </summary>
    public bool IsModifier => Kind is RuleLine.Tiers or RuleLine.SpeedPerTier or RuleLine.HealthPerTier ||
                              Kind == RuleLine.Skulls && (SkullMode) Args[0] == SkullMode.EvenlySpaced;
}

/// <summary>
/// The boss options of a game as short lines for the Challenge Rules panel of the pause screen (#140), every one that
/// differs from a normal boss game. Without the game's types so it can be unit tested; Texts.Summary words the lines.
/// The sliders go in the panel's Modifiers list (<see cref="Modifiers"/>), the rest in its Rules box
/// (<see cref="Rules"/>).
/// </summary>
public static class RulesSummary
{
    /// <summary>
    /// The lines for <paramref name="options"/>, in the order of <see cref="RuleLine"/>. In Advanced Mode every tier
    /// has a boss of its own, so the Boss line is left out.
    /// </summary>
    public static List<SummaryLine> Lines(BossOptions options)
    {
        var lines = new List<SummaryLine>();
        if (!options.Mixed) lines.Add(new SummaryLine(RuleLine.Boss, options.Boss, options.Elite));

        var changes = lines.Count;
        var tiers = TierMath.ClampTiers(options.Tiers);
        if (tiers != TierMath.VanillaTiers) lines.Add(new SummaryLine(RuleLine.Tiers, tiers));

        var rounds = options.EffectiveSpawnRounds;
        if (!rounds.SequenceEqual(TierMath.DefaultSpawnRounds(tiers)))
            lines.Add(new SummaryLine(RuleLine.SpawnRounds, TierMath.FormatRounds(rounds)));

        if (options.UsesBossEventRounds) lines.Add(new SummaryLine(RuleLine.BossEventRounds));

        var speed = TierMath.ClampSpeedPerTier(options.SpeedPerTier);
        if (speed != 0) lines.Add(new SummaryLine(RuleLine.SpeedPerTier, speed));

        var health = TierMath.ClampHealthPerTier(options.HealthPerTier);
        if (health != 0) lines.Add(new SummaryLine(RuleLine.HealthPerTier, health));

        switch (options.SkullMode)
        {
            case SkullMode.EvenlySpaced:
                lines.Add(new SummaryLine(RuleLine.Skulls, SkullMode.EvenlySpaced,
                    System.Math.Clamp(options.SkullCount, 0, SkullMath.MaxSkulls)));
                break;
            case SkullMode.Custom when options.SkullPercents is { } custom:
                lines.Add(new SummaryLine(RuleLine.Skulls, SkullMode.Custom, SkullMath.FormatCustom(custom)));
                break;
        }

        if (lines.Count == changes) lines.Add(new SummaryLine(RuleLine.NoChanges));
        return lines;
    }

    /// <summary>The lines for the Rules box: <see cref="Lines"/> without the sliders</summary>
    public static List<SummaryLine> Rules(BossOptions options) => Lines(options).Where(line => !line.IsModifier).ToList();

    /// <summary>The sliders, for rows of the Modifiers list</summary>
    public static List<SummaryLine> Modifiers(BossOptions options) => Lines(options).Where(line => line.IsModifier).ToList();
}
