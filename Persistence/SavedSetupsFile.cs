using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Reading and writing SavedSetups.json, without the game. A file that is damaged, as a whole or in some of its
/// setups, is copied before anything is written over it, so saving a setup never throws away the player's old ones
/// for good (#182, #230).
/// </summary>
public sealed class SavedSetupsFile
{
    private static readonly UTF8Encoding Strict = new(false, true);

    public string FilePath { get; }

    private readonly Action<string> warn;

    /// <summary>
    /// A file from a newer version stays in place; say so in the log only once
    /// </summary>
    private bool toldTooNew;

    /// <summary>
    /// A damaged file is read again each time the list is shown or a name checked; it's logged and copied once for
    /// each version of it (#182)
    /// </summary>
    private readonly FileChange broken = new();

    public SavedSetupsFile(string filePath, Action<string> warn)
    {
        FilePath = filePath;
        this.warn = warn;
    }

    /// <summary>
    /// The setups that could be read; none while the file comes from a newer version of the mod
    /// </summary>
    public (List<SavedSetup> Setups, bool TooNew) Read()
    {
        if (!File.Exists(FilePath)) return ([], false);

        FormatRead<List<SavedSetup>> read;
        var badText = false;
        try
        {
            var bytes = File.ReadAllBytes(FilePath);
            var start = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? Encoding.UTF8.Preamble.Length : 0;
            string text;
            try
            {
                text = Strict.GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                // Bytes that aren't text become "?"-like characters, which may change or drop a setup
                badText = true;
                text = Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
            }
            read = SavedSetupsFormat.Read(text);
        }
        catch (Exception e)
        {
            read = FormatRead<List<SavedSetup>>.Invalid(e.Message);
        }

        switch (read.Problem)
        {
            case FormatProblem.TooNew:
                if (!toldTooNew) warn($"Keeping {FilePath} from a newer version of the mod");
                toldTooNew = true;
                return ([], true);
            case FormatProblem.Invalid:
                KeepBroken($"Ignoring unreadable saved setups {FilePath}: {read.Error}");
                return ([], false);
        }

        if (badText || read.Changes.Count > 0)
        {
            KeepBroken($"Ignoring {read.Changes.Count} damaged saved setup(s) in {FilePath}" +
                       (badText ? ", which has text that isn't UTF-8" : ""));
        }
        return (read.Value!, false);
    }

    public void Write(List<SavedSetup> setups) => SafeFile.WriteAllText(FilePath, SavedSetupsFormat.Write(setups));

    /// <summary>
    /// Keeps the damaged file, since the next save writes only what could be read. The copy is named by the damaged
    /// file's time, so a later, different breakage doesn't replace it.
    /// </summary>
    private void KeepBroken(string message)
    {
        if (!broken.IsNew(FilePath)) return;
        warn(message);
        try
        {
            File.Copy(FilePath, BrokenPath, true);
        }
        catch (Exception)
        {
            // Nothing more can be done about a file that can't be copied either
        }
    }

    /// <summary>
    /// Where a damaged file is copied to: next to it, named by its last write time
    /// </summary>
    public string BrokenPath => $"{FilePath}.{File.GetLastWriteTimeUtc(FilePath):yyyyMMdd-HHmmss}.broken";
}
