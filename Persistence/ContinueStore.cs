using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Models.ServerEvents;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// A snapshot of a custom game, taken at the end of a round
/// </summary>
/// <param name="Round">The round it resumes on, as the player counts rounds</param>
/// <param name="DefeatedTiers">The boss tiers defeated by then</param>
/// <param name="Json">The map save as JSON, see <see cref="MapSaveJson"/></param>
public sealed record RoundSnapshot(int Round, int DefeatedTiers, string Json);

/// <summary>
/// A custom game that can be continued: the rules it was started with and its snapshots
/// </summary>
public sealed record ContinueGame(BossOptions Options, DailyChallengeModel Dcm, IReadOnlyList<(RoundSnapshot Snapshot, MapSaveDataModel Save)> Snapshots)
{
    /// <summary>
    /// The snapshot of the last round played, the one the game continues from
    /// </summary>
    public (RoundSnapshot Snapshot, MapSaveDataModel Save) Latest => Snapshots.MaxBy(entry => entry.Snapshot.Round);
}

/// <summary>
/// Why there is no game to continue
/// </summary>
public enum ContinueProblem
{
    None,

    /// <summary>No unfinished game was saved</summary>
    Missing,

    /// <summary>The file can't be used, e.g. after a BTD6 update; it was moved to <see cref="ContinueStore.BrokenPath"/></summary>
    Unusable,

    /// <summary>The file comes from a newer version of the mod and is left alone</summary>
    TooNew
}

/// <summary>
/// The setup of an unfinished game that can't be continued any more, so it can be played again from the start
/// </summary>
public sealed record LostGame(BossOptions Options, DailyChallengeModel Dcm, int Round);

/// <summary>
/// What <see cref="ContinueStore.Load"/> found: the game to continue, or why there is none and, if it could still be
/// read, the setup of the game that was lost
/// </summary>
public sealed record ContinueLoad(ContinueProblem Problem, ContinueGame? Game = null, LostGame? Lost = null);

/// <summary>
/// Keeps the custom game being played in the mod's own file, so it can be continued after the game was closed or
/// crashed. Custom games never write map saves, so the player's own Boss Challenge saves stay untouched.
/// </summary>
public sealed class ContinueStore
{
    public const int Version = 1;

    public static readonly ContinueStore Default =
        new(Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge", "Continue.json"));

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        Converters = {new StringEnumConverter()}
    };

    public string FilePath { get; }

    /// <summary>
    /// Where an unusable file is moved, so it isn't read again but can still go with a bug report
    /// </summary>
    public string BrokenPath => Path.ChangeExtension(FilePath, ".broken.json");

    /// <summary>
    /// A file from a newer version stays in place; say so in the log only once
    /// </summary>
    private bool toldTooNew;

    public ContinueStore(string filePath) => FilePath = filePath;

    private sealed class ContinueFile
    {
        public int Version { get; set; }
        public BossOptions? Options { get; set; }
        public string? Challenge { get; set; }
        public List<RoundSnapshot>? Snapshots { get; set; }
    }

    public bool Exists => File.Exists(FilePath);

    public void Save(BossOptions options, DailyChallengeModel dcm, IEnumerable<RoundSnapshot> snapshots)
    {
        var file = new ContinueFile
        {
            Version = Version, Options = options, Challenge = DcmJson.Serialize(dcm),
            Snapshots = snapshots.OrderBy(snapshot => snapshot.Round).ToList()
        };
        SafeFile.WriteAllText(FilePath, JsonConvert.SerializeObject(file, JsonSettings));
    }

    /// <summary>
    /// Loads the saved game, or null when there is nothing to continue. See <see cref="Load"/>.
    /// </summary>
    public ContinueGame? TryLoad() => Load().Game;

    /// <summary>
    /// Loads the saved game. A file that can't be used is moved to <see cref="BrokenPath"/> after a warning in the
    /// log, so it is read only once; the setup it was played with comes back when it can still be read.
    /// </summary>
    public ContinueLoad Load()
    {
        if (!File.Exists(FilePath)) return new ContinueLoad(ContinueProblem.Missing);

        LostGame? lost = null;
        try
        {
            var file = JsonConvert.DeserializeObject<ContinueFile>(File.ReadAllText(FilePath), JsonSettings);
            if (file?.Version > Version)
            {
                if (!toldTooNew) ModHelper.Warning<CustomBossChallenge>($"Keeping {FilePath} from a newer version of the mod");
                toldTooNew = true;
                return new ContinueLoad(ContinueProblem.TooNew);
            }
            if (file == null || file.Version != Version)
            {
                throw new InvalidDataException($"Version {file?.Version}, expected {Version}");
            }
            if (file.Options == null || string.IsNullOrEmpty(file.Challenge))
            {
                throw new InvalidDataException("Incomplete game");
            }

            var options = file.Options.Sanitized();
            var changes = ChallengeLimits.OptionChanges(file.Options, options);
            var dcm = DcmJson.Deserialize(file.Challenge) ?? throw new InvalidDataException("No challenge");
            changes.AddRange(ChallengeLimits.Sanitize(dcm, BossMode.DefaultDcm(options)));
            ChallengeLimits.Report($"Unfinished custom game {FilePath}", changes);

            if (file.Snapshots is not {Count: > 0}) throw new InvalidDataException("Incomplete game");
            lost = new LostGame(options, dcm, file.Snapshots.Max(snapshot => snapshot.Round));

            var snapshots = new List<(RoundSnapshot, MapSaveDataModel)>();
            foreach (var snapshot in file.Snapshots)
            {
                var save = MapSaveJson.Deserialize(snapshot.Json);
                if (save == null || !save.IsCompatible())
                {
                    throw new InvalidDataException($"The save for round {snapshot.Round} can't be used by this game version");
                }
                snapshots.Add((snapshot with {DefeatedTiers = Math.Clamp(snapshot.DefeatedTiers, 0, options.Tiers)}, save));
            }

            return new ContinueLoad(ContinueProblem.None, new ContinueGame(options, dcm, snapshots));
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Ignoring unusable unfinished custom game {FilePath}: {e.Message}");
            MoveAside();
            return new ContinueLoad(ContinueProblem.Unusable, Lost: lost);
        }
    }

    /// <summary>
    /// Moves an unusable file to <see cref="BrokenPath"/>, replacing an older one
    /// </summary>
    private void MoveAside()
    {
        try
        {
            File.Move(FilePath, BrokenPath, true);
            ModHelper.Msg<CustomBossChallenge>($"Moved it to {BrokenPath}");
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Could not move {FilePath} to {BrokenPath}: {e.Message}");
        }
    }

    public void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
