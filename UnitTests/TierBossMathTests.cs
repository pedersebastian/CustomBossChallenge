using System.Linq;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The list of bosses per tier: reading, writing and resolving Random
/// </summary>
public class TierBossMathTests
{
    [Theory]
    [InlineData("Lych,,RandomElite,VortexElite", "Lych,,RandomElite,VortexElite")]
    [InlineData(" lych , VORTEXelite ", "Lych,VortexElite")]
    [InlineData("Lych,,,", "Lych")]
    [InlineData("Nonsense,Lych", ",Lych")]
    [InlineData("", "")]
    [InlineData(",,", "")]
    [InlineData("Lych,Lych,Lych,Lych,Lych,Lych,Lych,Lych,Lych,Lych,Vortex", "Lych,Lych,Lych,Lych,Lych,Lych,Lych,Lych,Lych,Lych")]
    [InlineData("LychElite@120/80,Vortex@100/100", "LychElite@120/80,Vortex")]
    [InlineData(" lych @ 125 / 5000 ", "Lych@130/2000")]
    [InlineData("Lych@0/x", "Lych@10/100")]
    [InlineData("@150/100,,", "@150/100")]
    [InlineData("Nonsense@200/200", "@200/200")]
    [InlineData("Lych@99999999999/100", "Lych")]
    public void Normalize_gives_the_canonical_list(string list, string expected) =>
        Assert.Equal(expected, TierBossMath.Normalize(list));

    [Fact]
    public void Normalize_keeps_null_as_a_single_boss_setup() => Assert.Null(TierBossMath.Normalize(null));

    [Fact]
    public void Entry_reads_each_tier_and_follows_the_setup_past_the_end()
    {
        const string list = "Lych,,RandomElite";
        Assert.Equal(new TierBoss("Lych", false), TierBossMath.Entry(list, 1));
        Assert.True(TierBossMath.Entry(list, 2).FollowsSetup);
        Assert.Equal(new TierBoss(TierBossMath.Random, true), TierBossMath.Entry(list, 3));
        Assert.True(TierBossMath.Entry(list, 3).IsRandom);
        Assert.True(TierBossMath.Entry(list, 7).FollowsSetup);
        Assert.True(TierBossMath.Entry(list, 0).FollowsSetup);
    }

    [Fact]
    public void With_replaces_one_tier_and_pads_the_list()
    {
        Assert.Equal(",,,Phayze", TierBossMath.With("", 4, new TierBoss("Phayze", false)));
        Assert.Equal("Lych", TierBossMath.With("Lych,Vortex", 2, TierBoss.Follows));
        Assert.Equal("LychElite,Vortex", TierBossMath.With("Lych,Vortex", 1, new TierBoss("Lych", true)));
        Assert.Equal("Lych", TierBossMath.With("Lych", 11, new TierBoss("Vortex", false)));
    }

    [Fact]
    public void Step_goes_through_every_boss_and_random_and_wraps()
    {
        var current = TierBoss.Follows;
        var seen = Enumerable.Range(0, TierBossMath.Choices.Length).Select(_ =>
        {
            current = TierBossMath.Step(current, "Bloonarius", true, 1);
            return current;
        }).ToList();

        Assert.Equal(["Lych", "Vortex", "Dreadbloon", "Phayze", "Blastapopoulos", "Diamondback", "Random", "Bloonarius"],
            seen.Select(tier => tier.Boss!));
        Assert.All(seen, tier => Assert.True(tier.Elite));
        Assert.Equal(new TierBoss("Random", false), TierBossMath.Step(new TierBoss("Bloonarius", false), "Lych", true, -1));
    }

    [Fact]
    public void Resolve_gives_every_tier_a_concrete_boss()
    {
        var picks = new[] {3, 6};
        var next = 0;
        var resolved = TierBossMath.Resolve("Lych,,Random,RandomElite", "Vortex", true, 5, _ => picks[next++]);

        Assert.Equal("Lych,VortexElite,Dreadbloon,DiamondbackElite,VortexElite", resolved);
        Assert.False(TierBossMath.HasRandom(resolved, 5));
        Assert.True(TierBossMath.HasRandom("Lych,,Random", 3));
        Assert.False(TierBossMath.HasRandom("Lych,,Random", 2));
    }

    [Fact]
    public void Each_tier_keeps_its_speed_and_health()
    {
        var tier = TierBossMath.Entry("Lych,VortexElite@150/250", 2);
        Assert.Equal(new TierBoss("Vortex", true, 150, 250), tier);
        Assert.Equal(1.5, tier.SpeedFactor);
        Assert.Equal(2.5, tier.HealthFactor);
        Assert.Equal(1, TierBossMath.Entry("Lych", 1).SpeedFactor);

        // Stepping the boss and resolving Random or the setup's boss keep them
        Assert.Equal(new TierBoss("Dreadbloon", true, 150, 250), TierBossMath.Step(tier, "Lych", false, 1));
        Assert.Equal("VortexElite@120/90,Diamondback@200/100",
            TierBossMath.Resolve("@120/90,Random@200/100", "Vortex", true, 2, _ => 6));
    }

    [Fact]
    public void Resolve_gives_tiers_without_an_entry_the_setups_speed_and_health() =>
        Assert.Equal("LychElite@120/90,LychElite@200/300,LychElite@200/300",
            TierBossMath.Resolve("@120/90", "Lych", true, 3, _ => 0, 200, 300));

    [Fact]
    public void Resolve_clamps_a_bad_pick() =>
        Assert.Equal("Diamondback", TierBossMath.Resolve("Random", "Lych", false, 1, _ => 99));
}
