using System;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// A snapshot of a custom game, taken at the end of a round
/// </summary>
/// <param name="Round">The round it resumes on, as the player counts rounds</param>
/// <param name="DefeatedTiers">The boss tiers defeated by then</param>
/// <param name="Json">The map save as JSON, see <see cref="MapSaveJson"/></param>
public sealed record RoundSnapshot(int Round, int DefeatedTiers, string Json);

/// <summary>
/// Continue.json as it is on disk, read and checked: the challenge and the map saves are still JSON text, which
/// <see cref="ContinueStore"/> turns into the game's types
/// </summary>
/// <param name="Options">Already brought into range, see <see cref="BossOptions.Sanitized"/></param>
public sealed record ContinueFile(int Version, BossOptions Options, string Challenge, IReadOnlyList<RoundSnapshot> Snapshots)
{
    /// <summary>The last round played</summary>
    public int LastRound => Snapshots.Max(snapshot => snapshot.Round);
}

/// <summary>
/// The format of Continue.json, without the game. See <see cref="ContinueStore"/>.
/// </summary>
public static class ContinueFormat
{
    /// <summary>
    /// The newest file version this reads. Games with a different boss per tier are written as version 2 and games
    /// with Boss Event Rounds as version 3, so older versions of the mod, which would play them with one boss or the
    /// default rounds, leave them alone; other games stay version 1.
    /// </summary>
    public const int Version = 3;

    public const int MixedVersion = 2;

    public const int SingleBossVersion = 1;

    public static int VersionFor(BossOptions options) =>
        options.UsesBossEventRounds ? Version : options.Mixed ? MixedVersion : SingleBossVersion;

    private sealed class FileJson
    {
        public int Version { get; set; }
        public BossOptions? Options { get; set; }
        public string? Challenge { get; set; }
        public List<RoundSnapshot>? Snapshots { get; set; }
    }

    /// <summary>
    /// The text of Continue.json for a game, with the snapshots by round
    /// </summary>
    /// <param name="challenge">The challenge as JSON, see <see cref="DcmJson"/></param>
    public static string Write(BossOptions options, string challenge, IEnumerable<RoundSnapshot> snapshots) =>
        JsonConvert.SerializeObject(new FileJson
        {
            Version = VersionFor(options), Options = options, Challenge = challenge,
            Snapshots = snapshots.OrderBy(snapshot => snapshot.Round).ToList()
        }, FormatJson.WithEnumNames);

    public static FormatRead<ContinueFile> Read(string text)
    {
        FileJson? file;
        try
        {
            file = JsonConvert.DeserializeObject<FileJson>(text, FormatJson.WithEnumNames);
        }
        catch (Exception e)
        {
            return FormatRead<ContinueFile>.Invalid(e.Message);
        }

        if (file?.Version > Version) return FormatRead<ContinueFile>.TooNew(file.Version);
        if (file == null || file.Version < SingleBossVersion)
        {
            return FormatRead<ContinueFile>.Invalid($"Version {file?.Version}, expected {SingleBossVersion}-{Version}");
        }
        // The map saves stay text here; the store finds out whether the game can still use them
        if (file.Options == null || string.IsNullOrEmpty(file.Challenge) || file.Snapshots is not {Count: > 0} ||
            file.Snapshots.Any(snapshot => snapshot == null))
        {
            return FormatRead<ContinueFile>.Invalid("Incomplete game");
        }

        var options = file.Options.Sanitized();
        var snapshots = file.Snapshots
            .Select(snapshot => snapshot with {DefeatedTiers = Math.Clamp(snapshot.DefeatedTiers, 0, options.Tiers)})
            .ToList();
        return FormatRead<ContinueFile>.Read(new ContinueFile(file.Version, options, file.Challenge, snapshots),
            ChallengeLimits.OptionChanges(file.Options, options));
    }
}
