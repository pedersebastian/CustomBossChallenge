using System;
using System.IO;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Remembers a file's last write time and size, to do something once for each version of it: a broken saved setups
/// file is read every time the list is shown or a name is checked, and must not be logged and copied every time (#182)
/// </summary>
public sealed class FileChange
{
    private (DateTime Time, long Size)? seen;

    /// <summary>
    /// Whether the file at <paramref name="path"/> changed since the last call that returned true; true the first time.
    /// A file that can't be looked at counts as changed.
    /// </summary>
    public bool IsNew(string path)
    {
        (DateTime, long)? now;
        try
        {
            var info = new FileInfo(path);
            now = info.Exists ? (info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (Exception)
        {
            now = null;
        }

        if (now != null && now == seen) return false;
        seen = now;
        return true;
    }
}
