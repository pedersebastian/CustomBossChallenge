using System;
using System.IO;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Models.ServerEvents;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Remembers the last Boss Creator setup (boss options + the editor's challenge model) in the mod's own file
/// </summary>
public sealed class LastSetupStore
{
    public const int Version = 2;

    public static readonly LastSetupStore Default =
        new(Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge", "LastSetup.json"));

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        Converters = {new StringEnumConverter()}
    };

    public string FilePath { get; }

    public LastSetupStore(string filePath) => FilePath = filePath;

    private sealed class SetupFile
    {
        public int Version { get; set; }
        public BossOptions? Options { get; set; }
        public string? Challenge { get; set; }
    }

    public void Save(BossOptions options, DailyChallengeModel dcm)
    {
        var file = new SetupFile {Version = Version, Options = options, Challenge = DcmJson.Serialize(dcm)};
        SafeFile.WriteAllText(FilePath, JsonConvert.SerializeObject(file, JsonSettings));
    }

    /// <summary>
    /// Loads the saved setup. Returns false (and logs a warning for a broken file) when there is nothing usable.
    /// </summary>
    public bool TryLoad(out BossOptions options, out DailyChallengeModel? dcm)
    {
        options = new BossOptions();
        dcm = null;
        if (!File.Exists(FilePath)) return false;

        try
        {
            var file = JsonConvert.DeserializeObject<SetupFile>(File.ReadAllText(FilePath), JsonSettings);
            if (file?.Options == null || string.IsNullOrEmpty(file.Challenge)) throw new InvalidDataException("Incomplete setup");

            options = file.Options.Sanitized();
            var changes = ChallengeLimits.OptionChanges(file.Options, options);
            dcm = DcmJson.Deserialize(file.Challenge);
            if (dcm == null) return false;

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
