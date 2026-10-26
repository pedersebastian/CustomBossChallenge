using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Utils;

namespace CustomBossChallenge;

/// <summary>
/// Snapshots of a custom game taken at the end of every round, for the defeat screen's free retries and for
/// continuing the game after it was closed. Custom games never write map saves, so the snapshots are kept in memory
/// and in the mod's own <see cref="ContinueStore"/>; the player's own Boss Challenge saves stay untouched.
/// </summary>
public static class CustomBossRetries
{
    public const string SaveId = "CustomBossChallenge-Retry";

    /// <summary>
    /// Where the snapshots are written to; tests point it at a temporary file
    /// </summary>
    public static ContinueStore Store { get; set; } = ContinueStore.Default;

    /// <summary>
    /// Snapshots by the round they resume on, as the player counts rounds, with the tiers defeated by then and their
    /// JSON for the store
    /// </summary>
    private static readonly Dictionary<int, (MapSaveDataModel Save, RoundSnapshot Snapshot)> Saves = new();

    public static void OnRoundEnd(InGame game, int highestCompletedRound)
    {
        // The round the last tier is defeated on ends after the win, which leaves nothing to retry or continue
        if (CustomBossState.AllTiersDefeated) return;

        var options = CustomBossState.ActiveOptions;
        var save = game.CreateCurrentMapSave(highestCompletedRound, SaveId);
        if (options == null || save == null) return;

        string json;
        try
        {
            json = MapSaveJson.Serialize(save);
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Could not save round {save.round} of the custom game: {e.Message}");
            json = "";
        }
        Saves[save.round] = (save, new RoundSnapshot(save.round, CustomBossState.HighestDefeatedTier, json));

        var spawnRounds = options.EffectiveSpawnRounds;
        foreach (var round in Saves.Keys.Where(r => !TierMath.KeepSnapshot(spawnRounds, save.round, r)).ToList())
        {
            Saves.Remove(round);
        }

        Persist(options);
    }

    /// <summary>
    /// Writes the snapshots to the store, so the game can be continued after it was closed
    /// </summary>
    private static void Persist(BossOptions options)
    {
        var dcm = InGameData.CurrentGame?.dcModel;
        if (dcm == null || Saves.Values.Any(entry => entry.Snapshot.Json == "")) return;

        try
        {
            Store.Save(options, dcm, Saves.Values.Select(entry => entry.Snapshot));
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Could not write the custom game to {Store.FilePath}: {e.Message}");
        }
    }

    /// <summary>
    /// Takes the snapshots of a continued game back, for its retries
    /// </summary>
    public static void Restore(ContinueGame game)
    {
        Saves.Clear();
        foreach (var (snapshot, save) in game.Snapshots)
        {
            Saves[snapshot.Round] = (save, snapshot);
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
        CustomBossState.ResumeWith(entry.Snapshot.DefeatedTiers);
        InGame.instance.bridge.ContinueFromCheckpoint(entry.Save, new KonFuze(0), isRetryLastRound, false);
    }

    public static void Clear() => Saves.Clear();

    /// <summary>
    /// The game is over for good (won) or replaced by a new one: nothing is left to continue
    /// </summary>
    public static void Forget()
    {
        Saves.Clear();
        try
        {
            Store.Delete();
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Could not delete {Store.FilePath}: {e.Message}");
        }
    }
}
