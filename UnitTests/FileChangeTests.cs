using System;
using System.IO;
using CustomBossChallenge.Persistence;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// A broken saved setups file is logged and copied once for each version of it, not on every read (#182)
/// </summary>
public sealed class FileChangeTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("cbc-filechange-").FullName;

    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public void A_file_is_new_once_until_it_changes()
    {
        var path = Path.Combine(folder, "SavedSetups.json");
        File.WriteAllText(path, "{ broken");
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var change = new FileChange();

        Assert.True(change.IsNew(path));
        Assert.False(change.IsNew(path));
        Assert.False(change.IsNew(path));

        // Broken again, at another time
        File.WriteAllText(path, "[ broken again");
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc));
        Assert.True(change.IsNew(path));
        Assert.False(change.IsNew(path));

        // Another size at the same time is a change too
        File.WriteAllText(path, "[ broken again, longer");
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc));
        Assert.True(change.IsNew(path));
    }

    [Fact]
    public void A_missing_file_always_counts_as_new()
    {
        var change = new FileChange();
        var path = Path.Combine(folder, "missing.json");
        Assert.True(change.IsNew(path));
        Assert.True(change.IsNew(path));
    }
}
