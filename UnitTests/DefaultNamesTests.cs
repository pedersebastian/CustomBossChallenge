using System;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// A new setup's name is in the player's language where the Challenge Editor's name field keeps it, else English, and
/// a name given in any language still counts as one the Boss Creator gave (#279)
/// </summary>
public class DefaultNamesTests
{
    private static readonly string[] Bosses = {"Bloonarius", "Lych", "Vortex", "Dreadbloon", "Phayze", "Blastapopoulos"};

    private static string LocalizationDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Localization"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("Localization not found"), "Localization");
    }

    private static DefaultNameTexts Texts(string language)
    {
        var json = JObject.Parse(File.ReadAllText(Path.Combine(LocalizationDir(), language + ".json")));
        string Text(string key) => (string) json["CustomBossChallenge-" + key]!;
        return new DefaultNameTexts(Text("DefaultName"), Text("DefaultEliteName"), Text("MixedName"));
    }

    private static DefaultNameTexts[] AllTexts() =>
        Directory.GetFiles(LocalizationDir(), "*.json").Select(file => Texts(Path.GetFileNameWithoutExtension(file))).ToArray();

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(LocalizationDir(), "*.json").OrderBy(f => f)) data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    [Fact]
    public void English_names_are_as_before()
    {
        Assert.Equal(DefaultNameTexts.English, Texts("English"));
        Assert.Equal("Custom Lych", DefaultNames.For(DefaultNameTexts.English, "Lych"));
        Assert.Equal("Custom Elite Lych", DefaultNames.For(DefaultNameTexts.English, "Lych", true));
        Assert.Equal("Custom Boss Gauntlet", DefaultNames.Mixed(DefaultNameTexts.English));
    }

    [Fact]
    public void A_translation_the_field_keeps_stays_and_others_are_English()
    {
        var norwegian = Texts("Norwegian");
        Assert.Equal("Egen Lych", DefaultNames.For(norwegian, "Lych"));
        Assert.Equal("Egen Elite Vortex", DefaultNames.For(norwegian, "Vortex", true));
        Assert.Equal("Eget bossmaraton", DefaultNames.Mixed(norwegian));

        var french = Texts("French");
        Assert.Equal("Custom Lych", DefaultNames.For(french, "Lych"));
        Assert.Equal("Custom Boss Gauntlet", DefaultNames.Mixed(french));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_default_name_fits_the_name_field(string language)
    {
        var texts = Texts(language);
        foreach (var boss in Bosses)
        {
            Assert.True(NameField.Fits(DefaultNames.For(texts, boss)), $"{language} {boss}");
            Assert.True(NameField.Fits(DefaultNames.For(texts, boss, true)), $"{language} Elite {boss}");
        }
        Assert.True(NameField.Fits(DefaultNames.Mixed(texts)), $"{language} mixed");
    }

    [Fact]
    public void A_name_from_any_language_is_a_default_name()
    {
        var all = AllTexts();
        Assert.True(DefaultNames.IsDefault("Custom Lych", Array.Empty<DefaultNameTexts>(), Bosses));
        Assert.True(DefaultNames.IsDefault("Custom Boss Gauntlet", Array.Empty<DefaultNameTexts>(), Bosses));
        Assert.True(DefaultNames.IsDefault("Egen Elite Lych", all, Bosses), "Norwegian made, read in English");
        Assert.True(DefaultNames.IsDefault("Eigen bossmarathon", all, Bosses), "Dutch mixed");
        Assert.False(DefaultNames.IsDefault("Egen Lych", Array.Empty<DefaultNameTexts>(), Bosses),
            "Only the languages given are known");
        Assert.False(DefaultNames.IsDefault("My Lych", all, Bosses));
        Assert.False(DefaultNames.IsDefault("Egen Ghost", all, Bosses), "Not a boss");
        Assert.False(DefaultNames.IsDefault(null, all, Bosses));
        Assert.False(DefaultNames.IsDefault("", all, Bosses));
    }

    [Fact]
    public void A_broken_placeholder_gives_the_English_name()
    {
        var broken = new DefaultNameTexts("Egen {1}", "Egen {", "Eget");
        Assert.Equal("Custom Lych", DefaultNames.For(broken, "Lych"));
        Assert.Equal("Custom Elite Lych", DefaultNames.For(broken, "Lych", true));
    }
}
