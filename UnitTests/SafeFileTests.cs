using System;
using System.IO;
using System.Text;
using CustomBossChallenge.Persistence;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The mod's files are written whole or not at all: missing folders are created, old contents are fully replaced,
/// and a write that fails leaves the old file as it was
/// </summary>
public sealed class SafeFileTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "CustomBossChallengeTest-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(directory, "Nested", "Deeper", "Setup.json");

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Fact]
    public void Creates_missing_folders_and_leaves_only_the_file()
    {
        SafeFile.WriteAllText(FilePath, "{\"Version\":1}");

        Assert.Equal("{\"Version\":1}", File.ReadAllText(FilePath));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(FilePath)!));
    }

    [Fact]
    public void Shorter_text_replaces_longer_text_completely()
    {
        SafeFile.WriteAllText(FilePath, new string('x', 5000));

        SafeFile.WriteAllText(FilePath, "short");

        Assert.Equal("short", File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData("Ærlig boss – 日本語 – Ünïcödé 🎈")]
    [InlineData("")]
    public void Any_text_comes_back_unchanged(string text)
    {
        SafeFile.WriteAllText(FilePath, text);

        Assert.True(File.Exists(FilePath));
        Assert.Equal(text, File.ReadAllText(FilePath, Encoding.UTF8));
    }

    [Fact]
    public void A_temporary_file_left_by_a_crash_is_replaced()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath + ".tmp", "half written");

        SafeFile.WriteAllText(FilePath, "fresh");

        Assert.Equal("fresh", File.ReadAllText(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void A_file_in_use_keeps_its_old_contents()
    {
        // Only Windows stops a file that is open elsewhere from being replaced
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Files in use can be replaced on this system");
        SafeFile.WriteAllText(FilePath, "old");

        using (new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = Record.Exception(() => SafeFile.WriteAllText(FilePath, "new"));
            Assert.True(error is IOException or UnauthorizedAccessException, $"Got {error?.GetType().Name ?? "no error"}");
        }

        Assert.Equal("old", File.ReadAllText(FilePath));
    }

    [Fact]
    public void A_file_where_the_folder_should_be_throws_without_writing_elsewhere()
    {
        Directory.CreateDirectory(directory);
        var blocker = Path.Combine(directory, "Blocker");
        File.WriteAllText(blocker, "a file, not a folder");

        Assert.ThrowsAny<IOException>(() => SafeFile.WriteAllText(Path.Combine(blocker, "Setup.json"), "text"));

        Assert.Equal("a file, not a folder", File.ReadAllText(blocker));
        Assert.Single(Directory.GetFileSystemEntries(directory));
    }
}
