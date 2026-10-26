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
        data.SetupBossChallenge("", options.Boss, options.Elite, false, dcm, LeaderboardScoringType.GameTime);
        return data;
    }

    /// <summary>
    /// Leaves the Boss Creator and loads the custom game
    /// </summary>
    public static void Launch(DailyChallengeModel editorDcm, BossOptions options)
    {
        BossMode.Exit();
        BuildInGameData(editorDcm, options);
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
