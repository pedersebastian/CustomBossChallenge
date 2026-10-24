using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Utils;

namespace CustomBossChallenge;

/// <summary>
/// Snapshots of a custom game taken at the end of every round, for the defeat screen's free retries. They live in
/// memory only: custom games never write map saves, so the player's own Boss Challenge saves stay untouched.
/// </summary>
public static class CustomBossRetries
{
    public const string SaveId = "CustomBossChallenge-Retry";

    /// <summary>
    /// Snapshots by the round they resume on, as the player counts rounds, with the tiers defeated by then
    /// </summary>
    private static readonly Dictionary<int, (MapSaveDataModel Save, int DefeatedTiers)> Saves = new();

    public static void OnRoundEnd(InGame game, int highestCompletedRound)
    {
        var options = CustomBossState.ActiveOptions;
        var save = game.CreateCurrentMapSave(highestCompletedRound, SaveId);
        if (options == null || save == null) return;

        Saves[save.round] = (save, CustomBossState.HighestDefeatedTier);

        // Only the latest round and the rounds tiers arrive on can ever be retried
        var spawnRounds = options.EffectiveSpawnRounds;
        foreach (var round in Saves.Keys.Where(r => r != save.round && !spawnRounds.Contains(r)).ToList())
        {
            Saves.Remove(round);
        }
    }

    /// <summary>
    /// The snapshot that replays <paramref name="lostRound"/>
    /// </summary>
    public static bool HasLastRound(int lostRound) => Saves.ContainsKey(lostRound);

    /// <summary>
    /// The round the tier being played on <paramref name="lostRound"/> arrived on, or null when there is no snapshot
    /// for it or it is the lost round itself, which Retry Last Round already covers
    /// </summary>
    public static int? CheckpointRound(int[] spawnRounds, int lostRound)
    {
        var round = TierMath.CheckpointRound(spawnRounds, lostRound);
        return round != lostRound && Saves.ContainsKey(round) ? round : null;
    }

    public static MapSaveDataModel? SaveFor(int round) => Saves.TryGetValue(round, out var entry) ? entry.Save : null;

    /// <summary>
    /// Loads the snapshot for <paramref name="round"/> into the running game, free of charge
    /// </summary>
    public static void RetryFrom(int round, bool isRetryLastRound)
    {
        if (!Saves.TryGetValue(round, out var entry) || InGame.instance?.bridge == null) return;

        ModHelper.Msg<CustomBossChallenge>($"Retrying from round {round} for free");
        CustomBossState.ResumeWith(entry.DefeatedTiers);
        InGame.instance.bridge.ContinueFromCheckpoint(entry.Save, new KonFuze(0), isRetryLastRound, false);
    }

    public static void Clear() => Saves.Clear();
}
