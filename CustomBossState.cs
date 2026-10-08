using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;

namespace CustomBossChallenge;

/// <summary>
/// Tracks whether the game being started / played is a custom boss challenge
/// </summary>
public static class CustomBossState
{
    /// <summary>
    /// The config that was last applied to a challenge model, waiting for that challenge to be set up
    /// </summary>
    public static BossChallengeConfig? PendingConfig { get; private set; }

    /// <summary>
    /// Snapshot of the config the current custom game was started with, or null if it isn't a custom game
    /// </summary>
    public static BossChallengeConfig? ActiveConfig { get; private set; }

    /// <summary>
    /// Whether a custom boss challenge is being played right now. The snapshot is deliberately kept after leaving
    /// the game (so restarts keep their rules); the marker check on the current game keeps it from leaking into
    /// any other game.
    /// </summary>
    public static bool IsActive
    {
        get
        {
            if (ActiveConfig == null) return false;
            var game = InGameData.CurrentGame;
            return game != null && game.gameType == GameType.BossChallenge && ConfigApplier.IsMarked(game.dcModel);
        }
    }

    /// <summary>
    /// Applies <paramref name="config"/> to a clone of <paramref name="dcm"/> and remembers it for the game setup
    /// </summary>
    public static DailyChallengeModel Prepare(DailyChallengeModel dcm, BossChallengeConfig config)
    {
        PendingConfig = config;
        return ConfigApplier.ApplyToClone(dcm, config);
    }

    /// <summary>
    /// Called whenever a Boss Challenge is set up. Only marked challenges outside of co-op become custom.
    /// </summary>
    /// <returns>Whether the game will be a custom boss challenge</returns>
    public static bool OnSetupBossChallenge(DailyChallengeModel? dcm, bool coop)
    {
        if (!ConfigApplier.IsMarked(dcm) || coop)
        {
            Clear();
            return false;
        }

        ActiveConfig = PendingConfig ?? Settings.ToConfig();
        return true;
    }

    public static void Clear()
    {
        PendingConfig = null;
        ActiveConfig = null;
    }
}
