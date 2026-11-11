using System;
using System.Linq;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Boss Event Rounds in CHIMPS play the CHIMPS boss events' own rounds, whatever the boss (#139)
/// </summary>
public class ChimpsRoundSetTests
{
    public static TheoryData<BossType> Bosses => new(Enum.GetValues<BossType>());

    [Theory]
    [MemberData(nameof(Bosses))]
    public void Chimps_plays_the_chimps_events_rounds_for_every_boss(BossType boss) =>
        Assert.Equal("chimpsbloonarius", BossOptions.EventRoundSet(boss, "Clicks"));

    [Theory]
    [InlineData(null)]
    [InlineData("Standard")]
    [InlineData("Impoppable")]
    [InlineData("HalfCash")]
    [InlineData("AlternateBloonsRounds")]
    [InlineData("clicks")]
    public void Other_modes_play_the_boss_own_rounds(string? mode)
    {
        Assert.Equal("lych", BossOptions.EventRoundSet(BossType.Lych, mode));
        Assert.Equal("vortex", new BossOptions {Boss = BossType.Vortex}.BaseEventRoundSetIn(mode));
    }

    [Fact]
    public void The_match_set_follows_the_first_tier_and_the_mode()
    {
        var options = new BossOptions {Tiers = 2, TierBosses = "Phayze,Lych"};
        Assert.Equal("phayze", options.BaseEventRoundSetIn("Standard"));
        Assert.Equal("chimpsbloonarius", options.BaseEventRoundSetIn("Clicks"));
    }

    [Fact]
    public void The_mods_copy_has_every_round_once_in_order()
    {
        var rounds = ChimpsRoundSet.Rounds;
        Assert.Equal(Enumerable.Range(1, 140), rounds.Select(round => round.Number));
        Assert.DoesNotContain(rounds, round => round.AddToRound);
        Assert.All(rounds, round => Assert.NotEmpty(round.Groups));
    }

    [Fact]
    public void Every_group_has_a_bloon_bloons_and_a_time()
    {
        var groups = ChimpsRoundSet.Rounds.SelectMany(round => round.Groups).ToList();
        Assert.Equal(688, groups.Count);
        Assert.All(groups, group =>
        {
            Assert.Matches("^[A-Z][A-Za-z]+$", group.Bloon);
            Assert.True(group.Count > 0, $"{group.Bloon} has no bloons");
            Assert.True(group.Duration >= 0, $"{group.Bloon} ends before it starts");
            Assert.True(group.End >= group.Start - 0.5f, $"{group.Bloon} starts long before 0");
        });
    }

    [Fact]
    public void The_copy_has_the_events_rounds()
    {
        var rounds = ChimpsRoundSet.Rounds;
        Assert.Equal(new ChimpsRoundSet.Group("Red", 25, 0, 17.5125f), rounds[0].Groups[0]);
        Assert.Equal("Moab Ceramic", string.Join(" ", rounds[39].Groups.Select(group => group.Bloon)));
        Assert.Equal("Bad", rounds[99].Groups.Single().Bloon);
        Assert.Equal("BadFortified Bad", string.Join(" ", rounds[139].Groups.Select(group => group.Bloon)));
        Assert.Equal(45, rounds[139].Groups[1].Start);
    }

    [Fact]
    public void Parse_reads_the_games_format_in_round_order()
    {
        var rounds = ChimpsRoundSet.Parse("""
            {"rounds": [
              {"roundNumber": 2, "addToRound": true, "bloonGroups": [{"bloon": "Blue", "count": 3, "start": 1.5, "duration": 2}]},
              {"roundNumber": 1, "addToRound": false, "bloonGroups": []}
            ]}
            """);
        Assert.Equal([1, 2], rounds.Select(round => round.Number));
        Assert.True(rounds[1].AddToRound);
        Assert.Equal(new ChimpsRoundSet.Group("Blue", 3, 1.5f, 2), rounds[1].Groups.Single());
        Assert.Equal(3.5f, rounds[1].Groups[0].End);
    }
}
