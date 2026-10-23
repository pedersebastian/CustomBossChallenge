using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Skull threshold calculations
/// </summary>
public class SkullMathTests
{
    [Theory]
    [InlineData(3, new[] {0.75f, 0.5f, 0.25f})]
    [InlineData(4, new[] {0.8f, 0.6f, 0.4f, 0.2f})]
    [InlineData(1, new[] {0.5f})]
    [InlineData(0, new float[0])]
    [InlineData(-2, new float[0])]
    public void EvenlySpaced_splits_the_health_bar_equally(int count, float[] expected) =>
        Assert.Equal(expected, SkullMath.EvenlySpaced(count));

    [Fact]
    public void EvenlySpaced_is_capped() =>
        Assert.Equal(SkullMath.MaxSkulls, SkullMath.EvenlySpaced(99).Length);

    [Theory]
    [InlineData("75,50,25", new[] {0.75f, 0.5f, 0.25f})]
    [InlineData(" 25 ; 90%, 50, 50 ", new[] {0.9f, 0.5f, 0.25f})]
    [InlineData("abc, 0, 100, 150, 33.5", new[] {0.335f})]
    [InlineData("1, 99", new[] {0.99f, 0.01f})]
    public void ParseCustom_sorts_and_drops_duplicates_and_values_outside_1_to_99(string text, float[] expected) =>
        Assert.Equal(expected, SkullMath.ParseCustom(text));

    [Theory]
    [InlineData("nothing valid")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseCustom_without_a_valid_value_gives_null(string? text) =>
        Assert.Null(SkullMath.ParseCustom(text));

    [Fact]
    public void ParseCustom_keeps_at_most_the_maximum_highest_first()
    {
        var thresholds = SkullMath.ParseCustom("5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60");

        Assert.NotNull(thresholds);
        Assert.Equal(SkullMath.MaxSkulls, thresholds.Length);
        Assert.Equal(0.6f, thresholds[0]);
        Assert.Equal(0.15f, thresholds[^1]);
    }

    [Theory]
    [InlineData("25,25,50", "50, 25")]
    [InlineData(" 33.5% ; 90 abc", "90, 33.5")]
    [InlineData("33,5", "33, 5")]
    [InlineData("junk, 0, 100", "fallback")]
    [InlineData(null, "fallback")]
    public void NormalizeCustom_shows_the_list_as_it_is_understood(string? text, string expected) =>
        Assert.Equal(expected, SkullMath.NormalizeCustom(text, "fallback"));

    [Fact]
    public void FormatCustom_writes_percentages_without_trailing_zeros() =>
        Assert.Equal("50, 33.5, 12.35", SkullMath.FormatCustom(new[] {0.5f, 0.335f, 0.12345f}));
}
