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
/// "Advanced Mode" in the Boss Creator: the tick hides the Boss Type and makes the Tier Preview's cards buttons, a card's picker gives the tier
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
        Assert(!BossMode.Options.Mixed, "Advanced Mode starts off");
        Assert(section.BossTypeRow.activeInHierarchy, "The Boss Type shows while Advanced Mode is off");
        Assert(!card2.Button.interactable, "Cards can't be clicked while every tier has the Boss Type");
        Assert(!card2.Edit.gameObject.activeSelf, "No pencil on the cards while Advanced Mode is off");
        Assert(section.OpenPicker(2) == null, "No picker while Advanced Mode is off");

        // The tabs come first, the Tier Preview right under the boss, and Max Paragons after the skulls
        AssertEquals(section.Mode.Row.transform.GetSiblingIndex(), 0, "The mode tabs are the section's first row");
        AssertEquals(section.Strip.Row.transform.GetSiblingIndex(), section.BossTypeRow.transform.GetSiblingIndex() + 1,
            "The Tier Preview is right under the boss");
        Assert(!section.Mode.AdvancedPicked, "Standard is the picked tab");
        Assert(section.MaxParagons.Row.transform.GetSiblingIndex() > section.Skulls.Row.transform.GetSiblingIndex(),
            "Max Paragons comes after the skulls");

        // Advanced: every tier starts with the Boss Type, which is hidden
        section.Mode.Advanced.onClick.Invoke();
        Assert(section.Mode.AdvancedPicked, "Clicking Advanced picks its tab");
        AssertEquals(BossMode.Options.TierBosses, string.Join(",", Enumerable.Repeat("Vortex", TierMath.MaxTiers)),
            "Advanced Mode on: every tier starts with the Boss Type");
        Assert(!section.BossTypeRow.activeInHierarchy, "The Boss Type is hidden in Advanced Mode");
        Assert(card2.Button.interactable && card2.Edit.gameObject.activeSelf, "Cards are buttons in Advanced Mode");
        AssertEquals(editor!.chalName.text, BossMode.MixedName, "The default name follows Advanced Mode");

        // A click on the card opens its picker with the tier's boss ticked; picking Lych closes it
        card2.Button.onClick.Invoke();
        var picker = AssertNotNull(TierBossPicker.Current, "Clicking a card opens the picker")!;
        AssertEquals(picker.Tier, 2, "Picker tier");
        Assert(picker.Choices[nameof(BossType.Vortex)].transform.Find(BossEditorSection.SelectedMarker).gameObject.activeSelf,
            "The tier's boss is ticked");
        picker.Choices[nameof(BossType.Lych)].Button.onClick.Invoke();
        AssertEquals(TierBossPicker.Current, null, "Picking a boss closes the picker");
        AssertEquals(BossMode.Options.BossId(2), "Lych2", "Tier 2 has the picked boss");
        AssertEquals(BossMode.Options.BossId(1), "Vortex1", "Tier 1 keeps its boss");

        // Elite in the picker changes only that tier
        picker = section.OpenPicker(2)!;
        picker.Elite.Toggle.isOn = true;
        Assert(TierBossPicker.Current != null, "Elite keeps the picker open");
        AssertEquals(BossMode.Options.BossId(2), "LychElite2", "Elite for tier 2");
        AssertEquals(BossMode.Options.BossId(1), "Vortex1", "Elite for tier 2 leaves tier 1 alone");
        picker.Choices[TierBossMath.Random].Button.onClick.Invoke();
        Assert(BossMode.Options.EntryOf(2) is {IsRandom: true, Elite: true}, $"Random Elite for tier 2: {BossMode.Options.TierBosses}");
        AssertEquals(section.Strip.Cards[1].Health.Text.text, "?", "A Random tier's health is unknown");

        // Undo goes back one change at a time
        BossHistory.Undo(section.Editor);
        yield return null;
        AssertEquals(BossMode.Options.BossId(2), "LychElite2", "Undo of Random");
        BossHistory.Undo(section.Editor);
        yield return null;
        AssertEquals(BossMode.Options.BossId(2), "Lych2", "Undo of Elite");

        // The keyboard: the arrows step through the bosses, Delete puts the tier back to the boss it started with
        var stop = AssertNotNull(section.FocusStops.FirstOrDefault(s => s.Name == BossEditorSection.TierStopName(3)),
            "Tier 3 is a keyboard stop")!;
        stop.Step!(1);
        AssertEquals(BossMode.Options.BossId(3), "Dreadbloon3", "Right on tier 3 steps from Vortex to the next boss");
        stop.Step(-1);
        stop.Step(-1);
        AssertEquals(BossMode.Options.BossId(3), "Lych3", "Left steps back");
        stop.Reset!();
        AssertEquals(BossMode.Options.BossId(3), "Vortex3", "Delete puts the tier back to the boss it started with");
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
        Assert(code.StartsWith(SetupCode.Version3Prefix), $"Advanced Mode gives a version 3 code: {code}");

        // Off and on again brings the bosses back; the boss's reset turns it off
        var mixed = BossMode.Options.TierBosses;
        section.Mode.Standard.onClick.Invoke();
        Assert(!section.Mode.AdvancedPicked, "Clicking Standard picks its tab");
        AssertEquals(BossMode.Options.TierBosses, null, "Off: one boss for every tier");
        AssertEquals(BossMode.Options.BossId(2), "Vortex2", "Off: tier 2 has the Boss Type");
        Assert(!card2.Button.interactable, "Off: the cards are a preview again");
        Assert(section.BossTypeRow.activeInHierarchy, "Off: the Boss Type shows again");
        section.SetAdvanced(true);
        AssertEquals(BossMode.Options.TierBosses, mixed, "On again: the bosses per tier come back");
        section.ResetBoss();
        Assert(!BossMode.Options.Mixed && !section.Mode.AdvancedPicked, "Reset of the boss goes back to Standard");

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);
    }
}
