using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// Why a past event can't be opened at all
/// </summary>
public enum PastEventProblem
{
    None,

    /// <summary>The archive has no rules for it (some events of early 2026)</summary>
    NoRules,

    /// <summary>A boss this version of the mod doesn't know</summary>
    UnknownBoss,

    /// <summary>A map that isn't in this version of the game, or was renamed</summary>
    MissingMap
}

/// <summary>
/// A past event as a Boss Creator setup: the boss options, and the challenge as a share code's challenge diff, which
/// <see cref="SetupCode"/> turns into the editor's model the way it reads codes.
/// </summary>
/// <param name="Unsupported">The event's rules the Boss Creator can't play, which are left out, for the player</param>
/// <param name="Map">The event's map, also when it is missing</param>
/// <param name="EventOnly">
/// The event's rules that the game's model has but neither the Challenge Editor nor a share code can set (#212):
/// fields of the challenge's JSON, like "disableInstas" and "powers", set on the model when the event is opened. They
/// last as long as the model does (the last setup, Undo, Continue), but Copy and Save leave them out.
/// </param>
public sealed record PastEventSetup(
    PastEventProblem Problem,
    BossOptions Options,
    JObject Challenge,
    IReadOnlyList<string> Unsupported,
    string? Map,
    JObject? EventOnly = null);

/// <summary>
/// Maps the rules of an official boss event (#146), in the API's metadata shape (data/boss-events/README.md), onto
/// the Boss Creator: the boss and its rounds onto <see cref="BossOptions"/>, the rest onto the challenge fields a
/// share code may set (<see cref="CodeFormat.ChallengeFields"/>). Rules it can't play are listed, not half applied.
/// Free of the game's types, so the unit tests check every event in the archive.
/// </summary>
public static class PastEventRules
{
    /// <summary>The API's fields that go to the challenge under the same name</summary>
    private static readonly string[] SameName =
    [
        "map", "mode", "difficulty", "maxTowers", "maxParagons", "leastCashUsed", "leastTiersUsed", "seed",
        "removeableCostMultiplier", "abilityCooldownReductionMultiplier", "disableMK", "disableSelling",
        "disablePowers", "noContinues", "disableDoubleCash", "disableFastTrack", "displayIncludedTowers"
    ];

    /// <summary>The API's fields that go to the challenge's start rules</summary>
    private static readonly Dictionary<string, string> StartRules = new()
    {
        ["startingCash"] = "cash", ["lives"] = "lives", ["maxLives"] = "maxLives", ["startRound"] = "round",
        ["endRound"] = "endRound"
    };

    /// <summary>
    /// What the API says about the event rather than its rules (names, play counts, links), and what the mapping
    /// reads on its own (round sets, bloon modifiers, towers)
    /// </summary>
    private static readonly HashSet<string> NotRules =
    [
        "name", "id", "creator", "createdAt", "gameVersion", "mapURL", "plays", "wins", "restarts", "losses", "upvotes",
        "playsUnique", "winsUnique", "lossesUnique", "roundSets", "_bloonModifiers", "_towers"
    ];

    /// <summary>
    /// The API's fields that go to <see cref="PastEventSetup.EventOnly"/>: the model has them, a share code doesn't
    /// </summary>
    private static readonly HashSet<string> EventOnlyRules = ["disableInstas", "_powers"];

    /// <summary>The most uses of one power an event can allow, like the most towers of one kind</summary>
    public const int MaxPowerCount = 1000;

    /// <summary>
    /// The round set the first Bloonarius events played (2021), the boss's own event rounds under their old name
    /// </summary>
    public const string OldBossRoundSet = "bossRounds";

