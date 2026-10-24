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
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Saves screenshots of the Boss Creator button and editor for visual checks and the README. Does nothing unless the
/// game is started (without -batchmode) with <c>--cbc.screenshots=&lt;folder&gt;</c> or the environment variable
/// <c>CBC_SCREENSHOTS=&lt;folder&gt;</c>.
/// </summary>
public class ScreenshotTour : CustomBossTest
{
    public const string Argument = "--cbc.screenshots=";

    public const string EnvironmentVariable = "CBC_SCREENSHOTS";

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

        Directory.CreateDirectory(folder);
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
            yield return Capture(folder, "editor-top");

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

            // The skull preview with percentages that need decimals
            BossMode.Options = BossMode.Options with {SkullMode = SkullMode.EvenlySpaced, SkullCount = 5};
            BossEditorSection.Current!.Load(editor.dcm, BossMode.Options);
            scroll.verticalNormalizedPosition = 0.5f;
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(folder, "skull-preview");

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
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
        }
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
        BTD_Mod_Helper.ModHelper.Msg<CustomBossChallenge>($"{nameof(ScreenshotTour)}: saved {path}");
    }
}
