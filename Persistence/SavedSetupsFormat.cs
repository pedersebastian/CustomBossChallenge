using System;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// A saved Boss Creator setup: its name and its share code
/// </summary>
public sealed class SavedSetup
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public DateTime Saved { get; set; }
}

/// <summary>
/// The format of SavedSetups.json, without the game. See <see cref="SavedSetupsStore"/>.
/// </summary>
public static class SavedSetupsFormat
{
    /// <summary>
    /// The file version. Every setup is a share code, which has a version of its own, so new options don't need a new
    /// file version. A file with a higher version is left alone.
    /// </summary>
    public const int Version = 1;

    private sealed class FileJson
    {
        public int Version { get; set; }
        public List<SavedSetup?>? Setups { get; set; }
    }

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(FormatJson.Plain);

    public static string Write(IEnumerable<SavedSetup> setups) =>
        JsonConvert.SerializeObject(new FileJson {Version = Version, Setups = setups.ToList<SavedSetup?>()},
            FormatJson.Plain);

    /// <summary>
    /// The setups in the file. Setups without a name or code, and later ones with a name already used, can't be told
    /// apart, so they are left out; <see cref="FormatRead{T}.Changes"/> says so once for each.
    /// </summary>
    public static FormatRead<List<SavedSetup>> Read(string text)
    {
        // Each setup is read on its own, so one damaged setup doesn't hide the others (#110); only a file that isn't
        // JSON, or whose setups aren't a list, can't be read at all
        JObject? file;
        try
        {
            file = JsonConvert.DeserializeObject<JToken>(text, FormatJson.Plain) switch
            {
                null => null,
                JObject obj => obj,
                _ => throw new JsonSerializationException("Not an object")
            };
        }
        catch (Exception e)
        {
            return FormatRead<List<SavedSetup>>.Invalid(e.Message);
        }

        var version = file?["Version"] is JValue {Type: JTokenType.Integer, Value: var number}
            ? number switch {long l => l, int i => i, System.Numerics.BigInteger big => big > 0 ? long.MaxValue : 0, _ => 0}
            : 0;
        if (version > Version) return FormatRead<List<SavedSetup>>.TooNew((int) Math.Min(version, int.MaxValue));

        var entries = file?["Setups"];
        if (entries is not (null or JArray or {Type: JTokenType.Null}))
        {
            return FormatRead<List<SavedSetup>>.Invalid("The setups aren't a list");
        }

        var setups = new List<SavedSetup>();
        var changes = new List<string>();
        foreach (var entry in entries as JArray ?? [])
        {
            SavedSetup? setup;
            try
            {
                setup = entry.ToObject<SavedSetup?>(Serializer);
            }
            catch (Exception)
            {
                changes.Add("a damaged saved setup was left out");
                continue;
            }

            // The name is cleaned first, so names that only differ in what cleaning removes count as the same
            var name = setup == null ? "" : ChallengeLimits.CleanName(setup.Name, "");
            if (setup == null || name.Length == 0 || string.IsNullOrWhiteSpace(setup.Code) ||
                setups.Any(other => Same(other.Name, name)))
            {
                changes.Add("a damaged saved setup was left out");
                continue;
            }

            setup.Name = name;
            setups.Add(setup);
        }

        // More than SavedSetupsStore.MaxSetups (a hand edited file) are all kept, so writing the file never drops any
        return FormatRead<List<SavedSetup>>.Read(setups, changes);
    }

    /// <summary>Setup names are the same when they only differ in case</summary>
    public static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
