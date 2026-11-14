using System.Globalization;
using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The ranked panel's and the Boss Creator's numbers are the same characters on every system culture (#172)
/// </summary>
public class ShownNumberTests
{
    // Cultures whose own separators or minus the game's font may lack: U+00A0/U+202F (fr, nb, sv), U+2019 (de-CH),
    // U+2212 (sv, nb with ICU)
    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("nb-NO")]
    [InlineData("sv-SE")]
    [InlineData("de-CH")]
    public void Numbers_are_the_same_on_every_culture(string culture)
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            Assert.Equal("12,345", ShownNumber.Grouped(12345));
            Assert.Equal("1,234,567", ShownNumber.Grouped(1234567));
            Assert.Equal("0", ShownNumber.Grouped(0));
            Assert.Equal("-10", ShownNumber.Whole(-10));
            Assert.Equal("250", ShownNumber.Whole(249.6f));
            Assert.Equal("-10", ShownNumber.Signed(-10));
            Assert.Equal("+5", ShownNumber.Signed(5));
            Assert.Equal("0", ShownNumber.Signed(0));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
