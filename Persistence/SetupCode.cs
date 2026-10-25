using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
/// Turns a Boss Creator setup into a short text code for sharing, and back. Only what differs from the default setup
/// is stored, which keeps codes short enough for a chat message.
/// </summary>
public static class SetupCode
{
    /// <summary>
    /// Codes start with "CBC", the format version and a colon. Version 1 codes (2.2) are read the same way; a code
    /// that uses an option 2.2 doesn't know is version 2, so 2.2 asks for an update instead of silently dropping it.
    /// </summary>
    public const string Prefix = "CBC1:";
    public const string Version2Prefix = "CBC2:";
    public const string AnyVersionPrefix = "CBC";
    public const int PrefixLength = 5;

    /// <summary>
    /// Longer codes are refused without being read
    /// </summary>
    public const int MaxLength = 20000;

    /// <summary>
    /// Longest text a code may unpack to, so a small code can't blow up into a huge one
    /// </summary>
    public const int MaxJsonLength = 1_000_000;

    private static readonly JsonSerializer OptionsSerializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = {new StringEnumConverter()}
    });

    public static string Encode(BossOptions options, DailyChallengeModel dcm)
    {
        var payload = new JObject
        {
            ["o"] = Diff(OptionsJson(options), OptionsJson(new BossOptions())) ?? new JObject(),
            ["c"] = Diff(ChallengeJson(dcm), ChallengeJson(DailyChallengeModel.CreateDefaultEditorModel())) ?? new JObject()
        };
        return Pack(payload.ToString(Formatting.None), NeedsVersion2(options) ? Version2Prefix : Prefix);
    }

    /// <summary>
    /// Whether the code uses an option that version 1 readers (2.2) don't know
    /// </summary>
    public static bool NeedsVersion2(BossOptions options) => options.SpeedPerTier != 0 || options.HealthPerTier != 0;

    /// <summary>
    /// Turns JSON into a code without any checks, for tests
    /// </summary>
    internal static string Pack(string json, string prefix = Prefix) => prefix + ToBase64Url(Deflate(json));

    /// <summary>
    /// Reads a code. Never throws: anything that isn't a usable code gives a <see cref="CodeProblem"/>.
    /// <paramref name="source"/> says in the log where the code came from.
    /// </summary>
    public static bool TryDecode(string? code, out BossOptions options, out DailyChallengeModel? dcm,
        out CodeProblem problem, string source = "Share code")
    {
        options = new BossOptions();
        dcm = null;
        problem = CodeProblem.Invalid;

        code = code?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > MaxLength) return false;
        if (!code.StartsWith(Prefix) && !code.StartsWith(Version2Prefix))
        {
            if (code.StartsWith(AnyVersionPrefix) && code.Length > 4 && char.IsDigit(code[3])) problem = CodeProblem.TooNew;
            return false;
        }

        try
        {
            var payload = JObject.Parse(Inflate(FromBase64Url(code[PrefixLength..])));

            var optionsJson = OptionsJson(new BossOptions());
            if (payload["o"] is JObject optionsDiff) Apply(optionsJson, optionsDiff);
            var raw = optionsJson.ToObject<BossOptions>(OptionsSerializer);
            if (raw == null) return false;
            var decoded = raw.Sanitized();

            var challenge = ChallengeJson(DailyChallengeModel.CreateDefaultEditorModel());
            if (payload["c"] is JObject challengeDiff) Apply(challenge, challengeDiff);
            var model = DcmJson.Deserialize(challenge.ToString(Formatting.None));
            if (model == null) return false;

            // Same rule as saved setups: polymorphic models are never part of a setup
            model.customMapModel = null;
            model.simulationBehaviorsMod = null;
            var changes = ChallengeLimits.OptionChanges(raw, decoded);
            changes.AddRange(ChallengeLimits.Sanitize(model, BossMode.DefaultDcm(decoded)));
            ChallengeLimits.Report(source, changes);

            options = decoded;
            dcm = model;
            problem = CodeProblem.None;
            return true;
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"{source}: ignoring unreadable setup code: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Marks a list diff: the changed items by index, for lists as long as the default one
    /// </summary>
    internal const string ItemsKey = "$items";

    /// <summary>
    /// What differs in <paramref name="value"/> from <paramref name="defaults"/>, or null if nothing does. Objects
    /// are compared property by property and lists item by item, so changing one tower keeps the code short.
    /// </summary>
    internal static JToken? Diff(JToken value, JToken? defaults)
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
    /// Puts a <see cref="Diff"/> back on top of the defaults it was made against
    /// </summary>
    internal static JToken Apply(JToken target, JToken diff)
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
    internal static string Contents(string code) => Inflate(FromBase64Url(code.Trim()[PrefixLength..]));

    internal static JObject OptionsJson(BossOptions options) => JObject.FromObject(options, OptionsSerializer);

    /// <summary>
    /// The challenge as JSON, without the read only creation type: the game works it out itself, and a challenge read
    /// from JSON reports a different one, which would make the same setup give different codes
    /// </summary>
    internal static JObject ChallengeJson(DailyChallengeModel dcm)
    {
        var json = JObject.Parse(DcmJson.Serialize(dcm));
        json.Remove("ChallengeCreationType");
        return json;
    }

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
