using System;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Boss Event Rounds in CHIMPS play the CHIMPS boss events' own rounds, whatever the boss: the newest event's
/// (chimpsboss) or the earlier ones' (chimpsbloonarius), as the player picks (#139)
/// </summary>
public class ChimpsRoundSetTests
{
    public static TheoryData<BossType> Bosses => new(Enum.GetValues<BossType>());

    [Theory]
    [MemberData(nameof(Bosses))]
    public void Chimps_plays_the_picked_chimps_events_rounds_for_every_boss(BossType boss)
    {
        Assert.Equal("chimpsboss", BossOptions.EventRoundSet(boss, "Clicks"));
        Assert.Equal("chimpsboss", BossOptions.EventRoundSet(boss, "Clicks", ChimpsRounds.Newest));
        Assert.Equal("chimpsbloonarius", BossOptions.EventRoundSet(boss, "Clicks", ChimpsRounds.Earlier));
    }

    [Theory]
    [MemberData(nameof(Bosses))]
    public void Chimps_can_play_the_boss_own_rounds_like_the_other_modes(BossType boss)
    {
        Assert.Equal(BossOptions.BossEventRoundSet(boss), BossOptions.EventRoundSet(boss, "Clicks", ChimpsRounds.Own));
        Assert.Null(BossOptions.ChimpsEventRoundSet(ChimpsRounds.Own));
        Assert.False(BossOptions.IsChimpsEventRoundSet(BossOptions.EventRoundSet(boss, "Clicks", ChimpsRounds.Own)));
    }

