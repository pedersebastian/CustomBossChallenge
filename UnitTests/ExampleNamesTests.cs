using System;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// An example's name becomes the setup's name, and the Challenge Editor's name field keeps only ASCII letters, digits
/// and some punctuation (<see cref="NameField"/>): a translated name it would change ("Ingen hodeskaller, ingen nåde")
/// is replaced by the English one, so in every language the setup's name fits the field (#218)
/// </summary>
public class ExampleNamesTests
{
    private static string LocalizationDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Localization"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("Localization not found"), "Localization");
    }

    private static JObject Texts(string language) =>
        JObject.Parse(File.ReadAllText(Path.Combine(LocalizationDir(), language + ".json")));

    /// <summary>The examples' name keys, with their English names</summary>
    private static (string Key, string English)[] Names()
    {
        var english = Texts("English");
        return english.Properties()
            .Where(p => p.Name.StartsWith("CustomBossChallenge-Example") && !p.Name.EndsWith("Info") &&
                        p.Name is not ("CustomBossChallenge-ExampleSetups" or "CustomBossChallenge-ExampleLabel"))
            .Select(p => (p.Name, (string) p.Value!))
            .ToArray();
    }

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(LocalizationDir(), "*.json").OrderBy(f => f)) data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_examples_setup_name_fits_the_name_field(string language)
    {
        var texts = Texts(language);
        var names = Names();
        Assert.NotEmpty(names);
        foreach (var (key, english) in names)
        {
            Assert.True(NameField.Fits(english), $"English {key} \"{english}\" doesn't fit the name field");
            var name = NameField.Translated((string) texts[key]!, english);
            Assert.True(NameField.Fits(name), $"{language} {key} \"{name}\"");
        }
    }

    [Fact]
    public void A_translation_the_field_keeps_stays_and_others_are_English()
    {
        var norwegian = Texts("Norwegian");
        Assert.Equal("Maraton Lych", NameField.Translated((string) norwegian["CustomBossChallenge-ExampleMarathonLych"]!, "Marathon Lych"));
        Assert.Equal("No Skulls, No Mercy",
            NameField.Translated((string) norwegian["CustomBossChallenge-ExampleNoSkullsNoMercy"]!, "No Skulls, No Mercy"));
        Assert.Equal("Boss Gauntlet", NameField.Translated("ด่านบอสต่อเนื่อง", "Boss Gauntlet"));
    }
}
