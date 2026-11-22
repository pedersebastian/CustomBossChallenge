using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The difficulty rating (#263), with the cases in web/test/difficulty-cases.json that the website's port reads too
/// </summary>
public class DifficultyRatingTests
{
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = {new StringEnumConverter()}
    });

    public static IEnumerable<object[]> Cases()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "web", "test", "difficulty-cases.json")))
            folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("web/test/difficulty-cases.json not found above " + AppContext.BaseDirectory);

        var file = JObject.Parse(File.ReadAllText(Path.Combine(folder.FullName, "web", "test", "difficulty-cases.json")));
        foreach (var entry in (JArray) file["cases"]!)
        {
            yield return [(string) entry["name"]!, entry["facts"]!.ToString(Formatting.None), (double) entry["points"]!,
                (int) entry["stars"]!];
        }
    }

    [Fact]
    public void There_are_shared_cases() => Assert.True(Cases().Count() >= 40);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Each_case_gets_its_points_and_stars(string name, string facts, double points, int stars)
    {
        var read = JObject.Parse(facts).ToObject<SetupFacts>(Serializer)!;
        Assert.True(Math.Abs(DifficultyRating.Points(read) - points) < 1e-3,
            $"{name}: expected {points} points, got {DifficultyRating.Points(read)}");
        Assert.Equal(stars, DifficultyRating.Stars(read));
    }

    [Fact]
    public void Stars_go_from_1_to_5()
    {
        Assert.Equal(1, DifficultyRating.StarsOf(-100));
        Assert.Equal(DifficultyRating.MaxStars, DifficultyRating.StarsOf(100));
        for (var i = 0; i < DifficultyRating.StarSteps.Length; i++)
        {
            Assert.Equal(i + 2, DifficultyRating.StarsOf(DifficultyRating.StarSteps[i]));
            Assert.Equal(i + 1, DifficultyRating.StarsOf(DifficultyRating.StarSteps[i] - 0.001));
        }
    }

    [Fact]
    public void A_normal_boss_event_gets_one_star_and_an_Elite_one_four()
    {
        Assert.Equal(1, DifficultyRating.Stars(new SetupFacts {Options = BossOptions.NewSetup}));
        Assert.Equal(4, DifficultyRating.Stars(new SetupFacts {Options = BossOptions.NewSetup with {Elite = true}}));
    }

    [Fact]
    public void Every_boss_has_a_weight_and_Phayze_and_Lych_count_the_most()
    {
        foreach (var boss in Enum.GetValues<BossType>()) Assert.InRange(DifficultyRating.BossWeight(boss), 0, 1);
        var bloonarius = DifficultyRating.BossWeight(BossType.Bloonarius);
        Assert.True(DifficultyRating.BossWeight(BossType.Phayze) > bloonarius);
        Assert.True(DifficultyRating.BossWeight(BossType.Lych) > bloonarius);
    }

    [Fact]
    public void More_of_anything_hard_never_lowers_the_score()
    {
        var plain = new SetupFacts {Options = BossOptions.NewSetup};
        var points = DifficultyRating.Points(plain);
        Assert.True(DifficultyRating.Points(plain with {BossHealth = 2}) > points);
        Assert.True(DifficultyRating.Points(plain with {Options = plain.Options with {Tiers = 6}}) > points);
        Assert.True(DifficultyRating.Points(plain with {Options = plain.Options with {HealthPerTier = 50}}) > points);
        Assert.True(DifficultyRating.Points(plain with {Lives = 1}) > points);
        Assert.True(DifficultyRating.Points(plain with {BannedTowers = 5, AllTowers = 25}) > points);
        Assert.True(DifficultyRating.Points(plain with {Mode = BossOptions.ChimpsMode, Difficulty = "Hard"}) > points);
    }
}
