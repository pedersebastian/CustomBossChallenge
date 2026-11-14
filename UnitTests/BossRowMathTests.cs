using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The Boss Type row has room for more bosses than the game has now (#170)
/// </summary>
public class BossRowMathTests
{
    /// <summary>EditorRows.ControlWidth, the width of a row's controls, which ends before the row's reset button</summary>
    private const float Width = 1990;

    [Fact]
    public void Seven_bosses_keep_the_full_size()
    {
        Assert.Equal(BossRowMath.MaxStep, BossRowMath.Step(7, Width));
        Assert.Equal(190, BossRowMath.ButtonSize(7, Width), 3);
        // Where the Elite label was before the row was worked out from the number of bosses
        Assert.Equal(100 + 7 * 215 + 230, BossRowMath.EliteLabelX(7, Width), 3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(12)]
    public void The_Elite_label_ends_inside_the_controls_and_the_buttons_stay_apart(int bosses)
    {
        Assert.True(BossRowMath.RightEnd(bosses, Width) <= Width,
            $"{bosses} bosses: the Elite label ends at {BossRowMath.RightEnd(bosses, Width)}, past {Width}");
        Assert.True(BossRowMath.ButtonSize(bosses, Width) < BossRowMath.Step(bosses, Width), $"{bosses} bosses: the buttons touch");
        // The Elite tick (150 wide) comes after the last boss button
        var lastRight = BossRowMath.ButtonX(bosses - 1, bosses, Width) + BossRowMath.ButtonSize(bosses, Width) / 2;
        Assert.True(lastRight <= BossRowMath.EliteTickX(bosses, Width) - 75, $"{bosses} bosses: the last boss button covers the Elite tick");
    }

    [Fact]
    public void An_eighth_boss_still_gets_a_button_big_enough_to_tap()
    {
        Assert.True(BossRowMath.ButtonSize(8, Width) >= 150, $"Eight bosses get buttons of {BossRowMath.ButtonSize(8, Width)}");
    }
}
