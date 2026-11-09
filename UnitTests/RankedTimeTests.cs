using System;
using System.Linq;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The ranked boss time: frames to time and the event timer's format
/// </summary>
public class RankedTimeTests
{
    [Theory]
    [InlineData(0, "00:00.00")]
    [InlineData(363, "00:06.05")]
    [InlineData(3599, "00:59.98")]
    [InlineData(3600, "01:00.00")]
    [InlineData(8109, "02:15.15")]
    [InlineData(216000, "60:00.00")]
    [InlineData(270000, "75:00.00")]
    public void Frames_are_shown_like_the_event_timer(double frames, string expected) =>
        Assert.Equal(expected, RankedTime.Format(RankedTime.FromFrames(frames)));

    [Fact]
    public void Hundredths_are_cut_off_not_rounded_up() =>
        Assert.Equal("00:59.99", RankedTime.Format(TimeSpan.FromMilliseconds(59_999)));

    [Fact]
    public void A_negative_time_shows_as_zero()
    {
        Assert.Equal("00:00.00", RankedTime.Format(TimeSpan.FromSeconds(-3)));
        Assert.Equal(TimeSpan.Zero, RankedTime.FromFrames(-60));
    }

    [Fact]
    public void Only_defeated_tiers_have_a_time()
    {
        var times = RankedTime.TierTimes(new double[] {363, 405, 0, 0, 0}, 3, 2);
        Assert.Equal(new[] {"00:06.05", "00:06.75", "-"}, times.Select(t => t is { } time ? RankedTime.Format(time) : "-"));
    }

    [Fact]
    public void Tiers_past_the_game_counts_have_no_time() =>
        Assert.Equal(new TimeSpan?[] {TimeSpan.FromSeconds(1), null}, RankedTime.TierTimes(new double[] {60}, 2, 2));
}
