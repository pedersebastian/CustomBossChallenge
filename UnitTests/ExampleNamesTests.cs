using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// An example's name becomes the setup's name, and the Challenge Editor's name field keeps only letters, digits,
/// spaces and . , ! ' / # ? ~: a name with a hyphen or a full-width comma was changed when it was loaded ("Maraton-Lych"
/// in Norwegian), so every translation of an example's name may only use those
/// </summary>
public class ExampleNamesTests
{
    private const string Kept = " .,!'/#?~";

    private static string LocalizationDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Localization"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("Localization not found"), "Localization");
    }

    // Letters and digits, and the marks some scripts write on their letters (Thai vowels and tones)
    private static bool NameField(char c) =>
        char.IsLetterOrDigit(c) || Kept.Contains(c) ||
        char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(LocalizationDir(), "*.json").OrderBy(f => f)) data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Example_names_only_use_what_the_name_field_keeps(string language)
    {
        var texts = JObject.Parse(File.ReadAllText(Path.Combine(LocalizationDir(), language + ".json")));
        var names = texts.Properties()
            .Where(p => p.Name.StartsWith("CustomBossChallenge-Example") && !p.Name.EndsWith("Info") &&
                        p.Name is not ("CustomBossChallenge-ExampleSetups" or "CustomBossChallenge-ExampleLabel"))
            .ToList();
        Assert.NotEmpty(names);
        foreach (var name in names)
        {
            var text = (string) name.Value!;
            var bad = text.Where(c => !NameField(c)).Distinct().ToArray();
            Assert.True(bad.Length == 0, $"{language} {name.Name} \"{text}\" has {string.Join(" ", bad)}, which the name field drops");
        }
    }
}
