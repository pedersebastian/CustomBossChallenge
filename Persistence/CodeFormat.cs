using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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

        code = code?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > MaxLength) return false;
        if (!code.StartsWith(Prefix) && !code.StartsWith(Version2Prefix))
        {
            if (code.StartsWith(AnyVersionPrefix) && code.Length > 4 && char.IsDigit(code[3])) problem = CodeProblem.TooNew;
            return false;
        }

        try
        {
            payload = JObject.Parse(Inflate(FromBase64Url(code[PrefixLength..])));
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
