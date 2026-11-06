using System.Linq;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Boss Event Rounds move with the tiers' spawn rounds (#80)
/// </summary>
public class BossRoundsMathTests
{
    private const int Rounds = 140;

    [Theory]
    [InlineData(5)]
    [InlineData(3)]
    [InlineData(10)]
    public void The_default_spawn_rounds_play_every_round_where_it_is(int tiers) =>
        Assert.True(BossRoundsMath.IsIdentity(BossRoundsMath.Sources(TierMath.DefaultSpawnRounds(tiers), Rounds)));

    [Fact]
    public void Earlier_tiers_take_their_event_rounds_along_and_swap_with_the_rounds_they_replace()
    {
        var sources = BossRoundsMath.Sources([30, 50, 70, 90, 110], Rounds);
        foreach (var (spawn, eventRound) in new[] {(30, 40), (50, 60), (70, 80), (90, 100), (110, 120)})
        {
            Assert.Equal(eventRound, sources[spawn - 1]);
            Assert.Equal(spawn, sources[eventRound - 1]);
        }
        Assert.Equal(41, sources[40]);
    }

    [Fact]
    public void Overlapping_rounds_still_play_every_round_once()
    {
        // Tier 1 on 60 and tier 2 on 80 take the rounds tiers 2 and 3 would have had
        var sources = BossRoundsMath.Sources([60, 80, 100, 120, 130], Rounds);
        Assert.Equal(40, sources[59]);
        Assert.Equal(60, sources[79]);
        Assert.Equal(120, sources[129]);
        Assert.Equal(130, sources[39]);
        AssertPermutation(sources);
    }

    [Fact]
    public void Only_the_tiers_that_exist_move()
    {
        var sources = BossRoundsMath.Sources([45], Rounds);
        Assert.Equal(40, sources[44]);
        Assert.Equal(45, sources[39]);
        Assert.Equal(60, sources[59]);
        AssertPermutation(sources);
    }

    [Fact]
    public void A_tier_past_the_last_round_keeps_its_event_round()
    {
        var sources = BossRoundsMath.Sources([40, 60, 80, 100, 150], Rounds);
        Assert.True(BossRoundsMath.IsIdentity(sources));
    }

    [Fact]
    public void Tiers_above_5_have_no_event_round()
    {
        var sources = BossRoundsMath.Sources(TierMath.SpawnRounds(7, "40, 60, 80, 100, 120, 125, 130"), Rounds);
        Assert.True(BossRoundsMath.IsIdentity(sources));
    }

    private static void AssertPermutation(int[] sources) =>
        Assert.Equal(Enumerable.Range(1, sources.Length), sources.OrderBy(round => round));

    [Fact]
    public void Every_tier_plays_its_event_round_on_its_spawn_round()
    {
        var tiers = BossRoundsMath.TierEventRounds([30, 50, 70, 90, 110], Rounds);
        Assert.Equal([(30, 1, 40), (50, 2, 60), (70, 3, 80), (90, 4, 100), (110, 5, 120)], tiers);
        var sources = BossRoundsMath.Sources([30, 50, 70, 90, 110], Rounds);
        Assert.All(tiers, tier => Assert.Equal(tier.EventRound, sources[tier.Round - 1]));
    }

    [Fact]
    public void Only_tiers_that_exist_play_an_event_round() =>
        Assert.Equal([(25, 1, 40)], BossRoundsMath.TierEventRounds([25], Rounds));

    [Fact]
    public void Tiers_past_the_last_round_and_above_5_play_no_event_round()
    {
        var tiers = BossRoundsMath.TierEventRounds([40, 60, 80, 100, 150, 160, 180], Rounds);
        Assert.Equal([1, 2, 3, 4], tiers.Select(tier => tier.Tier));
        Assert.Empty(BossRoundsMath.TierEventRounds([40, 60], 30));
    }
}
