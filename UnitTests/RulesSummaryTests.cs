using System.Linq;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The lines the pause screen's Rules panel shows for a custom boss game (#141)
/// </summary>
public class RulesSummaryTests
{
    private static string Kinds(BossOptions options) =>
        string.Join(",", RulesSummary.Lines(options).Select(line => line.Kind));

    private static SummaryLine Line(BossOptions options, RuleLine kind) =>
        RulesSummary.Lines(options).Single(line => line.Kind == kind);

    [Fact]
    public void Default_options_give_the_boss_and_no_changes()
    {
        var lines = RulesSummary.Lines(new BossOptions());
        Assert.Equal("Boss,NoChanges", Kinds(new BossOptions()));
        Assert.Equal([BossType.Bloonarius, false], lines[0].Args);
    }

    [Fact]
    public void Elite_is_part_of_the_boss_line_and_not_a_change()
    {
        var options = new BossOptions {Boss = BossType.Lych, Elite = true};
        Assert.Equal("Boss,NoChanges", Kinds(options));
        Assert.Equal([BossType.Lych, true], Line(options, RuleLine.Boss).Args);
    }

    [Fact]
    public void More_tiers_give_the_tier_count_and_the_default_rounds_are_not_listed()
    {
        var options = new BossOptions {Tiers = 7};
        Assert.Equal("Boss,Tiers", Kinds(options));
        Assert.Equal([7], Line(options, RuleLine.Tiers).Args);
    }

    [Fact]
    public void Custom_spawn_rounds_are_listed_as_played()
    {
        var options = new BossOptions {Tiers = 3, SpawnRounds = "30,35,90"};
        Assert.Equal("Boss,Tiers,SpawnRounds", Kinds(options));
        Assert.Equal(["30, 35, 90"], Line(options, RuleLine.SpawnRounds).Args);
    }

    [Fact]
    public void Spawn_rounds_written_out_as_the_defaults_are_no_change() =>
        Assert.Equal("Boss,NoChanges", Kinds(new BossOptions {SpawnRounds = "40, 60, 80, 100, 120"}));

    [Fact]
    public void Boss_event_rounds_are_listed() =>
        Assert.Equal("Boss,BossEventRounds", Kinds(new BossOptions {BossEventRounds = true}));

    [Theory]
    [InlineData(10, -5)]
    [InlineData(-10, 200)]
    public void Speed_and_health_per_tier_keep_their_sign(int speed, int health)
    {
        var options = new BossOptions {SpeedPerTier = speed, HealthPerTier = health};
        Assert.Equal("Boss,SpeedPerTier,HealthPerTier", Kinds(options));
        Assert.Equal([speed], Line(options, RuleLine.SpeedPerTier).Args);
        Assert.Equal([health], Line(options, RuleLine.HealthPerTier).Args);
    }

    [Fact]
    public void Per_tier_values_out_of_range_are_shown_as_played() =>
        Assert.Equal([TierMath.MaxSpeedPerTier], Line(new BossOptions {SpeedPerTier = 999}, RuleLine.SpeedPerTier).Args);

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Evenly_spaced_skulls_give_their_count(int count) =>
        Assert.Equal([SkullMode.EvenlySpaced, count],
            Line(new BossOptions {SkullMode = SkullMode.EvenlySpaced, SkullCount = count}, RuleLine.Skulls).Args);

    [Fact]
    public void Custom_skulls_give_the_thresholds_as_understood() =>
        Assert.Equal([SkullMode.Custom, "50, 25"],
            Line(new BossOptions {SkullMode = SkullMode.Custom, CustomSkulls = "25,junk,50"}, RuleLine.Skulls).Args);

    [Fact]
    public void Custom_skulls_without_a_valid_value_keep_the_boss_skulls() =>
        Assert.Equal("Boss,NoChanges", Kinds(new BossOptions {SkullMode = SkullMode.Custom, CustomSkulls = "junk"}));

    [Fact]
    public void Advanced_mode_has_a_line_per_tier_in_place_of_the_boss()
    {
        Assert.Equal("Tier,Tier,Tier,Tier,Tier,NoChanges", Kinds(new BossOptions {TierBosses = "Lych,,Vortex"}));
        Assert.Equal("Tier,Tier,Tier,Tiers", Kinds(new BossOptions {Tiers = 3, TierBosses = "Lych,,Vortex"}));
    }

    private static object[] Tier(BossOptions options, int tier, int arrivedTier = int.MaxValue) =>
        RulesSummary.Lines(options, arrivedTier).Where(line => line.Kind == RuleLine.Tier).ElementAt(tier - 1).Args;

