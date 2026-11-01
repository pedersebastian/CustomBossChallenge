using System;
using System.Collections;
using System.IO;
using System.Linq;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// "Boss per Tier" in the Boss Creator: the tick makes the Tier Preview's cards buttons, a card's picker gives the tier
/// a boss of its own, Elite or Random, the keyboard does the same, and Undo, the reset and the tick bring it all back
/// </summary>
public class MixedBossCreatorTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var folder = Path.Combine(TempFolder, "Creator");
        var store = BossMode.Store;
        var saved = BossMode.Saved;
        var escape = BossCreatorKeys.EscapePressed;
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(folder, "SavedSetups.json"));
        try
        {
            yield return Edit();
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
            BossCreatorKeys.EscapePressed = escape;
            TierBossPicker.Current?.Hide();
        }

        yield return ToMainMenu();
    }

    private IEnumerator Edit()
    {
        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitFor(() => (editor = Object.FindObjectOfType<ChallengeEditor>()) != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Boss Creator");
        yield return new WaitForSecondsRealtime(1);
        var section = BossEditorSection.Current!;
        section.ResetAll();
        section.BossButtons[BossType.Vortex].Button.onClick.Invoke();
        var card2 = section.Strip.Cards[1];

        // Off: one boss for every tier, and the cards are only a preview
        Assert(!BossMode.Options.Mixed, "Boss per Tier starts off");
        Assert(!card2.Button.interactable, "Cards can't be clicked while every tier has the Boss Type");
        Assert(!card2.Edit.gameObject.activeSelf, "No pencil on the cards while Boss per Tier is off");
        Assert(section.OpenPicker(2) == null, "No picker while Boss per Tier is off");

        section.DifferentBosses.Toggle.isOn = true;
        AssertEquals(BossMode.Options.TierBosses, "", "Boss per Tier on, every tier still following the Boss Type");
        Assert(card2.Button.interactable && card2.Edit.gameObject.activeSelf, "Cards are buttons with Boss per Tier on");
        AssertEquals(card2.Portrait.Image.color, TierStrip.Following, "A following tier's portrait is faded");
        AssertEquals(editor!.chalName.text, BossMode.MixedName, "The default name follows Boss per Tier");

        // A click on the card opens its picker; picking Lych closes it and gives tier 2 Lych
        card2.Button.onClick.Invoke();
        var picker = AssertNotNull(TierBossPicker.Current, "Clicking a card opens the picker")!;
        AssertEquals(picker.Tier, 2, "Picker tier");
        Assert(!picker.Follow.Button.interactable, "A following tier can't be made to follow again");
        Assert(picker.Choices[nameof(BossType.Vortex)].transform.Find(BossEditorSection.SelectedMarker).gameObject.activeSelf == false,
            "A following tier has no ticked boss");
        picker.Choices[nameof(BossType.Lych)].Button.onClick.Invoke();
        AssertEquals(TierBossPicker.Current, null, "Picking a boss closes the picker");
        AssertEquals(BossMode.Options.BossId(2), "Lych2", "Tier 2 has the picked boss");
        AssertEquals(BossMode.Options.BossId(1), "Vortex1", "Tier 1 still has the Boss Type");
        AssertEquals(card2.Portrait.Image.color, Color.white, "A tier with its own boss isn't faded");

        // Elite in the picker changes only that tier, and the global Elite only the tiers that follow it
        picker = section.OpenPicker(2)!;
        picker.Elite.Toggle.isOn = true;
        Assert(TierBossPicker.Current != null, "Elite keeps the picker open");
        AssertEquals(BossMode.Options.BossId(2), "LychElite2", "Elite for tier 2");
        picker.Choices[TierBossMath.Random].Button.onClick.Invoke();
        Assert(BossMode.Options.EntryOf(2) is {IsRandom: true, Elite: true}, $"Random Elite for tier 2: {BossMode.Options.TierBosses}");
        AssertEquals(section.Strip.Cards[1].Health.Text.text, "?", "A Random tier's health is unknown");
        section.Elite.Toggle.isOn = true;
        AssertEquals(BossMode.Options.BossId(1), "VortexElite1", "The global Elite changes a following tier");
        Assert(BossMode.Options.EntryOf(2).IsRandom, "The global Elite leaves a tier with its own boss alone");

        // Undo goes back one change at a time
        BossHistory.Undo(section.Editor);
        yield return null;
        AssertEquals(BossMode.Options.BossId(1), "Vortex1", "Undo of the global Elite");
        BossHistory.Undo(section.Editor);
        yield return null;
        AssertEquals(BossMode.Options.BossId(2), "LychElite2", "Undo of Random");
        Assert(!section.Strip.Cards[1].Portrait.Image.color.Equals(TierStrip.Following), "The card follows Undo");

        // The keyboard: the arrows step through the bosses, Delete makes the tier follow again
        var stop = AssertNotNull(section.FocusStops.FirstOrDefault(s => s.Name == BossEditorSection.TierStopName(3)),
            "Tier 3 is a keyboard stop")!;
        stop.Step!(1);
        AssertEquals(BossMode.Options.BossId(3), "Dreadbloon3", "Right on tier 3 steps from the Boss Type to the next boss");
        stop.Step(-1);
        stop.Step(-1);
        AssertEquals(BossMode.Options.BossId(3), "Lych3", "Left steps back");
        stop.Reset!();
        Assert(BossMode.Options.EntryOf(3).FollowsSetup, "Delete makes the tier follow the Boss Type");
        stop.Activate!();
        picker = AssertNotNull(TierBossPicker.Current, "Enter on a card opens its picker")!;
        picker.Handle(ShortcutKey.Right);
        AssertEquals(FocusFrame.Current?.transform.parent?.name, $"Choice{nameof(BossType.Vortex)}",
            "The picker's keyboard starts on the tier's boss");
        picker.Handle(ShortcutKey.Right);
        picker.Handle(ShortcutKey.Enter);
        AssertEquals(BossMode.Options.BossId(3), "Dreadbloon3", "Enter in the picker picks the framed boss");

        // Esc closes the picker, not the Boss Creator
        section.OpenPicker(4);
        BossCreatorKeys.EscapePressed = () => true;
        Assert(BossCreatorKeys.EscapeEndsFocus(), "Esc must be taken by the picker");
        BossCreatorKeys.EscapePressed = () => false;
        AssertEquals(TierBossPicker.Current, null, "Esc closes the picker");

        // Codes keep the bosses per tier
        var code = SetupCode.Encode(BossMode.Options, section.Editor.dcm);
        Assert(code.StartsWith(SetupCode.Version3Prefix), $"Boss per Tier gives a version 3 code: {code}");

        // Off and on again brings the bosses back; the boss's reset turns it off
        var mixed = BossMode.Options.TierBosses;
        section.DifferentBosses.Toggle.isOn = false;
        AssertEquals(BossMode.Options.TierBosses, null, "Off: one boss for every tier");
        AssertEquals(BossMode.Options.BossId(2), "Vortex2", "Off: tier 2 has the Boss Type");
        Assert(!card2.Button.interactable, "Off: the cards are a preview again");
        section.DifferentBosses.Toggle.isOn = true;
        AssertEquals(BossMode.Options.TierBosses, mixed, "On again: the bosses per tier come back");
        section.ResetBossButton.Button.onClick.Invoke();
        Assert(!BossMode.Options.Mixed && !section.DifferentBosses.Toggle.isOn, "Reset of the boss turns Boss per Tier off");

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);
    }
}
