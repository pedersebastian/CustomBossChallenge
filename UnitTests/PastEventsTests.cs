using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The Past Events list (#146): reading boss-events.json, choosing between the built-in copy, the cache and the
/// download, mapping the events' rules onto the Boss Creator, and the list's search, on the real archive in
/// data/boss-events/
/// </summary>
public class PastEventsTests : IDisposable
{
    private static readonly string Root = FindRoot();
    private static readonly string ArchiveDir = Path.Combine(Root, "data", "boss-events");

    /// <summary>The default challenge as a share code sees it, which the in-game WebDataTest writes for the website</summary>
    private static readonly JObject Defaults =
        (JObject) JObject.Parse(File.ReadAllText(Path.Combine(Root, "web", "src", "data", "game.json")))["defaultChallenge"]!;

    private static readonly HashSet<string> Maps =
        ((JObject) JObject.Parse(File.ReadAllText(Path.Combine(Root, "web", "src", "data", "game.json")))["maps"]!)
        .Properties().Select(property => property.Name).ToHashSet();

    private static readonly string[] UnsupportedModes = ["Deflation", "Apopalypse"];

    private readonly string folder = Path.Combine(Path.GetTempPath(), "CbcPastEvents-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data", "boss-events"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("data/boss-events not found above " + AppContext.BaseDirectory);
    }

    /// <summary>The archive's files as one boss-events.json, like archive.mjs --bundle without what it adds</summary>
    private static string Bundle(string generated = "2026-11-12T06:00:00.000Z", int formatVersion = 1,
        Func<JObject, bool>? only = null)
    {
        var events = new JArray(Directory.GetFiles(ArchiveDir, "*.json")
            .Select(file => JObject.Parse(File.ReadAllText(file)))
            .Where(e => only == null || only(e)));
        return new JObject
        {
            ["formatVersion"] = formatVersion, ["generated"] = generated, ["license"] = "test", ["events"] = events
        }.ToString(Formatting.None);
    }

    private static PastEvent Event(string id)
    {
        var read = PastEventsFormat.Read(Bundle(only: e => (string?) e["id"] == id));
        return Assert.Single(read.Value!.Events);
    }

    private static PastEventSetup Map(PastEvent pastEvent, bool elite) =>
        PastEventRules.Map(pastEvent, elite, Defaults, Maps.Contains, UnsupportedModes);

    private static MemoryStream Gzip(string text)
    {
        var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.Optimal, true)) gzip.Write(Encoding.UTF8.GetBytes(text));
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Every_event_in_the_archive_is_read()
    {
        var read = PastEventsFormat.Read(Bundle());
        Assert.Equal(FormatProblem.None, read.Problem);
        Assert.Empty(read.Changes);
        Assert.Equal(Directory.GetFiles(ArchiveDir, "*.json").Length, read.Value!.Events.Count);
        Assert.All(read.Value.Events, e => Assert.NotNull(PastEventRules.Boss(e.BossKey)));
    }

    [Fact]
    public void Every_event_in_the_archive_maps_onto_the_Boss_Creator()
    {
        foreach (var pastEvent in PastEventsFormat.Read(Bundle()).Value!.Events)
        {
            foreach (var elite in new[] {false, true})
            {
                var setup = Map(pastEvent, elite);
                Assert.True(setup.Problem == PastEventProblem.None, $"{pastEvent.Id}: {setup.Problem}");
                Assert.Equal(elite, setup.Options.Elite);
                Assert.Equal(PastEventRules.Boss(pastEvent.BossKey), setup.Options.Boss);

                // Exactly what a share code may set, so nothing is dropped on the way into the editor
                var dropped = new List<string>();
                CodeFormat.FilterChallenge(setup.Challenge, (JObject) Defaults.DeepClone(), dropped);
                Assert.True(dropped.Count == 0, $"{pastEvent.Id}: dropped {string.Join(", ", dropped)}");
            }
        }
    }

    [Fact]
    public void Every_events_boss_speed_and_health_fit_the_Boss_Creator()
    {
        foreach (var pastEvent in PastEventsFormat.Read(Bundle()).Value!.Events)
        {
            foreach (var elite in new[] {false, true})
            {
                var modifiers = Map(pastEvent, elite).Challenge["bloonModifiers"];
                var speed = (float?) modifiers?["bossSpeedMultiplier"] ?? 1;
                var health = (float?) modifiers?["healthMultipliers"]?["boss"] ?? 1;
                Assert.True(speed is >= ChallengeLimits.MinBossSpeed and <= ChallengeLimits.MaxBossSpeed,
                    $"{pastEvent.Id}{(elite ? " Elite" : "")}: boss speed {speed}");
                Assert.True(health is >= ChallengeLimits.MinBossHealth and <= ChallengeLimits.MaxBossHealth,
                    $"{pastEvent.Id}{(elite ? " Elite" : "")}: boss health {health}");
            }
        }
    }

    [Fact]
    public void The_fastest_bosses_keep_their_speed()
    {
        // #220: Bloonarius 61 ran at 2500 %, Dreadbloon 27 Elite at 2000 %
        Assert.Equal(25f, (float) Map(Event("Bloonarius61_me1upcdz"), false).Challenge["bloonModifiers"]!["bossSpeedMultiplier"]!);
        Assert.Equal(25f, (float) Map(Event("Bloonarius61_me1upcdz"), true).Challenge["bloonModifiers"]!["bossSpeedMultiplier"]!);
        Assert.Equal(20f, (float) Map(Event("Dreadbloon27_m9xxdr7g"), true).Challenge["bloonModifiers"]!["bossSpeedMultiplier"]!);
        Assert.Equal(TierBossMath.MaxSpeed, (int) Math.Round(ChallengeLimits.MaxBossSpeed * TierBossMath.Normal));
    }

    [Fact]
    public void Every_event_that_opens_can_be_played()
    {
        // #232: Diamondback 6 starts on round 40 with its first tier on round 40, which Play refused
        var opened = 0;
        foreach (var pastEvent in PastEventsFormat.Read(Bundle()).Value!.Events)
        {
            foreach (var elite in new[] {false, true})
            {
                var setup = Map(pastEvent, elite);
                if (setup.Problem != PastEventProblem.None) continue;
                opened++;
                var start = (int?) setup.Challenge["startRules"]?["round"] ?? -1;
                Assert.False(TierMath.SkipsFirstTier(start, setup.Options.EffectiveSpawnRounds),
                    $"{pastEvent.Id}{(elite ? " Elite" : "")} starts on round {start}, after its first tier on {setup.Options.EffectiveSpawnRounds[0]}");
            }
        }
        Assert.True(opened >= 2 * 284, $"Events that open: {opened}");

        var diamondback = Map(Event("Diamondback6_mrea6zh0"), false);
        Assert.Equal(40, (int) diamondback.Challenge["startRules"]!["round"]!);
        Assert.Equal(40, diamondback.Options.EffectiveSpawnRounds[0]);
    }

    [Fact]
    public void Zero_max_towers_and_lives_are_the_games_default()
    {
        // Diamondback 6's placeholder metadata has 0 for both, which would leave the player no towers and one life
        foreach (var elite in new[] {false, true})
        {
            var challenge = Map(Event("Diamondback6_mrea6zh0"), elite).Challenge;
            Assert.Null(challenge["maxTowers"]);
            Assert.Null(challenge["startRules"]!["maxLives"]);
            Assert.Equal(elite ? 100 : 200, (int) challenge["startRules"]!["lives"]!);
        }

        foreach (var pastEvent in PastEventsFormat.Read(Bundle()).Value!.Events)
        {
            foreach (var elite in new[] {false, true})
            {
                var challenge = Map(pastEvent, elite).Challenge;
                Assert.NotEqual(0, (int?) challenge["maxTowers"] ?? -1);
                Assert.NotEqual(0, (int?) challenge["startRules"]?["maxLives"] ?? -1);
            }
        }
    }

    [Fact]
    public void Lych47_has_its_own_rules_normal_and_Elite()
    {
        var lych = Event("Lych47_m5zq59de");
        Assert.Equal("LeastCash", lych.NormalScoring);
        Assert.Equal("LeastTiers", lych.EliteScoring);

        var normal = Map(lych, false);
        Assert.Equal(BossType.Lych, normal.Options.Boss);
        Assert.True(normal.Options.BossEventRounds);
        Assert.Equal("Tinkerton", (string?) normal.Challenge["map"]);
        Assert.Equal("Medium", (string?) normal.Challenge["difficulty"]);
        Assert.Equal(2500, (int) normal.Challenge["startRules"]!["cash"]!);
        Assert.Equal(200, (int) normal.Challenge["startRules"]!["lives"]!);
        Assert.Equal(4, (int) normal.Challenge["maxParagons"]!);
        Assert.Equal(0.5, (double) normal.Challenge["bloonModifiers"]!["bossSpeedMultiplier"]!);
        Assert.Equal(2, (double) normal.Challenge["bloonModifiers"]!["healthMultipliers"]!["boss"]!);
        // The bundle's displayName ("Lych 47"), or the name without it
        Assert.Equal($"{lych.DisplayName}, 17 Jan 2025", (string?) normal.Challenge["name"]);
        Assert.Empty(normal.Unsupported);

        // Only the six towers the event lists are allowed; the rest of the game's towers are not
        var towers = ((JArray) normal.Challenge["towers"]!).Cast<JObject>().ToDictionary(t => (string) t["tower"]!);
        Assert.Equal(((JArray) Defaults["towers"]!).Count, towers.Count);
        Assert.Equal(99, (int) towers["Benjamin"]["max"]!);
        Assert.Equal(-1, (int) towers["BananaFarm"]["max"]!);
        Assert.Equal(0, (int) towers["DartMonkey"]["max"]!);
        Assert.Equal(0, (int) towers["Quincy"]["max"]!);
        Assert.Equal(6, towers.Values.Count(t => (int) t["max"]! != 0));

        var elite = Map(lych, true);
        Assert.True(elite.Options.Elite);
        Assert.Equal(3000, (int) elite.Challenge["startRules"]!["cash"]!);
        Assert.Equal($"{lych.DisplayName} Elite, 17 Jan 2025", (string?) elite.Challenge["name"]);
    }

    [Theory]
    [InlineData("Bloonarius55_m41kgdwk", ChimpsRounds.Earlier)]
    [InlineData("Diamondback8_mvaa404d", ChimpsRounds.Newest)]
    public void CHIMPS_events_play_the_CHIMPS_event_rounds_the_mod_carries(string id, ChimpsRounds rounds)
    {
        var setup = Map(Event(id), false);
        Assert.Equal("Clicks", (string?) setup.Challenge["mode"]);
        Assert.True(setup.Options.BossEventRounds);
        Assert.Equal(rounds, setup.Options.ChimpsRounds);
        Assert.Empty(setup.Unsupported);
    }

    [Fact]
    public void The_first_Bloonarius_events_round_set_is_the_bosss_own_under_its_old_name()
    {
        var setup = Map(Event("krmwspoc"), false);
        Assert.True(setup.Options.BossEventRounds);
        Assert.Empty(setup.Unsupported);
    }

    [Theory]
    [InlineData("Vortex10_l7si69ji", "round set default1")]
    [InlineData("Bloonarius52ABR_lyhw36tt", "round set alternateRound140")]
    public void Round_sets_the_Boss_Creator_cant_play_are_listed(string id, string unsupported)
    {
        var setup = Map(Event(id), false);
        Assert.Contains(unsupported, setup.Unsupported);
    }

    private static PastEvent WithRules(Action<JObject> change)
    {
        var lych = Event("Lych47_m5zq59de");
        var rules = (JObject) lych.Standard!.DeepClone();
        change(rules);
        return lych with {Standard = rules};
    }

    [Fact]
    public void Rules_the_Boss_Creator_cant_play_are_listed_not_half_applied()
    {
        var setup = Map(WithRules(rules =>
        {
            rules["disableInstas"] = true;
            rules["_powers"] = new JArray(new JObject {["power"] = "MonkeyBoost", ["max"] = 1});
            rules["someNewRule"] = 5;
            rules["mode"] = "Deflation";
            rules["_towers"] = new JArray(new JObject {["tower"] = "FutureMonkey", ["max"] = 2});
        }), false);

        Assert.Equal(PastEventProblem.None, setup.Problem);
        Assert.Contains("someNewRule", setup.Unsupported);
        Assert.Contains("mode Deflation", setup.Unsupported);
        Assert.Contains("tower FutureMonkey", setup.Unsupported);
        Assert.Equal("Standard", (string?) setup.Challenge["mode"]);
        Assert.DoesNotContain("disableInstas", setup.Challenge.Properties().Select(p => p.Name));
    }

    private static PastEvent WithInstasAndPowers(JArray powers) => WithRules(rules =>
    {
        rules["disableInstas"] = true;
        rules["_powers"] = powers;
    });

    [Fact]
    public void Insta_Monkeys_off_and_the_events_powers_are_set_on_the_model_not_listed()
    {
        var setup = Map(WithInstasAndPowers(new JArray(
            new JObject {["power"] = "MonkeyBoost", ["max"] = 1},
            new JObject {["power"] = "CashDrop", ["max"] = -1},
            new JObject {["power"] = "Thrive", ["max"] = 5000})), false);

        Assert.Empty(setup.Unsupported);
        var eventOnly = Assert.IsType<JObject>(setup.EventOnly);
        Assert.True((bool) eventOnly["disableInstas"]!);
        Assert.True((bool) eventOnly["displayIncludedPowers"]!);

        // Every power of the game, like the towers: one the event doesn't list isn't allowed
        var powers = ((JArray) eventOnly["powers"]!).Cast<JObject>().ToDictionary(p => (string) p["power"]!, p => (int) p["max"]!);
        var all = ((JArray) Defaults["powers"]!).Select(p => (string) p["power"]!).ToList();
        Assert.Equal(all, powers.Keys);
        Assert.Equal(1, powers["MonkeyBoost"]);
        Assert.Equal(-1, powers["CashDrop"]);
        Assert.Equal(PastEventRules.MaxPowerCount, powers["Thrive"]);
        Assert.Equal(0, powers["SuperMonkeyStorm"]);

        // Neither is a share code's: the challenge doesn't have them
        Assert.DoesNotContain("disableInstas", setup.Challenge.Properties().Select(p => p.Name));
        Assert.DoesNotContain("powers", setup.Challenge.Properties().Select(p => p.Name));
    }

    [Fact]
    public void A_power_the_game_doesnt_have_is_listed()
    {
        var setup = Map(WithInstasAndPowers(new JArray(
            new JObject {["power"] = "MonkeyBoost", ["max"] = 2},
            new JObject {["power"] = "FuturePower", ["max"] = 1},
            new JObject {["power"] = "GonePower", ["max"] = 0})), false);

        Assert.Equal(["power FuturePower"], setup.Unsupported);
        Assert.Contains((JArray) setup.EventOnly!["powers"]!, p => (string?) p["power"] == "MonkeyBoost" && (int) p["max"]! == 2);
    }

    [Fact]
    public void Only_powers_the_game_doesnt_have_keep_its_own_list()
    {
        var setup = Map(WithRules(rules => rules["_powers"] = new JArray(new JObject {["power"] = "FuturePower", ["max"] = 1})), false);
        Assert.Equal(["power FuturePower"], setup.Unsupported);
        Assert.Null(setup.EventOnly);
    }

    [Fact]
    public void The_archives_events_set_nothing_on_the_model()
    {
        foreach (var pastEvent in PastEventsFormat.Read(Bundle()).Value!.Events)
        {
            foreach (var elite in new[] {false, true})
            {
                if (pastEvent.Rules(elite) == null) continue;
                Assert.Null(Map(pastEvent, elite).EventOnly);
            }
        }
    }

    [Fact]
    public void Without_the_model_the_rules_are_listed_like_on_the_website()
    {
        var setup = PastEventRules.Map(WithInstasAndPowers(new JArray(new JObject {["power"] = "MonkeyBoost", ["max"] = 1})),
            false, Defaults, Maps.Contains, UnsupportedModes, eventOnly: false);
        Assert.Equal(["_powers", "disableInstas"], setup.Unsupported.Order());
        Assert.Null(setup.EventOnly);
    }

    [Fact]
    public void Dates_are_written_the_way_the_language_writes_them()
    {
        var date = new DateTime(2026, 10, 17);
        Assert.Equal("17 Oct 2026", PastEventDates.English(date));
        Assert.Equal("17. okt. 2026",
            PastEventDates.Format(date, "{0}. {1} {2}", "jan.,feb.,mar.,apr.,mai,jun.,jul.,aug.,sep.,okt.,nov.,des."));
        Assert.Equal("2026年10月17日",
            PastEventDates.Format(date, "{2}年{1}{0}日", string.Join(",", Enumerable.Range(1, 12).Select(m => $"{m}月"))));

        // A translation that can't be used gives the English date
        Assert.Equal("17 Oct 2026", PastEventDates.Format(date, "{0} {1} {2}", "only,three,months"));
        Assert.Equal("17 Oct 2026", PastEventDates.Format(date, "{0} {5}", PastEventDates.EnglishMonths));
    }

    [Fact]
    public void Every_language_has_a_usable_date()
    {
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Localization"), "*.json"))
        {
            var texts = JObject.Parse(File.ReadAllText(file));
            var months = (string?) texts["CustomBossChallenge-DateMonths"];
            var format = (string?) texts["CustomBossChallenge-DateFormat"];
            Assert.True(months != null && format != null, file);
            Assert.Equal(12, months!.Split(',').Count(month => month.Trim().Length > 0));
            var october = months.Split(',')[9].Trim();
            var written = PastEventDates.Format(new DateTime(2026, 10, 17), format!, months);
            Assert.True(written.Contains("17") && written.Contains("2026") && written.Contains(october), $"{file}: {written}");
        }
    }

    [Fact]
    public void Setup_names_have_the_date_in_the_players_language()
    {
        var lych = Event("Lych47_m5zq59de");
        string Norwegian(DateTime date) =>
            PastEventDates.Format(date, "{0}. {1} {2}", "jan.,feb.,mar.,apr.,mai,jun.,jul.,aug.,sep.,okt.,nov.,des.");
        Assert.Equal("Blastapopoulos 20, 17. jan. 2025", PastEventRules.SetupName(lych with {DisplayName = "Blastapopoulos 20"}, false, Norwegian));
        Assert.Equal("Lych 47 Elite, 17 Jan 2025", PastEventRules.SetupName(lych with {DisplayName = "Lych 47"}, true));

        // An Elite or a date the name field would change is English (#218)
        Assert.Equal("Lych 47 Elite, 17. jan. 2025", PastEventRules.SetupName(lych with {DisplayName = "Lych 47"}, true, Norwegian, "Élite"));
        string German(DateTime date) =>
            PastEventDates.Format(date, "{0}. {1} {2}", "Jan.,Feb.,März,Apr.,Mai,Juni,Juli,Aug.,Sep.,Okt.,Nov.,Dez.");
        Assert.Equal("Lych 47, 17 Mar 2025", PastEventRules.SetupName(lych with {DisplayName = "Lych 47", Start = new DateTimeOffset(2025, 3, 17, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()}, false, German));
        Assert.Equal("Lych 47, 17. Jan. 2025", PastEventRules.SetupName(lych with {DisplayName = "Lych 47"}, false, German));
    }

    [Fact]
    public void Setup_names_fit_the_name_field_in_every_language()
    {
        var events = PastEventsFormat.Read(Bundle()).Value!.Events;
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Localization"), "*.json"))
        {
            var texts = JObject.Parse(File.ReadAllText(file));
            var months = (string) texts["CustomBossChallenge-DateMonths"]!;
            var format = (string) texts["CustomBossChallenge-DateFormat"]!;
            var eliteName = (string) texts["CustomBossChallenge-Elite"]!;
            foreach (var pastEvent in events)
            {
                var name = PastEventRules.SetupName(pastEvent, true, date => PastEventDates.Format(date, format, months), eliteName);
                Assert.True(NameField.Fits(name), $"{Path.GetFileName(file)}: {name}");
            }
        }
    }

    [Fact]
    public void Setup_names_only_have_what_the_Challenge_Editors_name_field_keeps()
    {
        var lych = Event("Lych47_m5zq59de");
        Assert.Equal("Vortex 17 EX Elite, 17 Jan 2025", PastEventRules.SetupName(lych with {DisplayName = "Vortex 17 EX"}, true));
        Assert.Equal("Got you, 17 Jan 2025", PastEventRules.SetupName(lych with {DisplayName = "Got_you!"}, false));
        foreach (var pastEvent in PastEventsFormat.Read(Bundle()).Value!.Events)
        {
            var name = PastEventRules.SetupName(pastEvent, true);
            Assert.True(NameField.Fits(name), name);
            Assert.Single(name, ',');
        }
    }

    [Fact]
    public void Rules_that_change_nothing_are_not_listed()
    {
        var setup = Map(WithRules(rules =>
        {
            rules["disableInstas"] = false;
            rules["_powers"] = new JArray();
        }), false);
        Assert.Empty(setup.Unsupported);
    }

    [Fact]
    public void A_missing_map_unknown_boss_or_event_without_rules_cant_be_opened()
    {
        var missingMap = Map(WithRules(rules => rules["map"] = "RemovedMap"), false);
        Assert.Equal(PastEventProblem.MissingMap, missingMap.Problem);
        Assert.Equal("RemovedMap", missingMap.Map);

        var lych = Event("Lych47_m5zq59de");
        Assert.Equal(PastEventProblem.UnknownBoss, Map(lych with {BossKey = "newboss"}, false).Problem);
        Assert.Equal(PastEventProblem.NoRules, Map(lych with {Standard = null}, false).Problem);
        Assert.False((lych with {Standard = null, Elite = null}).HasRules);
    }

    [Fact]
    public void A_file_from_a_newer_format_is_too_new_and_so_is_an_event_in_it()
    {
        Assert.Equal(FormatProblem.TooNew, PastEventsFormat.Read(Bundle(formatVersion: 2)).Problem);
        Assert.Equal(FormatProblem.Invalid, PastEventsFormat.Read("not json").Problem);
        Assert.Equal(FormatProblem.Invalid, PastEventsFormat.Read("{\"formatVersion\": 1}").Problem);

        var bundle = JObject.Parse(Bundle(only: e => (string?) e["id"] == "Lych47_m5zq59de"));
        var newer = (JObject) bundle["events"]![0]!.DeepClone();
        newer["formatVersion"] = 2;
        newer["id"] = "Future";
        ((JArray) bundle["events"]!).Add(newer);
        var read = PastEventsFormat.Read(bundle.ToString());
        Assert.Single(read.Value!.Events);
        Assert.Contains(read.Changes, change => change.Contains("Future"));
    }

    [Fact]
    public void A_huge_format_version_is_too_new_instead_of_throwing()
    {
        var bundle = JObject.Parse(Bundle(only: e => (string?) e["id"] == "Lych47_m5zq59de"));
        var newer = (JObject) bundle["events"]![0]!.DeepClone();
        newer["formatVersion"] = JToken.Parse("99999999999999999999999");
        newer["id"] = "Huge";
        ((JArray) bundle["events"]!).Add(newer);
        var read = PastEventsFormat.Read(bundle.ToString());
        Assert.Single(read.Value!.Events);
        Assert.Contains(read.Changes, change => change.Contains("Huge"));

        foreach (var huge in new[] {"99999999999999999999999", "4294967296"})
        {
            bundle["formatVersion"] = JToken.Parse(huge);
            Assert.Equal(FormatProblem.TooNew, PastEventsFormat.Read(bundle.ToString()).Problem);
        }
    }

    [Fact]
    public void A_cache_that_cant_be_read_sends_no_ETag_so_it_is_replaced()
    {
        var store = Store(Bundle());
        Directory.CreateDirectory(Path.GetDirectoryName(store.CachePath)!);
        File.WriteAllText(store.CachePath, "broken");
        File.WriteAllText(store.ETagPath, "\"abc\"");
        store.Load();
        Assert.Null(store.ETag());

        Assert.True(store.Accept(Bundle("2026-11-15T00:00:00Z"), "\"def\""));
        Assert.Equal("\"def\"", store.ETag());
    }

    [Fact]
    public void The_file_that_knows_the_newest_event_is_used()
    {
        var all = PastEventsFormat.Read(Bundle("2026-11-01T00:00:00Z")).Value!;
        var older = PastEventsFormat.Read(Bundle("2026-11-20T00:00:00Z",
            only: e => (long) e["start"]! < all.NewestStart)).Value!;
        Assert.Same(all, PastEventsFormat.Newest(older, all));
        Assert.Same(all, PastEventsFormat.Newest(null, all));

        var sameButLater = PastEventsFormat.Read(Bundle("2026-11-05T00:00:00Z")).Value!;
        Assert.Same(sameButLater, PastEventsFormat.Newest(all, sameButLater));
        Assert.Null(PastEventsFormat.Newest(null, null));
    }

    private PastEventsStore Store(string? builtIn, List<string>? warnings = null) =>
        new(Path.Combine(folder, "boss-events.json"), () => builtIn == null ? null : Gzip(builtIn),
            message => warnings?.Add(message));

    [Fact]
    public void Without_a_cache_the_built_in_copy_is_shown()
    {
        var store = Store(Bundle());
        store.Load();
        Assert.Equal(PastEventsSource.BuiltIn, store.Source);
        Assert.NotEmpty(store.Current!.Events);
        Assert.Null(store.ETag());
    }

    [Fact]
    public void A_download_is_cached_with_its_ETag_and_used_on_the_next_start()
    {
        var builtIn = Bundle("2026-11-01T00:00:00Z", only: e => (string?) e["bossType"] != "diamondback");
        var store = Store(builtIn);
        store.Load();
        var before = store.Current!.Events.Count;

        Assert.True(store.Accept(Bundle("2026-11-15T00:00:00Z"), "\"abc\""));
        Assert.Equal(PastEventsSource.Download, store.Source);
        Assert.True(store.Current!.Events.Count > before);
        Assert.Equal("\"abc\"", store.ETag());

        var next = Store(builtIn);
        next.Load();
        Assert.Equal(PastEventsSource.Cache, next.Source);
        Assert.Equal(store.Current.Events.Count, next.Current!.Events.Count);
    }

    [Fact]
    public void A_cache_older_than_the_built_in_copy_doesnt_hide_its_events()
    {
        var store = Store(Bundle("2026-11-15T00:00:00Z"));
        store.Load();
        Assert.True(store.Accept(Bundle("2026-11-01T00:00:00Z", only: e => (string?) e["bossType"] != "diamondback"), null));
        Assert.Equal(PastEventsSource.BuiltIn, store.Source);

        var next = Store(Bundle("2026-11-15T00:00:00Z"));
        next.Load();
        Assert.Equal(PastEventsSource.BuiltIn, next.Source);
    }

    [Fact]
    public void A_broken_cache_gives_the_built_in_copy()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "boss-events.json"), "{ broken");
        var warnings = new List<string>();
        var store = Store(Bundle(), warnings);
        store.Load();
        Assert.Equal(PastEventsSource.BuiltIn, store.Source);
        Assert.Contains(warnings, warning => warning.Contains("cached"));
    }

    [Fact]
    public void A_cache_from_a_newer_version_is_kept_and_not_overwritten()
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "boss-events.json");
        var newer = Bundle(formatVersion: 2);
        File.WriteAllText(path, newer);

        var store = Store(Bundle());
        store.Load();
        Assert.Equal(PastEventsSource.BuiltIn, store.Source);
        Assert.True(store.Accept(Bundle("2026-12-01T00:00:00Z"), "\"x\""));
        Assert.Equal(newer, File.ReadAllText(path));
    }

    [Fact]
    public void A_download_that_cant_be_read_changes_nothing()
    {
        var store = Store(Bundle());
        store.Load();
        var shown = store.Current;
        Assert.False(store.Accept("<!DOCTYPE html>", "\"y\""));
        Assert.False(store.Accept(Bundle(formatVersion: 2), "\"z\""));
        Assert.Same(shown, store.Current);
        Assert.False(File.Exists(store.CachePath));
    }

    [Fact]
    public void Without_a_built_in_copy_or_cache_there_are_no_events()
    {
        var warnings = new List<string>();
        var store = Store(null, warnings);
        store.Load();
        Assert.Null(store.Current);
        Assert.Equal(PastEventsSource.None, store.Source);
        Assert.NotEmpty(warnings);
    }

    private static string Shown(string id) => id switch
    {
        "Tutorial" => "Monkey Meadow",
        "Clicks" => "CHIMPS",
        _ => id
    };

    [Fact]
    public void The_search_finds_events_by_name_boss_year_map_and_mode()
    {
        var events = PastEventsFormat.Read(Bundle()).Value!.Events;
        IReadOnlyList<PastEvent> Find(PastEventQuery query) =>
            events.Where(e => PastEventFilter.Matches(e, query, Shown)).ToList();

        Assert.Equal(events.Count, Find(new PastEventQuery()).Count);
        Assert.Contains(Find(new PastEventQuery("lych 47")), e => e.Id == "Lych47_m5zq59de");
        Assert.Contains(Find(new PastEventQuery("TINKERTON 2025")), e => e.Id == "Lych47_m5zq59de");
        Assert.All(Find(new PastEventQuery(Boss: "vortex")), e => Assert.Equal("vortex", e.BossKey));
        Assert.All(Find(new PastEventQuery(Year: 2024)), e => Assert.Equal(2024, e.StartDate.Year));
        Assert.All(Find(new PastEventQuery(Mode: "Clicks")), e => Assert.Contains("Clicks", PastEventFilter.Modes(e)));
        Assert.Equal(Find(new PastEventQuery(Mode: "Clicks")).Count, Find(new PastEventQuery("chimps")).Count);
        Assert.Empty(Find(new PastEventQuery("lych", Boss: "vortex")));
        Assert.Empty(Find(new PastEventQuery("no such event")));
    }

    [Fact]
    public void The_filters_offer_the_years_and_modes_the_events_have()
    {
        var events = PastEventsFormat.Read(Bundle()).Value!.Events;
        var years = PastEventFilter.Years(events);
        Assert.Equal(years.OrderByDescending(year => year), years);
        Assert.Contains(2021, years);
        var modes = PastEventFilter.AllModes(events);
        Assert.Equal("Standard", modes[0]);
        Assert.Contains("Clicks", modes);

        var newestFirst = PastEventsFormat.NewestFirst(events);
        Assert.Equal(events.Max(e => e.Start), newestFirst[0].Start);
    }
}
