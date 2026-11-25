using System.Collections.Generic;
using System.Linq;
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
    public const string Version3Prefix = CodeFormat.Version3Prefix;
    public const string Version4Prefix = CodeFormat.Version4Prefix;
    public const string Version5Prefix = CodeFormat.Version5Prefix;
    public const string Version6Prefix = CodeFormat.Version6Prefix;
    public const string Version7Prefix = CodeFormat.Version7Prefix;
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
        return Pack(payload.ToString(Formatting.None), PrefixFor(options, dcm));
    }

    /// <summary>
    /// The oldest code version that holds everything in <paramref name="options"/> and the boss's health in
    /// <paramref name="dcm"/>
    /// </summary>
    public static string PrefixFor(BossOptions options, DailyChallengeModel? dcm = null) =>
        NeedsVersion7(options) ? Version7Prefix
        : NeedsVersion6(options, dcm) ? Version6Prefix
        : NeedsVersion5(options) ? Version5Prefix
        : NeedsVersion4(options) ? Version4Prefix
        : NeedsVersion3(options) ? Version3Prefix
        : NeedsVersion2(options) ? Version2Prefix
        : Prefix;

    /// <summary>
    /// Whether the code holds the bosses a game picked for its Random tiers (#270), which version 6 readers drop
    /// </summary>
    public static bool NeedsVersion7(BossOptions options) => options.RandomTiers != null;

    /// <summary>
    /// Whether version 5 readers would change the setup's Boss Health or a tier's own health (#250)
    /// </summary>
    public static bool NeedsVersion6(BossOptions options, DailyChallengeModel? dcm) =>
        CodeFormat.NeedsVersion6(dcm?.bloonModifiers?.healthMultipliers?.boss ?? 1,
            TierBossMath.Parse(options.TierBosses).Select(tier => tier.Health));

    /// <summary>
    /// Whether the code uses an option that version 4 readers (2.10-2.11) don't know: the boss's own event rounds in
    /// CHIMPS. The value is in the code even with Boss Event Rounds off, and 2.11 can't read it.
    /// </summary>
    public static bool NeedsVersion5(BossOptions options) => options.ChimpsRounds == ChimpsRounds.Own;

    /// <summary>
    /// Whether the code uses an option that version 3 readers (2.9) don't know
    /// </summary>
    public static bool NeedsVersion4(BossOptions options) => options.UsesBossEventRounds;

    /// <summary>
    /// Whether the code uses an option that version 2 readers (2.3-2.8) don't know
    /// </summary>
    public static bool NeedsVersion3(BossOptions options) => options.Mixed;

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
            if (payload["o"] is JObject optionsDiff) CodeFormat.Apply(optionsJson, CodeFormat.TypedOptions(optionsDiff));
            var raw = optionsJson.ToObject<BossOptions>(OptionsSerializer);
            if (raw == null) return false;
            var decoded = raw.SanitizedFromCode(CodeFormat.HoldsRandomTiers(code));

            var changes = ChallengeLimits.OptionChanges(raw, decoded);
            var model = Challenge(payload["c"] as JObject, decoded, changes, new List<string>());
            if (model == null) return false;
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
    /// The editor's challenge from a code's challenge diff: only what the Challenge Editor and the Boss Creator can
    /// set, with the right types (#108), in the range they allow. What was left out goes to <paramref name="ignored"/>
    /// and, with what was brought into range, to <paramref name="changes"/> for the log. Past events (#146) are read
    /// the same way.
    /// </summary>
    internal static DailyChallengeModel? Challenge(JObject? diff, BossOptions options, List<string> changes,
        List<string> ignored)
    {
        var challenge = ChallengeJson(DailyChallengeModel.CreateDefaultEditorModel());
        if (diff != null)
        {
            CodeFormat.Apply(challenge, CodeFormat.FilterChallenge(diff, (JObject) challenge.DeepClone(), ignored));
        }
        var model = DcmJson.Deserialize(challenge.ToString(Formatting.None));
        if (model == null) return null;

        if (ignored.Count > 0) changes.Add($"ignored {string.Join(", ", ignored)}");
        changes.AddRange(ChallengeLimits.Sanitize(model, BossMode.DefaultDcm(options)));
        return model;
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
