using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;

namespace CustomBossChallenge.Creator;

/// <summary>
/// Starts a custom Boss Challenge from a Boss Creator setup
/// </summary>
public static class BossGameLauncher
{
    /// <summary>
    /// Sets up the game data for a custom Boss Challenge. Ranked is always off.
    /// </summary>
    public static InGameData BuildInGameData(DailyChallengeModel editorDcm, BossOptions options)
    {
        var dcm = CustomBossState.Prepare(editorDcm, options);

        InGameData.CreateNewInstance();
        var data = InGameData.Editable;
        data.selectedMap = dcm.map;
        data.selectedMode = dcm.mode;
        data.selectedDifficulty = dcm.difficulty;
        // With a different boss per tier the game starts as the first tier's boss; MixedBosses switches it per tier
        var (boss, elite) = options.BossOf(1);
        data.SetupBossChallenge("", boss, elite, false, dcm, LeaderboardScoringType.GameTime);
        return data;
    }

    /// <summary>
    /// The options a new game is played with: Random tiers get their boss here, once, so a restart, a retry and
    /// continuing the game later all have the same bosses
    /// </summary>
    public static BossOptions Resolve(BossOptions options)
    {
        if (!options.Mixed) return options;
        var resolved = options.ResolveRandom(new System.Random());
        ModHelper.Msg<CustomBossChallenge>(
            $"Bosses per tier: {string.Join(", ", Enumerable.Range(1, resolved.Tiers).Select(resolved.BossId))}");
        return resolved;
    }

    /// <summary>
    /// Leaves the Boss Creator and loads the custom game
    /// </summary>
    public static void Launch(DailyChallengeModel editorDcm, BossOptions options)
    {
        BossMode.Exit();
        BuildInGameData(editorDcm, Resolve(options));
        UI.instance.LoadGame(null, null, null, false, false);
    }

    /// <summary>
    /// Loads a custom game saved by <see cref="CustomBossRetries"/> where it was left, with its retries
    /// </summary>
    public static void Continue(ContinueGame game)
    {
        BossMode.Exit();
        BuildInGameData(game.Dcm, game.Options);

        // Setting up the game forgets the retries and defeated tiers, so they come back after it
        var (latest, save) = game.Latest;
        CustomBossRetries.Restore(game);
        CustomBossState.ResumeWith(latest.DefeatedTiers);
        ModHelper.Msg<CustomBossChallenge>($"Continuing the custom game on round {latest.Round}");
        UI.instance.LoadGame(null, null, save, false, false);
    }
}
