using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Menu;

namespace CustomBossChallenge.Creator;

/// <summary>
/// A Boss Creator session: the vanilla Challenge Editor opened with the boss setup instead of the player's own draft
/// </summary>
public static class BossMode
{
    /// <summary>
    /// Whether the Challenge Editor is currently being used as the Boss Creator
    /// </summary>
    public static bool Active { get; private set; }

    /// <summary>
    /// The boss options being edited
    /// </summary>
    public static BossOptions Options { get; set; } = new();

    /// <summary>
    /// The challenge model being edited; the Challenge Editor works directly on this object
    /// </summary>
    public static DailyChallengeModel? Dcm { get; private set; }

    /// <summary>
    /// Where the last setup is remembered; tests point this at a temporary file
    /// </summary>
    public static LastSetupStore Store { get; set; } = LastSetupStore.Default;

    private static DailyChallengeModel? userDraft;

    /// <summary>
    /// Loads the last setup (or defaults) and opens the Challenge Editor as the Boss Creator
    /// </summary>
    public static void Enter()
    {
        if (Active) Exit();

        if (!Store.TryLoad(out var options, out var dcm) || dcm == null)
        {
            options = new BossOptions();
            dcm = DefaultDcm(options);
        }

        Options = options;
        Dcm = dcm;
        userDraft = Game.Player?.Data?.challengeEditorModel;
        Active = true;

        MenuManager.instance.OpenMenu(SceneNames.ChallengeEditor, dcm);
    }

    /// <summary>
    /// Saves the setup and stops treating the Challenge Editor as the Boss Creator
    /// </summary>
    public static void Exit()
    {
        if (!Active) return;

        Save();
        RestoreUserDraft();
        Active = false;
    }

    public static void Save()
    {
        if (!Active || Dcm == null) return;

        try
        {
            Store.Save(Options, Dcm);
        }
        catch (System.Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Could not save the Boss Creator setup: {e.Message}");
        }
    }

    /// <summary>
    /// The player's own Challenge Editor draft must never be replaced by the boss setup
    /// </summary>
    private static void RestoreUserDraft()
    {
        var profile = Game.Player?.Data;
        if (profile == null || userDraft == null) return;

        if (profile.challengeEditorModel == null || profile.challengeEditorModel.Pointer != userDraft.Pointer)
        {
            profile.challengeEditorModel = userDraft;
        }

        userDraft = null;
    }

    public static string DefaultName(Il2CppAssets.Scripts.Data.Boss.BossType boss) => $"Custom {boss}";

    public static DailyChallengeModel DefaultDcm(BossOptions options)
    {
        var dcm = DailyChallengeModel.CreateDefaultEditorModel();
        dcm.name = DefaultName(options.Boss);
        return dcm;
    }
}