    [Fact]
    public void A_tier_line_has_its_round_boss_speed_health_and_skulls()
    {
        var options = new BossOptions {Tiers = 3, SpawnRounds = "30, 45, 90", TierBosses = "Lych,VortexElite@120/80#4,Phayze#60%;30%"};
        Assert.Equal([1, 30, BossType.Lych, false, false, 100, 100, SkullMode.Vanilla, ""], Tier(options, 1));
        Assert.Equal([2, 45, BossType.Vortex, true, false, 120, 80, SkullMode.EvenlySpaced, 4], Tier(options, 2));
        Assert.Equal([3, 90, BossType.Phayze, false, false, 100, 100, SkullMode.Custom, "60, 30"], Tier(options, 3));
    }

    [Fact]
    public void A_tier_that_follows_the_setup_has_the_setup_boss()
    {
        var options = new BossOptions {Boss = BossType.Dreadbloon, Elite = true, Tiers = 2, TierBosses = "Lych,"};
        Assert.Equal([2, 60, BossType.Dreadbloon, true, false, 100, 100, SkullMode.Vanilla, ""], Tier(options, 2));
    }

    [Fact]
    public void A_tier_without_skulls_of_its_own_has_the_setup_skulls()
    {
        var options = new BossOptions {Tiers = 2, SkullMode = SkullMode.EvenlySpaced, SkullCount = 2, TierBosses = "Lych,Vortex#0"};
        Assert.Equal(SkullMode.EvenlySpaced, Tier(options, 1)[7]);
        Assert.Equal(2, Tier(options, 1)[8]);
        Assert.Equal(0, Tier(options, 2)[8]);
    }

    [Fact]
    public void Advanced_mode_has_no_spawn_rounds_or_skulls_line()
    {
        var options = new BossOptions
        {
            Tiers = 2, SpawnRounds = "30, 35", SkullMode = SkullMode.Custom, CustomSkulls = "50", TierBosses = "Lych,Vortex"
        };
        Assert.Equal("Tier,Tier,Tiers", Kinds(options));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(5, false)]
    public void A_random_tier_is_a_secret_until_its_boss_arrives(int arrivedTier, bool secret)
    {
        var options = new BossOptions {Tiers = 3, TierBosses = "Lych,Vortex,Phayze", RandomTiers = "2"};
        Assert.Equal(secret, Tier(options, 2, arrivedTier)[4]);
        Assert.Equal(false, Tier(options, 3, arrivedTier)[4]);
    }

    [Fact]
    public void Tier_lines_are_rules_not_modifiers()
    {
        var options = new BossOptions {Tiers = 6, SpeedPerTier = 5, TierBosses = "Lych"};
        Assert.Equal(6, RulesSummary.Rules(options).Count(line => line.Kind == RuleLine.Tier));
        Assert.Equal("Tiers,SpeedPerTier", string.Join(",", RulesSummary.Modifiers(options).Select(l => l.Kind)));
    }

    [Fact]
    public void Sliders_go_to_the_modifiers_and_the_rest_to_the_rules()
    {
        var options = new BossOptions
        {
            Tiers = 6, SpawnRounds = "41", BossEventRounds = true, SpeedPerTier = 6, HealthPerTier = 20,
            SkullMode = SkullMode.EvenlySpaced, SkullCount = 4
        };
        Assert.Equal("Boss,SpawnRounds,BossEventRounds", string.Join(",", RulesSummary.Rules(options).Select(l => l.Kind)));
        Assert.Equal("Tiers,SpeedPerTier,HealthPerTier,Skulls",
            string.Join(",", RulesSummary.Modifiers(options).Select(l => l.Kind)));
    }

    [Fact]
    public void Custom_skulls_are_a_rule_not_a_slider()
    {
        var options = new BossOptions {SkullMode = SkullMode.Custom, CustomSkulls = "75, 50"};
        Assert.Equal("Boss,Skulls", string.Join(",", RulesSummary.Rules(options).Select(l => l.Kind)));
        Assert.Empty(RulesSummary.Modifiers(options));
    }

    [Fact]
    public void Only_sliders_changed_still_has_no_no_changes_line()
    {
        var options = new BossOptions {SpeedPerTier = 10};
        Assert.Equal("Boss", string.Join(",", RulesSummary.Rules(options).Select(l => l.Kind)));
        Assert.Equal("SpeedPerTier", string.Join(",", RulesSummary.Modifiers(options).Select(l => l.Kind)));
    }

    [Fact]
    public void Every_change_comes_in_order()
    {
        var options = new BossOptions
        {
            Boss = BossType.Vortex, Tiers = 6, SpawnRounds = "41", BossEventRounds = true, SpeedPerTier = 5,
            HealthPerTier = 15, SkullMode = SkullMode.EvenlySpaced, SkullCount = 4
        };
        Assert.Equal("Boss,Tiers,SpawnRounds,BossEventRounds,SpeedPerTier,HealthPerTier,Skulls", Kinds(options));
        Assert.Equal(["41, 61, 81, 101, 121, 141"], Line(options, RuleLine.SpawnRounds).Args);
    }
}
