using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CustomBossChallenge.Config;
using Newtonsoft.Json;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The made-up language that finds texts too long for their box and texts that skip the localization (#267)
/// </summary>
public class PseudoLocalizationTests
{
    [Theory]
    [InlineData("Speed per Tier", "[Śpééd pér Tíér ~~~]")]
    [InlineData("Play", "[Płáy ~]")]
    [InlineData("", "")]
    public void Make_accents_brackets_and_pads(string english, string expected) =>
        Assert.Equal(expected, PseudoLocalization.Make(english));

    [Fact]
    public void Make_keeps_placeholders_and_tags()
    {
        var pseudo = PseudoLocalization.Make("<b>Elite</b> {0} on tier {1:0.0} <color=#FFD24A>now</color>");
        Assert.StartsWith("[<b>Éłíté</b> {0} óñ tíér {1:0.0} <color=#FFD24A>ñów</color> ~", pseudo);
        Assert.EndsWith("~]", pseudo);
    }

    [Fact]
    public void Make_grows_a_text_by_about_40_percent()
    {
        var english = new string('x', 100);
        Assert.Equal(140, PseudoLocalization.Make(english).Length);
    }

    [Theory]
    [InlineData("[Śpééd ~~] 20 %", new string[0])]
    [InlineData("Lych [Éłíté ~]", new[] {"Lych"})]
    [InlineData("<b>Boss</b> [Tíér [Bóśś ~] ~]", new[] {"Boss"})]
    [InlineData("Tier 2 [Cut óff", new[] {"Tier"})]
    [InlineData("x ok", new[] {"ok"})]
    public void PlainWords_are_outside_every_pseudo_text(string shown, string[] expected) =>
        Assert.Equal(expected, PseudoLocalization.PlainWords(shown));

    [Fact]
    public void Every_English_text_keeps_its_placeholders_and_gets_longer()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "Localization", "English.json"))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("Localization/English.json not found above " + AppContext.BaseDirectory);

        var english = JsonConvert.DeserializeObject<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(folder.FullName, "Localization", "English.json")))!;
        var pseudo = PseudoLocalization.MakeAll(english);
        Assert.Equal(english.Keys, pseudo.Keys);
        foreach (var (key, text) in english)
        {
            Assert.Equal(Placeholders(text), Placeholders(pseudo[key]));
            Assert.True(pseudo[key].Length > text.Length, key);
            Assert.Empty(PseudoLocalization.PlainWords(pseudo[key]));
        }
    }

    private static string Placeholders(string text) =>
        string.Join(",", Regex.Matches(text, @"\{\d+(?::[^{}]*)?\}").Select(match => match.Value).OrderBy(value => value));
}
