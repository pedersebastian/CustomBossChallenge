using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Data.Gameplay;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Rounds;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CustomBossChallenge.Config;

/// <summary>
/// Boss Event Rounds in the match (#80). <see cref="ConfigApplier.Prepare"/> has the game merge the first tier's boss's
/// round set over the default rounds; here its boss rounds move to the tiers' Spawn Rounds
/// (<see cref="BossRoundsMath"/>), and in Advanced Mode every tier with another boss gets that boss's own round (#84).
/// </summary>
public static class BossEventRounds
{
    /// <summary>The game's round sets count time in seconds, its matches in frames</summary>
    private const float FramesPerSecond = 60;

    /// <summary>
    /// Moves the match's boss event rounds to the tiers' spawn rounds and gives every tier its own boss's round, on a
    /// copy of its round set so the game's own round sets stay as they are. Returns what it did, for the log.
    /// </summary>
    public static string Apply(GameModel match, BossOptions options)
    {
        var set = match.roundSet;
        if (set == null) return "missing round set";

        var name = options.BaseEventRoundSet;
        if (GameData.Instance.MergeableRoundSetByName(name) == null)
        {
            ModHelper.Warning<CustomBossChallenge>($"The game has no round set '{name}', playing the default rounds");
            return "not in the game";
        }

        var rounds = set.rounds;
        var sources = BossRoundsMath.Sources(options.EffectiveSpawnRounds, rounds.Length);
        var reordered = sources.Select(source => rounds[source - 1]).ToArray();
        var own = new List<string>();
        // Each tier's own boss round goes over the match's own rounds as the game has them, before the first tier's
        // boss rounds were merged in: AlternateBloonsRounds' for that mode, the default ones otherwise (#112)
        var defaults = (GameData.Instance.RoundSetByName(set.name) ?? GameData.Instance.RoundSetByName("DefaultRoundSet"))?.rounds;
        if (options.Mixed && defaults != null)
        {
            foreach (var (round, tier, eventRound) in BossRoundsMath.TierEventRounds(options.EffectiveSpawnRounds, rounds.Length))
            {
                var tierSet = BossOptions.BossEventRoundSet(options.BossOf(tier).Boss);
                if (tierSet == name) continue;
                if (eventRound > defaults.Length) continue;
                var replaced = RoundOf(tierSet, eventRound, defaults[eventRound - 1]);
                if (replaced == null) continue;
                reordered[round - 1] = replaced;
                own.Add($"tier {tier} {tierSet}");
            }
        }

        if (BossRoundsMath.IsIdentity(sources) && own.Count == 0) return name;

        var moved = set.Clone().Cast<RoundSetModel>();
        moved.rounds = new Il2CppReferenceArray<RoundModel>(reordered);
        match._roundSet_k__BackingField = moved;
        var log = name;
        if (!BossRoundsMath.IsIdentity(sources))
        {
            log += $", moved to {TierMath.FormatRounds(options.EffectiveSpawnRounds.Take(TierMath.VanillaTiers).ToArray())}";
        }
        return own.Count == 0 ? log : $"{log}, {string.Join(", ", own)}";
    }

    /// <summary>
    /// The given round of a boss's own round set as the game merges it over <paramref name="normal"/>, the default
    /// round there: the set's bloons replace the round's, or come on top of them. The round as it is if the set
    /// doesn't change it, or null if the game has no such set.
    /// </summary>
    public static RoundModel? RoundOf(string setName, int roundNumber, RoundModel normal)
    {
        var set = GameData.Instance.MergeableRoundSetByName(setName);
        if (set == null)
        {
            ModHelper.Warning<CustomBossChallenge>($"The game has no round set '{setName}', keeping round {roundNumber}");
            return null;
        }

        RoundInfo? info = null;
        foreach (var candidate in set.rounds)
        {
            if (candidate.roundNumber == roundNumber) info = candidate;
        }
        if (info == null) return normal;

        var groups = info.bloonGroups.ToArray().Select(group =>
            new BloonGroupModel("", group.bloon, group.start * FramesPerSecond, group.End * FramesPerSecond, group.count));
        if (info.addToRound) groups = normal.groups.Concat(groups);
        return new RoundModel("", new Il2CppReferenceArray<BloonGroupModel>(groups.ToArray()));
    }
}
