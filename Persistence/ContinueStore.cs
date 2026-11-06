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

namespace CustomBossChallenge.Persistence;

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
/// <br/>
/// The file's format and versions are in <see cref="ContinueFormat"/>; this finds the file and turns the challenge
/// and the map saves into the game's types.
/// </summary>
public sealed class ContinueStore
{
    /// <inheritdoc cref="ContinueFormat.Version"/>
    public const int Version = ContinueFormat.Version;

    public const int MixedVersion = ContinueFormat.MixedVersion;

    public const int SingleBossVersion = ContinueFormat.SingleBossVersion;

    public static int VersionFor(BossOptions options) => ContinueFormat.VersionFor(options);

    public static readonly ContinueStore Default =
        new(Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge", "Continue.json"));

    public string FilePath { get; }

    /// <summary>
    /// Where an unusable file is moved, so it isn't read again but can still go with a bug report
    /// </summary>
    public string BrokenPath => Path.ChangeExtension(FilePath, ".broken.json");

    /// <summary>
    /// Where a file from a newer version of the mod is kept while this version writes a game of its own, so the newer
    /// game isn't lost. <see cref="Load"/> moves it back once it can be read and there is no other game.
    /// </summary>
    public string NewerPath => Path.ChangeExtension(FilePath, ".newer.json");

    /// <summary>
    /// A file from a newer version stays in place; say so in the log only once
    /// </summary>
    private bool toldTooNew;

    /// <summary>
    /// The file on disk was written or read by this version in this session, so it isn't one from a newer version and
    /// doesn't have to be read again before every save
    /// </summary>
    private bool ownFile;

    public ContinueStore(string filePath) => FilePath = filePath;

    public bool Exists => File.Exists(FilePath);

    /// <summary>
    /// Writes the game. A file from a newer version of the mod is moved to <see cref="NewerPath"/> first.
    /// </summary>
    public void Save(BossOptions options, DailyChallengeModel dcm, IEnumerable<RoundSnapshot> snapshots)
    {
        if (!ownFile && IsTooNewOnDisk())
        {
            File.Move(FilePath, NewerPath, true);
            ModHelper.Warning<CustomBossChallenge>($"Keeping {FilePath} from a newer version of the mod as {NewerPath}");
        }

        SafeFile.WriteAllText(FilePath, ContinueFormat.Write(options, DcmJson.Serialize(dcm), snapshots));
        ownFile = true;
    }

    /// <summary>
    /// Whether the file comes from a newer version of the mod. A file that can't be read now, e.g. because it is
    /// locked, counts as one too, so it is never written over or deleted unseen.
    /// </summary>
    private bool IsTooNewOnDisk()
    {
        if (!File.Exists(FilePath)) return false;
        try
        {
            return ContinueFormat.Read(File.ReadAllText(FilePath)).Problem == FormatProblem.TooNew;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private void TellTooNew()
    {
        if (!toldTooNew) ModHelper.Warning<CustomBossChallenge>($"Keeping {FilePath} from a newer version of the mod");
        toldTooNew = true;
    }

    /// <summary>
    /// Moves a game kept at <see cref="NewerPath"/> back once this version can read it and there is no other game
    /// </summary>
    private void TakeBackNewer()
    {
        if (File.Exists(FilePath) || !File.Exists(NewerPath)) return;
        try
        {
            if (ContinueFormat.Read(File.ReadAllText(NewerPath)).Problem == FormatProblem.TooNew) return;
            File.Move(NewerPath, FilePath);
            ModHelper.Msg<CustomBossChallenge>($"Took the unfinished custom game back from {NewerPath}");
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Could not take back {NewerPath}: {e.Message}");
        }
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
        TakeBackNewer();
        ownFile = false;
        if (!File.Exists(FilePath)) return new ContinueLoad(ContinueProblem.Missing);

        LostGame? lost = null;
        try
        {
            var read = ContinueFormat.Read(File.ReadAllText(FilePath));
            if (read.Problem == FormatProblem.TooNew)
            {
                TellTooNew();
                return new ContinueLoad(ContinueProblem.TooNew);
            }
            var file = read.Value ?? throw new InvalidDataException(read.Error);

            var options = file.Options;
            var changes = read.Changes.ToList();
            var dcm = DcmJson.Deserialize(file.Challenge) ?? throw new InvalidDataException("No challenge");
            changes.AddRange(ChallengeLimits.Sanitize(dcm, BossMode.DefaultDcm(options)));
            ChallengeLimits.Report($"Unfinished custom game {FilePath}", changes);

            lost = new LostGame(options, dcm, file.LastRound);

            var snapshots = new List<(RoundSnapshot, MapSaveDataModel)>();
            foreach (var snapshot in file.Snapshots)
            {
                var save = MapSaveJson.Deserialize(snapshot.Json);
                if (save == null || !save.IsCompatible())
                {
                    throw new InvalidDataException($"The save for round {snapshot.Round} can't be used by this game version");
                }
                snapshots.Add((snapshot, save));
            }

            ownFile = true;
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

    /// <summary>
    /// Deletes the game, but never a file from a newer version of the mod
    /// </summary>
    public void Delete()
    {
        if (!ownFile && IsTooNewOnDisk())
        {
            TellTooNew();
            return;
        }

        if (File.Exists(FilePath)) File.Delete(FilePath);
        ownFile = false;
    }
}
