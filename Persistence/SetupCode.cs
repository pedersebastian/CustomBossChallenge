using System;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Turns a Boss Creator setup into a short text code for sharing, and back. Only what differs from the default setup
/// is stored, which keeps codes short enough for a chat message. The text format itself is <see cref="CodeFormat"/>.
/// </summary>
public static class SetupCode
{
    public const string Prefix = CodeFormat.Prefix;
    public const string Version2Prefix = CodeFormat.Version2Prefix;
    public const int PrefixLength = CodeFormat.PrefixLength;
    public const int MaxLength = CodeFormat.MaxLength;
    public const int MaxJsonLength = CodeFormat.MaxJsonLength;

    private static readonly JsonSerializer OptionsSerializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = {new StringEnumConverter()}
    });

    public static string Encode(BossOptions options, DailyChallengeModel dcm)
    {
        var payload = CodeFormat.Payload(OptionsJson(options), OptionsJson(new BossOptions()),
            ChallengeJson(dcm), ChallengeJson(DailyChallengeModel.CreateDefaultEditorModel()));
        return Pack(payload.ToString(Formatting.None), NeedsVersion2(options) ? Version2Prefix : Prefix);
    }

    /// <summary>
    /// Whether the code uses an option that version 1 readers (2.2) don't know
    /// </summary>
    public static bool NeedsVersion2(BossOptions options) => options.SpeedPerTier != 0 || options.HealthPerTier != 0;

    /// <summary>
    /// Turns JSON into a code without any checks, for tests
    /// </summary>
    internal static string Pack(string json, string prefix = Prefix) => CodeFormat.Pack(json, prefix);

    /// <summary>
    /// Reads a code. Never throws: anything that isn't a usable code gives a <see cref="CodeProblem"/>.
    /// <paramref name="source"/> says in the log where the code came from.
    /// </summary>
    public static bool TryDecode(string? code, out BossOptions options, out DailyChallengeModel? dcm,
        out CodeProblem problem, string source = "Share code")
    {
        options = new BossOptions();
        dcm = null;

        if (!CodeFormat.TryUnpack(code, out var payload, out problem, out var error))
        {
            if (error != null) ModHelper.Warning<CustomBossChallenge>($"{source}: ignoring unreadable setup code: {error}");
            return false;
        }

        problem = CodeProblem.Invalid;
        try
        {
            var optionsJson = OptionsJson(new BossOptions());
            if (payload["o"] is JObject optionsDiff) CodeFormat.Apply(optionsJson, optionsDiff);
            var raw = optionsJson.ToObject<BossOptions>(OptionsSerializer);
            if (raw == null) return false;
            var decoded = raw.Sanitized();

            var challenge = ChallengeJson(DailyChallengeModel.CreateDefaultEditorModel());
            if (payload["c"] is JObject challengeDiff) CodeFormat.Apply(challenge, challengeDiff);
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

    /// <inheritdoc cref="CodeFormat.Contents"/>
    internal static string Contents(string code) => CodeFormat.Contents(code);

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
}
