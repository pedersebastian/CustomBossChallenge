using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Why a setup code couldn't be read
/// </summary>
public enum CodeProblem
{
    None,

    /// <summary>Not a setup code, or a damaged one</summary>
    Invalid,

    /// <summary>A code from a newer version of the mod</summary>
    TooNew
}

/// <summary>
/// The text format of setup codes, without the game: a prefix, then the JSON of what differs from the defaults,
/// deflated and in Base64. <see cref="SetupCode"/> turns setups into that JSON and back. Kept free of the game's types,
/// so the property tests (PropertyTests/) can run it.
/// </summary>
public static class CodeFormat
{
    /// <summary>
    /// Codes start with "CBC", the format version and a colon. Version 1 codes (2.2) are read the same way; a code
    /// that uses an option 2.2 doesn't know is version 2, so 2.2 asks for an update instead of silently dropping it.
    /// The same goes for version 3, which has a different boss per tier (2.9), version 4, which has Boss Event
    /// Rounds, and version 5, which has the boss's own event rounds in CHIMPS (2.12): 2.11 reads every CHIMPS rounds
    /// value it doesn't know as a broken code.
    /// </summary>
    public const string Prefix = "CBC1:";
    public const string Version2Prefix = "CBC2:";
    public const string Version3Prefix = "CBC3:";
    public const string Version4Prefix = "CBC4:";
    public const string Version5Prefix = "CBC5:";

    /// <summary>
    /// The prefixes this version reads
    /// </summary>
    public static readonly string[] KnownPrefixes = [Prefix, Version2Prefix, Version3Prefix, Version4Prefix, Version5Prefix];
    public const string AnyVersionPrefix = "CBC";
    public const int PrefixLength = 5;

    /// <summary>
    /// The website that shows a code's setup. A link to a code is this, "#code=" and the code.
    /// </summary>
    public const string WebsiteUrl = "https://pedersebastian.github.io/CustomBossChallenge/";

    public const string LinkKey = "code=";

    /// <summary>
    /// A link that opens the code's setup on the website. The code goes after the #, so it never reaches the server.
    /// Codes only use letters, digits, - and _ after "CBCn:", so they go in as they are.
    /// </summary>
    public static string Link(string code) => WebsiteUrl + "#" + LinkKey + code;

    /// <summary>
    /// The code in pasted text: the text itself, or the part after # in a link to the website ("#code=CBC2:…" or
    /// "#CBC2:…", also %-encoded or wrapped in &lt;…&gt; as Discord does). Text without a code in a link comes back
    /// trimmed. Same rules as extractCode on the website (web/packages/share-code/src/decode.ts).
    /// </summary>
    public static string ExtractCode(string? text)
    {
        var trimmed = (text ?? "").Trim();
        if (trimmed.StartsWith('<') && trimmed.EndsWith('>')) trimmed = trimmed[1..^1].Trim();
        var hash = trimmed.LastIndexOf('#');
        if (hash < 0) return trimmed;

        var fragment = trimmed[(hash + 1)..];
        if (fragment.StartsWith(LinkKey, StringComparison.Ordinal)) fragment = fragment[LinkKey.Length..];
        // Ordinal: compared by culture, a code after a %00 would count as one (#113)
        fragment = Uri.UnescapeDataString(fragment).Trim();
        return fragment.StartsWith(AnyVersionPrefix, StringComparison.Ordinal) ? fragment : trimmed;
    }

    /// <summary>
    /// Longer codes are refused without being read
    /// </summary>
    public const int MaxLength = 20000;

    /// <summary>
    /// Longest text a code may unpack to, so a small code can't blow up into a huge one
    /// </summary>
    public const int MaxJsonLength = 1_000_000;

    /// <summary>
    /// Marks a list diff: the changed items by index, for lists as long as the default one
    /// </summary>
    public const string ItemsKey = "$items";

    /// <summary>
    /// What a code holds: the boss options ("o") and the challenge ("c"), each as what differs from its defaults
    /// </summary>
    public static JObject Payload(JObject options, JObject defaultOptions, JObject challenge, JObject defaultChallenge) =>
        new()
        {
            ["o"] = Diff(options, defaultOptions) ?? new JObject(),
            ["c"] = Diff(challenge, defaultChallenge) ?? new JObject()
        };

    /// <summary>
    /// Turns JSON into a code without any checks
    /// </summary>
    public static string Pack(string json, string prefix = Prefix) => prefix + ToBase64Url(Deflate(json));

