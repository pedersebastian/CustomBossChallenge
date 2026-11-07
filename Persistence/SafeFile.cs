using System.IO;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Writing the mod's own files
/// </summary>
internal static class SafeFile
{
    /// <summary>
    /// Writes <paramref name="text"/> to a temporary file first and then moves it into place, so a crash can't leave
    /// a half written file behind, and flushes it to disk before the move. Creates the folder when it is missing.
    /// </summary>
    public static void WriteAllText(string path, string text)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // On disk before it takes the file's place, so a crash or power cut right after can't leave an empty file
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            writer.Write(text);
            writer.Flush();
            stream.Flush(true);
        }
        File.Move(temp, path, true);
    }
}
