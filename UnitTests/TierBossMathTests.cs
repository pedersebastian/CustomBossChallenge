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
    [InlineData("LychElite@120/80#4", "LychElite@120/80#4")]
    [InlineData(" lych # 12 ", "Lych#10")]
    [InlineData("Lych#-3", "Lych#0")]
    [InlineData("Vortex#25;50%;75;junk;50", "Vortex#75%;50%;25%")]
    [InlineData("Vortex#33.5%", "Vortex#33.5%")]
    [InlineData("Lych#junk,Lych#%", "Lych,Lych")]
    [InlineData("#5,,", "#5")]
    [InlineData("Lych#4@150/100", "Lych")]
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
    public void Random_tiers_are_remembered_by_number()
    {
        Assert.Equal("2,5", TierBossMath.RandomTiers("Lych,Random,,Vortex,RandomElite@120/80,Random", 5));
        Assert.Null(TierBossMath.RandomTiers("Lych,,Vortex", 3));
        Assert.Null(TierBossMath.RandomTiers(null, 3));
        Assert.Equal([2, 5], TierBossMath.ParseTierList(" 2, 5 ,x,0,11"));
        Assert.Empty(TierBossMath.ParseTierList(null));
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
    public void Each_tier_keeps_its_skulls()
    {
        var even = TierBossMath.Entry("Lych,Vortex#4", 2);
        Assert.Equal(new TierBoss("Vortex", false, Skulls: SkullMode.EvenlySpaced, SkullCount: 4), even);
        Assert.Equal(new[] {0.8f, 0.6f, 0.4f, 0.2f}, even.SkullPercents);

        var custom = TierBossMath.Entry("Vortex@200/100#60%;30%", 1);
        Assert.Equal(SkullMode.Custom, custom.Skulls);
        Assert.Equal("60, 30", custom.CustomSkulls);
        Assert.Equal(new[] {0.6f, 0.3f}, custom.SkullPercents);
        Assert.Equal(200, custom.Speed);

        // The boss's own skulls, and no skulls at all, which is not the same
        Assert.Null(TierBossMath.Entry("Lych", 1).SkullPercents);
        Assert.Empty(TierBossMath.Entry("Lych#0", 1).SkullPercents!);

        // A mode's leftover count or list doesn't go into the token
        Assert.Equal("Lych", new TierBoss("Lych", false, SkullCount: 7, CustomSkulls: "10").ToString());
        Assert.Equal("Lych", new TierBoss("Lych", false, Skulls: SkullMode.Custom, CustomSkulls: "junk").ToString());

        // Stepping the boss and resolving Random keep them
        Assert.Equal("Dreadbloon#4", TierBossMath.Step(even, "Lych", false, 1).ToString());
        Assert.Equal("Diamondback#60%;30%", TierBossMath.Resolve("Random#60%;30%", "Lych", false, 1, _ => 6));
    }

    [Fact]
    public void Resolve_gives_tiers_without_an_entry_the_setups_speed_health_and_skulls() =>
        Assert.Equal("LychElite@120/90,LychElite@200/300#5,LychElite@200/300#5",
            TierBossMath.Resolve("@120/90", "Lych", true, 3, _ => 0,
                new TierBoss(null, false, 200, 300, SkullMode.EvenlySpaced, 5)));

    [Fact]
    public void Resolve_clamps_a_bad_pick() =>
        Assert.Equal("Diamondback", TierBossMath.Resolve("Random", "Lych", false, 1, _ => 99));
}
