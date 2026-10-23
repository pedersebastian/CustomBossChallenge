using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Models.ServerEvents;
using MelonLoader.Utils;
using Newtonsoft.Json;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// The player's saved Boss Creator setups, by name, in the mod's own file next to the last setup.
/// <br/>
/// Every setup is kept as a share code, so saved setups get the same versioning, range checks and protection against
/// damaged data as shared ones.
/// </summary>
public sealed class SavedSetupsStore
{
    public const int Version = 1;
    public const int MaxSetups = 50;

    public static readonly SavedSetupsStore Default =
        new(Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge", "SavedSetups.json"));

    private static readonly JsonSerializerSettings JsonSettings = new() {Formatting = Formatting.Indented};

    public string FilePath { get; }

    public SavedSetupsStore(string filePath) => FilePath = filePath;

    public sealed class Entry
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public DateTime Saved { get; set; }
    }

    private sealed class SetupsFile
    {
        public int Version { get; set; }
        public List<Entry>? Setups { get; set; }
    }

    public enum SaveResult
    {
        Saved,
        Replaced,
        Full
    }

    /// <summary>
    /// The saved setups, sorted by name
    /// </summary>
    public List<Entry> All => Read().OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>
    /// The name a setup is saved under: the challenge's name, cleaned like a shared one
    /// </summary>
    public static string NameOf(BossOptions options, DailyChallengeModel dcm) =>
        ChallengeLimits.CleanName(dcm.name, BossMode.DefaultName(options.Boss, options.Elite));

    public bool Contains(string name) => Read().Any(entry => Same(entry.Name, name));

    /// <summary>
    /// Saves the setup under its challenge name, replacing a setup with the same name
    /// </summary>
    public SaveResult Save(BossOptions options, DailyChallengeModel dcm)
    {
        var name = NameOf(options, dcm);
        var entries = Read();
        var replaced = entries.RemoveAll(entry => Same(entry.Name, name)) > 0;
        if (!replaced && entries.Count >= MaxSetups) return SaveResult.Full;

        entries.Add(new Entry {Name = name, Code = SetupCode.Encode(options, dcm), Saved = DateTime.UtcNow});
        Write(entries);
        return replaced ? SaveResult.Replaced : SaveResult.Saved;
    }

    /// <summary>
    /// Reads a saved setup the way a share code is read: values out of range are brought back, and a setup from a
    /// newer version of the mod gives <see cref="CodeProblem.TooNew"/>
    /// </summary>
    public bool TryLoad(string name, out BossOptions options, out DailyChallengeModel? dcm, out CodeProblem problem)
    {
        var entry = Read().FirstOrDefault(entry => Same(entry.Name, name));
        if (entry != null) return SetupCode.TryDecode(entry.Code, out options, out dcm, out problem, $"Saved setup '{entry.Name}'");

        options = new BossOptions();
        dcm = null;
        problem = CodeProblem.Invalid;
        return false;
    }

    public bool Delete(string name)
    {
        var entries = Read();
        if (entries.RemoveAll(entry => Same(entry.Name, name)) == 0) return false;

        Write(entries);
        return true;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private List<Entry> Read()
    {
        if (!File.Exists(FilePath)) return new List<Entry>();

        try
        {
            var file = JsonConvert.DeserializeObject<SetupsFile>(File.ReadAllText(FilePath), JsonSettings);
            var entries = new List<Entry>();
            foreach (var entry in file?.Setups ?? new List<Entry>())
            {
                // Entries without a name or code, and later ones with a name already used, can't be told apart. The
                // name is cleaned first, so names that only differ in what cleaning removes count as the same.
                var name = entry == null ? "" : ChallengeLimits.CleanName(entry.Name, "");
                if (entry == null || name.Length == 0 || string.IsNullOrWhiteSpace(entry.Code) ||
                    entries.Any(other => Same(other.Name, name)))
                {
                    ModHelper.Warning<CustomBossChallenge>($"Ignoring a damaged saved setup in {FilePath}");
                    continue;
                }

                entry.Name = name;
                entries.Add(entry);
            }

            // More than MaxSetups (a hand edited file) are all kept, so writing the file never drops any; only new
            // setups are refused while the list is full
            return entries;
        }
        catch (Exception e)
        {
            // Keep the unreadable file, so saving a new setup doesn't throw away the player's old ones for good. The
            // copy is named by the broken file's time, so a later, different breakage doesn't replace it.
            ModHelper.Warning<CustomBossChallenge>($"Ignoring unreadable saved setups {FilePath}: {e.Message}");
            try
            {
                var time = File.GetLastWriteTimeUtc(FilePath).ToString("yyyyMMdd-HHmmss");
                File.Copy(FilePath, $"{FilePath}.{time}.broken", true);
            }
            catch (Exception)
            {
                // Nothing more can be done about a file that can't be copied either
            }

            return new List<Entry>();
        }
    }

    private void Write(List<Entry> entries)
    {
        var file = new SetupsFile {Version = Version, Setups = entries};
        SafeFile.WriteAllText(FilePath, JsonConvert.SerializeObject(file, JsonSettings));
    }
}
