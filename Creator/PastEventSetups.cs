using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Creator;

/// <summary>
/// Opens a past boss event (#146) as a Boss Creator setup: <see cref="PastEventRules"/> maps its rules, and the
/// challenge is read like a share code's, so only what the Challenge Editor can set gets in, in the range it allows
/// </summary>
public static class PastEventSetups
{
    /// <summary>The event's rules as the Boss Creator's options and challenge, before they are read into a model</summary>
    public static PastEventSetup Map(PastEvent pastEvent, bool elite) =>
        PastEventRules.Map(pastEvent, elite, SetupCode.ChallengeJson(DailyChallengeModel.CreateDefaultEditorModel()),
            ChallengeLimits.IsKnownMap, BossEditorSection.UnsupportedModes, Texts.Date, Texts.Elite.T());

    /// <summary>
    /// The setup of the normal or the Elite event. False when it can't be opened; <paramref name="setup"/> says why.
    /// What the challenge left out is added to the setup's unsupported rules.
    /// </summary>
    public static bool TryBuild(PastEvent pastEvent, bool elite, out PastEventSetup setup, out DailyChallengeModel? dcm)
    {
        setup = Map(pastEvent, elite);
        dcm = null;
        if (setup.Problem != PastEventProblem.None) return false;

        var changes = new List<string>();
        var ignored = new List<string>();
        dcm = SetupCode.Challenge(setup.Challenge, setup.Options, changes, ignored);
        if (dcm == null) return false;

        if (setup.EventOnly != null) dcm = WithEventOnly(dcm, setup.EventOnly);
        if (dcm == null) return false;

        ChallengeLimits.Report($"Past event {pastEvent.Id}{(elite ? " (Elite)" : "")}", changes);
        if (ignored.Count > 0) setup = setup with {Unsupported = setup.Unsupported.Concat(ignored).ToList()};
        return true;
    }

    /// <summary>
    /// The model with the event's rules a share code can't hold (#212), like Insta Monkeys turned off or only some
    /// powers allowed, set the way the game reads the model from JSON
    /// </summary>
    internal static DailyChallengeModel? WithEventOnly(DailyChallengeModel dcm, JObject eventOnly)
    {
        var json = SetupCode.ChallengeJson(dcm);
        foreach (var (name, value) in eventOnly) json[name] = value?.DeepClone();
        return DcmJson.Deserialize(json.ToString(Newtonsoft.Json.Formatting.None));
    }

    /// <summary>
    /// Whether the model has rules only a past event sets (#212), which Copy and Save leave out: Insta Monkeys turned
    /// off, or not every power allowed as often as the game's default
    /// </summary>
    public static (bool NoInstas, bool LimitedPowers) EventOnlyRules(DailyChallengeModel? dcm)
    {
        if (dcm == null) return (false, false);
        var limited = false;
        if (dcm.powers != null)
        {
            foreach (var power in dcm.powers)
            {
                if (power != null && power.max >= 0) limited = true;
            }
        }
        return (dcm.disableInstas, limited);
    }
}
