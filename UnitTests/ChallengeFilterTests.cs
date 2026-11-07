using System.Collections.Generic;
using CustomBossChallenge.Persistence;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// A code can only set what the Challenge Editor and the Boss Creator can, with the right types (#108)
/// </summary>
public class ChallengeFilterTests
{
    /// <summary>A small default challenge, shaped like the game's</summary>
    private static readonly JObject Defaults = JObject.Parse("""
        {
          "name": "", "map": "Tutorial", "maxParagons": -1, "seed": 0,
          "bloonModifiers": {"speedMultiplier": 1.0, "healthMultipliers": {"boss": 1.0}, "allCamo": false},
          "cashModifiers": {"popCashMultiplier": 1.0},
          "towers": [
            {"tower": "ChosenPrimaryHero", "max": 0, "path1NumBlockedTiers": 0, "isHero": true, "restrictParagon": false},
            {"tower": "DartMonkey", "max": -1, "path1NumBlockedTiers": 0, "isHero": false, "restrictParagon": false},
            {"tower": "BoomerangMonkey", "max": -1, "path1NumBlockedTiers": 0, "isHero": false, "restrictParagon": false}
          ]
        }
        """);

    private static (JObject Kept, List<string> Dropped) Filter(string diff)
    {
        var dropped = new List<string>();
        return (CodeFormat.FilterChallenge(JObject.Parse(diff), Defaults, dropped), dropped);
    }

    [Fact]
    public void What_the_editor_can_set_is_kept_as_it_is()
    {
        const string diff = """
            {"name": "Run", "map": "Logs", "maxParagons": 2, "seed": 123, "noInstaReward": false,
             "startRules": {"cash": 5000, "endRound": 120},
             "bloonModifiers": {"bossSpeedMultiplier": 2.5, "allFortified": true, "healthMultipliers": {"boss": 0.5, "moabs": 3}},
             "towers": {"$items": {"1": {"max": 2, "path1NumBlockedTiers": 3}, "2": {"restrictParagon": true}}}}
            """;
        var (kept, dropped) = Filter(diff);
        Assert.Empty(dropped);
        Assert.True(JToken.DeepEquals(JObject.Parse(diff), kept), kept.ToString());
    }

    [Fact]
    public void Fields_no_player_can_set_are_left_out()
    {
        var (kept, dropped) = Filter("""
            {"cashModifiers": {"popCashMultiplier": "NaN"}, "roundSets": ["anything"], "powers": {"$items": {"0": {"max": 99}}},
             "maxPowers": 5, "rewards": "free", "temporaryUnlockedTowers": ["SuperMonkey"], "disableTowerXp": true,
             "startRules": {"revives": 50, "cash": 100}, "customMapModel": {"x": 1}, "simulationBehaviorsMod": {},
             "$type": "System.IO.FileInfo, System.IO.FileSystem", "__proto__": {"polluted": true}}
            """);
        Assert.Equal("""{"startRules":{"cash":100}}""", kept.ToString(Newtonsoft.Json.Formatting.None));
        Assert.Equal(
            ["cashModifiers", "roundSets", "powers", "maxPowers", "rewards", "temporaryUnlockedTowers", "disableTowerXp",
             "startRules.revives", "customMapModel", "simulationBehaviorsMod", "$type", "__proto__"], dropped);
    }

    [Theory]
    [InlineData("""{"bloonModifiers": null}""", "bloonModifiers")]
    [InlineData("""{"bloonModifiers": 5}""", "bloonModifiers")]
    [InlineData("""{"map": null}""", "map")]
    [InlineData("""{"map": 3}""", "map")]
    [InlineData("""{"maxParagons": "2"}""", "maxParagons")]
    [InlineData("""{"maxParagons": 2.5}""", "maxParagons")]
    [InlineData("""{"maxParagons": 1e10}""", "maxParagons")]
    [InlineData("""{"maxParagons": 99999999999999999999999}""", "maxParagons")]
    [InlineData("""{"bloonModifiers": {"speedMultiplier": NaN}}""", "bloonModifiers.speedMultiplier")]
    [InlineData("""{"bloonModifiers": {"speedMultiplier": Infinity}}""", "bloonModifiers.speedMultiplier")]
    [InlineData("""{"bloonModifiers": {"speedMultiplier": true}}""", "bloonModifiers.speedMultiplier")]
    [InlineData("""{"bloonModifiers": {"allCamo": 1}}""", "bloonModifiers.allCamo")]
    [InlineData("""{"towers": null}""", "towers")]
    [InlineData("""{"towers": {"$items": {"01": {"max": 2}}}}""", "towers[01]")]
    [InlineData("""{"towers": {"$items": {"9": {"max": 2}}}}""", "towers[9]")]
    [InlineData("""{"towers": {"$items": {"-1": {"max": 2}}}}""", "towers[-1]")]
    [InlineData("""{"towers": {"$items": {"1": {"tower": "SuperMonkey"}}}}""", "towers[1].tower")]
    [InlineData("""{"towers": {"$items": {"0": {"isHero": false}}}}""", "towers[0].isHero")]
    public void A_wrong_value_is_left_out_and_named(string diff, string path)
    {
        var (kept, dropped) = Filter(diff);
        Assert.False(kept.HasValues, kept.ToString());
        Assert.Equal([path], dropped);
    }

    [Fact]
    public void Numbers_are_read_as_JavaScript_reads_them()
    {
        var (kept, dropped) = Filter("""{"maxParagons": 5.0, "bloonModifiers": {"speedMultiplier": 1e30, "allCamo": true}}""");
        Assert.Empty(dropped);
        Assert.Equal(JTokenType.Integer, kept["maxParagons"]!.Type);
        Assert.Equal(5, (int) kept["maxParagons"]!);
        Assert.Equal(1e30, (double) kept["bloonModifiers"]!["speedMultiplier"]!);
    }

    [Fact]
    public void A_whole_tower_list_becomes_changes_to_the_known_towers()
    {
        // As in a code made before the game added a tower: the list has a different length, so it is written whole
        var (kept, dropped) = Filter("""
            {"towers": [
              {"tower": "BoomerangMonkey", "max": 3, "path1NumBlockedTiers": 0, "isHero": false, "restrictParagon": false},
              {"tower": "DartMonkey", "max": -1, "path1NumBlockedTiers": 0, "isHero": false, "restrictParagon": false},
              {"tower": "NoSuchTower", "max": 5},
              {"max": 5},
              7
            ]}
            """);
        Assert.Equal("""{"towers":{"$items":{"2":{"max":3}}}}""", kept.ToString(Newtonsoft.Json.Formatting.None));
        Assert.Equal(["towers[2]", "towers[3]", "towers[4]"], dropped);
    }

    [Fact]
    public void An_empty_object_changes_nothing_and_names_nothing()
    {
        var (kept, dropped) = Filter("""{"bloonModifiers": {}}""");
        Assert.False(kept.HasValues);
        Assert.Empty(dropped);
    }

    [Fact]
    public void The_list_of_fields_only_has_known_types()
    {
        static void Check(JToken fields)
        {
            switch (fields)
            {
                case JObject obj:
                    foreach (var (name, value) in obj) if (name != "$key") Check(value!);
                    break;
                case JArray list:
                    Assert.Single(list);
                    Assert.IsType<JObject>(list[0]);
                    Check(list[0]);
                    break;
                default:
                    Assert.Contains((string?) fields, new[] {"text", "int", "float", "bool"});
                    break;
            }
        }

        Check(CodeFormat.ChallengeFields);
    }
}
