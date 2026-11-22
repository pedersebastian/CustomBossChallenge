using System.Collections;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppNinjaKiwi.Localization;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Saves screenshots of the Boss Creator button and editor for visual checks and the README. Does nothing unless the
/// game is started (without -batchmode) with <c>--cbc.screenshots=&lt;folder&gt;</c> or the environment variable
/// <c>CBC_SCREENSHOTS=&lt;folder&gt;</c>. With <c>CBC_RESOLUTIONS=1280x800,1024x768</c> as well, the tour is taken once
/// per screen size, each in a subfolder such as <c>1024x768</c>.
/// <para>
/// The README's pictures are made from it by <c>tools/screenshots/Make-Screenshots.ps1</c> (#269): the tour is always in
/// English with fixed setups, and the screenshots the README uses are saved under the README's names.
/// </para>
/// </summary>
public class ScreenshotTour : RulesPanelTestBase
{
    public const string Argument = "--cbc.screenshots=";

    public const string EnvironmentVariable = "CBC_SCREENSHOTS";

    public const string ResolutionsVariable = "CBC_RESOLUTIONS";

    /// <summary>
    /// The screen sizes from <see cref="ResolutionsVariable"/>, e.g. "1280x800,1024x768"
    /// </summary>
    internal static (int Width, int Height)[] Resolutions =>
        (System.Environment.GetEnvironmentVariable(ResolutionsVariable) ?? "")
        .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
        .Select(size => size.Split('x'))
        .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
        .Select(parts => (int.Parse(parts[0]), int.Parse(parts[1])))
        .ToArray();

    internal static string? Folder => System.Environment.GetCommandLineArgs()
        .FirstOrDefault(arg => arg.StartsWith(Argument))?[Argument.Length..].Trim('"', '\'') ??
        System.Environment.GetEnvironmentVariable(EnvironmentVariable);

    protected override IEnumerator Run()
    {
        var folder = Folder;
        if (string.IsNullOrEmpty(folder))
        {
            Log("skipped, no screenshot folder given");
            yield break;
        }

        var resolutions = Resolutions;
        if (resolutions.Length == 0)
        {
            yield return Tour(folder);
            yield break;
        }

        var (width, height, mode) = (UnityEngine.Screen.width, UnityEngine.Screen.height, UnityEngine.Screen.fullScreenMode);
        try
        {
            foreach (var (w, h) in resolutions)
            {
                UnityEngine.Screen.SetResolution(w, h, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(2);
                Log($"tour at {UnityEngine.Screen.width}x{UnityEngine.Screen.height}");
                yield return Tour(Path.Combine(folder, $"{w}x{h}"));
                yield return ToMainMenu();
            }
        }
        finally
        {
            UnityEngine.Screen.SetResolution(width, height, mode);
        }
    }

    private IEnumerator Tour(string folder)
    {
        Directory.CreateDirectory(folder);

        // Always in English, whatever the player's language, so the README's pictures don't change with it
        var language = LocalizationManager.Instance.ActiveLanguage;
        if (language != Language.English)
        {
            var task = LocalizationManager.Instance.UpdateLanguageAsync(Language.English);
            yield return WaitFor(() => task.IsCompleted, "Switching to English", 30);
        }

        yield return new WaitForSecondsRealtime(3);
        yield return Capture(folder, "main-menu");

        // Use a throwaway setup file so the screenshots don't change the player's own last setup
        var store = BossMode.Store;
        var saved = BossMode.Saved;
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(folder, "SavedSetups.json"));
        if (File.Exists(BossMode.Saved.FilePath)) File.Delete(BossMode.Saved.FilePath);
        try
        {
            BossMode.Enter();
            ChallengeEditor? editor = null;
            for (var i = 0; i < 40 && editor == null; i++)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                editor = Object.FindObjectOfType<ChallengeEditor>();
            }

            AssertNotNull(editor, "The Boss Creator did not open");
            yield return new WaitForSecondsRealtime(2);
            yield return Capture(folder, "editor-default");

            BossEditorSection.Current!.BossButtons[BossType.Vortex].Button.onClick.Invoke();
            BossMode.Options = new BossOptions
            {
                Boss = BossType.Vortex, Elite = true, Tiers = 7, SpeedPerTier = 15, HealthPerTier = 40,
                SkullMode = SkullMode.Custom, CustomSkulls = "80, 60, 40, 20"
            };
            editor!.dcm.bloonModifiers.bossSpeedMultiplier = 1.5f;
            editor.dcm.bloonModifiers.healthMultipliers.boss = 3;
            editor.dcm.maxParagons = 1;
            editor.dcm.bloonModifiers.allFortified = true;
            editor.dcm.bloonModifiers.allCamo = true;
            BossEditorSection.Current!.Load(editor.dcm, BossMode.Options);
            editor.chalName.text = editor.dcm.name = "Tanky Elite Vortex";
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "boss-creator");

            EditorRows.ShowInfo(Texts.BossTiers, Texts.BossTiersInfo);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture(folder, "info-popup");
            Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()?.TryHidePop();
            yield return new WaitForSecondsRealtime(1);

