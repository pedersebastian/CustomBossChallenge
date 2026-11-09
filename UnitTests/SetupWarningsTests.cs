using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The warnings the Boss Creator shows before Play (issue #70), with the cases in web/test/setup-warnings-cases.json
/// </summary>
public class SetupWarningsTests
{
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = {new StringEnumConverter()}
    });

    public static IEnumerable<object[]> Cases()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "web", "test", "setup-warnings-cases.json")))
            folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("web/test/setup-warnings-cases.json not found above " + AppContext.BaseDirectory);

        var file = JObject.Parse(File.ReadAllText(Path.Combine(folder.FullName, "web", "test", "setup-warnings-cases.json")));
        foreach (var entry in (JArray) file["cases"]!)
        {
            yield return [(string) entry["name"]!, entry["facts"]!.ToString(Formatting.None),
                string.Join(",", entry["warnings"]!.Select(w => (string) w!))];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Check_gives_the_warnings_of_each_case(string name, string facts, string expected)
    {
        var read = JObject.Parse(facts).ToObject<SetupFacts>(Serializer)!;
        Assert.True(expected == Kinds(read), $"{name}: expected [{expected}], got [{Kinds(read)}]");
    }

    [Theory]
    [InlineData(1, "")]
    [InlineData(35, "")]
    [InlineData(36, "StartRoundClose")]
    [InlineData(39, "StartRoundClose")]
    // On or after the first boss the game can't be played, which BossEditorSection.PlayProblem says
    [InlineData(40, "")]
    [InlineData(41, "")]
    public void StartRoundClose_warns_within_five_rounds_before_the_first_boss(int start, string expected) =>
        Assert.Equal(expected, Kinds(new SetupFacts {StartRound = start}));

    [Fact]
    public void StartRoundClose_says_how_many_rounds_are_left()
    {
        var warning = SetupWarnings.Check(new SetupFacts {StartRound = 37}).Single();
        Assert.Equal([37, 3, 40], warning.Args);
    }

    [Theory]
    [InlineData("40, 60, 80, 100, 120", "")]
    [InlineData("40, 45, 50, 55, 60", "")]
    [InlineData("40, 60, 80, 81, 100", "80-81")]
    [InlineData("40, 44, 80, 81, 83", "40-44, 80-81, 81-83")]
    // Rounds the setup repeats are moved one round apart, which is close too
    [InlineData("40, 40, 40", "40-41, 41-42")]
    public void TiersClose_warns_about_tiers_fewer_than_five_rounds_apart(string spawnRounds, string expected)
    {
        var options = new BossOptions {SpawnRounds = spawnRounds, Tiers = TierMath.ParseRounds(spawnRounds).Count};
        var warning = SetupWarnings.Check(new SetupFacts {Options = options}).Where(w => w.Kind == SetupWarning.TiersClose).ToList();
        Assert.Equal(expected, warning.Count == 0 ? "" : (string) warning[0].Args[0]);
    }

    [Theory]
    [InlineData(1, 2_000_000_000, false)]
    [InlineData(1, 2_000_000_001, true)]
    // Boss Health is applied on top of the model, so it lowers the most a model can have
    [InlineData(5, 400_000_000, false)]
    [InlineData(5, 400_000_001, true)]
    public void HealthCapped_warns_when_a_tier_is_above_the_models_limit(double bossHealth, double tier5Health, bool capped)
    {
        var facts = new SetupFacts {BossHealth = bossHealth, Options = new BossOptions {HealthPerTier = 100}, TierBases = Bases(5, tier5Health / 5)};
        Assert.Equal(capped, SetupWarnings.Check(facts).Any(w => w.Kind == SetupWarning.HealthCapped));
    }

    [Fact]
    public void HealthCapped_says_the_most_health_a_tier_can_have_with_boss_health()
    {
        var facts = new SetupFacts {BossHealth = 4, TierBases = Bases(5, 1e9)};
        var warning = SetupWarnings.Check(facts).First(w => w.Kind == SetupWarning.HealthCapped);
        Assert.Equal(2e9, (double) warning.Args[0], 0);
    }

    [Fact]
    public void Nothing_is_capped_or_the_same_without_the_boss_models() =>
        Assert.Equal("", Kinds(new SetupFacts {Options = new BossOptions {Tiers = 10, SpeedPerTier = 50, HealthPerTier = 200}})
            .Replace("HardSetup", "").Trim(','));

    [Fact]
    public void SameTiers_finds_the_tiers_the_speed_limit_makes_equal()
    {
        // Speed reaches ×5 on tier 9; health stops at the limit from tier 7
        var facts = new SetupFacts
        {
            Options = new BossOptions {Tiers = 10, SpeedPerTier = 50, HealthPerTier = 200},
            TierBases = Bases(10, 1e8)
        };
        Assert.Equal([(9, 10)], SetupWarnings.SameTiers(facts));
        Assert.Equal("9-10", SetupWarnings.Check(facts).Single(w => w.Kind == SetupWarning.SameTiers).Args[0]);
    }

    [Fact]
    public void SameTiers_counts_the_health_limit_not_different_models()
    {
        // Lower every tier, but each tier's model is stronger than the last, so no two tiers end up the same
        var bases = new TierBase?[] {new(100, 1), new(1000, 1), new(10000, 1), new(1e6, 1), new(1e7, 1)};
        var facts = new SetupFacts
        {
            Options = new BossOptions {Tiers = 5, SpeedPerTier = -10, HealthPerTier = -10},
            TierBases = bases
        };
        Assert.Empty(SetupWarnings.SameTiers(facts));

        var capped = facts with {BossHealth = 20, Options = facts.Options with {HealthPerTier = 200, SpeedPerTier = 0}, TierBases = Bases(5, 1e8)};
        // Tier 1 stays under the limit, tiers 2-5 are held to it
        Assert.Equal([(2, 5)], SetupWarnings.SameTiers(capped));
    }

    [Fact]
    public void SameTiers_leaves_tiers_the_settings_keep_equal_alone()
    {
        // Nothing changes from tier to tier on purpose: no limit is involved
        var facts = new SetupFacts {TierBases = Bases(5, 1e5)};
        Assert.Empty(SetupWarnings.SameTiers(facts));
    }

    [Fact]
    public void SameTiers_skips_tiers_with_another_boss()
    {
        var facts = new SetupFacts
        {
            BossHealth = 20,
            Options = new BossOptions {HealthPerTier = 200, TierBosses = ",,Lych,,"},
            TierBases = Bases(5, 6e8)
        };
        Assert.Equal([(1, 2), (4, 5)], SetupWarnings.SameTiers(facts));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public void Difficulty_is_low_for_the_games_own_bosses(double expected)
    {
        var elite = expected > 0;
        Assert.Equal(expected, SetupWarnings.Difficulty(new SetupFacts {Options = new BossOptions {Elite = elite}}), 6);
    }

    [Fact]
    public void Difficulty_counts_doublings_of_the_boss()
    {
        var facts = new SetupFacts {BossHealth = 2, BossSpeed = 2};
        Assert.Equal(2, SetupWarnings.Difficulty(facts), 6);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(10, 1)]
    [InlineData(50, 0.5)]
    [InlineData(150, 0)]
    // The difficulty's own lives
    [InlineData(-1, 0)]
    public void Difficulty_counts_few_lives(int lives, double expected) =>
        Assert.Equal(expected, SetupWarnings.Difficulty(new SetupFacts {Lives = lives}), 6);

    [Fact]
    public void HardSetup_comes_last_and_the_order_is_fixed()
    {
        var facts = new SetupFacts
        {
            StartRound = 38, BossHealth = 20, Lives = 1, NoSelling = true,
            Options = new BossOptions {Elite = true, Tiers = 7, HealthPerTier = 200},
            TierBases = Bases(7, 1e8)
        };
        Assert.Equal("StartRoundClose,HealthCapped,SameTiers,HardSetup", Kinds(facts));
        Assert.Equal("StartRoundClose,TiersClose,HealthCapped,SameTiers,HardSetup",
            Kinds(facts with {Options = facts.Options with {SpawnRounds = "40, 60, 80, 81, 100, 120, 140"}}));
    }

    private static string Kinds(SetupFacts facts) => string.Join(",", SetupWarnings.Check(facts).Select(w => w.Kind));

    private static TierBase?[] Bases(int tiers, double health) =>
        Enumerable.Range(1, tiers).Select(tier => (TierBase?) new TierBase(health * Math.Min(tier, 5) / 5, 2)).ToArray();
}
