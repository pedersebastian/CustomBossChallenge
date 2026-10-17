using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CustomBossChallenge.Menus;
using Il2CppNinjaKiwi.Localization;
using Newtonsoft.Json;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Every translation has exactly the English keys and placeholders, and the game shows the translation for its
/// active language
/// </summary>
public class LocalizationTest : CustomBossTest
{
    public const string ResourcePrefix = "CustomBossChallenge.Localization.";

    /// <summary>
    /// The languages BTD6 supports (apart from the joke Monkey Cipher)
    /// </summary>
    public static readonly string[] Languages =
    {
        "English", "Arabic", "ChineseSimplified", "ChineseTraditional", "Danish", "Dutch", "Finnish", "French",
        "German", "Italian", "Japanese", "Korean", "Norwegian", "Polish", "Portuguese_Brazil", "Russian", "Spanish",
        "Spanish_LATAM", "Swedish", "Thai", "Turkish"
    };

    protected override IEnumerator Run()
    {
        AssertEquals(Texts.BossCreator, "CustomBossChallenge-BossCreator", "Localization key format");

        var english = Load("English");
        foreach (var language in Languages)
        {
            var table = Load(language);
            AssertEquals(string.Join(",", table.Keys), string.Join(",", english.Keys), $"{language} keys");
            foreach (var (key, text) in english)
            {
                Assert(!string.IsNullOrWhiteSpace(table[key]), $"{language} {key} is empty");
                AssertEquals(Placeholders(table[key]), Placeholders(text), $"{language} {key} placeholders");
            }
        }

        var active = LocalizationManager.Instance.ActiveLanguage.ToString();
        if (Languages.Contains(active))
        {
            AssertEquals(Texts.BossCreator.T(), Load(active)[Texts.BossCreator], $"Boss Creator in {active}");
            AssertEquals(Texts.BossTiersInfo.T(), Load(active)[Texts.BossTiersInfo], $"Boss Tiers info in {active}");
        }
        Log($"active language {active}: {Texts.BossCreator.T()}");
        yield break;
    }

    private Dictionary<string, string> Load(string language)
    {
        var stream = AssertNotNull(typeof(LocalizationTest).Assembly.GetManifestResourceStream(ResourcePrefix + language + ".json"),
            $"No {language} translation in the mod");
        using var reader = new StreamReader(stream!);
        return AssertNotNull(JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd()),
            $"{language} translation is not valid JSON")!;
    }

    private static string Placeholders(string text) =>
        string.Join(",", Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).OrderBy(value => value));
}
