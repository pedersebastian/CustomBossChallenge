using System;
using System.Collections.Generic;
using System.IO;
using CustomBossChallenge.Persistence;
using CustomBossChallenge.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Paste finds the code in a link to the website, with the same cases as the website (web/test/link-cases.json)
/// </summary>
public class CodeFormatTests
{
    public static IEnumerable<object[]> Cases()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "web", "test", "link-cases.json"))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("web/test/link-cases.json not found above " + AppContext.BaseDirectory);

        var file = JObject.Parse(File.ReadAllText(Path.Combine(folder.FullName, "web", "test", "link-cases.json")));
        foreach (var entry in (JArray) file["cases"]!) yield return [(string) entry["text"]!, (string) entry["code"]!];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExtractCode_finds_the_code_like_the_website(string text, string code) =>
        Assert.Equal(code, CodeFormat.ExtractCode(text));

    /// <summary>
    /// A code's options with values of every JSON type, from web/test/option-cases.json, which the website reads too
    /// </summary>
    public static IEnumerable<object[]> OptionCases()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "web", "test", "option-cases.json"))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("web/test/option-cases.json not found above " + AppContext.BaseDirectory);

        var file = JObject.Parse(File.ReadAllText(Path.Combine(folder.FullName, "web", "test", "option-cases.json")));
        foreach (var entry in (JArray) file["cases"]!)
        {
            yield return [entry["o"]!.ToString(Formatting.None), entry["options"]!.ToString(Formatting.None)];
        }
    }

    private static readonly JsonSerializer OptionsSerializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = {new StringEnumConverter()}
    });

    /// <summary>The options as SetupCode.TryDecode reads them, without RandomTiers, which only the game fills in</summary>
    private static JObject Read(JObject diff)
    {
        var json = JObject.FromObject(new BossOptions(), OptionsSerializer);
        CodeFormat.Apply(json, CodeFormat.TypedOptions(diff));
        var options = json.ToObject<BossOptions>(OptionsSerializer)!.Sanitized();
        return JObject.FromObject(options with {RandomTiers = null}, OptionsSerializer);
    }

    [Theory]
    [MemberData(nameof(OptionCases))]
    public void Options_of_every_JSON_type_are_read_like_on_the_website(string o, string expected)
    {
        string actual;
        try
        {
            var read = Read(JObject.Parse(o));
            var defaults = Read(new JObject());
            var changed = new JObject();
            foreach (var (key, value) in read)
            {
                if (!JToken.DeepEquals(value, defaults[key])) changed[key] = value;
            }
            foreach (var (key, _) in defaults)
            {
                if (read[key] == null) changed[key] = JValue.CreateNull();
            }
            actual = changed.ToString(Formatting.None);
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException or OverflowException)
        {
            actual = "\"error\"";
        }

        Assert.True(JToken.DeepEquals(JToken.Parse(expected), JToken.Parse(actual)), $"{o}: expected {expected}, got {actual}");
    }

    [Theory]
    [InlineData("{\"o\":{},\"c\":{\"bloonModifiers\":{\"speedMultiplier\":NaN}}}")]
    [InlineData("{\"o\":{},\"c\":{\"cash\":Infinity}}")]
    [InlineData("{\"o\":{},\"c\":{\"cash\":-Infinity}}")]
    [InlineData("{\"o\":{\"Tiers\":NaN},\"c\":{}}")]
    public void A_code_with_NaN_or_Infinity_is_refused_like_on_the_website(string json)
    {
        Assert.False(CodeFormat.TryUnpack(CodeFormat.Pack(json, CodeFormat.Version2Prefix), out _, out var problem, out _));
        Assert.Equal(CodeProblem.Invalid, problem);
    }

    [Theory]
    [InlineData("{\"o\":{},\"c\":{\"name\":\"NaN Infinity\"}}")]
    [InlineData("{\"o\":{},\"c\":{\"name\":\"a \\\" NaN\"}}")]
    [InlineData("{\"o\":{},\"c\":{\"cash\":1e400}}")]
    public void NaN_in_text_and_numbers_too_large_still_unpack(string json) =>
        Assert.True(CodeFormat.TryUnpack(CodeFormat.Pack(json, CodeFormat.Version2Prefix), out _, out _, out _));

    [Fact]
    public void A_link_reads_back_as_the_code()
    {
        var code = CodeFormat.Pack("{\"o\":{},\"c\":{}}", CodeFormat.Version2Prefix);
        var link = CodeFormat.Link(code);

        Assert.Equal(CodeFormat.WebsiteUrl + "#code=" + code, link);
        Assert.True(CodeFormat.TryUnpack(link, out _, out var problem, out _));
        Assert.Equal(CodeProblem.None, problem);
    }

    [Theory]
    [InlineData(CodeFormat.Prefix)]
    [InlineData(CodeFormat.Version2Prefix)]
    [InlineData(CodeFormat.Version3Prefix)]
    [InlineData(CodeFormat.Version4Prefix)]
    public void Every_known_version_is_read(string prefix)
    {
        Assert.True(CodeFormat.TryUnpack(CodeFormat.Pack("{\"o\":{},\"c\":{}}", prefix), out _, out var problem, out _));
        Assert.Equal(CodeProblem.None, problem);
    }

    [Fact]
    public void A_link_to_a_newer_code_still_says_it_is_too_new()
    {
        Assert.False(CodeFormat.TryUnpack(CodeFormat.WebsiteUrl + "#code=CBC7:abc", out _, out var problem, out _));
        Assert.Equal(CodeProblem.TooNew, problem);
    }
}
