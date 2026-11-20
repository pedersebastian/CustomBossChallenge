using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CustomBossChallenge.Persistence;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// A saved setups file with setups that can't be read is copied before the next save writes over it, so the damaged
/// setups aren't lost for good (#230), like a file that can't be read at all (#182)
/// </summary>
public sealed class SavedSetupsFileTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "CustomBossChallengeTest-" + Guid.NewGuid().ToString("N"));

    private readonly List<string> warnings = [];
    private string FilePath => Path.Combine(directory, "SavedSetups.json");

    public SavedSetupsFileTests() => Directory.CreateDirectory(directory);

    public void Dispose() => Directory.Delete(directory, true);

    private SavedSetupsFile File() => new(FilePath, warnings.Add);

    private static string Entry(string name) =>
        $$"""{"Name": "{{name}}", "Code": "CBC1:{{name.Replace(" ", "")}}", "Saved": "2026-10-23T14:08:23Z"}""";

    private static string FileWith(params string[] entries) => $$"""{"Version": 1, "Setups": [{{string.Join(",", entries)}}]}""";

    private string[] Copies() => Directory.GetFiles(directory, "SavedSetups.json.*.broken");

    private static SavedSetup New(string name) => new() {Name = name, Code = "CBC1:new", Saved = DateTime.UtcNow};

    [Fact]
    public void A_damaged_setup_is_kept_in_a_copy_before_the_next_save()
    {
        var damaged = """{"Name": 5, "Code": 6, "Saved": 7}""";
        var text = FileWith(Entry("Good one"), damaged, Entry("Good two"));
        System.IO.File.WriteAllText(FilePath, text);
        var file = File();

        var (setups, _) = file.Read();
        Assert.Equal(["Good one", "Good two"], setups.Select(setup => setup.Name));
        setups.Add(New("New one"));
        file.Write(setups);

        var copy = Assert.Single(Copies());
        Assert.Equal(text, System.IO.File.ReadAllText(copy));
        Assert.Equal(["Good one", "Good two", "New one"], file.Read().Setups.Select(setup => setup.Name));
        Assert.Single(warnings);
    }

    [Fact]
    public void Texts_as_numbers_in_every_setup_are_kept_in_a_copy()
    {
        var text = FileWith("""{"Name": 1, "Code": 2, "Saved": 3}""", """{"Name": 4, "Code": 5, "Saved": 6}""");
        System.IO.File.WriteAllText(FilePath, text);
        var file = File();

        var (setups, _) = file.Read();
        Assert.Empty(setups);
        file.Write([New("New one")]);

        Assert.Equal(text, System.IO.File.ReadAllText(Assert.Single(Copies())));
    }

    [Fact]
    public void Text_that_isnt_UTF8_is_kept_in_a_copy()
    {
        var bytes = Encoding.UTF8.GetBytes(FileWith(Entry("Good one"), Entry("Bad XX name"), Entry("Good two")));
        var at = Encoding.UTF8.GetString(bytes).IndexOf("XX", StringComparison.Ordinal);
        bytes[at] = 0xC3;
        bytes[at + 1] = 0x28; // not a UTF-8 sequence
        System.IO.File.WriteAllBytes(FilePath, bytes);
        var file = File();

        var (setups, _) = file.Read();
        file.Write(setups);

        Assert.Equal(bytes, System.IO.File.ReadAllBytes(Assert.Single(Copies())));
        Assert.Contains("Good one", file.Read().Setups.Select(setup => setup.Name));
    }

    [Fact]
    public void A_damaged_file_is_copied_and_logged_once_however_often_it_is_read()
    {
        System.IO.File.WriteAllText(FilePath, FileWith(Entry("Good"), """{"Name": ""}"""));
        var file = File();

        for (var i = 0; i < 5; i++) file.Read();

        Assert.Single(Copies());
        Assert.Single(warnings);
    }

    [Fact]
    public void A_good_file_is_not_copied()
    {
        System.IO.File.WriteAllText(FilePath, FileWith(Entry("Good one"), Entry("Good two")));
        var file = File();

        var (setups, _) = file.Read();
        file.Write(setups);

        Assert.Empty(Copies());
        Assert.Empty(warnings);
    }

    [Fact]
    public void A_byte_order_mark_is_not_damage()
    {
        System.IO.File.WriteAllText(FilePath, FileWith(Entry("Good")), new UTF8Encoding(true));

        Assert.Equal(["Good"], File().Read().Setups.Select(setup => setup.Name));
        Assert.Empty(Copies());
    }

    [Fact]
    public void An_unreadable_file_is_still_copied()
    {
        System.IO.File.WriteAllText(FilePath, "not json {");
        var file = File();

        Assert.Empty(file.Read().Setups);
        Assert.Single(Copies());
    }
}
