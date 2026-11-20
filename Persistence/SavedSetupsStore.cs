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

    public string FilePath => file.FilePath;

    /// <summary>
    /// The file itself; a damaged one is copied before it is written over (#182, #230)
    /// </summary>
    private readonly SavedSetupsFile file;

    public SavedSetupsStore(string filePath) =>
        file = new SavedSetupsFile(filePath, message => ModHelper.Warning<CustomBossChallenge>(message));

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

    private (List<SavedSetup> Setups, bool TooNew) Read() => file.Read();

    private void Write(List<SavedSetup> setups) => file.Write(setups);
}