    [Fact]
    public void With_the_boss_own_rounds_every_tier_gets_its_own_boss_round_in_chimps()
    {
        var options = new BossOptions {Tiers = 3, TierBosses = "Phayze,Lych,Vortex", ChimpsRounds = ChimpsRounds.Own};
        Assert.Equal("phayze", options.BaseEventRoundSetIn("Clicks"));
        Assert.Equal("lych", options.EventRoundSetIn(2, "Clicks"));
        Assert.Equal("vortex", options.EventRoundSetIn(3, "Clicks"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Standard")]
    [InlineData("Impoppable")]
    [InlineData("HalfCash")]
    [InlineData("AlternateBloonsRounds")]
    [InlineData("clicks")]
    public void Other_modes_play_the_boss_own_rounds(string? mode)
    {
        Assert.Equal("lych", BossOptions.EventRoundSet(BossType.Lych, mode, ChimpsRounds.Earlier));
        Assert.Equal("vortex", new BossOptions {Boss = BossType.Vortex, ChimpsRounds = ChimpsRounds.Earlier}.BaseEventRoundSetIn(mode));
    }

    [Fact]
    public void The_match_set_follows_the_first_tier_the_mode_and_the_pick()
    {
        var options = new BossOptions {Tiers = 2, TierBosses = "Phayze,Lych"};
        Assert.Equal("phayze", options.BaseEventRoundSetIn("Standard"));
        Assert.Equal("lych", options.EventRoundSetIn(2, "Standard"));
        Assert.Equal("chimpsboss", options.BaseEventRoundSetIn("Clicks"));
        Assert.Equal("chimpsbloonarius", (options with {ChimpsRounds = ChimpsRounds.Earlier}).EventRoundSetIn(2, "Clicks"));
    }

    [Theory]
    [InlineData("chimpsboss", true)]
    [InlineData("chimpsbloonarius", true)]
    [InlineData("bloonarius", false)]
    [InlineData(null, false)]
    public void Both_chimps_sets_are_chimps_sets(string? name, bool chimps) =>
        Assert.Equal(chimps, BossOptions.IsChimpsEventRoundSet(name));

    [Theory]
    [InlineData("chimpsboss", 498)]
    [InlineData("chimpsbloonarius", 688)]
    public void The_mods_copies_have_every_round_once_in_order(string set, int groupCount)
    {
        var rounds = ChimpsRoundSet.Rounds(set);
        Assert.Equal(Enumerable.Range(1, 140), rounds.Select(round => round.Number));
        Assert.DoesNotContain(rounds, round => round.AddToRound);
        Assert.All(rounds, round => Assert.NotEmpty(round.Groups));

        var groups = rounds.SelectMany(round => round.Groups).ToList();
        Assert.Equal(groupCount, groups.Count);
        Assert.All(groups, group =>
        {
            Assert.Matches("^[A-Z][A-Za-z]+$", group.Bloon);
            Assert.True(group.Count > 0, $"{group.Bloon} has no bloons");
            Assert.True(group.Duration >= 0, $"{group.Bloon} ends before it starts");
            Assert.True(group.End >= group.Start - 0.5f, $"{group.Bloon} starts long before 0");
        });
    }

    [Fact]
    public void The_copies_have_the_events_rounds()
    {
        var newest = ChimpsRoundSet.Rounds("chimpsboss");
        Assert.Equal(new ChimpsRoundSet.Group("Red", 60, 0, 17.5125f), newest[0].Groups.Single());
        Assert.Equal(new ChimpsRoundSet.Group("Moab", 10, 0, 10), newest[39].Groups.Single());
        Assert.Equal(new ChimpsRoundSet.Group("Bad", 10, 0, 25), newest[99].Groups.Single());

        var earlier = ChimpsRoundSet.Rounds("chimpsbloonarius");
        Assert.Equal(new ChimpsRoundSet.Group("Red", 25, 0, 17.5125f), earlier[0].Groups[0]);
        Assert.Equal("Moab Ceramic", string.Join(" ", earlier[39].Groups.Select(group => group.Bloon)));
        Assert.Equal("Bad", earlier[99].Groups.Single().Bloon);

        foreach (var rounds in new[] {newest, earlier})
        {
            Assert.Equal("BadFortified Bad", string.Join(" ", rounds[139].Groups.Select(group => group.Bloon)));
            Assert.Equal(45, rounds[139].Groups[1].Start);
        }
    }

    [Fact]
    public void The_two_sets_differ()
    {
        var newest = ChimpsRoundSet.Rounds("chimpsboss");
        var earlier = ChimpsRoundSet.Rounds("chimpsbloonarius");
        var differ = newest.Zip(earlier).Count(pair => !pair.First.Groups.SequenceEqual(pair.Second.Groups));
        Assert.True(differ > 100, $"Only {differ} rounds differ");
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

    /// <summary>Options as JSON the way SetupCode writes them, enums by name</summary>
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = {new StringEnumConverter()}
    });

    private static JObject Json(BossOptions options) => JObject.FromObject(options, Serializer);

    [Fact]
    public void The_newest_set_is_the_default_and_left_out_of_codes_and_saves()
    {
        Assert.Equal(ChimpsRounds.Newest, new BossOptions().ChimpsRounds);
        Assert.Null(Json(new BossOptions {BossEventRounds = true})["ChimpsRounds"]);
        Assert.DoesNotContain("ChimpsRounds", JsonConvert.SerializeObject(new BossOptions()));
        Assert.Equal(ChimpsRounds.Newest, JsonConvert.DeserializeObject<BossOptions>("{\"BossEventRounds\": true}")!.ChimpsRounds);
    }

    [Fact]
    public void The_earlier_set_goes_into_codes_and_reads_back()
    {
        var options = new BossOptions {BossEventRounds = true, ChimpsRounds = ChimpsRounds.Earlier};
        var defaults = Json(new BossOptions());
        var diff = (JObject) CodeFormat.Diff(Json(options), defaults)!;
        Assert.Equal("Earlier", (string?) diff["ChimpsRounds"]);

        var typed = CodeFormat.TypedOptions(diff);
        Assert.Equal("Earlier", (string?) typed["ChimpsRounds"]);
        var read = CodeFormat.Apply(defaults.DeepClone(), typed).ToObject<BossOptions>(Serializer);
        Assert.Equal(options, read);
    }

    [Fact]
    public void The_boss_own_rounds_go_into_codes_and_read_back()
    {
        var options = new BossOptions {BossEventRounds = true, ChimpsRounds = ChimpsRounds.Own};
        var defaults = Json(new BossOptions());
        var diff = (JObject) CodeFormat.Diff(Json(options), defaults)!;
        Assert.Equal("Own", (string?) diff["ChimpsRounds"]);
        var read = CodeFormat.Apply(defaults.DeepClone(), CodeFormat.TypedOptions(diff)).ToObject<BossOptions>(Serializer);
        Assert.Equal(options, read);
        Assert.True(CodeFormat.TryUnpack(CodeFormat.Pack("{\"o\":" + diff.ToString(Formatting.None) + ",\"c\":{}}",
            CodeFormat.Version5Prefix), out _, out var problem, out _), $"{problem}");
    }

    [Theory]
    [InlineData(2, ChimpsRounds.Own)]
    [InlineData(3, ChimpsRounds.Newest)]
    public void A_number_keeps_its_meaning_and_an_unknown_one_plays_the_newest(int number, ChimpsRounds expected) =>
        Assert.Equal(expected, new BossOptions {ChimpsRounds = (ChimpsRounds) number}.Sanitized().ChimpsRounds);

    [Fact]
    public void A_chimps_set_that_isnt_one_cant_be_read() =>
        Assert.Throws<FormatException>(() => CodeFormat.TypedOptions(new JObject {["ChimpsRounds"] = true}));
}
