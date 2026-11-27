using System;
using System.Collections.Generic;
using System.IO;
using CustomBossChallenge.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Skull threshold calculations
/// </summary>
public class SkullMathTests
{
    /// <summary>
    /// The custom skulls the website reads from the same file (web/test/skull-cases.json), so both round alike
    /// </summary>
    public static IEnumerable<object[]> ShownCases()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "web", "test", "skull-cases.json"))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("web/test/skull-cases.json not found above " + AppContext.BaseDirectory);

        var file = JObject.Parse(File.ReadAllText(Path.Combine(folder.FullName, "web", "test", "skull-cases.json")));
        foreach (var entry in (JArray) file["cases"]!) yield return [(string) entry["text"]!, (string) entry["shown"]!];
    }

    [Theory]
    [MemberData(nameof(ShownCases))]
    public void A_custom_skull_shows_like_on_the_website(string text, string shown) =>
        Assert.Equal(shown, SkullMath.NormalizeCustom(text, "-"));

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

    [Fact]
    public void Cycle_goes_round_Dreadbloons_immunities_for_more_skulls() =>
        Assert.Equal(new[] {"Primary", "Military", "Magic", "Support", "Primary", "Military", "Magic", "Support", "Primary"},
            SkullMath.Cycle(new[] {"Primary", "Military", "Magic", "Support"}, 9));

    [Fact]
    public void Cycle_of_a_list_that_already_repeats_keeps_going_round() =>
        Assert.Equal(new[] {1, 2, 1, 2, 1}, SkullMath.Cycle(new[] {1, 2, 1, 2}, 5));

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void Cycle_never_shortens_the_bosss_own_list(int count) =>
        Assert.Equal(new[] {1, 2, 3, 4}, SkullMath.Cycle(new[] {1, 2, 3, 4}, count));

    [Fact]
    public void Cycle_of_nothing_stays_empty() => Assert.Empty(SkullMath.Cycle(Array.Empty<int>(), 5));

    [Fact]
    public void ContinueSteps_grows_Phayzes_power_by_its_last_step() =>
        Assert.Equal(new[] {1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.35f}, SkullMath.ContinueSteps(new[] {1.1f, 1.15f, 1.2f}, 6));

    [Fact]
    public void ContinueSteps_goes_on_like_Elite_Phayze() =>
        // Elite Phayze 1 has the normal one's levels and two more
        Assert.Equal(new[] {1.1f, 1.15f, 1.2f, 1.25f, 1.3f}, SkullMath.ContinueSteps(new[] {1.1f, 1.15f, 1.2f}, 5));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ContinueSteps_never_shortens_the_bosss_own_list(int count) =>
        Assert.Equal(new[] {1.1f, 1.15f, 1.2f}, SkullMath.ContinueSteps(new[] {1.1f, 1.15f, 1.2f}, count));

    [Fact]
    public void ContinueSteps_repeats_a_single_value() =>
        Assert.Equal(new[] {1.5f, 1.5f, 1.5f}, SkullMath.ContinueSteps(new[] {1.5f}, 3));

    [Fact]
    public void ContinueSteps_of_nothing_stays_empty() => Assert.Empty(SkullMath.ContinueSteps(Array.Empty<float>(), 5));
}