    /// <summary>
    /// Reads a code into its <see cref="Payload"/>. Never throws: anything that isn't a code gives a
    /// <see cref="CodeProblem"/>, and <paramref name="error"/> says why a code that looked like one couldn't be read.
    /// </summary>
    public static bool TryUnpack(string? code, out JObject payload, out CodeProblem problem, out string? error)
    {
        payload = new JObject();
        problem = CodeProblem.Invalid;
        error = null;

        code = ExtractCode(code);
        if (code.Length == 0 || code.Length > MaxLength) return false;
        if (!Array.Exists(KnownPrefixes, prefix => code.StartsWith(prefix, StringComparison.Ordinal)))
        {
            if (code.StartsWith(AnyVersionPrefix, StringComparison.Ordinal) && code.Length > 4 && code[3] is >= '0' and <= '9')
            {
                problem = CodeProblem.TooNew;
            }
            return false;
        }

        try
        {
            var json = Inflate(FromBase64Url(code[PrefixLength..]));
            if (HasNonFiniteLiteral(json))
            {
                error = "NaN or Infinity is not a number a code may hold";
                return false;
            }

            payload = JObject.Parse(json);
            problem = CodeProblem.None;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    /// <summary>
    /// Whether the JSON has NaN, Infinity or -Infinity as a value. Json.NET reads them, but the website's JSON.parse
    /// can't, so a code with one is refused on both (#113). The mod never writes them. Outside of strings, valid JSON
    /// has no N or I, so finding one there is enough; inside strings they are text.
    /// </summary>
    internal static bool HasNonFiniteLiteral(string json)
    {
        var inString = false;
        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c is 'N' or 'I') return true;
        }

        return false;
    }

    /// <summary>
    /// What differs in <paramref name="value"/> from <paramref name="defaults"/>, or null if nothing does. Objects
    /// are compared property by property and lists item by item, so changing one tower keeps the code short.
    /// </summary>
    public static JToken? Diff(JToken value, JToken? defaults)
    {
        switch (value, defaults)
        {
            case (JObject obj, JObject standard):
            {
                var diff = new JObject();
                foreach (var (name, token) in obj)
                {
                    var inner = token == null ? null : Diff(token, standard[name]);
                    if (inner != null) diff[name] = inner;
                }

                return diff.HasValues ? diff : null;
            }
            case (JArray list, JArray standard) when list.Count == standard.Count:
            {
                var items = new JObject();
                for (var i = 0; i < list.Count; i++)
                {
                    var inner = Diff(list[i], standard[i]);
                    if (inner != null) items[i.ToString(CultureInfo.InvariantCulture)] = inner;
                }

                return items.HasValues ? new JObject {[ItemsKey] = items} : null;
            }
            default:
                return JToken.DeepEquals(value, defaults) ? null : value.DeepClone();
        }
    }

    /// <summary>
    /// What a code may set in the challenge: the settings of the game's Challenge Editor and the Boss Creator, each with
    /// the JSON type it must have ("text", "int", "float" or "bool"). A list has one item: the fields of its entries,
    /// with "$key" naming the field that tells the entries apart, which a code can't change. Everything else in a code
    /// is ignored (#108), so the fields the game adds later, and those no player can set, keep their defaults.
    /// <br/>
    /// When the game's Challenge Editor gets a new setting, add it here and run WebDataTest, which gives the website
    /// the same list.
    /// </summary>
    public static readonly JObject ChallengeFields = JObject.Parse("""
        {
          "name": "text", "map": "text", "difficulty": "text", "mode": "text",
          "startRules": {"cash": "int", "lives": "int", "maxLives": "int", "round": "int", "endRound": "int"},
          "bloonModifiers": {
            "speedMultiplier": "float", "moabSpeedMultiplier": "float", "bossSpeedMultiplier": "float",
            "regrowRateMultiplier": "float",
            "healthMultipliers": {"bloons": "float", "moabs": "float", "boss": "float"},
            "allCamo": "bool", "allRegen": "bool", "allFortified": "bool"
          },
          "towers": [{
            "$key": "tower", "max": "int", "path1NumBlockedTiers": "int", "path2NumBlockedTiers": "int",
            "path3NumBlockedTiers": "int", "restrictParagon": "bool"
          }],
          "maxTowers": "int", "maxParagons": "int", "leastCashUsed": "int", "leastTiersUsed": "int", "seed": "int",
          "removeableCostMultiplier": "float", "abilityCooldownReductionMultiplier": "float",
          "disableMK": "bool", "disableSelling": "bool", "disablePowers": "bool", "noContinues": "bool",
          "disableDoubleCash": "bool", "disableFastTrack": "bool", "noInstaReward": "bool",
          "displayIncludedTowers": "bool"
        }
        """);

