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

    /// <summary>
    /// One tier of Advanced Mode, in place of Boss and Spawn Rounds. Args: the tier, its round, its
    /// <see cref="BossType"/>, whether it is Elite, whether the boss is still a secret, its speed and health in
    /// percent, its <see cref="SkullMode"/> (Vanilla for the boss's own), then the skull count for Evenly Spaced or the
    /// thresholds as written for Custom.
    /// </summary>
    Tier,

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
    /// The lines for <paramref name="options"/>. In Advanced Mode every tier has a line of its own in place of the Boss
    /// and Spawn Rounds lines, with its skulls in place of the Skulls line. A boss picked at random stays a secret
    /// for tiers after <paramref name="arrivedTier"/>, the highest tier whose boss has arrived, like on the HUD.
    /// </summary>
    public static List<SummaryLine> Lines(BossOptions options, int arrivedTier = int.MaxValue)
    {
        var lines = new List<SummaryLine>();
        var tiers = TierMath.ClampTiers(options.Tiers);
        var rounds = options.EffectiveSpawnRounds;
        if (!options.Mixed) lines.Add(new SummaryLine(RuleLine.Boss, options.Boss, options.Elite));
        else
        {
            for (var tier = 1; tier <= tiers; tier++) lines.Add(TierLine(options, tier, rounds[tier - 1], arrivedTier));
        }

        var changes = lines.Count;
        if (tiers != TierMath.VanillaTiers) lines.Add(new SummaryLine(RuleLine.Tiers, tiers));

        if (!options.Mixed && !rounds.SequenceEqual(TierMath.DefaultSpawnRounds(tiers)))
            lines.Add(new SummaryLine(RuleLine.SpawnRounds, TierMath.FormatRounds(rounds)));

        if (options.UsesBossEventRounds) lines.Add(new SummaryLine(RuleLine.BossEventRounds));

        var speed = TierMath.ClampSpeedPerTier(options.SpeedPerTier);
        if (speed != 0) lines.Add(new SummaryLine(RuleLine.SpeedPerTier, speed));

        var health = TierMath.ClampHealthPerTier(options.HealthPerTier);
        if (health != 0) lines.Add(new SummaryLine(RuleLine.HealthPerTier, health));

        if (!options.Mixed && Skulls(options.SkullMode, options.SkullCount, options.CustomSkulls) is var (mode, value))
            lines.Add(new SummaryLine(RuleLine.Skulls, mode, value));

        if (lines.Count == changes) lines.Add(new SummaryLine(RuleLine.NoChanges));
        return lines;
    }

    private static SummaryLine TierLine(BossOptions options, int tier, int round, int arrivedTier)
    {
        var entry = options.EntryOf(tier);
        var (boss, elite) = options.BossOf(tier);
        var secret = options.WasRandom(tier) && tier > arrivedTier;
        var skulls = entry.Skulls != SkullMode.Vanilla
            ? Skulls(entry.Skulls, entry.SkullCount, entry.CustomSkulls)
            : Skulls(options.SkullMode, options.SkullCount, options.CustomSkulls);
        return new SummaryLine(RuleLine.Tier, tier, round, boss, elite, secret, entry.Speed, entry.Health,
            skulls?.Mode ?? SkullMode.Vanilla, skulls?.Value ?? "");
    }

    /// <summary>
    /// Skulls other than the boss's own, as line args: the count for Evenly Spaced, the thresholds as written for
    /// Custom; null for Vanilla and for a Custom list without a valid value, which keeps the boss's own
    /// </summary>
    private static (SkullMode Mode, object Value)? Skulls(SkullMode mode, int count, string? custom) => mode switch
    {
        SkullMode.EvenlySpaced => (mode, System.Math.Clamp(count, 0, SkullMath.MaxSkulls)),
        SkullMode.Custom when SkullMath.ParseCustom(custom) is { } values => (mode, SkullMath.FormatCustom(values)),
        _ => null
    };

    /// <summary>The lines for the Rules box: <see cref="Lines"/> without the sliders</summary>
    public static List<SummaryLine> Rules(BossOptions options, int arrivedTier = int.MaxValue) =>
        Lines(options, arrivedTier).Where(line => !line.IsModifier).ToList();

    /// <summary>The sliders, for rows of the Modifiers list</summary>
    public static List<SummaryLine> Modifiers(BossOptions options) => Lines(options).Where(line => line.IsModifier).ToList();
}
