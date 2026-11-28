using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CustomBossChallenge.Config;

/// <summary>
/// One point under a version's <c>### Added</c> in CHANGELOG.md: <c>- **Title** (#1, #2): text</c>
/// </summary>
/// <param name="Title">What is in bold at the start, or "" when the point doesn't start with bold</param>
/// <param name="Refs">What is in brackets after the title, like "#272, #19, for contributors", or ""</param>
/// <param name="Text">The rest after the colon, in Markdown, on one line</param>
public sealed record ChangelogPoint(string Title, string Refs, string Text)
{
    /// <summary>A point only contributors care about, like a new workflow: players aren't shown it</summary>
    public bool ForContributors => Refs.Contains("for contributors", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A version's section in CHANGELOG.md
/// </summary>
/// <param name="Version">"2.14.0", or "Unreleased"</param>
/// <param name="Date">"2026-11-22", or "" (Unreleased)</param>
/// <param name="Added">The points under its <c>### Added</c></param>
public sealed record ChangelogEntry(string Version, string Date, IReadOnlyList<ChangelogPoint> Added)
{
    public const string Unreleased = "Unreleased";

    public bool IsUnreleased => string.Equals(Version, Unreleased, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Reads CHANGELOG.md, which the mod carries from its build, for the What's new popup (#264). The website reads it
/// with its own copy of this (web/src/news/changelog.ts); web/test/changelog-cases.json holds the cases both must
/// read alike.
/// </summary>
public static class Changelog
{
    private static readonly Regex VersionHeading = new(@"^##\s+\[([^\]]+)\](?:\s*-\s*(\S+))?", RegexOptions.CultureInvariant);
    private static readonly Regex KindHeading = new(@"^###\s+(.+?)\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex Lead = new(@"^\*\*(.+?)\*\*\s*(?:\(([^)]*)\))?\s*:?\s*(.*)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Every version's section, newest first as in the file, with the points under its <c>### Added</c>
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Parse(string? markdown)
    {
        var entries = new List<ChangelogEntry>();
        if (string.IsNullOrEmpty(markdown)) return entries;

        string? version = null, date = null, kind = null;
        var added = new List<ChangelogPoint>();
        StringBuilder? point = null;
        var inComment = false;

        void EndPoint()
        {
            if (point != null && kind == "added") added.Add(ReadPoint(point.ToString()));
            point = null;
        }

        void EndEntry()
        {
            EndPoint();
            if (version != null) entries.Add(new ChangelogEntry(version, date ?? "", added.ToArray()));
            added.Clear();
        }

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            // Comments, like the one Keep a Changelog's template puts above Unreleased, are no part of any version
            if (inComment || line.TrimStart().StartsWith("<!--", StringComparison.Ordinal))
            {
                inComment = !line.Contains("-->", StringComparison.Ordinal);
                continue;
            }

            if (VersionHeading.Match(line) is {Success: true} heading)
            {
                EndEntry();
                version = heading.Groups[1].Value.Trim();
                date = heading.Groups[2].Value;
                kind = null;
                continue;
            }
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                EndPoint();
                // A level 1 or 2 heading that isn't a version (the title) ends the version before it
                kind = KindHeading.Match(line) is {Success: true} sub ? sub.Groups[1].Value.ToLowerInvariant() : null;
                continue;
            }
            if (version == null) continue;

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                EndPoint();
                point = new StringBuilder(line[2..].Trim());
            }
            else if (line.Length > 0 && char.IsWhiteSpace(line[0]) && point != null)
            {
                point.Append(' ').Append(line.Trim());
            }
            else
            {
                EndPoint();
            }
        }
        EndEntry();
        return entries;
    }

    private static ChangelogPoint ReadPoint(string text)
    {
        var lead = Lead.Match(text);
        return lead.Success
            ? new ChangelogPoint(lead.Groups[1].Value.Trim(), lead.Groups[2].Value.Trim(), lead.Groups[3].Value.Trim())
            : new ChangelogPoint("", "", text.Trim());
    }

    /// <summary>
    /// A version like "2.14.0"; null for "Unreleased" or anything else. A prerelease or build part ("2.15.0-beta",
    /// "2.15.0+abc") is read as its release, so a test build's record doesn't show the popup again (#321).
    /// </summary>
    public static Version? ReadVersion(string? text) =>
        System.Version.TryParse(WithoutSuffix(text?.Trim().TrimStart('v', 'V')), out var version) ? Normal(version) : null;

    /// <summary>The version without what follows its first "-" or "+"</summary>
    private static string? WithoutSuffix(string? text)
    {
        var end = text?.IndexOfAny(['-', '+']) ?? -1;
        return end < 0 ? text : text![..end];
    }

    /// <summary>2.14 is 2.14.0, so versions written either way compare equal</summary>
    private static Version Normal(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));

