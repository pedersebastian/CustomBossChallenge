using System.IO;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Writing the mod's own files
/// </summary>
internal static class SafeFile
{
    /// <summary>
    /// Writes <paramref name="text"/> to a temporary file first and then moves it into place, so a crash can't leave
    /// a half written file behind. Creates the folder when it is missing.
    /// </summary>
    public static void WriteAllText(string path, string text)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, true);
    }
}