    /// <summary>
    /// The part of a challenge <see cref="Diff"/> that <see cref="ChallengeFields"/> allows, with the right types. Every
    /// part left out is added to <paramref name="dropped"/> by its path, e.g. "cashModifiers" or "towers[3].tower",
    /// for the log. A whole list, as a code holds when the game's list has changed length since, becomes changes to the
    /// entries of <paramref name="defaults"/> with the same key; entries it doesn't have are left out.
    /// </summary>
    public static JObject FilterChallenge(JObject diff, JObject defaults, List<string> dropped) =>
        FilterObject(diff, ChallengeFields, defaults, "", dropped);

    private static JObject FilterObject(JObject diff, JObject fields, JToken? defaults, string path, List<string> dropped)
    {
        var kept = new JObject();
        foreach (var (name, value) in diff)
        {
            var at = path.Length == 0 ? name : $"{path}.{name}";
            var field = name.StartsWith('$') ? null : fields[name];
            var before = dropped.Count;
            var filtered = value == null || field == null ? null : Filter(value, field, defaults?[name], at, dropped);
            if (filtered != null) kept[name] = filtered;
            // Name what was left out, unless a part of it already says what; an empty object changes nothing
            else if (field == null || (dropped.Count == before && value is not JObject {HasValues: false})) dropped.Add(at);
        }

        return kept;
    }

    private static JToken? Filter(JToken value, JToken field, JToken? defaults, string path, List<string> dropped)
    {
        switch (field)
        {
            case JObject fields:
                if (value is not JObject obj) return null;
                var inner = FilterObject(obj, fields, defaults, path, dropped);
                return inner.HasValues ? inner : null;
            case JArray {Count: 1} list when list[0] is JObject entry:
                return FilterList(value, entry, defaults as JArray, path, dropped);
            default:
                return Typed(value, (string?) field);
        }
    }

    private static JToken? FilterList(JToken value, JObject entry, JArray? defaults, string path, List<string> dropped)
    {
        var key = (string?) entry["$key"];
        var items = new JObject();
        void Add(int index, JObject change, string at)
        {
            var inner = FilterObject(change, entry, defaults?[index], at, dropped);
            if (inner.HasValues) items[index.ToString(CultureInfo.InvariantCulture)] = inner;
        }

        if (value is JObject {Count: 1} diff && diff[ItemsKey] is JObject changes)
        {
            foreach (var (index, change) in changes)
            {
                var at = $"{path}[{index}]";
                if (change is JObject changed && int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var i) &&
                    i < (defaults?.Count ?? 0) && index == i.ToString(CultureInfo.InvariantCulture))
                {
                    Add(i, changed, at);
                }
                else dropped.Add(at);
            }
        }
        else if (value is JArray whole && key != null && defaults != null)
        {
            for (var n = 0; n < whole.Count; n++)
            {
                var at = $"{path}[{n}]";
                var id = whole[n] is JObject item && item[key] is {Type: JTokenType.String} token ? (string?) token : null;
                var i = id == null ? -1 : defaults.ToList().FindIndex(standard => (string?) standard[key] == id);
                if (i < 0)
                {
                    dropped.Add(at);
                    continue;
                }

                // The entry's key says which entry it is; only its settings are changes
                var change = (JObject) whole[n].DeepClone();
                change.Remove(key);
                foreach (var name in change.Properties().Select(property => property.Name).ToList())
                {
                    if (JToken.DeepEquals(change[name], defaults[i][name])) change.Remove(name);
                }
                Add(i, change, at);
            }
        }
        else return null;