    /// <summary>
    /// The setup for the normal or the Elite event. <paramref name="defaults"/> is the default challenge as a share
    /// code sees it (<c>SetupCode.ChallengeJson</c>), whose tower list says which towers the game has;
    /// <paramref name="mapExists"/> says whether the game has a map, and <paramref name="unsupportedModes"/> are the
    /// modes the Boss Creator can't play (<c>BossEditorSection.UnsupportedModes</c>). <paramref name="date"/> and
    /// <paramref name="eliteName"/> write the setup's name in the player's language (<see cref="SetupName"/>). With
    /// <paramref name="eventOnly"/> the rules a share code can't hold but the model can go to
    /// <see cref="PastEventSetup.EventOnly"/> (#212); without it they are listed as unsupported, like on the website,
    /// which only makes codes.
    /// </summary>
    public static PastEventSetup Map(PastEvent pastEvent, bool elite, JObject defaults, Func<string, bool> mapExists,
        IReadOnlyCollection<string> unsupportedModes, Func<DateTime, string>? date = null, string eliteName = "Elite",
        bool eventOnly = true)
    {
        var rules = pastEvent.Rules(elite);
        var options = new BossOptions {Elite = elite};
        var challenge = new JObject();
        var unsupported = new List<string>();
        if (rules == null) return new PastEventSetup(PastEventProblem.NoRules, options, challenge, unsupported, null);

        var boss = Boss(pastEvent.BossKey);
        if (boss == null) return new PastEventSetup(PastEventProblem.UnknownBoss, options, challenge, unsupported, null);
        options = options with {Boss = boss.Value};

        var map = rules["map"] is {Type: JTokenType.String} mapToken ? (string?) mapToken : null;
        if (map == null || !mapExists(map))
        {
            return new PastEventSetup(PastEventProblem.MissingMap, options, challenge, unsupported, map);
        }

        challenge["name"] = SetupName(pastEvent, elite, date, eliteName);
        foreach (var field in SameName)
        {
            if (IsSet(rules, field)) challenge[field] = rules[field]!.DeepClone();
        }

        var start = new JObject();
        foreach (var (from, to) in StartRules)
        {
            if (IsSet(rules, from)) start[to] = rules[from]!.DeepClone();
        }
        if (start.HasValues) challenge["startRules"] = start;

        if (rules["_bloonModifiers"] is JObject modifiers) challenge["bloonModifiers"] = modifiers.DeepClone();
        challenge["towers"] = Towers(rules["_towers"] as JArray, defaults, unsupported);

        var mode = (string?) rules["mode"];
        if (mode != null && unsupportedModes.Contains(mode))
        {
            unsupported.Add($"mode {mode}");
            challenge["mode"] = "Standard";
            mode = "Standard";
        }

        options = Rounds(options, rules["roundSets"] as JArray, mode, unsupported);
        var modelOnly = eventOnly ? EventOnly(rules, defaults, unsupported) : new JObject();

        // Rules the API may add later, or that no player can set in the Challenge Editor: listed when they change
        // anything
        foreach (var (name, value) in rules)
        {
            if (value == null || NotRules.Contains(name) || SameName.Contains(name) || StartRules.ContainsKey(name) ||
                eventOnly && EventOnlyRules.Contains(name)) continue;
            if (!IsDefault(value)) unsupported.Add(name);
        }

        return new PastEventSetup(PastEventProblem.None, options, challenge, unsupported, map,
            modelOnly.HasValues ? modelOnly : null);
    }

    /// <summary>
    /// The setup's name: the event's name, a comma and its date, so a saved setup still says which event it was, like
    /// "Lych 47 Elite, 17 Jan 2025", or "Blastapopoulos 20, 10. okt. 2026" in Norwegian: <paramref name="date"/>
    /// writes the date in the player's language (<see cref="PastEventDates"/>, English without it).
    /// <br/>
    /// The Challenge Editor's name field only keeps ASCII letters, digits and some punctuation (<see cref="NameField"/>),
    /// so the name keeps letters and digits, and the date its dots. A translated Elite or date the field would change
    /// ("Élite", "17. März 2025") is written in English instead (#218), and so is a name that would be longer than the
    /// field's 40 characters; one still too long is cut there.
    /// </summary>
    public static string SetupName(PastEvent pastEvent, bool elite, Func<DateTime, string>? date = null,
        string eliteName = "Elite")
    {
        var start = pastEvent.StartDate;
        var english = Name(pastEvent, elite, "Elite", PastEventDates.English(start));
        if (date == null) return Fit(english);
        var translated = Name(pastEvent, elite, NameField.Translated(eliteName, "Elite"),
            NameField.Translated(date(start), PastEventDates.English(start)));
        return translated.Length <= NameField.MaxLength ? translated : Fit(english);
    }

    private static string Name(PastEvent pastEvent, bool elite, string eliteName, string date) =>
        $"{NameText(pastEvent.DisplayName + (elite ? " " + eliteName : ""), "")}, {NameText(date, ".")}";

    private static string Fit(string name) => name.Length <= NameField.MaxLength ? name : name[..NameField.MaxLength].TrimEnd();

