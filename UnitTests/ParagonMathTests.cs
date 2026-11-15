using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The Max Paragons slider reads and sets maxParagons like the website, from web/test/paragon-cases.json, which
/// web/test/math.test.ts reads too
/// </summary>
public class ParagonMathTests
{
    private static JObject Cases()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "web", "test", "paragon-cases.json"))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("web/test/paragon-cases.json not found above " + AppContext.BaseDirectory);
        return JObject.Parse(File.ReadAllText(Path.Combine(folder.FullName, "web", "test", "paragon-cases.json")));
    }

    public static IEnumerable<object[]> ToSliderCases() =>
        ((JArray) Cases()["toSlider"]!).Select(c =>
            new object[] {(int) c["available"]!, (int) c["maxParagons"]!, (bool) c["unlimited"]!, (int) c["slider"]!});

    public static IEnumerable<object[]> FromSliderCases() =>
        ((JArray) Cases()["fromSlider"]!).Select(c =>
            new object[] {(int) c["available"]!, (int) c["position"]!, (int) c["maxParagons"]!});

    [Theory]
    [MemberData(nameof(ToSliderCases))]
    public void The_slider_shows_a_limit_like_the_website(int available, int maxParagons, bool unlimited, int slider)
    {
        Assert.Equal(unlimited, ParagonMath.IsUnlimited(maxParagons, available));
        Assert.Equal(slider, ParagonMath.ToSlider(maxParagons, available));
    }

    [Theory]
    [MemberData(nameof(FromSliderCases))]
    public void The_slider_sets_a_limit_like_the_website(int available, int position, int maxParagons) =>
        Assert.Equal(maxParagons, ParagonMath.FromSlider(position, available));

    [Fact]
    public void Every_slider_position_reads_back_as_itself()
    {
        for (var available = 1; available <= 20; available++)
        for (var position = 0; position <= available; position++)
            Assert.Equal(position, ParagonMath.ToSlider(ParagonMath.FromSlider(position, available), available));
    }
}
