using System.Collections;
using System.IO;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Boss Speed and Boss Health can be told apart (#306): every speed row has the speed icon and every health row the
/// heart, in Standard, in Advanced Mode's tier picker and in the Rules panel, and moving one of their sliders briefly
/// lifts the matching line on the Tier Preview's cards, on every card or only on the picked tier's
/// </summary>
public class SpeedHealthApartTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var folder = Path.Combine(TempFolder, "Creator");
        var store = BossMode.Store;
        var saved = BossMode.Saved;
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(folder, "SavedSetups.json"));
        try
        {
            yield return Check();
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
            TierBossPicker.Current?.Hide();
        }

        yield return ToMainMenu();
    }

    private IEnumerator Check()
    {
        BossMode.Enter();
        yield return WaitFor(() => Object.FindObjectOfType<ChallengeEditor>() != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Boss Creator");
        yield return new WaitForSecondsRealtime(1);
        var section = BossEditorSection.Current!;
        section.ResetAll();
        var strip = section.Strip;

        // Two different icons, one for each kind
        Assert(EditorRows.SpeedIcon != EditorRows.HealthIcon, "Speed and health rows have the same icon");
        foreach (var row in new[] {section.BossSpeed, section.SpeedPerTier})
        {
            AssertEquals(row.Icon, EditorRows.SpeedIcon, $"{row.Row.name} has the speed icon");
        }
        foreach (var row in new[] {section.BossHealth, section.HealthPerTier})
        {
            AssertEquals(row.Icon, EditorRows.HealthIcon, $"{row.Row.name} has the heart");
        }

        // Boss Speed lifts the speed line on every card, and the line goes back to its size
        Assert(!strip.Flashing, "No line is lifted before a slider moves");
        section.BossSpeed.Step(1);
        AssertEquals(strip.LastFlash, (TierStrip.Line.Speed, (int?) null), "Boss Speed lifts the speed line");
        Assert(strip.Flashing, "The speed line is lifted");
        yield return new WaitForSecondsRealtime(TierStrip.FlashTime / 2);
        Assert(strip.Cards[0].Speed.transform.localScale.x > 1.05f, "The speed line is bigger halfway");
        AssertEquals(strip.Cards[0].Health.transform.localScale.x, 1f, "The health line keeps its size while speed moves");

        // Moving Boss Health right after ends the speed line's lift
        section.BossHealth.Step(1);
        AssertEquals(strip.LastFlash, (TierStrip.Line.Health, (int?) null), "Boss Health lifts the health line");
        yield return null;
        yield return null;
        AssertEquals(strip.Cards[0].Speed.transform.localScale.x, 1f, "The speed line is back when health moves");
        yield return WaitFor(() => !strip.Flashing, "The health line's lift ends", TierStrip.FlashTime + 2);
        AssertEquals(strip.Cards[0].Health.transform.localScale.x, 1f, "The health line is back to its size");

        section.SpeedPerTier.Step(1);
        AssertEquals(strip.LastFlash, (TierStrip.Line.Speed, (int?) null), "Speed per Tier lifts the speed line");
        section.HealthPerTier.Step(1);
        AssertEquals(strip.LastFlash, (TierStrip.Line.Health, (int?) null), "Health per Tier lifts the health line");
        yield return WaitFor(() => !strip.Flashing, "The lift ends", TierStrip.FlashTime + 2);

        // Advanced Mode: the tier picker's rows have the same icons and lift only their tier's line
        section.Mode.Advanced.onClick.Invoke();
        var picker = AssertNotNull(section.OpenPicker(3), "Advanced Mode opens tier 3's picker")!;
        AssertEquals(picker.Speed.Icon, EditorRows.SpeedIcon, "The picker's speed row has the speed icon");
        AssertEquals(picker.Health.Icon, EditorRows.HealthIcon, "The picker's health row has the heart");
        picker.Health.Step(1);
        AssertEquals(strip.LastFlash, (TierStrip.Line.Health, (int?) 3), "The picker lifts only its tier's health line");
        yield return new WaitForSecondsRealtime(TierStrip.FlashTime / 2);
        Assert(strip.Cards[2].Health.transform.localScale.x > 1.05f, "Tier 3's health line is bigger halfway");
        AssertEquals(strip.Cards[1].Health.transform.localScale.x, 1f, "Tier 2's health line keeps its size");
        picker.Speed.Step(1);
        AssertEquals(strip.LastFlash, (TierStrip.Line.Speed, (int?) 3), "The picker lifts only its tier's speed line");
        yield return WaitFor(() => !strip.Flashing, "The picker's lift ends", TierStrip.FlashTime + 2);
        picker.Hide();

        // The Rules panel's per-tier lines use the same icons
        var rows = RulesPanel.ModifierRows(BossMode.Options with {SpeedPerTier = 10, HealthPerTier = 10});
        Assert(rows.Exists(row => row.Icon == EditorRows.SpeedIcon), "The Rules panel's Speed per Tier has the speed icon");
        Assert(rows.Exists(row => row.Icon == EditorRows.HealthIcon), "The Rules panel's Health per Tier has the heart");

        section.ResetAll();
        yield return null;
    }
}