            var scroll = editor.GetComponentInChildren<ScrollRect>();
            foreach (var position in new[] {0.75f, 0.5f, 0f})
            {
                scroll.verticalNormalizedPosition = position;
                yield return new WaitForSecondsRealtime(1);
                yield return Capture(folder, $"editor-{(int) (100 - position * 100)}");
            }

            // The README's views of the sliders, the Tier Preview and the Share Code row
            var creator = BossEditorSection.Current!;
            ScrollTo(scroll, creator.BossSpeed.Row.GetComponent<RectTransform>(), 0.13f);
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "boss-options");
            ScrollTo(scroll, creator.Strip.Panel.RectTransform, 0.75f);
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "tier-preview");
            // Low enough that Past Events, whose download date changes every day, is out of view
            ScrollTo(scroll, creator.CopyCode.RectTransform, 0.93f);
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "share-code");

            // The skull preview with percentages that need decimals
            BossMode.Options = BossMode.Options with {SkullMode = SkullMode.EvenlySpaced, SkullCount = 5};
            BossEditorSection.Current!.Load(editor.dcm, BossMode.Options);
            scroll.verticalNormalizedPosition = 0.5f;
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "skull-preview");

            // Advanced Mode: the Tier Preview's cards with their own bosses, and a card's boss picker
            BossMode.Options = BossMode.Options with
            {
                SkullMode = SkullMode.Vanilla, TierBosses = "Bloonarius,Lych#4,RandomElite,,PhayzeElite,Diamondback"
            };
            BossEditorSection.Current!.Load(editor.dcm, BossMode.Options);
            editor.chalName.text = editor.dcm.name = "Boss Gauntlet";
            scroll.verticalNormalizedPosition = 1f;
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "tier-bosses");
            BossEditorSection.Current!.OpenPicker(2);
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "tier-boss-picker");
            TierBossPicker.Current?.Hide();
            BossEditorSection.Current!.OpenPicker(3);
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "tier-boss-picker-random-elite");
            TierBossPicker.Current?.Hide();
            BossMode.Options = BossMode.Options with {TierBosses = null};
            BossEditorSection.Current!.Load(editor.dcm, BossMode.Options);

            // A few saved setups, and the question before loading one
            var section = BossEditorSection.Current!;
            foreach (var (name, boss, tiers) in new[] {("Lych marathon", BossType.Lych, 10), ("Speedy Vortex", BossType.Vortex, 5), ("Phayze practice", BossType.Phayze, 3)})
            {
                var setup = editor.dcm.Clone();
                setup.name = name;
                BossMode.Saved.Save(new BossOptions {Boss = boss, Elite = true, Tiers = tiers}, setup);
            }
            section.ShowSavedSetups();
            scroll.verticalNormalizedPosition = 0.4f;
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "saved-setups");
            section.SavedList.Dropdown.Show();
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "saved-setups-open");
            section.SavedList.Dropdown.Hide();
            yield return new WaitForSecondsRealtime(1);
            section.LoadSetup.Button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture(folder, "saved-setups-load");
            Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()?.CancelClicked();
            yield return new WaitForSecondsRealtime(1);

            // All Fortified among the editor's own modifiers
            var modifiers = FortifiedModifier.Settings(editor);
            if (modifiers != null)
            {
                modifiers.ApplyDCMtoUI(editor.dcm);
                modifiers.OpenEditPopup();
                yield return new WaitForSecondsRealtime(1.5f);
                FortifiedModifier.Find(modifiers)?.Select();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capture(folder, "modifiers");
                modifiers.CloseEditPopup();
                yield return new WaitForSecondsRealtime(1);
            }

            Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3);
            Assert(!BossMode.Active, "Leaving the Boss Creator must end boss mode");
            yield return Capture(folder, "after-back");
            yield return MixedGame(folder);
            yield return RulesPanels(folder);
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
            if (language != Language.English) LocalizationManager.Instance.UpdateLanguageAsync(language);
        }

        if (language != Language.English)
            yield return WaitFor(() => LocalizationManager.Instance.ActiveLanguage == language, "Going back to the player's language", 30);
    }

    /// <summary>
    /// The pause screen's Rules panel of a Standard Mode game and of an Advanced Mode line-up with two Random tiers,
    /// set up like <see cref="RulesPanelTest"/> and <see cref="RulesPanelAdvancedTest"/>
    /// </summary>
    private IEnumerator RulesPanels(string folder)
    {
        var dcm = TestUtils.BaseDcm();
        dcm.bloonModifiers.healthMultipliers.boss = 3.69f;
        dcm.bloonModifiers.bossSpeedMultiplier = 1.3f;
        dcm.disableSelling = true;
        dcm.disableMK = true;
        yield return Play(new BossOptions
        {
            Boss = BossType.Vortex, Elite = true, Tiers = 6, SpawnRounds = "40, 50, 60, 70, 80, 90", SpeedPerTier = 10,
            HealthPerTier = -5, SkullMode = SkullMode.EvenlySpaced, SkullCount = 4, BossEventRounds = true
        }, dcm);
        yield return OpenRules();
        yield return new WaitForSecondsRealtime(1);
        yield return Capture(folder, "rules-panel");
        yield return CloseRules();

        yield return Play(new BossOptions
        {
            Tiers = 10,
            TierBosses = "Lych,VortexElite@120/80,Random,Phayze#4,,Bloonarius#60%;30%,DreadbloonElite,Blastapopoulos,Random,Lych@200/150"
        });
        yield return OpenRules();
        yield return new WaitForSecondsRealtime(1);
        yield return Capture(folder, "rules-panel-advanced");
        yield return CloseRules();
        yield return ToMainMenu();
    }

    /// <summary>
    /// Scrolls so that the middle of <paramref name="target"/> is <paramref name="at"/> (0 top, 1 bottom) down the view
    /// </summary>
    internal static void ScrollTo(ScrollRect scroll, RectTransform target, float at)
    {
        Canvas.ForceUpdateCanvases();
        var content = scroll.content;
        var view = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
        var scrollable = content.rect.height - view.rect.height;
        if (scrollable <= 0) return;
        var fromTop = content.rect.yMax - content.InverseTransformPoint(target.TransformPoint(target.rect.center)).y;
        scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01((fromTop - at * view.rect.height) / scrollable);
    }

    /// <summary>
    /// A game with a different boss per tier: the banner when a boss arrives and the line-up of every tier's boss
    /// </summary>
    private IEnumerator MixedGame(string folder)
    {
        var options = new BossOptions {Tiers = 5, TierBosses = "Lych,VortexElite,Diamondback,Phayze,BloonariusElite"};
        var dcm = TestUtils.BaseDcm();
        dcm.startRules.round = 39;
        dcm.startRules.cash = 20000;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        yield return new WaitForSecondsRealtime(2);

        manager.SpawnBoss(1, false);
        yield return new WaitForSecondsRealtime(1.5f);
        yield return Capture(folder, "boss-arrival");

        // The first tier defeated, the second one on its way
        manager.CurrentBoss?.Destroy();
        CustomBossState.OnBossDefeated(1);
        yield return new WaitForSecondsRealtime(0.5f);
        manager.SpawnBoss(2, false);
        yield return new WaitForSecondsRealtime(Visuals.BossHud.BannerSeconds + 1);
        yield return Capture(folder, "boss-lineup");
        yield return ToMainMenu();
    }

    internal static IEnumerator Capture(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".png");
        yield return new WaitForEndOfFrame();

        // CaptureScreenshot(path) doesn't marshal its string through Il2Cpp interop, so encode the texture here
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
        Object.Destroy(texture);
        File.WriteAllText(Path.Combine(folder, name + ".regions.json"), Regions());
        BTD_Mod_Helper.ModHelper.Msg<CustomBossChallenge>($"{nameof(ScreenshotTour)}: saved {path}");
    }

    /// <summary>
    /// Where this mod's own UI is in the screenshot, in pixels from the top left, so .github/scripts/Compare-Screenshots.ps1
    /// can tell changes to it from changes elsewhere (e.g. another mod's own button)
    /// </summary>
    private static string Regions()
    {
        var regions = new System.Collections.Generic.List<string>();
        void AddRect(string name, Rect rect)
        {
            rect = Rect.MinMaxRect(Mathf.Max(rect.xMin, 0), Mathf.Max(rect.yMin, 0), Mathf.Min(rect.xMax, UnityEngine.Screen.width), Mathf.Min(rect.yMax, UnityEngine.Screen.height));
            if (rect.width <= 0 || rect.height <= 0) return;
            regions.Add($"{{\"name\":\"{name}\",\"x\":{rect.xMin:0},\"y\":{UnityEngine.Screen.height - rect.yMax:0},\"width\":{rect.width:0},\"height\":{rect.height:0}}}");
        }
        void Add(string name, Component? component)
        {
            if (component == null || !component.gameObject.activeInHierarchy) return;
            AddRect(name, LayoutChecks.ScreenRect(component.GetComponent<RectTransform>()));
        }

        Add("boss-creator-button", Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.Main.MainMenu>()?.transform.Find(MainMenuButton.Name));
        var section = BossEditorSection.Current;
        if (section != null)
        {
            Add("boss-section", section.Section.transform);
            Add("play", section.Play.transform);

            // The Saved Setups and Share Code rows across the section, which the README shows on their own
            if (section.SavedList.gameObject.activeInHierarchy && section.PasteCode.gameObject.activeInHierarchy)
            {
                var box = LayoutChecks.ScreenRect(section.Section.GetComponent<RectTransform>());
                var top = LayoutChecks.ScreenRect(section.SavedList.RectTransform);
                var bottom = LayoutChecks.ScreenRect(section.PasteCode.RectTransform);
                AddRect("saved-setups", Rect.MinMaxRect(box.xMin, Mathf.Min(top.yMin, bottom.yMin), box.xMax, Mathf.Max(top.yMax, bottom.yMax)));
            }
        }

        return $"{{\"width\":{UnityEngine.Screen.width},\"height\":{UnityEngine.Screen.height},\"regions\":[{string.Join(",", regions)}]}}";
    }
}
