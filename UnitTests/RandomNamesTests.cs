using System;
using System.Linq;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The describing names Random gives a setup: the rules, their order and the names they can make
/// </summary>
public class RandomNamesTests
{
    private static readonly NameTraits Lych = new() {Boss = "Lych"};

    private static string? Name(NameTraits traits, int seed = 1) => RandomNames.For(traits, new Random(seed));

    [Fact]
    public void A_setup_without_a_striking_trait_gets_no_name() => Assert.Null(Name(Lych));

    [Fact]
    public void Near_normal_values_are_not_striking() =>
        Assert.Null(Name(Lych with {BossSpeed = 200, BossHealth = 70, Tiers = 7, SpeedPerTier = 10, HealthPerTier = 20}));

    [Theory]
    [InlineData(50, 400, 1, "The Unkillable Snail", "Slow but Unstoppable")]
    [InlineData(400, 50, 5, "Fast and Fragile", "Paper Rocket")]
    [InlineData(400, 100, 1, "Blink and It's Gone", "Gone in a Flash")]
    [InlineData(300, 100, 5, "Turbo Lych", "Lych in a Hurry")]
    [InlineData(100, 300, 5, "Heavyweight Lych", "Lych the Unbreakable")]
    [InlineData(50, 100, 5, "Sleepy Lych", "Lych in Slow Motion")]
    [InlineData(100, 50, 5, "Paper Lych", "Fragile Lych")]
    [InlineData(100, 100, 1, "One and Done", "Just the One")]
    public void Speed_health_and_tiers_pick_the_rule(int speed, int health, int tiers, string one, string other)
    {
        var traits = Lych with {BossSpeed = speed, BossHealth = health, Tiers = tiers};
        Assert.Contains(Name(traits), new[] {one, other});
        Assert.True(RandomNames.Fits(one, traits));
        Assert.True(RandomNames.Fits(other, traits));
    }

    [Fact]
    public void Ten_tougher_tiers_are_the_long_haul() =>
        Assert.Contains(Name(Lych with {Tiers = 10, HealthPerTier = 50}), new[] {"The Long Haul", "It Just Keeps Going"});

    [Fact]
    public void Elite_and_all_fortified_is_armoured_and_angry() =>
        Assert.Contains(Name(Lych with {Boss = "Elite Lych", Elite = true, AllFortified = true}),
            new[] {"Armoured and Angry", "Elite and Armoured"});

    [Fact]
    public void Skulls_off_is_a_skull_free_diet() =>
        Assert.Contains(Name(Lych with {NoSkulls = true}), new[] {"No Skulls Allowed", "No Skulls Attached"});

    [Fact]
    public void The_same_boss_on_every_tier_repeats_it() =>
        Assert.Contains(Name(new NameTraits {SameBoss = "Lych"}), new[] {"Lych, Lych, Lych...", "Nothing but Lych", "Lych on Repeat"});

    [Fact]
    public void The_same_boss_on_two_tiers_is_not_a_repeat() =>
        Assert.Null(Name(new NameTraits {SameBoss = "Lych", Tiers = 2}));

    [Theory]
    [InlineData(10, 0, 0, "Tier After Tier", "The Long Road")]
    [InlineData(5, 25, 0, "Faster Every Time", "Picking Up Speed")]
    [InlineData(5, 0, 50, "Tougher Every Time", "Growing Pains")]
    public void Many_or_changing_tiers_have_names(int tiers, int speedPerTier, int healthPerTier, string one, string other) =>
        Assert.Contains(Name(Lych with {Tiers = tiers, SpeedPerTier = speedPerTier, HealthPerTier = healthPerTier}),
            new[] {one, other});

    [Fact]
    public void All_fortified_alone_is_a_hard_shell() =>
        Assert.Contains(Name(Lych with {AllFortified = true}), new[] {"Hard Shell", "Fortified Everything"});

