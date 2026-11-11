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
    public void Advanced_mode_has_no_boss_line()
    {
        Assert.Equal("NoChanges", Kinds(new BossOptions {TierBosses = "Lych,,Vortex"}));
        Assert.Equal("Tiers", Kinds(new BossOptions {Tiers = 3, TierBosses = "Lych,,Vortex"}));
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
