using System;
using System.Linq;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Tier and spawn round calculations
/// </summary>
public class TierMathTests
{
    [Theory]
    [InlineData(5, "40, 60, 80, 100, 120")]
    [InlineData(7, "40, 60, 80, 100, 120, 140, 160")]
    [InlineData(1, "40")]
    public void DefaultSpawnRounds_continue_every_20_rounds(int tiers, string expected) =>
        Assert.Equal(expected, Show(TierMath.DefaultSpawnRounds(tiers)));

    [Theory]
    [InlineData(40, 20, 5, "40, 60, 80, 100, 120")]
    [InlineData(25, 10, 7, "25, 35, 45, 55, 65, 75, 85")]
    [InlineData(60, 1, 3, "60, 61, 62")]
    [InlineData(10, 0, 3, "10, 11, 12")]
    public void EvenRounds_keep_their_spacing_for_any_tier_count(int first, int gap, int tiers, string expected) =>
        Assert.Equal(expected, Show(TierMath.SpawnRounds(tiers, TierMath.EvenRounds(first, gap))));

    [Theory]
    [InlineData("40, 60, 80", 20, 40, 20)]
    [InlineData("30, 35, 90", 20, 30, 5)]
    [InlineData("70", 15, 70, 15)]
    [InlineData("", 20, TierMath.FirstSpawnRound, 20)]
    public void FirstAndGap_read_the_first_two_tiers(string rounds, int gap, int first, int expectedGap) =>
        Assert.Equal((first, expectedGap), TierMath.FirstAndGap(TierMath.ParseRounds(rounds).ToArray(), gap));

    [Theory]
    [InlineData("40, 50, 60", 49, 1, 10)]
    [InlineData("40, 50, 60", 59, 2, 10)]
    [InlineData("40, 45, 80", 79, 2, 35)]
    [InlineData("40, 45, 80", 90, 2, 35)]
    [InlineData("40, 41", 40, 1, 1)]
    [InlineData("40", 60, 1, TierMath.RoundsBetweenTiers)]
    public void TimeLimit_is_the_rounds_until_the_next_tier(string rounds, int round, int tier, int between) =>
        Assert.Equal((tier, between), TierMath.TimeLimit(TierMath.ParseRounds(rounds).ToArray(), round));

    [Theory]
    [InlineData("40, 50, 60", true)]
    [InlineData("40", true)]
    [InlineData("40, 45, 80", false)]
    public void EvenlySpaced_needs_the_same_rounds_between_all_tiers(string rounds, bool expected) =>
        Assert.Equal(expected, TierMath.EvenlySpaced(TierMath.ParseRounds(rounds).ToArray()));

    [Theory]
    [InlineData(99, TierMath.MaxTiers)]
    [InlineData(0, TierMath.MinTiers)]
    [InlineData(-3, TierMath.MinTiers)]
    public void DefaultSpawnRounds_keep_the_tier_count_in_range(int tiers, int expected) =>
        Assert.Equal(expected, TierMath.DefaultSpawnRounds(tiers).Length);

    [Theory]
    [InlineData(5, null, "40, 60, 80, 100, 120")]
    [InlineData(5, "  ", "40, 60, 80, 100, 120")]
    [InlineData(3, "30, 50,70", "30, 50, 70")]
    [InlineData(4, "30;45", "30, 45, 65, 85")]
    [InlineData(2, "30, 50, 70", "30, 50")]
    public void SpawnRounds_use_the_players_rounds_and_continue_the_rest(int tiers, string? custom, string expected) =>
        Assert.Equal(expected, Show(TierMath.SpawnRounds(tiers, custom)));

    [Theory]
    [InlineData(3, "50, 50, 20", "50, 51, 52")]
    [InlineData(3, "40, 60, 50", "40, 60, 61")]
    public void SpawnRounds_that_dont_ascend_move_to_the_round_after_the_previous_tier(int tiers, string custom,
        string expected) =>
        Assert.Equal(expected, Show(TierMath.SpawnRounds(tiers, custom)));

    [Theory]
    [InlineData(2, "x, -5, 0, 60", "60, 80")]
    [InlineData(3, "40, 0, 80", "40, 80, 100")]
    public void SpawnRounds_skip_junk_and_the_later_rounds_move_up(int tiers, string custom, string expected) =>
        Assert.Equal(expected, Show(TierMath.SpawnRounds(tiers, custom)));

    [Theory]
    [InlineData(1, "5000", "1000")]
    [InlineData(2, "1000", "999, 1000")]
    [InlineData(3, "995, 2000", "995, 999, 1000")]
    public void SpawnRounds_stop_at_the_last_round_without_sharing_one(int tiers, string custom, string expected) =>
        Assert.Equal(expected, Show(TierMath.SpawnRounds(tiers, custom)));

