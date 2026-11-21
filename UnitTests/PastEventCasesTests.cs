using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The Past Events cases the website shares (#213): web/test/past-event-cases.json holds events and the setup the mod
/// makes of each, normal or Elite, which web/test/past-events.test.ts checks the website's port against. With the
/// environment variable CBC_PAST_EVENT_CASES=1 this writes the file; otherwise it checks the committed one is up to
/// date.
/// </summary>
public class PastEventCasesTests
{
    private static readonly string Root = FindRoot();
    private static readonly string CasesFile = Path.Combine(Root, "web", "test", "past-event-cases.json");

    private static readonly JObject Game = JObject.Parse(File.ReadAllText(Path.Combine(Root, "web", "src", "data", "game.json")));
    private static readonly JObject Defaults = (JObject) Game["defaultChallenge"]!;
    private static readonly HashSet<string> Maps = ((JObject) Game["maps"]!).Properties().Select(p => p.Name).ToHashSet();
    private static readonly string[] UnsupportedModes = ((JArray) Game["unsupportedModes"]!).Select(m => (string) m!).ToArray();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data", "boss-events"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("data/boss-events not found above " + AppContext.BaseDirectory);
    }

    private static JObject Archived(string id) =>
        JObject.Parse(File.ReadAllText(Path.Combine(Root, "data", "boss-events", id + ".json")));

    private static JObject Changed(string id, string newId, Action<JObject> change)
    {
        var e = Archived(id);
        e["id"] = newId;
        change(e);
        return e;
    }

    /// <summary>The events of the cases: real ones that show each part of the mapping, and made-up ones for the rest</summary>
    private static IEnumerable<(string Name, JObject Event)> Events()
    {
        yield return ("Lych 47, towers, paragons and modifiers", Archived("Lych47_m5zq59de"));
        yield return ("CHIMPS with the earlier CHIMPS rounds", Archived("Bloonarius55_m41kgdwk"));
        yield return ("CHIMPS with the newest CHIMPS rounds", Archived("Diamondback8_mvaa404d"));
        yield return ("the old name of the boss's own rounds", Archived("krmwspoc"));
        yield return ("a round set the Boss Creator can't play", Archived("Vortex10_l7si69ji"));
        yield return ("Alternate Bloons Rounds", Archived("Bloonarius52ABR_lyhw36tt"));
        yield return ("blocked tiers and a start round", Archived("Blastapopoulos20_mv08g41v"));
        yield return ("rules the Boss Creator can't play", Changed("Lych47_m5zq59de", "MadeUpUnsupported", e =>
        {
            foreach (var level in new[] {"standard", "elite"})
            {
                var rules = (JObject) e[level]!;
                rules["disableInstas"] = true;
                rules["_powers"] = new JArray(new JObject {["power"] = "MonkeyBoost", ["max"] = 1});
                rules["someNewRule"] = 5;
                rules["mode"] = "Deflation";
                ((JArray) rules["_towers"]!).Add(new JObject {["tower"] = "FutureMonkey", ["max"] = 2});
            }
        }));
        yield return ("rules that change nothing", Changed("Lych47_m5zq59de", "MadeUpDefaults", e =>
        {
            foreach (var level in new[] {"standard", "elite"})
            {
                var rules = (JObject) e[level]!;
                rules["disableInstas"] = false;
                rules["_powers"] = new JArray();
            }
        }));
        yield return ("no rules", Changed("Lych47_m5zq59de", "MadeUpNoRules", e => e.Remove("standard")));
        yield return ("a boss this version doesn't know", Changed("Lych47_m5zq59de", "MadeUpBoss", e => e["bossType"] = "newboss"));
        yield return ("a map this version doesn't know", Changed("Lych47_m5zq59de", "MadeUpMap", e => e["standard"]!["map"] = "RemovedMap"));
        yield return ("a name with odd characters", Changed("Lych47_m5zq59de", "MadeUpName", e =>
        {
            e["name"] = "Got_you!";
            e["displayName"] = "Got you!";
        }));
    }

    private static PastEvent Read(JObject e)
    {
        var bundle = new JObject {["formatVersion"] = 1, ["generated"] = "2026-11-16T00:00:00.000Z", ["events"] = new JArray(e)};
        return Assert.Single(PastEventsFormat.Read(bundle.ToString()).Value!.Events);
    }

    /// <summary>The setup the mod makes of each case, as the website reads it</summary>
    private static JObject Expected()
    {
        var cases = new JArray();
        foreach (var (name, e) in Events())
        {
            var pastEvent = Read(e);
            foreach (var elite in new[] {false, true})
            {
                // The website only makes codes, which can't hold the rules the mod sets on the model (#212)
                var setup = PastEventRules.Map(pastEvent, elite, Defaults, Maps.Contains, UnsupportedModes, eventOnly: false);
                var dropped = new List<string>();
                var challenge = setup.Problem == PastEventProblem.None
                    ? CodeFormat.FilterChallenge(setup.Challenge, (JObject) Defaults.DeepClone(), dropped)
                    : new JObject();
                cases.Add(new JObject
                {
                    ["name"] = $"{name}{(elite ? " (Elite)" : "")}",
                    ["id"] = pastEvent.Id,
                    ["elite"] = elite,
                    ["problem"] = setup.Problem.ToString(),
                    ["options"] = new JObject
                    {
                        ["Boss"] = setup.Options.Boss.ToString(), ["Elite"] = setup.Options.Elite,
                        ["BossEventRounds"] = setup.Options.BossEventRounds,
                        ["ChimpsRounds"] = setup.Options.ChimpsRounds.ToString()
                    },
                    ["challenge"] = challenge,
                    ["unsupported"] = new JArray(setup.Unsupported.Concat(dropped).Cast<object>().ToArray()),
                    ["setupName"] = PastEventRules.SetupName(pastEvent, elite)
                });
            }
        }

        return new JObject
        {
            ["about"] = "Past events and the setup the mod makes of each (#146, #213). Made by UnitTests/PastEventCasesTests.cs " +
                        "(CBC_PAST_EVENT_CASES=1 dotnet test --project UnitTests), read by web/test/past-events.test.ts.",
            ["events"] = new JArray(Events().Select(entry => (JToken) entry.Event).ToArray()),
            ["cases"] = cases
        };
    }

    [Fact]
    public void The_cases_the_website_checks_are_up_to_date()
    {
        var expected = Expected();
        if (Environment.GetEnvironmentVariable("CBC_PAST_EVENT_CASES") == "1")
        {
            File.WriteAllText(CasesFile, expected.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
            return;
        }

        Assert.True(File.Exists(CasesFile), "web/test/past-event-cases.json is missing: run CBC_PAST_EVENT_CASES=1 dotnet test --project UnitTests");
        var committed = JObject.Parse(File.ReadAllText(CasesFile));
        Assert.True(JToken.DeepEquals(expected, committed),
            "web/test/past-event-cases.json is out of date: run CBC_PAST_EVENT_CASES=1 dotnet test --project UnitTests");
    }
}