        return items.HasValues ? new JObject {[ItemsKey] = items} : null;
    }

    /// <summary>
    /// The Boss Creator's options a code can hold, and the JSON type each must have. A "?" type may also be null.
    /// </summary>
    private static readonly Dictionary<string, string> OptionTypes = new()
    {
        ["Boss"] = "enum", ["Elite"] = "bool", ["SkullMode"] = "enum", ["SkullCount"] = "int", ["CustomSkulls"] = "text?",
        ["Tiers"] = "int", ["SpeedPerTier"] = "int", ["HealthPerTier"] = "int", ["ExtraTierHealth"] = "int?",
        ["SpawnRounds"] = "text?", ["TierBosses"] = "text?", ["RandomTiers"] = "text?", ["BossEventRounds"] = "bool",
        ["ChimpsRounds"] = "enum"
    };

    /// <summary>A whole number written as text, as Json.NET reads it into an int: only ASCII digits and spaces</summary>
    private static readonly System.Text.RegularExpressions.Regex IntText = new(@"^[ \t\r\n]*[+-]?[0-9]+[ \t\r\n]*$");

    /// <summary>An enum's name as text; a number in text, or names joined by commas, are not one</summary>
    private static readonly System.Text.RegularExpressions.Regex EnumText = new(@"^[ \t\r\n]*[A-Za-z]+[ \t\r\n]*$");

    /// <summary>
    /// The options of a code (its "o"), each with the JSON type it must have; a value of another type throws a
    /// <see cref="FormatException"/>, and the code can't be read (#113). The website reads codes the same way
    /// (web/packages/share-code/src/options.ts): Json.NET on its own turns 5.5 into 6, "1" into Lych and 3 into true, which JavaScript
    /// doesn't, so both only take what the mod itself writes. Whole numbers may be written as 5.0 or "5", since
    /// JavaScript can't tell 5.0 from 5. Options the mod doesn't have are left out.
    /// </summary>
    public static JObject TypedOptions(JObject diff)
    {
        var typed = new JObject();
        foreach (var (name, value) in diff)
        {
            if (value == null || !OptionTypes.TryGetValue(name, out var type)) continue;
            if (value.Type == JTokenType.Null)
            {
                if (!type.EndsWith('?')) throw new FormatException($"{name} is null");
                typed[name] = JValue.CreateNull();
                continue;
            }

            var number = Typed(value, "int");
            var text = value.Type == JTokenType.String ? (string?) value : null;
            typed[name] = type.TrimEnd('?') switch
            {
                "bool" when value.Type == JTokenType.Boolean => value.DeepClone(),
                "text" when text != null => value.DeepClone(),
                "int" when number != null => number,
                "int" when text != null && IntText.IsMatch(text) &&
                            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => new JValue(parsed),
                "enum" when number != null => number,
                "enum" when text != null && EnumText.IsMatch(text) => value.DeepClone(),
                _ => throw new FormatException($"{name} has the wrong type")
            };
        }

        return typed;
    }

    /// <summary>
    /// The value as the JSON type the field has, or null when it isn't one. Numbers are read the way JavaScript
    /// reads them, so the website keeps the same ones: 5.0 is a whole number, and 1e30 a float.
    /// </summary>
    private static JToken? Typed(JToken value, string? type)
    {
        double? number = value switch
        {
            JValue {Value: System.Numerics.BigInteger big} => (double) big,
            {Type: JTokenType.Integer or JTokenType.Float} => value.Value<double>(),
            _ => null
        };
        return type switch
        {
            "text" when value.Type == JTokenType.String => value.DeepClone(),
            "bool" when value.Type == JTokenType.Boolean => value.DeepClone(),
            "int" when number is >= int.MinValue and <= int.MaxValue && Math.Floor(number.Value) == number.Value =>
                new JValue((int) number.Value),
            "float" when number is { } finite && double.IsFinite(finite) => value.Type == JTokenType.Integer && IsInt(value)
                ? value.DeepClone()
                : new JValue(finite),
            _ => null
        };
    }

    /// <summary>A whole number the game can read as an int; larger ones are kept as BigInteger</summary>
    private static bool IsInt(JToken value) =>
        value is JValue {Type: JTokenType.Integer, Value: var number} &&
        number switch {long l => l is >= int.MinValue and <= int.MaxValue, int => true, _ => false};

    /// <summary>
    /// Puts a <see cref="Diff"/> back on top of the defaults it was made against
    /// </summary>
    public static JToken Apply(JToken target, JToken diff)
    {
        if (diff is JObject {Count: 1} items && items[ItemsKey] is JObject changes && target is JArray list)
        {
            foreach (var (index, inner) in changes)
            {
                var i = int.Parse(index, NumberStyles.None, CultureInfo.InvariantCulture);
                if (i < list.Count && inner != null) list[i] = Apply(list[i], inner);
            }

            return list;
        }

        if (diff is JObject changed && target is JObject obj)
        {
            foreach (var (name, inner) in changed)
            {
                if (inner == null) continue;
                var current = obj[name];
                obj[name] = current == null ? inner.DeepClone() : Apply(current, inner);
            }

            return obj;
        }

        return diff.DeepClone();
    }

    /// <summary>
    /// The JSON inside a code, for tests and troubleshooting
    /// </summary>
    public static string Contents(string code) => Inflate(FromBase64Url(code.Trim()[PrefixLength..]));

    private static byte[] Deflate(string text)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            deflate.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    private static string Inflate(byte[] data)
    {
        using var deflate = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
        using var reader = new StreamReader(deflate, Encoding.UTF8);

        // Read in small pieces, so a normal code of a few KB doesn't need a buffer for the largest allowed one
        var text = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            text.Append(buffer, 0, read);
            if (text.Length > MaxJsonLength) throw new InvalidDataException("Setup code unpacks to too much data");
        }

        return text.ToString();
    }

    /// <summary>
    /// Base64 without the characters chat apps like to change: no +, / or = padding
    /// </summary>
    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
