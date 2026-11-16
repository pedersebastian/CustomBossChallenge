using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// One official Boss Bloon Event from boss-events.json (#146), as data/boss-events/README.md describes it. The rules
/// stay JSON in the API's shape; <see cref="Config.PastEventRules"/> turns them into a Boss Creator setup.
/// </summary>
/// <param name="BossKey">The API's boss, lowercase: "lych"</param>
/// <param name="Start">Unix time in milliseconds</param>
/// <param name="DatesGuessed">The archive only knows the dates roughly (<c>lossy: ["dates"]</c>)</param>
/// <param name="Standard">The normal event's rules, or null when the archive has none</param>
public sealed record PastEvent(
    string Id,
    string Name,
    string DisplayName,
    string BossKey,
    long Start,
    long End,
    string? NormalScoring,
    string? EliteScoring,
    bool DatesGuessed,
    JObject? Standard,
    JObject? Elite,
    IReadOnlyList<string> Replaces,
    IReadOnlyList<string> ReplacedBy)
{
    public DateTime StartDate => DateTimeOffset.FromUnixTimeMilliseconds(Start).UtcDateTime;
    public DateTime EndDate => DateTimeOffset.FromUnixTimeMilliseconds(End).UtcDateTime;

    /// <summary>The rules of the normal or the Elite event, or null when the archive has none</summary>
    public JObject? Rules(bool elite) => elite ? Elite : Standard;

    public bool HasRules => Standard != null || Elite != null;

    /// <summary>How the normal or Elite event was ranked: GameTime, LeastCash, LeastTiers, or null when not known</summary>
    public string? Scoring(bool elite) => elite ? EliteScoring : NormalScoring;
}

/// <summary>
/// The events of one boss-events.json
/// </summary>
/// <param name="Generated">When the file was built</param>
public sealed record PastEventsFile(DateTimeOffset Generated, IReadOnlyList<PastEvent> Events)
{
    /// <summary>When the newest event in the file starts, or 0 for a file without events</summary>
    public long NewestStart => Events.Count == 0 ? 0 : Events.Max(e => e.Start);
}

/// <summary>
/// Reads boss-events.json (#146) without the game, so the unit tests can check it: the copy built into the mod
/// (gzipped), the one downloaded from the website and the one cached on disk are read the same way.
/// </summary>
public static class PastEventsFormat
{
    /// <summary>
    /// A formatVersion as a long; a broken file's number too big even for that counts as the biggest, so it reads as
    /// too new instead of throwing out of the read
    /// </summary>
    private static long VersionOf(JToken token)
    {
        try
        {
            return (long) token;
        }
        catch (OverflowException)
        {
            return token.ToString(Formatting.None).StartsWith("-") ? long.MinValue : long.MaxValue;
        }
    }

    /// <summary>The newest formatVersion this version of the mod reads, of the file and of each event</summary>
    public const int Version = 1;

    /// <summary>Where the website publishes the file; pages.yml builds it again on every deploy</summary>
    public const string Url = CodeFormat.WebsiteUrl + "boss-events.json";

    /// <summary>The name of the copy built into the mod (CustomBossChallenge.csproj)</summary>
    public const string ResourceName = "CustomBossChallenge.boss-events.json.gz";

    /// <summary>
    /// Reads the file's text. A file from a newer format is <see cref="FormatProblem.TooNew"/> and must be left
    /// alone; an event from a newer format, or one without what every event has, is left out and named in the
    /// changes.
    /// </summary>
    public static FormatRead<PastEventsFile> Read(string text)
    {
        JObject root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(text)) {DateParseHandling = DateParseHandling.None};
            root = JObject.Load(reader);
        }
        catch (Exception e) when (e is JsonException or InvalidCastException)
        {
            return FormatRead<PastEventsFile>.Invalid(e.Message);
        }

        if (root["formatVersion"] is not {Type: JTokenType.Integer} versionToken)
        {
            return FormatRead<PastEventsFile>.Invalid("no formatVersion");
        }
        var version = VersionOf(versionToken);
        if (version > Version) return FormatRead<PastEventsFile>.TooNew((int) Math.Min(version, int.MaxValue));
        if (root["events"] is not JArray list) return FormatRead<PastEventsFile>.Invalid("no events");
        if (root["generated"] is not {Type: JTokenType.String} generatedToken ||
            !DateTimeOffset.TryParse((string?) generatedToken, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var generated))
        {
            return FormatRead<PastEventsFile>.Invalid("no generated time");
        }

        var events = new List<PastEvent>();
        var changes = new List<string>();
        foreach (var item in list)
        {
            var read = ReadEvent(item, out var why);
            if (read != null) events.Add(read);
            else changes.Add(why);
        }

        return FormatRead<PastEventsFile>.Read(new PastEventsFile(generated, events), changes);
    }

    /// <summary>Reads gzipped text, as the mod carries its copy</summary>
    public static string Gunzip(Stream stream)
    {
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static PastEvent? ReadEvent(JToken item, out string why)
    {
        why = "";
        if (item is not JObject e)
        {
            why = "skipped an event that isn't an object";
            return null;
        }

        var id = Text(e, "id");
        if (e["formatVersion"] is {Type: JTokenType.Integer} v && VersionOf(v) > Version)
        {
            why = $"skipped {id ?? "an event"}: format version {v} is newer";
            return null;
        }

        var name = Text(e, "name");
        var boss = Text(e, "bossType");
        if (id == null || name == null || boss == null ||
            e["start"] is not {Type: JTokenType.Integer} start || e["end"] is not {Type: JTokenType.Integer} end)
        {
            why = $"skipped {id ?? "an event"}: no id, name, boss or dates";
            return null;
        }

        return new PastEvent(
            id, name, Text(e, "displayName") ?? name, boss.ToLowerInvariant(), (long) start, (long) end,
            Scoring(e, "normalScoringType"), Scoring(e, "eliteScoringType"),
            e["lossy"] is JArray lossy && lossy.Any(token => (string?) token == "dates"),
            e["standard"] as JObject, e["elite"] as JObject,
            Ids(e["replaces"]), Ids(e["replacedBy"]));
    }

    private static string? Text(JObject e, string field) =>
        e[field] is {Type: JTokenType.String} token && (string?) token is {Length: > 0} text ? text : null;

    /// <summary>The known scoring types only: the archive keeps odd values like 0 as they were</summary>
    private static string? Scoring(JObject e, string field) =>
        Text(e, field) is "GameTime" or "LeastCash" or "LeastTiers" ? Text(e, field) : null;

    private static IReadOnlyList<string> Ids(JToken? token) =>
        token is JArray ids ? ids.Where(id => id.Type == JTokenType.String).Select(id => (string) id!).ToList() : [];

    /// <summary>
    /// The file to use of those at hand (the copy built in, the cached one, a downloaded one): the one that knows the
    /// newest event, and of two that know the same, the one built last. A cache from before a mod update is older
    /// than the copy the update brought, so it doesn't hide the events that copy has.
    /// </summary>
    public static PastEventsFile? Newest(params PastEventsFile?[] files) =>
        files.Where(file => file != null)
            .OrderByDescending(file => file!.NewestStart)
            .ThenByDescending(file => file!.Generated)
            .ThenByDescending(file => file!.Events.Count)
            .FirstOrDefault();

    /// <summary>The events newest first, as the Past Events list shows them</summary>
    public static IReadOnlyList<PastEvent> NewestFirst(IEnumerable<PastEvent> events) =>
        events.OrderByDescending(e => e.Start).ThenByDescending(e => e.Id, StringComparer.Ordinal).ToList();
}
