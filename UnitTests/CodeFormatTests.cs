using System;
using System.Collections.Generic;
using System.IO;
using CustomBossChallenge.Persistence;
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

    [Fact]
    public void A_link_reads_back_as_the_code()
    {
        var code = CodeFormat.Pack("{\"o\":{},\"c\":{}}", CodeFormat.Version2Prefix);
        var link = CodeFormat.Link(code);

        Assert.Equal(CodeFormat.WebsiteUrl + "#code=" + code, link);
        Assert.True(CodeFormat.TryUnpack(link, out _, out var problem, out _));
        Assert.Equal(CodeProblem.None, problem);
    }

    [Fact]
    public void A_link_to_a_newer_code_still_says_it_is_too_new()
    {
        Assert.False(CodeFormat.TryUnpack(CodeFormat.WebsiteUrl + "#code=CBC7:abc", out _, out var problem, out _));
        Assert.Equal(CodeProblem.TooNew, problem);
    }
}
