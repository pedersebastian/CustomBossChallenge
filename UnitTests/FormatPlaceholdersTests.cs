using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Placeholders stay whole while the Arabic shaper turns a text around (#244)
/// </summary>
public class FormatPlaceholdersTests
{
    [Fact]
    public void Each_placeholder_is_one_letter_and_comes_back()
    {
        var hidden = FormatPlaceholders.Hide("a ({0}) b {1:0.0} {0}", out var placeholders);
        Assert.Equal("a (\uE000) b \uE001 \uE002", hidden);
        Assert.Equal(["{0}", "{1:0.0}", "{0}"], placeholders);
        Assert.Equal("{0} {1:0.0} b ){0}( a", FormatPlaceholders.Show("\uE002 \uE001 b )\uE000( a", placeholders));
    }

    [Fact]
    public void A_text_without_placeholders_stays_as_it_is()
    {
        Assert.Equal("no {x} here", FormatPlaceholders.Hide("no {x} here", out var placeholders));
        Assert.Empty(placeholders);
    }

    [Fact]
    public void A_text_that_already_has_the_letters_is_left_alone()
    {
        Assert.Equal("\uE000 {0}", FormatPlaceholders.Hide("\uE000 {0}", out var placeholders));
        Assert.Empty(placeholders);
    }
}
