using CustomBossChallenge.Config;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Tags in texts laid out right to left, for Arabic (#244)
/// </summary>
public class RichTextTests
{
    [Theory]
    [InlineData("<b>Lych</b>", true)]
    [InlineData("<color=#FFD24A>Elite</color>", true)]
    [InlineData("<noparse>a</noparse>", true)]
    [InlineData("<rotate=180>▲</rotate>", true)]
    [InlineData("<Program Directive>", false)]
    [InlineData("a < b > c", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasTags_knows_only_TextMeshPro_tags(string? text, bool expected) =>
        Assert.Equal(expected, RichText.HasTags(text));

    [Theory]
    [InlineData("<b>Lych</b>  <color=#C8D8FF>21-26</color>", "Lych  21-26")]
    [InlineData("<noparse><b>name</noparse> x", "<b>name x")]
    [InlineData("<size=80%>small</size>\nline", "small\nline")]
    [InlineData("no tags <here>", "no tags <here>")]
    public void Strip_keeps_what_shows(string text, string expected) => Assert.Equal(expected, RichText.Strip(text));

    [Fact]
    public void Without_tags_the_text_is_reversed_like_the_games_own() =>
        Assert.Equal("cba 21", RichText.ForRightToLeft("12 abc"));

    [Fact]
    public void Tags_open_before_what_they_wrap()
    {
        Assert.Equal("dc <b>ba</b>", RichText.ForRightToLeft("<b>ab</b> cd"));
        Assert.Equal("<color=#FFD24A>ab</color>", RichText.ForRightToLeft("<color=#FFD24A>ba</color>"));
        Assert.Equal("<b>d<i>cb</i>a</b>", RichText.ForRightToLeft("<b>a<i>bc</i>d</b>"));
    }

    [Fact]
    public void Noparse_keeps_its_inside_as_text()
    {
        Assert.Equal("<noparse>>x< hcyL</noparse>", RichText.ForRightToLeft("<noparse>Lych <x></noparse>"));
        Assert.Equal("<noparse>>b/<</noparse>", RichText.ForRightToLeft("<noparse></b></noparse>"));
    }

    [Fact]
    public void An_unpaired_tag_stays_as_it_is()
    {
        Assert.Equal("ba<br>", RichText.ForRightToLeft("<br>ab"));
        Assert.Equal("b</b>a", RichText.ForRightToLeft("a</b>b"));
    }

    [Fact]
    public void Letters_outside_the_basic_plane_stay_whole() =>
        Assert.Equal("b\U0001F600a", RichText.ForRightToLeft("a\U0001F600b"));

    [Fact]
    public void Laid_out_right_to_left_it_shows_the_same_letters()
    {
        const string text = "<b><noparse>Phayze 32</noparse></b>  <color=#C8D8FF>21 - 26</color> | x";
        Assert.Equal(RichText.Strip(text), Reverse(RichText.Strip(RichText.ForRightToLeft(text))));
    }

    private static string Reverse(string text)
    {
        var letters = text.ToCharArray();
        System.Array.Reverse(letters);
        return new string(letters);
    }
}
