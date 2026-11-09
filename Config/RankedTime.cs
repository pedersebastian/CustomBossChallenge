using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// The time a boss game is scored by in the ranked boss events: the time each tier's boss was alive, from its arrival
/// to its defeat, added up, plus any penalty. The game counts it in simulation frames, so it follows fast forward and
/// stops while the game is paused. Pure, so it can be unit tested.
/// </summary>
public static class RankedTime
{
    /// <summary>
    /// The game simulates 60 frames per second of game time
    /// </summary>
    public const double FramesPerSecond = 60;

    public static TimeSpan FromFrames(double frames) =>
        TimeSpan.FromTicks((long) Math.Round(Math.Max(frames, 0) / FramesPerSecond * TimeSpan.TicksPerSecond));

    /// <summary>
    /// Formats a time like the event timer does: minutes, seconds and hundredths, e.g. 02:15.15. Past an hour the
    /// minutes keep counting (75:00.00) rather than adding hours, so times stay easy to compare.
    /// </summary>
    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        var hundredths = time.Ticks / (TimeSpan.TicksPerMillisecond * 10);
        var minutes = hundredths / 6000;
        var seconds = hundredths / 100 % 60;
        return $"{minutes:00}:{seconds:00}.{hundredths % 100:00}";
    }

    /// <summary>
    /// The time each of the first <paramref name="tiers"/> tiers took, from the game's per tier frame counts. Tiers
    /// the game has no count for (not reached yet) are null.
    /// </summary>
    public static IReadOnlyList<TimeSpan?> TierTimes(IReadOnlyList<double> frames, int tiers, int defeatedTiers) =>
        Enumerable.Range(0, Math.Max(tiers, 0))
            .Select(i => i < defeatedTiers && i < frames.Count ? FromFrames(frames[i]) : (TimeSpan?) null)
            .ToList();
}
