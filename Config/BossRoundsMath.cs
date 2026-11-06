using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// Boss Event Rounds (#80): the official boss events play the boss's own round set, merged over the default rounds,
/// with the round each tier arrives on (40, 60, 80, 100, 120) made for that tier. When the tiers spawn on other rounds,
/// those rounds move with them.
/// </summary>
public static class BossRoundsMath
{
    /// <summary>
    /// The rounds the boss events' own round sets are made for: tier i arrives on the i-th of them
    /// </summary>
    public static int[] EventRounds => TierMath.DefaultSpawnRounds(TierMath.VanillaTiers);

    /// <summary>
    /// For every round 1..<paramref name="roundCount"/>, the round of the merged round set it plays: element r - 1 is
    /// the round whose bloons round r gets. Round S of a tier gets the event round E made for that tier; the rounds
    /// that move out of the way fill the event rounds no tier uses, in order, so no round is lost or played twice.
    /// Tiers above 5 have no event round, and a tier past the last round keeps its event round where it is.
    /// </summary>
    public static int[] Sources(int[] spawnRounds, int roundCount)
    {
        var sources = Enumerable.Range(1, Math.Max(roundCount, 0)).ToArray();
        var moves = EventRounds.Zip(spawnRounds, (from, to) => (From: from, To: to))
            .Where(move => move.To >= 1 && move.To <= roundCount && move.From <= roundCount)
            .ToList();

        var targets = moves.Select(move => move.To).ToHashSet();
        var used = moves.Select(move => move.From).ToHashSet();
        foreach (var (from, to) in moves) sources[to - 1] = from;

        // The rounds pushed out by a tier go where an event round left, both in order
        var pushedOut = targets.Where(round => !used.Contains(round)).OrderBy(round => round);
        var freed = used.Where(round => !targets.Contains(round)).OrderBy(round => round);
        foreach (var (pushed, free) in pushedOut.Zip(freed)) sources[free - 1] = pushed;

        return sources;
    }

    /// <summary>
    /// Every tier that plays an event round, with the round it arrives on and the event round made for it, after
    /// <see cref="Sources"/> moved them: tiers 1-5 whose spawn round and event round are both in the game. In Advanced
    /// Mode (#84) each of these rounds comes from the tier's own boss.
    /// </summary>
    public static (int Round, int Tier, int EventRound)[] TierEventRounds(int[] spawnRounds, int roundCount) =>
        EventRounds.Zip(spawnRounds, (from, to) => (From: from, To: to))
            .Select((move, i) => (Round: move.To, Tier: i + 1, EventRound: move.From))
            .Where(tier => tier.Round >= 1 && tier.Round <= roundCount && tier.EventRound <= roundCount)
            .ToArray();

    /// <summary>
    /// Whether <paramref name="sources"/> leaves every round where it is
    /// </summary>
    public static bool IsIdentity(IReadOnlyList<int> sources) => sources.Select((source, i) => source == i + 1).All(same => same);
}