    [Fact]
    public void The_most_striking_trait_wins()
    {
        // Every trait at once: the repeated boss comes first, then the slow tank, and so on down the list
        var all = new NameTraits
        {
            Boss = "Elite Lych", Elite = true, SameBoss = "Lych", Tiers = 10, BossSpeed = 50, BossHealth = 400,
            SpeedPerTier = 25, HealthPerTier = 50, NoSkulls = true, AllFortified = true
        };
        Assert.True(RandomNames.Fits("Lych on Repeat", all));
        Assert.True(RandomNames.Fits("The Unkillable Snail", all with {SameBoss = null}));
        Assert.True(RandomNames.Fits("The Long Haul", all with {SameBoss = null, BossSpeed = 100}));
        Assert.True(RandomNames.Fits("Armoured and Angry", all with {SameBoss = null, BossSpeed = 100, Tiers = 5}));
        Assert.True(RandomNames.Fits("No Skulls Allowed", all with {SameBoss = null, BossSpeed = 100, Tiers = 5, Elite = false}));
        Assert.True(RandomNames.Fits("Heavyweight Elite Lych",
            all with {SameBoss = null, BossSpeed = 100, Tiers = 5, Elite = false, NoSkulls = false}));
        Assert.False(RandomNames.Fits("Hard Shell", all));
    }

    [Fact]
    public void Names_with_the_boss_are_left_out_when_the_tiers_have_different_bosses()
    {
        // Turbo {0} has no boss to name, so the next rule that has a name for the setup wins
        var gauntlet = new NameTraits {BossSpeed = 300, Tiers = 1};
        Assert.Contains(Name(gauntlet), new[] {"Blink and It's Gone", "Gone in a Flash"});
        Assert.Null(Name(gauntlet with {Tiers = 5}));
        Assert.Contains(Name(gauntlet with {Tiers = 5, AllFortified = true}), new[] {"Hard Shell", "Fortified Everything"});
    }

    [Fact]
    public void A_fixed_seed_gives_the_same_name()
    {
        var traits = Lych with {BossSpeed = 400};
        Assert.Equal(Name(traits, 7), Name(traits, 7));
        Assert.Equal(2, Enumerable.Range(0, 50).Select(seed => Name(traits, seed)).Distinct().Count());
    }

    [Fact]
    public void A_name_too_long_for_the_name_field_is_left_out()
    {
        var traits = new NameTraits {Boss = "Blastapopoulos", SameBoss = "Blastapopoulos", Tiers = 5};
        var names = Enumerable.Range(0, 50).Select(seed => Name(traits, seed)).Distinct().ToList();
        Assert.Equal(new[] {"Blastapopoulos on Repeat", "Nothing but Blastapopoulos"}, names.Order());
        Assert.False(RandomNames.IsGenerated("Blastapopoulos, Blastapopoulos, Blastapopoulos..."));
    }

    [Fact]
    public void Every_name_is_generated_and_fits_a_challenge_name()
    {
        var names = RandomNames.AllNames.ToList();
        Assert.Contains("Turbo Elite Blastapopoulos", names);
        Assert.Contains("Diamondback, Diamondback, Diamondback...", names);
        Assert.DoesNotContain(names, name => name.Contains('{'));
        Assert.All(names, name =>
        {
            Assert.True(RandomNames.IsGenerated(name), name);
            Assert.InRange(name.Length, 1, RandomNames.MaxLength);
            Assert.Equal(name.Trim(), name);
            Assert.All(name, c => Assert.True(char.IsAsciiLetterOrDigit(c) || RandomNames.FieldPunctuation.Contains(c),
                $"'{c}' in {name}, which the game's name field drops"));
        });
    }

    [Theory]
    [InlineData("My Own Challenge")]
    [InlineData("Custom Lych")]
    [InlineData("turbo lych")]
    [InlineData("Turbo ")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_names_are_not_generated(string? name) => Assert.False(RandomNames.IsGenerated(name));
}
