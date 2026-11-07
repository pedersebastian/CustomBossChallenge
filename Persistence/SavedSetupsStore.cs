using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Models.ServerEvents;
using MelonLoader.Utils;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// The player's saved Boss Creator setups, by name, in the mod's own file next to the last setup. The file's format
/// is in <see cref="SavedSetupsFormat"/>.
/// <br/>
/// Every setup is kept as a share code, so saved setups get the same versioning, range checks and protection against
/// damaged data as shared ones.
/// </summary>
public sealed class SavedSetupsStore
{
    public const int Version = SavedSetupsFormat.Version;
    public const int MaxSetups = 50;

    public static readonly SavedSetupsStore Default =
        new(Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge", "SavedSetups.json"));

    public string FilePath { get; }

    /// <summary>
    /// A file from a newer version stays in place; say so in the log only once
    /// </summary>
    private bool toldTooNew;

    public SavedSetupsStore(string filePath) => FilePath = filePath;

    public enum SaveResult
    {
        Saved,
        Replaced,
        Full,

        /// <summary>The file comes from a newer version of the mod, so nothing was written</summary>
        TooNew
    }

    /// <summary>
    /// The saved setups, sorted by name; none while the file comes from a newer version of the mod
    /// </summary>
    public List<SavedSetup> All => Read().Setups.OrderBy(setup => setup.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>
    /// Whether the file comes from a newer version of the mod, which this version neither shows nor changes
    /// </summary>
    public bool IsTooNew => Read().TooNew;

    /// <summary>
    /// The name a setup is saved under: the challenge's name, cleaned like a shared one
    /// </summary>
    public static string NameOf(BossOptions options, DailyChallengeModel dcm) =>
        ChallengeLimits.CleanName(dcm.name, BossMode.DefaultName(options));

    public bool Contains(string name) => Read().Setups.Any(setup => SavedSetupsFormat.Same(setup.Name, name));

    /// <summary>
    /// Saves the setup under its challenge name, replacing a setup with the same name
    /// </summary>
    public SaveResult Save(BossOptions options, DailyChallengeModel dcm)
    {
        var (setups, tooNew) = Read();
        if (tooNew) return SaveResult.TooNew;

        var name = NameOf(options, dcm);
        var replaced = setups.RemoveAll(setup => SavedSetupsFormat.Same(setup.Name, name)) > 0;
        if (!replaced && setups.Count >= MaxSetups) return SaveResult.Full;

        setups.Add(new SavedSetup {Name = name, Code = SetupCode.Encode(options, dcm), Saved = DateTime.UtcNow});
        Write(setups);
        return replaced ? SaveResult.Replaced : SaveResult.Saved;
    }

    /// <summary>
    /// Reads a saved setup the way a share code is read: values out of range are brought back, and a setup from a
    /// newer version of the mod gives <see cref="CodeProblem.TooNew"/>
    /// </summary>
    public bool TryLoad(string name, out BossOptions options, out DailyChallengeModel? dcm, out CodeProblem problem)
    {
        var saved = Read().Setups.FirstOrDefault(setup => SavedSetupsFormat.Same(setup.Name, name));
        if (saved != null) return SetupCode.TryDecode(saved.Code, out options, out dcm, out problem, $"Saved setup '{saved.Name}'");

        options = new BossOptions();
        dcm = null;
        problem = CodeProblem.Invalid;
        return false;
    }

    /// <summary>
    /// Deletes the setup; false when there is none by that name, or the file comes from a newer version of the mod
    /// </summary>
    public bool Delete(string name)
    {
        var (setups, tooNew) = Read();
        if (tooNew || setups.RemoveAll(setup => SavedSetupsFormat.Same(setup.Name, name)) == 0) return false;

        Write(setups);
        return true;
    }

    private (List<SavedSetup> Setups, bool TooNew) Read()
    {
        if (!File.Exists(FilePath)) return ([], false);

        FormatRead<List<SavedSetup>> read;
        try
        {
            read = SavedSetupsFormat.Read(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            read = FormatRead<List<SavedSetup>>.Invalid(e.Message);
        }

        switch (read.Problem)
        {
            case FormatProblem.TooNew:
                if (!toldTooNew) ModHelper.Warning<CustomBossChallenge>($"Keeping {FilePath} from a newer version of the mod");
                toldTooNew = true;
                return ([], true);
            case FormatProblem.Invalid:
                KeepBroken(read.Error);
                return ([], false);
        }

        foreach (var _ in read.Changes) ModHelper.Warning<CustomBossChallenge>($"Ignoring a damaged saved setup in {FilePath}");
        return (read.Value!, false);
    }

    /// <summary>
    /// Keeps the unreadable file, so saving a new setup doesn't throw away the player's old ones for good. The copy is
    /// named by the broken file's time, so a later, different breakage doesn't replace it.
    /// </summary>
    private void KeepBroken(string error)
    {
        ModHelper.Warning<CustomBossChallenge>($"Ignoring unreadable saved setups {FilePath}: {error}");
        try
        {
            var time = File.GetLastWriteTimeUtc(FilePath).ToString("yyyyMMdd-HHmmss");
            File.Copy(FilePath, $"{FilePath}.{time}.broken", true);
        }
        catch (Exception)
        {
            // Nothing more can be done about a file that can't be copied either
        }
    }

    private void Write(List<SavedSetup> setups) => SafeFile.WriteAllText(FilePath, SavedSetupsFormat.Write(setups));
}
