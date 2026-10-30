using System.Collections;
using System.Linq;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Patches;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.Player;
using Il2CppAssets.Scripts.Unity.UI_New.Main;
using UnityEngine;
using UnityEngine.UI;
using static CustomBossChallenge.Tests.LayoutChecks;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Lists other mods' patches on the methods this mod patches, for the compatibility tests (issue #24). Another mod
/// patching the same method is not an error, so that never fails the test. What is tested is that the report and the
/// layout check notice a conflict at all: a stand-in mod patches a guard and puts a button on top of ours.
/// </summary>
public class ModConflictTest : CustomBossTest
{
    private const string ProbeId = "cbc.conflict-probe";

    protected override IEnumerator Run()
    {
        var overlaps = ConflictReport.Find();
        Log($"{overlaps.Count} patches of other mods on methods this mod patches");
        foreach (var overlap in overlaps)
        {
            Log(overlap.ToString());
            BTD_Mod_Helper.ModHelper.Msg<CustomBossChallenge>(overlap.LogLine);
        }

        Assert(ConflictReport.OurTargets().Count(target => target.Guard) >= 10, "The guards must be marked");
        CheckProbe();
        yield return CheckButtonOnTop();
    }

    /// <summary>
    /// A prefix that runs first and skips the record guard's method, as a careless mod could do
    /// </summary>
    private void CheckProbe()
    {
        var record = AccessTools.Method(typeof(Btd6Player), nameof(Btd6Player.SetBossChallengeRecord));
        var harmony = new HarmonyLib.Harmony(ProbeId);
        try
        {
            harmony.Patch(record, new HarmonyMethod(typeof(ModConflictTest), nameof(SkipPrefix)) {priority = Priority.First});
            var found = ConflictReport.Find().Where(overlap => overlap.HarmonyId == ProbeId).ToList();
            AssertEquals(found.Count, 1, "The stand-in mod's patch must be found");
            var probe = found[0];
            AssertEquals(probe.Method, "Btd6Player.SetBossChallengeRecord", "Method");
            AssertEquals(probe.Kind, "prefix", "Kind");
            Assert(probe.Guard, "The record guard must count as a guard");
            Assert(probe.RunsFirst, "A prefix with Priority.First runs before ours");            Assert(probe.LogLine.StartsWith(ConflictReport.LogPrefix + "Btd6Player.SetBossChallengeRecord|"), probe.LogLine);
        }
        finally
        {
            harmony.UnpatchSelf();
        }

        Assert(ConflictReport.Find().All(overlap => overlap.HarmonyId != ProbeId), "The stand-in mod's patch must be gone");
    }

    private static bool SkipPrefix() => false;

    /// <summary>
    /// A button another mod puts on its own canvas, right where the Boss Creator button is
    /// </summary>
    private IEnumerator CheckButtonOnTop()
    {
        var mainMenu = AssertNotNull(Object.FindObjectOfType<MainMenu>(), "No main menu")!;
        var ours = AssertNotNull(mainMenu.transform.Find(MainMenuButton.Name), "No Boss Creator button")!;
        var ourRect = ScreenRect(ours.GetComponentInChildren<Button>(true));

        var canvasObject = new GameObject("ConflictProbeCanvas");
        try
        {
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var buttonObject = new GameObject("ConflictProbeButton");
            buttonObject.transform.SetParent(canvasObject.transform, false);
            buttonObject.AddComponent<Image>();
            buttonObject.AddComponent<Button>();
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = ourRect.min;
            rect.sizeDelta = ourRect.size / 2;
            yield return new WaitForEndOfFrame();

            var hits = OtherControls(ours).Where(control => Overlap(ourRect, control.Rect)).Select(control => control.Name).ToList();
            Assert(hits.Contains("ConflictProbeCanvas/ConflictProbeButton"),
                $"A button on top of the Boss Creator button must be found, found {string.Join(", ", hits)}");
        }
        finally
        {
            Object.Destroy(canvasObject);
        }

        yield return null;
    }
}