    /// <summary>ASCII letters, digits and <paramref name="keep"/>, one space between the words</summary>
    private static string NameText(string text, string keep)
    {
        var name = new string(text.Select(c => NameField.IsAsciiLetterOrDigit(c) || keep.Contains(c) ? c : ' ').ToArray());
        return string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The boss of the API's lowercase name, or null for one this version doesn't know</summary>
    public static BossType? Boss(string key) =>
        Enum.GetValues<BossType>().Cast<BossType?>()
            .FirstOrDefault(boss => string.Equals(boss.ToString(), key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every tower of the game, as a whole list a share code can hold: a tower the event doesn't list wasn't allowed
    /// (max 0), also when the game added it after the event. A tower the game doesn't have is listed as unsupported.
    /// </summary>
    private static JArray Towers(JArray? towers, JObject defaults, List<string> unsupported)
    {
        var listed = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var token in towers ?? [])
        {
            if (token is JObject tower && tower["tower"] is {Type: JTokenType.String} name) listed[(string) name!] = tower;
        }

        var result = new JArray();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in defaults["towers"] as JArray ?? [])
        {
            if (token is not JObject standard || (string?) standard["tower"] is not { } name) continue;
            known.Add(name);
            var entry = new JObject {["tower"] = name, ["max"] = 0};
            if (listed.TryGetValue(name, out var tower))
            {
                foreach (var field in new[] {"max", "path1NumBlockedTiers", "path2NumBlockedTiers", "path3NumBlockedTiers"})
                {
                    if (tower[field] is {Type: JTokenType.Integer} value) entry[field] = value.DeepClone();
                }
            }
            result.Add(entry);
        }

        unsupported.AddRange(listed.Where(pair => !known.Contains(pair.Key) && (int?) pair.Value["max"] != 0)
            .Select(pair => $"tower {pair.Key}"));
        return result;
    }

    /// <summary>
    /// The rules the model has but a share code doesn't (#212): Insta Monkeys turned off, and the powers the event
    /// allows. An empty power list, as every event so far has, changes nothing: the game's own default allows them all.
    /// </summary>
    private static JObject EventOnly(JObject rules, JObject defaults, List<string> unsupported)
    {
        var result = new JObject();
        if (rules["disableInstas"] is {Type: JTokenType.Boolean} instas && (bool) instas) result["disableInstas"] = true;

        if (rules["_powers"] is JArray {HasValues: true} powers && Powers(powers, defaults, unsupported) is { } list)
        {
            result["powers"] = list;
            // The game shows the allowed powers before the match, like the towers with displayIncludedTowers
            result["displayIncludedPowers"] = true;
        }

        return result;
    }

    /// <summary>
    /// Every power of the game, as the model's whole list: a power the event doesn't list isn't allowed (max 0), like
    /// a tower. A power the game doesn't have is listed as unsupported. Null when the event lists no power the game
    /// has, so the default list stays.
    /// </summary>
    private static JArray? Powers(JArray powers, JObject defaults, List<string> unsupported)
    {
        var listed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in powers)
        {
            if (token is not JObject power || power["power"] is not {Type: JTokenType.String} name) continue;
            var max = power["max"] is {Type: JTokenType.Integer} value ? (long) value : -1;
            listed[(string) name!] = max < 0 ? -1 : (int) Math.Min(max, MaxPowerCount);
        }

        var result = new JArray();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in defaults["powers"] as JArray ?? [])
        {
            if (token is not JObject standard || (string?) standard["power"] is not { } name) continue;
            known.Add(name);
            result.Add(new JObject {["power"] = name, ["max"] = listed.TryGetValue(name, out var max) ? max : 0});
        }

        unsupported.AddRange(listed.Where(pair => !known.Contains(pair.Key) && pair.Value != 0)
            .Select(pair => $"power {pair.Key}"));
        return listed.Keys.Any(known.Contains) ? result : null;
    }

    /// <summary>
    /// The event's round sets as Boss Event Rounds: the boss's own (also under its old name), or in CHIMPS the CHIMPS
    /// events' sets the mod carries. Any other set is listed as unsupported, and the match plays the default rounds.
    /// </summary>
    private static BossOptions Rounds(BossOptions options, JArray? sets, string? mode, List<string> unsupported)
    {
        var own = BossOptions.BossEventRoundSet(options.Boss);
        foreach (var set in (sets ?? []).Select(token => (string?) token).Where(set => set is not (null or "default")))
        {
            if (set == own || set == OldBossRoundSet)
            {
                options = options with
                {
                    BossEventRounds = true,
                    ChimpsRounds = mode == BossOptions.ChimpsMode ? ChimpsRounds.Own : options.ChimpsRounds
                };
            }
            else if (set == BossOptions.ChimpsBossRoundSet && mode == BossOptions.ChimpsMode)
            {
                options = options with {BossEventRounds = true, ChimpsRounds = ChimpsRounds.Newest};
            }
            else if (set == BossOptions.ChimpsBloonariusRoundSet && mode == BossOptions.ChimpsMode)
            {
                options = options with {BossEventRounds = true, ChimpsRounds = ChimpsRounds.Earlier};
            }
            else unsupported.Add($"round set {set}");
        }

        return options;
    }

    /// <summary>
    /// The limits the API writes as 0 when it has no rules for them, like Diamondback 6's placeholder metadata
    /// (<c>"id": "n/a"</c>): no boss event has 0 towers or 0 max lives, so 0 means the game's default
    /// </summary>
    private static readonly HashSet<string> ZeroIsUnset = ["maxTowers", "maxLives"];

    /// <summary>Whether the event sets a rule: not null, and not 0 for a <see cref="ZeroIsUnset"/> limit</summary>
    private static bool IsSet(JObject rules, string field) =>
        rules[field] is { } value && value.Type != JTokenType.Null &&
        !(ZeroIsUnset.Contains(field) && value.Type == JTokenType.Integer && (long) value == 0);

    /// <summary>Whether a value the mapping doesn't know changes nothing: false, 0, -1, null, empty or 1 (a multiplier)</summary>
    private static bool IsDefault(JToken value) => value.Type switch
    {
        JTokenType.Null => true,
        JTokenType.Boolean => !(bool) value,
        JTokenType.Integer or JTokenType.Float => (double) value is 0 or -1 or 1,
        JTokenType.String => ((string?) value)?.Length == 0,
        JTokenType.Array => !value.HasValues,
        JTokenType.Object => !value.HasValues,
        _ => false
    };
}
