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
    /// The options of the challenge model that was last prepared, waiting for that challenge to be set up
    /// </summary>
    public static BossOptions? PendingOptions { get; private set; }

    /// <summary>
    /// The options the current custom game was started with, or null if it isn't a custom game
    /// </summary>
    public static BossOptions? ActiveOptions { get; private set; }

    /// <summary>
    /// Whether a custom boss challenge is being played right now. The options are deliberately kept after leaving
    /// the game (so restarts keep their rules); the marker check on the current game keeps them from leaking into
    /// any other game.
    /// </summary>
    public static bool IsActive
    {
        get
        {
            if (ActiveOptions == null) return false;
            var game = InGameData.CurrentGame;
            return game != null && game.gameType == GameType.BossChallenge && ConfigApplier.IsMarked(game.dcModel);
        }
    }

    /// <summary>
    /// Prepares a clone of the editor's model for a custom game and remembers the options for the game setup
    /// </summary>
    public static DailyChallengeModel Prepare(DailyChallengeModel editorDcm, BossOptions options)
    {
        PendingOptions = options;
        return ConfigApplier.Prepare(editorDcm, options);
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

        ActiveOptions = PendingOptions ?? new BossOptions();
        HighestDefeatedTier = 0;
        return true;
    }

    /// <summary>
    /// The highest boss tier defeated in the current custom game
    /// </summary>
    public static int HighestDefeatedTier { get; private set; }

    public static bool AllTiersDefeated =>
        ActiveOptions != null && HighestDefeatedTier >= TierMath.ClampTiers(ActiveOptions.Tiers);

    public static void OnBossDefeated(int tier)
    {
        if (tier > HighestDefeatedTier) HighestDefeatedTier = tier;
    }

    /// <summary>
    /// Plays fewer tiers than chosen, for when the game has no model for the later ones
    /// </summary>
    /// <returns>The options the game is now played with</returns>
    public static BossOptions LimitTiers(int tiers)
    {
        var options = ActiveOptions ?? new BossOptions();
        ActiveOptions = options with {Tiers = TierMath.ClampTiers(System.Math.Min(tiers, options.Tiers))};
        return ActiveOptions;
    }

    /// <summary>
    /// Called when a (custom) match starts or restarts. Restarts reuse the game model, so this is also called from
    /// the boss manager's Init.
    /// </summary>
    public static void OnMatchStart() => HighestDefeatedTier = 0;

    public static void Clear()
    {
        PendingOptions = null;
        ActiveOptions = null;
        HighestDefeatedTier = 0;
    }
}