    /// <summary>
    /// The versions a player who last saw <paramref name="lastSeen"/> hasn't seen yet, up to <paramref name="current"/>,
    /// newest first. With <paramref name="lastSeen"/> null (updated from a version before the popup) only the current
    /// one. A release is built before the release workflow renames its Unreleased section (build.yml), so when the
    /// file has no section for <paramref name="current"/>, Unreleased is its section.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> NewSince(IEnumerable<ChangelogEntry> entries, Version? lastSeen,
        Version current)
    {
        current = Normal(current);
        var list = entries.ToList();
        var hasCurrent = list.Any(entry => ReadVersion(entry.Version) == current);
        var result = new List<(Version Version, ChangelogEntry Entry)>();
        foreach (var entry in list)
        {
            var version = entry.IsUnreleased && !hasCurrent ? current : ReadVersion(entry.Version);
            if (version == null || version > current) continue;
            if (lastSeen == null ? version != current : version <= Normal(lastSeen)) continue;
            result.Add((version, entry with {Version = version.ToString(3)}));
        }
        return result.OrderByDescending(item => item.Version).Select(item => item.Entry).ToArray();
    }

    /// <summary>
    /// The points players are shown from <paramref name="entries"/>: those under Added, without the ones for
    /// contributors
    /// </summary>
    public static IEnumerable<(ChangelogEntry Entry, ChangelogPoint Point)> PlayerPoints(IEnumerable<ChangelogEntry> entries) =>
        entries.SelectMany(entry => entry.Added.Where(point => !point.ForContributors).Select(point => (entry, point)));

    /// <summary>
    /// Markdown as TextMeshPro rich text: bold as &lt;b&gt;, <c>code</c> and links as their plain text. A "&lt;" in
    /// the text gets a zero width space after it, so text like <c>&lt;noparse&gt;</c> shows instead of becoming a tag.
    /// </summary>
    public static string ToRichText(string markdown)
    {
        var text = markdown.Replace("<", "<​");
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]*\)", "$1");
        text = Regex.Replace(text, @"`([^`]*)`", "$1");
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
        return text;
    }
}

/// <summary>
/// When the What's new popup shows (#264): once per version, after an update, never on a fresh install
/// </summary>
public static class WhatsNewRule
{
    /// <summary>
    /// What to do when the Boss Creator opens
    /// </summary>
    /// <param name="Show">Whether to show the popup (if there is something to show, and it is not turned off)</param>
    /// <param name="Since">The last version seen; null shows only the current version's points</param>
    /// <param name="Record">The version to remember as seen</param>
    public readonly record struct Decision(bool Show, Version? Since, string Record);

    /// <param name="recorded">The version the file remembers, or null without a file</param>
    /// <param name="usedBefore">Whether the mod's folder was there before this start of the game: an update from a
    /// version before the popup, not a fresh install</param>
    /// <param name="current">This version of the mod</param>
    public static Decision Decide(string? recorded, bool usedBefore, string current)
    {
        var now = Changelog.ReadVersion(current) ?? new Version(0, 0, 0);
        var seen = Changelog.ReadVersion(recorded);
        if (seen == null) return new Decision(usedBefore, null, now.ToString(3));
        // A downgrade keeps the newer version, so the points aren't shown again after updating back
        return seen < now
            ? new Decision(true, seen, now.ToString(3))
            : new Decision(false, seen, seen.ToString(3));
    }
}
