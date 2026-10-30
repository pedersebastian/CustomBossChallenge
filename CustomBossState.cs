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
        resumeTiers = null;
        CustomBossRetries.Clear();
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

        // The last tier wins the game, which leaves nothing to continue
        if (AllTiersDefeated) CustomBossRetries.Forget();
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
    public static void OnMatchStart()
    {
        // A retry or a continued game loads a snapshot into a match that already defeated some tiers; anything else
        // starts over, and replaces the game that could be continued. Loading a game starts the match twice (new game
        // model, then the boss manager), so the resume is kept until a round starts or the game restarts.
        if (resumeTiers == null) CustomBossRetries.Forget();
        HighestDefeatedTier = resumeTiers ?? 0;

        // The bosses of the last match are gone; their displays may be reused
        Visuals.TierLookApplier.Clear();
    }

    private static int? resumeTiers;

    /// <summary>
    /// Called right before a retry or a continued game loads a snapshot, with the tiers that were defeated when it
    /// was taken
    /// </summary>
    public static void ResumeWith(int defeatedTiers)
    {
        HighestDefeatedTier = defeatedTiers;
        resumeTiers = defeatedTiers;
    }

    /// <summary>
    /// The first round after a retry has started, so a later restart starts over
    /// </summary>
    public static void OnRoundStart() => resumeTiers = null;

    /// <summary>
    /// A restart always starts over, even right after a retry
    /// </summary>
    public static void OnRestart() => resumeTiers = null;

    public static void Clear()
    {
        PendingOptions = null;
        ActiveOptions = null;
        HighestDefeatedTier = 0;
        resumeTiers = null;
        CustomBossRetries.Clear();
    }
}
