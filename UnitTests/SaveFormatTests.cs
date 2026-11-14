using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The mod's files from every released version still read the same (#63), from the fixtures in
/// UnitTests/Fixtures/saves/. The stores around these formats need the game and are tested in-game.
/// </summary>
public class SaveFormatTests
{
    private static readonly string Folder = FindFolder();

    private static string FindFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !Directory.Exists(Path.Combine(folder.FullName, "UnitTests", "Fixtures", "saves"))) folder = folder.Parent;
        if (folder == null) throw new DirectoryNotFoundException("UnitTests/Fixtures/saves not found above " + AppContext.BaseDirectory);
        return Path.Combine(folder.FullName, "UnitTests", "Fixtures", "saves");
    }

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(Folder, name));

    private static T Read<T>(FormatRead<T> read) where T : class
    {
        Assert.True(read.Problem == FormatProblem.None, $"{read.Problem}: {read.Error}");
        return read.Value!;
    }

    // Old files load with the values they were saved with, upgraded where the format changed

    [Fact]
    public void Continue_v1_from_2_7_has_one_boss()
    {
        var file = Read(ContinueFormat.Read(Fixture("continue-v1-2.7.0.json")));
        Assert.Equal(1, file.Version);
        Assert.Equal(BossType.Vortex, file.Options.Boss);
        Assert.True(file.Options.Elite);
        Assert.Equal(SkullMode.EvenlySpaced, file.Options.SkullMode);
        Assert.Equal(5, file.Options.SkullCount);
        Assert.Equal(10, file.Options.SpeedPerTier);
        Assert.False(file.Options.Mixed);
        Assert.False(file.Options.UsesBossEventRounds);
        Assert.Equal([14, 41], file.Snapshots.Select(snapshot => snapshot.Round));
        Assert.Equal(41, file.LastRound);
        // Seven defeated tiers in a game with five are brought back to five
        Assert.Equal([0, 5], file.Snapshots.Select(snapshot => snapshot.DefeatedTiers));
        Assert.StartsWith("{\"towers\":", file.Challenge);
    }

    [Fact]
    public void Continue_v2_from_2_9_has_a_boss_per_tier()
    {
        var file = Read(ContinueFormat.Read(Fixture("continue-v2-2.9.0.json")));
        Assert.Equal(ContinueFormat.MixedVersion, file.Version);
        Assert.True(file.Options.Mixed);
        Assert.Equal((BossType.Lych, false), file.Options.BossOf(1));
        Assert.Equal((BossType.Vortex, false), file.Options.BossOf(2));
        Assert.Equal((BossType.Diamondback, true), file.Options.BossOf(3));
        Assert.Null(file.Options.RandomTiers);
        Assert.False(file.Options.UsesBossEventRounds);
        Assert.Equal(10, file.Options.HealthPerTier);
        Assert.All(file.Snapshots, snapshot => Assert.Equal(2, snapshot.DefeatedTiers));
    }

    [Fact]
    public void Continue_v3_from_2_10_has_boss_event_rounds()
    {
        var file = Read(ContinueFormat.Read(Fixture("continue-v3-2.10.0.json")));
        Assert.Equal(ContinueFormat.Version, file.Version);
        Assert.True(file.Options.UsesBossEventRounds);
        Assert.Equal("phayze", file.Options.BaseEventRoundSet);
        Assert.Equal((BossType.Blastapopoulos, false), file.Options.BossOf(2));
        Assert.True(file.Options.WasRandom(2));
        Assert.False(file.Options.WasRandom(1));
        Assert.Equal(5, file.Options.SpeedPerTier);
    }

    [Fact]
    public void LastSetup_from_2_0_gets_health_per_tier_from_extra_tier_health()
    {
        var read = LastSetupFormat.Read(Fixture("lastsetup-v2-2.0.0.json"));
        var file = Read(read);
        Assert.Equal(BossType.Lych, file.Options.Boss);
        Assert.True(file.Options.Elite);
        Assert.Equal(SkullMode.EvenlySpaced, file.Options.SkullMode);
        Assert.Equal(4, file.Options.SkullCount);
        Assert.Equal(7, file.Options.Tiers);
        Assert.Equal([40, 60, 80, 90, 100, 110, 120], file.Options.EffectiveSpawnRounds);
        // Tier 6 had 300% of tier 5; now that is 1.5 x (1 + 5 x 20%)
        Assert.Equal(20, file.Options.HealthPerTier);
        Assert.Null(file.Options.ExtraTierHealth);
        Assert.Equal(0, file.Options.SpeedPerTier);
        Assert.False(file.Options.Mixed);
        Assert.Contains(read.Changes, change => change.StartsWith("HealthPerTier 0 -> 20"));
    }

    [Fact]
    public void LastSetup_from_2_10_reads_as_it_was_saved()
    {
        var read = LastSetupFormat.Read(Fixture("lastsetup-v2-2.10.0.json"));
        var file = Read(read);
        Assert.Equal(new BossOptions(), file.Options);
        Assert.Empty(read.Changes);
        Assert.Contains("\"name\":\"Fortified Everything\"", file.Challenge);
    }

    [Fact]
    public void SavedSetups_from_2_5_read_as_they_were_saved()
    {
        var setups = Read(SavedSetupsFormat.Read(Fixture("savedsetups-v1-2.5.0.json")));
        Assert.Equal(["Custom Lych", "Custom Dreadbloon"], setups.Select(setup => setup.Name));
        Assert.All(setups, setup => Assert.StartsWith("CBC", setup.Code));
        Assert.Equal("2026-10-23T14:08:23", setups[0].Saved.ToUniversalTime().ToString("s"));
    }

    [Fact]
    public void SavedSetups_from_2_10_leave_out_damaged_setups()
    {
        var read = SavedSetupsFormat.Read(Fixture("savedsetups-v1-2.10.0.json"));
        var setups = Read(read);
        Assert.Equal(["Custom Lych", "Custom Dreadbloon", "Custom Elite Phayze", "Custom Elite Diamondback"],
            setups.Select(setup => setup.Name));
        // No name, no code, "custom lych" again and null
        Assert.Equal(4, read.Changes.Count);
    }

    // Files from a newer version of the mod are left alone

    public static IEnumerable<object[]> FutureFiles() =>
        [["continue-v99-future.json"], ["lastsetup-v99-future.json"], ["savedsetups-v99-future.json"]];

    [Theory]
    [MemberData(nameof(FutureFiles))]
    public void A_file_from_a_newer_version_is_too_new(string name)
    {
        var text = Fixture(name);
        var read = Format(name).Read(text);
        Assert.Equal(FormatProblem.TooNew, read.Problem);
        Assert.Null(read.Value);
    }

    // Read, write and read again gives the same file

    public static IEnumerable<object[]> ReleasedFiles() =>
        Fixtures().Where(fixture => !fixture.Future).Select(fixture => new object[] {fixture.Name});

    [Theory]
    [MemberData(nameof(ReleasedFiles))]
    public void A_file_reads_the_same_after_writing_it_again(string name)
    {
        var format = Format(name);
        var first = format.Read(Fixture(name));
        Assert.Equal(FormatProblem.None, first.Problem);

        var written = format.Write(first.Value!);
        var second = format.Read(written);
        Assert.Equal(FormatProblem.None, second.Problem);
        Assert.Empty(second.Changes);
        Assert.Equal(format.Describe(first.Value!), format.Describe(second.Value!));
        // Writing it a second time gives the same text
        Assert.Equal(written, format.Write(second.Value!));
    }

    public static IEnumerable<object[]> GameKinds() =>
    [
        [new BossOptions {Boss = BossType.Dreadbloon, Tiers = 8, SpawnRounds = "40, 60, 80, 90, 100, 110, 120, 130"}, ContinueFormat.SingleBossVersion],
        [new BossOptions {TierBosses = "Lych,VortexElite", Tiers = 2}, ContinueFormat.MixedVersion],
        [new BossOptions {Boss = BossType.Bloonarius, BossEventRounds = true}, ContinueFormat.Version],
        [new BossOptions {TierBosses = ",Phayze", Tiers = 2, RandomTiers = "2", BossEventRounds = true}, ContinueFormat.Version]
    ];

    [Theory]
    [MemberData(nameof(GameKinds))]
    public void Every_kind_of_game_is_written_with_its_version_and_reads_back(BossOptions options, int version)
    {
        RoundSnapshot[] snapshots = [new(41, 1, "{\"b\":2}"), new(40, 0, "{\"a\":1}")];
        var text = ContinueFormat.Write(options, "{\"name\":\"Test\"}", snapshots);
        var file = Read(ContinueFormat.Read(text));
        Assert.Equal(version, file.Version);
        Assert.Equal(options, file.Options);
        Assert.Equal("{\"name\":\"Test\"}", file.Challenge);
        // Written by round
        Assert.Equal(snapshots.OrderBy(snapshot => snapshot.Round), file.Snapshots);
    }

    [Fact]
    public void Saved_setups_read_back_as_written()
    {
        SavedSetup[] setups =
        [
            new() {Name = "One", Code = "CBC2:abc", Saved = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)},
            new() {Name = "Two", Code = "CBC4:def", Saved = new DateTime(2026, 6, 7, 8, 9, 10, DateTimeKind.Utc)}
        ];
        var read = Read(SavedSetupsFormat.Read(SavedSetupsFormat.Write(setups)));
        Assert.Equal(setups.Select(setup => (setup.Name, setup.Code, setup.Saved)),
            read.Select(setup => (setup.Name, setup.Code, setup.Saved.ToUniversalTime())));
    }

    // Every released version has a fixture, so raising a Version without adding one fails here

    /// <summary>The first version of each file that was released</summary>
    private static readonly Dictionary<string, (int First, int Current)> Versions = new()
    {
        ["continue"] = (1, ContinueFormat.Version), // 2.7.0
        ["lastsetup"] = (2, LastSetupFormat.Version), // 2.0.0, which started at 2
        ["savedsetups"] = (1, SavedSetupsFormat.Version) // 2.5.0
    };

    [Theory]
    [InlineData("continue")]
    [InlineData("lastsetup")]
    [InlineData("savedsetups")]
    public void Every_released_version_has_a_fixture(string file)
    {
        var (first, current) = Versions[file];
        var released = Fixtures().Where(fixture => fixture.File == file && !fixture.Future)
            .Select(fixture => fixture.Version).Distinct().Order().ToList();
        Assert.Equal(Enumerable.Range(first, current - first + 1), released);
        Assert.Contains(Fixtures(), fixture => fixture.File == file && fixture.Future && fixture.Version > current);
    }

    [Theory]
    [MemberData(nameof(ReleasedFiles))]
    public void A_fixture_has_the_version_its_name_says(string name)
    {
        var version = Fixtures().Single(fixture => fixture.Name == name).Version;
        Assert.Matches($"\"Version\": {version}[,\\r\\n]", Fixture(name));
    }

    // Damaged files, as far as SafeFile and #14 don't cover them

    [Theory]
    [InlineData("")]
    [InlineData("{\"Version\": 2, \"Options\": 5, \"Challenge\": \"{}\"}")]
    [InlineData("{\"Version\": 2, \"Options\": {\"Tiers\": \"many\"}, \"Challenge\": \"{}\"}")]
    public void A_damaged_last_setup_is_invalid(string text) =>
        Assert.Equal(FormatProblem.Invalid, LastSetupFormat.Read(text).Problem);

    [Theory]
    [InlineData("")]
    [InlineData("{\"Version\": 0, \"Options\": {}, \"Challenge\": \"{}\", \"Snapshots\": [{\"Round\": 1, \"DefeatedTiers\": 0, \"Json\": \"{}\"}]}")]
    [InlineData("{\"Version\": 1, \"Options\": 5, \"Challenge\": \"{}\", \"Snapshots\": []}")]
    [InlineData("{\"Version\": 1, \"Options\": {}, \"Challenge\": \"{}\", \"Snapshots\": []}")]
    [InlineData("{\"Version\": 1, \"Options\": {}, \"Challenge\": \"{}\", \"Snapshots\": [null]}")]
    public void A_damaged_unfinished_game_is_invalid(string text) =>
        Assert.Equal(FormatProblem.Invalid, ContinueFormat.Read(text).Problem);

    [Fact]
    public void Saved_setups_with_the_wrong_shape_are_invalid() =>
        Assert.Equal(FormatProblem.Invalid, SavedSetupsFormat.Read("{\"Version\": 1, \"Setups\": 5}").Problem);

    [Fact]
    public void One_damaged_saved_setup_doesnt_hide_the_others()
    {
        // A field of the wrong type used to make the whole file unreadable (#110)
        var read = SavedSetupsFormat.Read("""
            {"Version": 1, "Setups": [
              {"Name": "Good", "Code": "CBC1:abc", "Saved": "2026-10-23T14:08:23Z"},
              {"Name": "Bad date", "Code": "CBC1:abc", "Saved": "yesterday"},
              {"Name": {}, "Code": "CBC1:abc"}
            ]}
            """);
        var setups = Read(read);
        Assert.Equal(["Good"], setups.Select(setup => setup.Name));
        Assert.Equal(2, read.Changes.Count);
    }

    [Theory]
    [InlineData("""{"Version": 99999999999999999999, "Setups": []}""")]
    [InlineData("""{"Version": 2, "Setups": []}""")]
    public void Saved_setups_with_a_higher_version_are_too_new(string text) =>
        Assert.Equal(FormatProblem.TooNew, SavedSetupsFormat.Read(text).Problem);

    [Fact]
    public void An_empty_saved_setups_file_has_no_setups() =>
        Assert.Empty(Read(SavedSetupsFormat.Read("")));

    // The fixtures and the three formats, side by side

    private sealed record FixtureFile(string Name, string File, int Version, bool Future);

    private static List<FixtureFile> Fixtures() =>
        Directory.GetFiles(Folder, "*.json").Select(Path.GetFileName).Select(name =>
        {
            var match = Regex.Match(name!, @"^(\w+)-v(\d+)-(.+)\.json$");
            Assert.True(match.Success, $"{name} isn't named <file>-v<version>-<release>.json");
            return new FixtureFile(name!, match.Groups[1].Value, int.Parse(match.Groups[2].Value), match.Groups[3].Value == "future");
        }).ToList();

    private sealed record SaveFormat(
        Func<string, FormatRead<object>> Read,
        Func<object, string> Write,
        Func<object, string> Describe);

    private static FormatRead<object> Boxed<T>(FormatRead<T> read) where T : class =>
        new(read.Problem, read.Value, read.Error, read.Changes);

    private static SaveFormat Format(string name) => name.Split('-')[0] switch
    {
        "continue" => new SaveFormat(text => Boxed(ContinueFormat.Read(text)),
            file => ((ContinueFile) file) is var game ? ContinueFormat.Write(game.Options, game.Challenge, game.Snapshots) : "",
            file => ((ContinueFile) file) is var game
                ? $"{game.Version} {game.Options} {game.Challenge} {string.Join(";", game.Snapshots)}"
                : ""),
        "lastsetup" => new SaveFormat(text => Boxed(LastSetupFormat.Read(text)),
            file => ((LastSetupFile) file) is var setup ? LastSetupFormat.Write(setup.Options, setup.Challenge) : "",
            file => file.ToString()!),
        "savedsetups" => new SaveFormat(text => Boxed(SavedSetupsFormat.Read(text)),
            file => SavedSetupsFormat.Write((List<SavedSetup>) file),
            file => string.Join(";", ((List<SavedSetup>) file).Select(setup => $"{setup.Name}|{setup.Code}|{setup.Saved.ToUniversalTime():O}"))),
        _ => throw new ArgumentException($"No format for {name}")
    };
}
