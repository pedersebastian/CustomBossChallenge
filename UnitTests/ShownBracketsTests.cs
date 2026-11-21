using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Brackets face inwards in Arabic texts the shaper has turned around (#244)
/// </summary>
public class ShownBracketsTests
{
    [Theory]
    [InlineData("عجارت )Ctrl+Z(", "عجارت (Ctrl+Z)")]
    [InlineData(")582( ثادحألا", "(582) ثادحألا")]
    [InlineData("(Ctrl+Z) x", "(Ctrl+Z) x")]
    [InlineData("a )b )c( d(", "a (b (c) d)")]
    [InlineData("]x[ )y(", "[x] (y)")]
    [InlineData("only ) one", "only ) one")]
    [InlineData("( open", "( open")]
    [InlineData("", "")]
    public void Every_pair_faces_inwards(string shown, string expected) =>
        Assert.Equal(expected, ShownBrackets.FacingIn(shown));
}
