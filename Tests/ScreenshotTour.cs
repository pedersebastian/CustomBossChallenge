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
/// game is started (without -batchmode) with <c>--cbc.screenshots=&lt;folder&gt;</c>.
/// </summary>
public class ScreenshotTour : CustomBossTest
{
    public const string Argument = "--cbc.screenshots=";

    private static string? Folder => System.Environment.GetCommandLineArgs()
        .FirstOrDefault(arg => arg.StartsWith(Argument))?[Argument.Length..].Trim('"', '\'');

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
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
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

            BossMode.Options = new BossOptions
            {
                Boss = BossType.Vortex, Elite = true, Tiers = 7, ExtraTierHealth = 175,
                SkullMode = SkullMode.Custom, CustomSkulls = "80, 60, 40, 20"
            };
            editor!.chalName.text = editor.dcm.name = BossMode.DefaultName(BossType.Vortex);
            editor.dcm.bloonModifiers.bossSpeedMultiplier = 1.5f;
            editor.dcm.bloonModifiers.healthMultipliers.boss = 3;
            BossEditorSection.Current!.Load(editor.dcm, BossMode.Options);
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

            Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3);
            Assert(!BossMode.Active, "Leaving the Boss Creator must end boss mode");
            yield return Capture(folder, "after-back");
        }
        finally
        {
            BossMode.Store = store;
        }
    }

    private IEnumerator Capture(string folder, string name)
    {
        var path = Path.Combine(folder, name + ".png");
        yield return new WaitForEndOfFrame();

        // CaptureScreenshot(path) doesn't marshal its string through Il2Cpp interop, so encode the texture here
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
        Object.Destroy(texture);
        Log($"saved {path}");
    }
}
