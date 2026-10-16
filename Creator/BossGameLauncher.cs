using CustomBossChallenge.Config;
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
}
