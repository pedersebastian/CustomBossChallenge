using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;

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
    /// The boss options when the editor was opened, which the editor's Undo goes back to along with its own model
    /// </summary>
    public static BossOptions OpenedOptions { get; private set; } = new();

    public static void OnEditorOpened()
    {
        OpenedOptions = Options;
        BossHistory.Clear();
    }

    /// <summary>
    /// The challenge model being edited; the Challenge Editor works directly on this object
    /// </summary>
    public static DailyChallengeModel? Dcm { get; private set; }

    /// <summary>
    /// Where the last setup is remembered; tests point this at a temporary file
    /// </summary>
    public static LastSetupStore Store { get; set; } = LastSetupStore.Default;

    /// <summary>
    /// The player's saved setups; tests point this at a temporary file
    /// </summary>
    public static SavedSetupsStore Saved { get; set; } = SavedSetupsStore.Default;

    private static DailyChallengeModel? userDraft;

    /// <summary>
    /// Opens the Challenge Editor as the Boss Creator, with the last setup (or defaults)
    /// </summary>
    public static void Enter()
    {
        // A last setup with Gildebloon while it is off starts from the defaults; the file keeps it for later (#25)
        if (!Store.TryLoad(out var options, out var dcm) || dcm == null || Menus.BossEditorSection.GildebloonProblem(options) != null)
        {
            options = BossOptions.NewSetup;
            dcm = DefaultDcm(options);
        }

        Enter(options, dcm);
        TellAboutNewerFiles();
        Menus.WhatsNew.ShowIfNew();
    }

    /// <summary>
    /// Whether the player was told this session that the setup files come from a newer version of the mod
    /// </summary>
    internal static bool ToldAboutNewerFiles { get; set; }

    /// <summary>
    /// The last setup and the saved setups of a newer version of the mod are kept as they are but not used, so the
    /// player is told once per session why the Boss Creator starts from the defaults or the list is empty (#110)
    /// </summary>
    /// <returns>Whether the popup was shown</returns>
    internal static bool TellAboutNewerFiles()
    {
        if (ToldAboutNewerFiles || !(Store.IsTooNew || Saved.IsTooNew)) return false;

        ToldAboutNewerFiles = true;
        Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.SafelyQueue(screen =>
            screen.ShowOkPopup(Menus.Texts.T(Menus.Texts.NewerSetupFiles), null));
        return true;
    }

    /// <summary>
    /// Opens the Challenge Editor as the Boss Creator with this setup
    /// </summary>
    public static void Enter(BossOptions options, DailyChallengeModel dcm)
    {
        if (Active) Exit();

        Options = options;
        Dcm = dcm;
        userDraft = Game.Player?.Data?.challengeEditorModel;
        Active = true;

        MenuManager.instance.OpenMenu(SceneNames.ChallengeEditor, dcm);
    }

    /// <summary>
    /// Picks the session back up when the game returns to the Boss Creator editor after a custom game
    /// </summary>
    public static void Resume()
    {
        if (Active || Dcm == null) return;

        userDraft = Game.Player?.Data?.challengeEditorModel;
        Active = true;
    }

    /// <summary>
    /// Saves the setup and stops treating the Challenge Editor as the Boss Creator
    /// </summary>
    public static void Exit()
    {
        if (!Active) return;

        Save();
        RestoreUserDraft();
        BossHistory.Clear();
        Active = false;
    }

    /// <summary>
    /// Replaces the setup being edited, e.g. with one from a shared code. The editor gets the new model too, since the
    /// Boss Creator's editor is recognised by its model.
    /// </summary>
    public static void Replace(ChallengeEditor editor, BossOptions options, DailyChallengeModel dcm)
    {
        if (!Active) return;

        // Paste, Load and Undo replace the whole setup in one step. What the tabs remembered belongs to the old setup
        // (#166); Undo puts back what the new one had.
        BossHistory.RecordSetup(editor);
        BossEditorSection.Current?.ForgetAdvanced();
        Options = options;
        Dcm = dcm;
        editor.dcm = dcm;
        // After a game the game reopens the editor with the model it was opened with. That must be this one, or the
        // editor comes back as the plain Challenge Editor with the setup from before (#229).
        if (MenuManager.instance != null && MenuManager.instance.IsMenuOnStack(SceneNames.ChallengeEditor))
            MenuManager.instance.SetMenuData(SceneNames.ChallengeEditor, dcm);
        editor.ApplyDcmSettingsToUI();
        Save();
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

    /// <summary>
    /// The challenge name a new setup gets, e.g. "Custom Lych" or "Custom Elite Lych"
    /// </summary>
    public static string DefaultName(Il2CppAssets.Scripts.Data.Boss.BossType boss, bool elite = false) =>
        DefaultNames.For(PlayerNameTexts, BossList.Name(boss), elite);

    /// <summary>
    /// The name a setup with a different boss per tier gets
    /// </summary>
    public static string MixedName => DefaultNames.Mixed(PlayerNameTexts);

    /// <summary>The default names in the player's language, as the name field would get them</summary>
    private static DefaultNameTexts PlayerNameTexts => new(Raw(Texts.DefaultName), Raw(Texts.DefaultEliteName),
        Raw(Texts.MixedName));

    private static string Raw(string key) => Menus.LanguageText.WrittenText(key);

    /// <summary>
    /// The default names in every language the mod has, so a name given in another language is still known as one
    /// </summary>
    private static List<DefaultNameTexts>? allNameTexts;

    private static List<DefaultNameTexts> AllNameTexts => allNameTexts ??= LoadAllNameTexts();

    private static List<DefaultNameTexts> LoadAllNameTexts()
    {
        const string prefix = "CustomBossChallenge.Localization.";
        var assembly = typeof(BossMode).Assembly;
        var all = new List<DefaultNameTexts>();
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(prefix, System.StringComparison.Ordinal) && name.EndsWith(".json")))
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resource);
                if (stream == null) continue;
                using var reader = new System.IO.StreamReader(stream);
                var table = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
                if (table == null) continue;
                string Text(string key, string english) => table.TryGetValue(key, out var text) ? text : english;
                all.Add(new DefaultNameTexts(Text(Texts.DefaultName, DefaultNameTexts.English.Name),
                    Text(Texts.DefaultEliteName, DefaultNameTexts.English.EliteName),
                    Text(Texts.MixedName, DefaultNameTexts.English.MixedName)));
            }
            catch (System.Exception e)
            {
                ModHelper.Warning<CustomBossChallenge>($"Couldn't read the default names in {resource}: {e.Message}");
            }
        }
        return all;
    }

    /// <summary>
    /// The challenge name a new setup with these options gets
    /// </summary>
    public static string DefaultName(BossOptions options) =>
        options.Mixed ? MixedName : DefaultName(options.Boss, options.Elite);

    /// <summary>
    /// Whether a challenge still has a name the Boss Creator gave it, rather than one the player chose. A name Random
    /// made up counts too.
    /// </summary>
    public static bool IsDefaultName(string? name) =>
        RandomNames.IsGenerated(name) || DefaultNames.IsDefault(name, AllNameTexts.Append(PlayerNameTexts),
            BossList.AllNames);

    /// <summary>
    /// What a describing name for the setup looks at
    /// </summary>
    public static NameTraits NameTraits(BossOptions options, DailyChallengeModel? dcm)
    {
        var tiers = Enumerable.Range(1, options.Tiers).ToList();
        var bosses = tiers.Select(options.BossOf).ToList();
        var modifiers = dcm?.bloonModifiers;
        return new NameTraits
        {
            Boss = options.Mixed ? "" : options.Elite ? $"Elite {BossList.Name(options.Boss)}" : BossList.Name(options.Boss),
            Elite = bosses.All(boss => boss.Elite),
            Tiers = options.Tiers,
            BossSpeed = (int) System.Math.Round((modifiers?.bossSpeedMultiplier ?? 1) * 100),
            BossHealth = (int) System.Math.Round((modifiers?.healthMultipliers?.boss ?? 1) * 100),
            SpeedPerTier = options.SpeedPerTier,
            HealthPerTier = options.HealthPerTier,
            NoSkulls = tiers.All(tier => options.TierSkullPercents(tier) is {Length: 0}),
            AllFortified = modifiers?.allFortified ?? false,
            SameBoss = options.Mixed && !options.HasRandom && bosses.Select(boss => boss.Boss).Distinct().Count() == 1
                ? BossList.Name(bosses[0].Boss)
                : null
        };
    }

    /// <summary>
    /// The name Random gives a setup: a describing one when something about it stands out, otherwise the default name
    /// </summary>
    public static string RandomName(BossOptions options, DailyChallengeModel? dcm) =>
        RandomNames.For(NameTraits(options, dcm), BossRandomizer.Random) ?? DefaultName(options);

    public static DailyChallengeModel DefaultDcm(BossOptions options)
    {
        var dcm = DailyChallengeModel.CreateDefaultEditorModel();
        dcm.name = DefaultName(options);
        return dcm;
    }
}
