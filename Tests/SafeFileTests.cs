using System;
using System.Collections;
using System.IO;
using System.Text;
using CustomBossChallenge.Persistence;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The mod's files are written whole or not at all: missing folders are created, old contents are fully replaced,
/// and a write that fails leaves the old file as it was
/// </summary>
public class SafeFileTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CustomBossChallengeTest-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Missing folders are created, and only the file itself is left behind
            var path = Path.Combine(directory, "Nested", "Deeper", "Setup.json");
            SafeFile.WriteAllText(path, "{\"Version\":1}");
            AssertEquals(File.ReadAllText(path), "{\"Version\":1}", "Written text");
            Assert(!File.Exists(path + ".tmp"), "The temporary file was left behind");
            AssertEquals(Directory.GetFiles(Path.GetDirectoryName(path)!).Length, 1, "Files in the folder");

            // Shorter text replaces longer text completely, with nothing of the old file at the end
            SafeFile.WriteAllText(path, new string('x', 5000));
            SafeFile.WriteAllText(path, "short");
            AssertEquals(File.ReadAllText(path), "short", "Text after replacing a longer file");

            // Names typed in any language survive, and empty text gives an empty file rather than none
            const string names = "Ærlig boss – 日本語 – Ünïcödé 🎈";
            SafeFile.WriteAllText(path, names);
            AssertEquals(File.ReadAllText(path, Encoding.UTF8), names, "Text outside ASCII");
            SafeFile.WriteAllText(path, "");
            Assert(File.Exists(path), "Empty text must still write the file");
            AssertEquals(new FileInfo(path).Length, 0L, "Size of an empty file");

            // A temporary file left by a crash in the middle of a write is replaced, not moved into place
            File.WriteAllText(path + ".tmp", "half written");
            SafeFile.WriteAllText(path, "fresh");
            AssertEquals(File.ReadAllText(path), "fresh", "Text after a stale temporary file");
            Assert(!File.Exists(path + ".tmp"), "The stale temporary file was left behind");

            // While another program holds the file open, the write fails and the old contents are kept
            SafeFile.WriteAllText(path, "old");
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Exception? locked = null;
                try
                {
                    SafeFile.WriteAllText(path, "new");
                }
                catch (Exception e)
                {
                    locked = e;
                }

                // Windows reports a file in use as either of these, depending on how it is held
                Assert(locked is IOException or UnauthorizedAccessException,
                    $"Writing a locked file gave {locked?.GetType().Name ?? "no error"}");
            }
            AssertEquals(File.ReadAllText(path), "old", "A failed write must keep the old file");

            // A folder that can't be made, because a file has its name, throws instead of writing somewhere else
            var blocker = Path.Combine(directory, "Blocker");
            File.WriteAllText(blocker, "a file, not a folder");
            AssertThrows<IOException>(() => SafeFile.WriteAllText(Path.Combine(blocker, "Setup.json"), "text"),
                "Writing below a file");
            AssertEquals(File.ReadAllText(blocker), "a file, not a folder", "The file in the way is untouched");
            Assert(!File.Exists(blocker + ".tmp") && !File.Exists(Path.Combine(directory, "Setup.json.tmp")),
                "No temporary file next to the file in the way");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        yield break;
    }
}
