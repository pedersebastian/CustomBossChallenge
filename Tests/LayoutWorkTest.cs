using System.Collections;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Changing an option in the Boss Creator does only the work it needs (#184): the Tier Preview is shown once, the
/// section is only fitted into the editor again when a row was shown, hidden or changed height, and the editor's rules
/// are only copied for Undo when they changed. Each check runs within one frame, so the Boss Creator's own look at the
/// rules every half second can't add to the counts.
/// </summary>
public class LayoutWorkTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        ClickBossCreator();
        yield return WaitForBossCreator();
        var editor = UnityEngine.Object.FindObjectOfType<ChallengeEditor>();
        var section = AssertNotNull(BossEditorSection.Current, "No boss section")!;
        AssertEquals(BossMode.Options.SpeedPerTier, 0, "Speed per Tier starts at 0");
        AssertEquals(BossMode.Options.SkullMode, Config.SkullMode.Vanilla, "Skulls start at Vanilla");

        // An option that shows or hides no row: the Tier Preview once, no fitting
        var shows = TierStrip.Shows;
        var fits = BossEditorSection.Fits;
        section.SpeedPerTier.Slider.value = 5;
        AssertEquals(BossMode.Options.SpeedPerTier, 5, "Speed per Tier changed");
        AssertEquals(TierStrip.Shows - shows, 1, "Tier Preview shown for one Speed per Tier step");
        AssertEquals(BossEditorSection.Fits - fits, 0, "Section fitted for a step that shows or hides no row");

        // A change of the challenge's Boss Speed shows the Tier Preview once too
        shows = TierStrip.Shows;
        section.BossSpeed.Slider.value = 150;
        AssertEquals(TierStrip.Shows - shows, 1, "Tier Preview shown for one Boss Speed step");

        // Evenly Spaced shows the Skull Count row: fitted once
        fits = BossEditorSection.Fits;
        section.SkullMode.Dropdown.value = (int) Config.SkullMode.EvenlySpaced;
        Assert(section.SkullCount.Row.activeSelf, "Evenly Spaced shows the Skull Count row");
        AssertEquals(BossEditorSection.Fits - fits, 1, "Section fitted when a row is shown");

        // The rules are copied for Undo once, and then only when they change
        BossHistory.WatchRules(editor, now: true);
        var snapshots = BossHistory.RulesSnapshots;
        BossHistory.WatchRules(editor, now: true);
        BossHistory.WatchRules(editor, now: true);
        AssertEquals(BossHistory.RulesSnapshots - snapshots, 0, "Rules copied while they didn't change");

        var cash = editor.dcm.startRules.cash;
        editor.dcm.startRules.cash = cash + 1234;
        editor.ApplyDcmSettingsToUI();
        BossHistory.WatchRules(editor, now: true);
        AssertEquals(BossHistory.RulesSnapshots - snapshots, 1, "Rules copied once after they changed");

        // Undo still puts the rules from before the change back
        Assert(BossHistory.Undo(editor), "Undo the cash change");
        AssertEquals(editor.dcm.startRules.cash, cash, "Undo puts the cash back");
        AssertEquals(BossMode.Options.SkullMode, Config.SkullMode.EvenlySpaced, "Undoing the rules keeps the boss options");

        yield return ToMainMenu();
    }
}