    [Theory]
    [InlineData(null)]
    [InlineData("990")]
    [InlineData("1, 1, 1, 1, 1, 1, 1, 1, 1, 1")]
    [InlineData("1000, 900, 800")]
    public void SpawnRounds_always_give_every_tier_its_own_ascending_round(string? custom)
    {
        var rounds = TierMath.SpawnRounds(TierMath.MaxTiers, custom);

        Assert.Equal(TierMath.MaxTiers, rounds.Length);
        Assert.All(rounds.Zip(rounds.Skip(1)), pair => Assert.True(pair.First < pair.Second, Show(rounds)));
        Assert.InRange(rounds[^1], rounds[0], TierMath.MaxRound);
    }

    [Theory]
    [InlineData("40, 60;80 100", new[] {40, 60, 80, 100})]
    [InlineData("80, 40", new[] {80, 40})]
    [InlineData("abc, -1, 0, 1.5, 20", new[] {20})]
    [InlineData(null, new int[0])]
    public void ParseRounds_keep_positive_whole_numbers_in_the_given_order(string? text, int[] expected) =>
        Assert.Equal(expected, TierMath.ParseRounds(text));

    [Fact]
    public void ToGameRounds_count_from_zero_and_never_go_below_it() =>
        Assert.Equal(new[] {0, 0, 39, 59}, TierMath.ToGameRounds(new[] {0, 1, 40, 60}));

    [Theory]
    [InlineData("VortexElite7", 7)]
    [InlineData("Bloonarius10", 10)]
    [InlineData("Lych1", 1)]
    [InlineData("Moab", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void TierOf_reads_the_number_at_the_end_of_a_boss_id(string? id, int expected) =>
        Assert.Equal(expected, TierMath.TierOf(id));

    [Fact]
    public void Tier_1_and_0_percent_keep_the_games_values()
    {
        Assert.Equal(1, TierMath.SpeedFactor(1, 50));
        Assert.Equal(1, TierMath.HealthFactor(1, 100));
        Assert.Equal(1, TierMath.SpeedFactor(7, 0));
        Assert.Equal(1, TierMath.HealthFactor(7, 0));
    }

    [Theory]
    [InlineData(3, 20, 1.4)]
    [InlineData(3, -10, 0.8)]
    [InlineData(10, TierMath.MaxSpeedPerTier, TierMath.MaxSpeedFactor)]
    public void SpeedFactor_adds_the_percentage_for_every_tier_after_the_first(int tier, int percent, double expected) =>
        Assert.Equal(expected, TierMath.SpeedFactor(tier, percent), 9);

    [Theory]
    [InlineData(3, 50, 2)]
    [InlineData(5, 100, 5)]
    [InlineData(10, TierMath.MinHealthPerTier, TierMath.MinPerTierFactor)]
    public void HealthFactor_adds_the_percentage_and_never_drops_below_the_minimum(int tier, int percent,
        double expected) =>
        Assert.Equal(expected, TierMath.HealthFactor(tier, percent), 9);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 1.5)]
    [InlineData(7, 2.25)]
    public void ExtraTierFactor_gives_tiers_above_5_another_50_percent_each(int tier, double expected) =>
        Assert.Equal(expected, TierMath.ExtraTierFactor(tier), 9);

    [Theory]
    [InlineData(150, 0)]
    [InlineData(225, 10)]
    [InlineData(175, 5)]
    [InlineData(100, -5)]
    [InlineData(500, 45)]
    [InlineData(100000, TierMath.MaxHealthPerTier)]
    public void HealthPerTierFromExtraTierHealth_converts_the_old_setting(int extraTierHealth, int expected) =>
        Assert.Equal(expected, TierMath.HealthPerTierFromExtraTierHealth(extraTierHealth));

    [Theory]
    [InlineData(3000000, 2.25, 1, 6750000)]
    [InlineData(int.MaxValue, 1000, 1, TierMath.MaxGameHealth)]
    [InlineData(int.MaxValue, 1000, 20, TierMath.MaxGameHealth / 20)]
    [InlineData(int.MaxValue, 1000, 0.1, TierMath.MaxGameHealth)]
    [InlineData(1000, 0.0001, 1, 1)]
    public void ScaleHealth_never_overflows_or_drops_to_0(int health, double factor, double bossHealth, int expected) =>
        Assert.Equal(expected, TierMath.ScaleHealth(health, factor, bossHealth));

    [Fact]
    public void MaxModelHealth_leaves_room_for_Boss_Health() =>
        Assert.Equal(666_666_666, TierMath.MaxModelHealth(3));

    [Theory]
    [InlineData(-100, TierMath.MinSpeedPerTier)]
    [InlineData(20, 20)]
    [InlineData(100, TierMath.MaxSpeedPerTier)]
    public void ClampSpeedPerTier_keeps_the_slider_range(int percent, int expected) =>
        Assert.Equal(expected, TierMath.ClampSpeedPerTier(percent));

    private static string Show(int[] rounds) => TierMath.FormatRounds(rounds);
}
