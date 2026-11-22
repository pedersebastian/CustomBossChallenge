using System;
using System.IO;
using BTD_Mod_Helper;
using MelonLoader.Utils;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// The version of the mod the What's new popup was last shown for, or would have been (#264), in
/// <c>UserData/CustomBossChallenge/WhatsNew.json</c>: <c>{"seen": "2.15.0"}</c>
/// </summary>
public sealed class WhatsNewStore
{
    /// <summary>The mod's own folder in UserData, where all its files are</summary>
    public static string ModFolder => Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge");

    public static readonly WhatsNewStore Default = new(Path.Combine(ModFolder, "WhatsNew.json"));

    public string FilePath { get; }

    public WhatsNewStore(string filePath) => FilePath = filePath;

    /// <summary>
    /// The version last seen; null without a file, or with one that can't be read
    /// </summary>
    public string? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JObject.Parse(File.ReadAllText(FilePath))["seen"]?.Value<string>();
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Couldn't read {FilePath}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Remembers <paramref name="version"/>; a file that can't be written is only logged, so at worst the popup shows
    /// again the next time
    /// </summary>
    public void Save(string version)
    {
        try
        {
            SafeFile.WriteAllText(FilePath, new JObject {["seen"] = version}.ToString());
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Couldn't write {FilePath}: {e.Message}");
        }
    }
}
