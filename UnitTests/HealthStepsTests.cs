using System.Linq;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The Boss Health sliders' steps (#250): 5 % to 500 % in steps of 5, then on to 5000 % in steps of 10
/// </summary>
public class HealthStepsTests
{
    [Fact]
    public void The_range_is_5_to_5000_percent()
    {
        Assert.Equal(5, HealthSteps.Min);
        Assert.Equal(5000, HealthSteps.Max);
        Assert.Equal(5, TierBossMath.MinHealth);
        Assert.Equal(5000, TierBossMath.MaxHealth);
        Assert.Equal(5, HealthSteps.ValueAt(0));
        Assert.Equal(5000, HealthSteps.ValueAt(HealthSteps.LastPosition));
        // 5, 10, ..., 500 and 510, 520, ..., 5000
        Assert.Equal(100 + 450 - 1, HealthSteps.LastPosition);
    }

    [Fact]
    public void Every_position_is_a_step_of_5_up_to_500_and_of_10_above()
    {
        var values = Enumerable.Range(0, HealthSteps.LastPosition + 1).Select(HealthSteps.ValueAt).ToList();
        for (var i = 1; i < values.Count; i++)
        {
            Assert.Equal(values[i - 1] < 500 ? 5 : 10, values[i] - values[i - 1]);
        }

        Assert.Contains(100, values);
        Assert.Contains(500, values);
        Assert.DoesNotContain(505, values);
        Assert.All(values, value => Assert.Equal(value, HealthSteps.ValueAt(HealthSteps.PositionOf(value))));
    }

    [Theory]
    [InlineData(-50, 5)]
    [InlineData(0, 5)]
    [InlineData(7, 5)]
    [InlineData(7.5, 10)]
    [InlineData(123, 125)]
    [InlineData(500, 500)]
    [InlineData(504, 500)]
    [InlineData(505, 510)]
    [InlineData(1337, 1340)]
    [InlineData(1335, 1340)]
    [InlineData(4999, 5000)]
    [InlineData(99999, 5000)]
    [InlineData(double.NaN, 5)]
    public void A_value_snaps_to_the_nearest_step(double percent, int expected) =>
        Assert.Equal(expected, HealthSteps.Snap(percent));

    [Theory]
    [InlineData("Lych@100/123", 125)]
    [InlineData("Lych@100/1337", 1340)]
    [InlineData("Lych@100/4200", 4200)]
    [InlineData("Lych@100/9000", 5000)]
    [InlineData("Lych@100/1", 5)]
    [InlineData("Lych@100/junk", 100)]
    public void A_tiers_own_health_lands_on_a_step(string list, int health) =>
        Assert.Equal(health, TierBossMath.Parse(list)[0].Health);
}
