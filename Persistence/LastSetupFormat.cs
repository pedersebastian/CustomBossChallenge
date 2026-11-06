using System;
using CustomBossChallenge.Config;
using Newtonsoft.Json;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// LastSetup.json as it is on disk, read and checked
/// </summary>
/// <param name="Options">Already brought into range, see <see cref="BossOptions.Sanitized"/></param>
/// <param name="Challenge">The challenge as JSON, see <see cref="DcmJson"/></param>
public sealed record LastSetupFile(BossOptions Options, string Challenge);

/// <summary>
/// The format of LastSetup.json, without the game. See <see cref="LastSetupStore"/>.
/// </summary>
public static class LastSetupFormat
{
    /// <summary>
    /// The file version. It has been 2 since the file came in (2.0); new options only add properties, which older
    /// versions of the mod skip. A file with a higher version is left alone.
    /// </summary>
    public const int Version = 2;

    private sealed class FileJson
    {
        public int Version { get; set; }
        public BossOptions? Options { get; set; }
        public string? Challenge { get; set; }
    }

    /// <param name="challenge">The challenge as JSON, see <see cref="DcmJson"/></param>
    public static string Write(BossOptions options, string challenge) =>
        JsonConvert.SerializeObject(new FileJson {Version = Version, Options = options, Challenge = challenge},
            FormatJson.WithEnumNames);

    public static FormatRead<LastSetupFile> Read(string text)
    {
        FileJson? file;
        try
        {
            file = JsonConvert.DeserializeObject<FileJson>(text, FormatJson.WithEnumNames);
        }
        catch (Exception e)
        {
            return FormatRead<LastSetupFile>.Invalid(e.Message);
        }

        if (file?.Version > Version) return FormatRead<LastSetupFile>.TooNew(file.Version);
        if (file?.Options == null || string.IsNullOrEmpty(file.Challenge)) return FormatRead<LastSetupFile>.Invalid("Incomplete setup");

        var options = file.Options.Sanitized();
        return FormatRead<LastSetupFile>.Read(new LastSetupFile(options, file.Challenge),
            ChallengeLimits.OptionChanges(file.Options, options));
    }
}
