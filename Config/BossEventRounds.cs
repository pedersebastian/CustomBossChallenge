using System.Linq;
using BTD_Mod_Helper;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Rounds;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CustomBossChallenge.Config;

/// <summary>
/// Boss Event Rounds in the match (#80). <see cref="ConfigApplier.Prepare"/> has the game merge the boss's own round
/// set over the default rounds; here its boss rounds move to the tiers' Spawn Rounds (<see cref="BossRoundsMath"/>).
/// </summary>
public static class BossEventRounds
{
    /// <summary>
    /// Moves the match's boss event rounds to the tiers' spawn rounds, on a copy of its round set so the game's own
    /// round sets stay as they are. Returns what it did, for the log.
    /// </summary>
    public static string Apply(GameModel match, BossOptions options)
    {
        var set = match.roundSet;
        if (set == null) return "missing round set";

        var name = BossOptions.BossEventRoundSet(options.Boss);
        if (Il2CppAssets.Scripts.Data.GameData.Instance.MergeableRoundSetByName(name) == null)
        {
            ModHelper.Warning<CustomBossChallenge>($"The game has no round set '{name}', playing the default rounds");
            return "not in the game";
        }

        var rounds = set.rounds;
        var sources = BossRoundsMath.Sources(options.EffectiveSpawnRounds, rounds.Length);
        if (BossRoundsMath.IsIdentity(sources)) return name;

        var moved = set.Clone().Cast<RoundSetModel>();
        moved.rounds = new Il2CppReferenceArray<RoundModel>(sources.Select(source => rounds[source - 1]).ToArray());
        match._roundSet_k__BackingField = moved;
        return $"{name}, moved to {TierMath.FormatRounds(options.EffectiveSpawnRounds.Take(TierMath.VanillaTiers).ToArray())}";
    }
}
