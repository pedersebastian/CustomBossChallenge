using System;
using System.IO;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Models.ServerEvents;
using MelonLoader.Utils;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Remembers the last Boss Creator setup (boss options + the editor's challenge model) in the mod's own file. The
/// file's format is in <see cref="LastSetupFormat"/>.
/// </summary>
public sealed class LastSetupStore
{
    public const int Version = LastSetupFormat.Version;

    public static readonly LastSetupStore Default =
        new(Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge", "LastSetup.json"));

    public string FilePath { get; }

    /// <summary>
    /// A file from a newer version stays in place; say so in the log only once
    /// </summary>
    private bool toldTooNew;

    public LastSetupStore(string filePath) => FilePath = filePath;

    /// <summary>
    /// Whether the file comes from a newer version of the mod, which this version neither reads nor overwrites
    /// </summary>
    public bool IsTooNew
    {
        get
        {
            try
            {
                return File.Exists(FilePath) && LastSetupFormat.Read(File.ReadAllText(FilePath)).Problem == FormatProblem.TooNew;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Saves the setup, unless the file comes from a newer version of the mod
    /// </summary>
    public void Save(BossOptions options, DailyChallengeModel dcm)
    {
        if (IsTooNew)
        {
            TellTooNew();
            return;
        }
        SafeFile.WriteAllText(FilePath, LastSetupFormat.Write(options, DcmJson.Serialize(dcm)));
    }

    private void TellTooNew()
    {
        if (!toldTooNew) ModHelper.Warning<CustomBossChallenge>($"Keeping {FilePath} from a newer version of the mod");
        toldTooNew = true;
    }

    /// <summary>
    /// Loads the saved setup. Returns false (and logs a warning for a broken file or one from a newer version of the
    /// mod) when there is nothing usable.
    /// </summary>
    public bool TryLoad(out BossOptions options, out DailyChallengeModel? dcm)
    {
        options = new BossOptions();
        dcm = null;
        if (!File.Exists(FilePath)) return false;

        try
        {
            var read = LastSetupFormat.Read(File.ReadAllText(FilePath));
            if (read.Problem == FormatProblem.TooNew)
            {
                TellTooNew();
                return false;
            }
            var file = read.Value ?? throw new InvalidDataException(read.Error);

            dcm = DcmJson.Deserialize(file.Challenge);
            if (dcm == null) return false;

            options = file.Options;
            var changes = read.Changes.ToList();
            changes.AddRange(ChallengeLimits.Sanitize(dcm, BossMode.DefaultDcm(options)));
            ChallengeLimits.Report($"Boss Creator setup {FilePath}", changes);
            return true;
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Ignoring unreadable Boss Creator setup {FilePath}: {e.Message}");
            options = new BossOptions();
            dcm = null;
            return false;
        }
    }
}
