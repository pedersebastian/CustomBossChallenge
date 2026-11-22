using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// What's new (#264): reading CHANGELOG.md, with the cases in web/test/changelog-cases.json that the website reads
/// too, which versions a player hasn't seen, and when the popup shows
/// </summary>
public class ChangelogTests
{
    private static string RepoFile(params string[] path)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine([folder.FullName, ..path]))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException($"{string.Join('/', path)} not found above {AppContext.BaseDirectory}");
        return Path.Combine([folder.FullName, ..path]);
    }

    public static IEnumerable<object[]> Cases()
    {
        var file = JObject.Parse(File.ReadAllText(RepoFile("web", "test", "changelog-cases.json")));
        foreach (var entry in (JArray) file["cases"]!)
        {
            yield return [(string) entry["name"]!, (string) entry["markdown"]!, entry["entries"]!.ToString(Formatting.None)];
        }
    }

    [Fact]
    public void There_are_shared_cases() => Assert.True(Cases().Count() >= 5);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Each_case_is_read_like_the_website_reads_it(string name, string markdown, string expected)
    {
        var read = Changelog.Parse(markdown).Select(entry => new JObject
        {
            ["version"] = entry.Version,
            ["date"] = entry.Date,
            ["added"] = new JArray(entry.Added.Select(point => new JObject
            {
                ["title"] = point.Title,
                ["refs"] = point.Refs,
                ["text"] = point.Text,
                ["forContributors"] = point.ForContributors
            }))
        });
        Assert.True(JToken.DeepEquals(JArray.Parse(expected), new JArray(read)),
            $"{name}: read {new JArray(read).ToString(Formatting.None)}");
    }

    [Fact]
    public void The_real_changelog_has_released_versions_with_points()
    {
        var entries = Changelog.Parse(File.ReadAllText(RepoFile("CHANGELOG.md")));
        Assert.Equal(ChangelogEntry.Unreleased, entries[0].Version);
        var released = entries.Where(entry => !entry.IsUnreleased).ToList();
        Assert.True(released.Count >= 10);
        Assert.All(released, entry => Assert.NotNull(Changelog.ReadVersion(entry.Version)));
        // Newest first, as the popup and the website expect
        Assert.Equal(released.OrderByDescending(entry => Changelog.ReadVersion(entry.Version)).Select(entry => entry.Version),
            released.Select(entry => entry.Version));
        Assert.Contains(entries.Single(entry => entry.Version == "2.14.0").Added,
            point => point.Title == "Past events with Insta Monkeys off or only some powers" && point.Refs == "#212");
        Assert.All(released.SelectMany(entry => entry.Added), point => Assert.NotEmpty(point.Text));
    }

    private static readonly IReadOnlyList<ChangelogEntry> Entries = Changelog.Parse(
        "## [Unreleased]\n### Added\n- **Next**: n.\n" +
        "## [2.15.0] - 2026-12-01\n### Added\n- **Fifteen**: f.\n- **Tools** (for contributors): t.\n" +
        "## [2.14.1] - 2026-11-25\n### Fixed\n- **Fix**: x.\n" +
        "## [2.14.0] - 2026-11-22\n### Added\n- **Fourteen**: a.\n" +
        "## [2.13.0] - 2026-11-17\n### Added\n- **Thirteen**: b.\n");

    private static string[] New(string? lastSeen, string current) =>
        Changelog.NewSince(Entries, Changelog.ReadVersion(lastSeen), Changelog.ReadVersion(current)!)
            .Select(entry => entry.Version).ToArray();

    [Fact]
    public void Versions_after_the_last_seen_up_to_this_one_newest_first()
    {
        Assert.Equal(["2.15.0", "2.14.1", "2.14.0"], New("2.13.0", "2.15.0"));
        Assert.Equal(["2.15.0"], New("2.14.1", "2.15.0"));
        Assert.Equal([], New("2.15.0", "2.15.0"));
        // Unreleased is never shown while the file has a section for this version
        Assert.Equal(["2.14.0"], New("2.13.0", "2.14.0"));
    }

    [Fact]
    public void A_release_built_before_its_section_was_named_reads_Unreleased_as_its_own()
    {
        Assert.Equal(["2.16.0", "2.15.0"], New("2.14.1", "2.16.0"));
        Assert.Equal(["2.16.0"], New(null, "2.16.0"));
    }

    [Fact]
    public void Without_a_last_seen_version_only_this_one()
    {
        Assert.Equal(["2.14.0"], New(null, "2.14.0"));
        Assert.Equal(["2.15.0"], New(null, "2.15.0"));
    }

    [Fact]
    public void Players_are_not_shown_points_for_contributors()
    {
        var news = Changelog.NewSince(Entries, new Version(2, 14, 1), new Version(2, 15, 0));
        Assert.Equal(["Fifteen"], Changelog.PlayerPoints(news).Select(item => item.Point.Title));
        // A patch with only fixes has nothing to show
        Assert.Empty(Changelog.PlayerPoints(Changelog.NewSince(Entries, new Version(2, 14, 0), new Version(2, 14, 1))));
    }

    [Theory]
    [InlineData("2.14", "2.14.0")]
    [InlineData("v2.15.1", "2.15.1")]
    [InlineData(" 3.0.0 ", "3.0.0")]
    public void Versions_are_read_with_three_parts(string text, string expected) =>
        Assert.Equal(expected, Changelog.ReadVersion(text)!.ToString(3));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unreleased")]
    [InlineData("two")]
    public void Anything_else_is_no_version(string? text) => Assert.Null(Changelog.ReadVersion(text));

    [Fact]
    public void A_fresh_install_shows_nothing_and_remembers_this_version() =>
        Assert.Equal(new WhatsNewRule.Decision(false, null, "2.15.0"), WhatsNewRule.Decide(null, false, "2.15.0"));

    [Fact]
    public void An_update_from_before_the_popup_shows_this_version() =>
        Assert.Equal(new WhatsNewRule.Decision(true, null, "2.15.0"), WhatsNewRule.Decide(null, true, "2.15.0"));

    [Fact]
    public void An_update_shows_what_came_after_the_last_seen_version() =>
        Assert.Equal(new WhatsNewRule.Decision(true, new Version(2, 13, 0), "2.15.0"),
            WhatsNewRule.Decide("2.13.0", true, "2.15.0"));

    [Fact]
    public void The_same_version_shows_nothing_the_next_time() =>
        Assert.Equal(new WhatsNewRule.Decision(false, new Version(2, 15, 0), "2.15.0"),
            WhatsNewRule.Decide("2.15.0", true, "2.15.0"));

    [Fact]
    public void A_downgrade_shows_nothing_and_keeps_the_newer_version() =>
        Assert.Equal(new WhatsNewRule.Decision(false, new Version(2, 16, 0), "2.16.0"),
            WhatsNewRule.Decide("2.16.0", true, "2.15.0"));

    [Fact]
    public void An_unreadable_record_counts_as_none() =>
        Assert.Equal(new WhatsNewRule.Decision(true, null, "2.15.0"), WhatsNewRule.Decide("garbage", true, "2.15.0"));

    [Theory]
    [InlineData("the Boss Creator shows stars. The website too.", "the Boss Creator shows stars.")]
    [InlineData("one sentence without an end", "one sentence without an end")]
    [InlineData("version 2.14 is out! More later.", "version 2.14 is out!")]
    public void The_first_sentence(string text, string expected) => Assert.Equal(expected, Changelog.FirstSentence(text));

    [Fact]
    public void A_long_sentence_is_cut_after_a_word()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 60)) + ".";
        var first = Changelog.FirstSentence(text, 50);
        Assert.True(first.Length <= 50, first);
        Assert.EndsWith("word…", first);
    }

    [Fact]
    public void Markdown_becomes_rich_text()
    {
        Assert.Equal("<b>Bold</b> and code and a link", Changelog.ToRichText("**Bold** and `code` and a [link](https://x.y)"));
        // A tag written in the text stays text
        Assert.Equal("tags like <​noparse> show", Changelog.ToRichText("tags like `<noparse>` show"));
    }
}
