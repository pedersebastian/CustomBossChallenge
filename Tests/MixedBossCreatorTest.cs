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
using static CustomBossChallenge.Tests.TestUtils;
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

        // Each card shows its boss's skulls, and each tier's picker has its own skulls with their preview
        section.SetTierBoss(1, new TierBoss(nameof(BossType.Bloonarius), false));
        section.SetTierBoss(2, new TierBoss(nameof(BossType.Bloonarius), true));
        section.SetTierBoss(4, new TierBoss(TierBossMath.Random, false));
        yield return null;
        AssertEquals(section.Strip.Cards[0].SkullCount.Text.text, "3", "Bloonarius has 3 skulls");
        AssertEquals(section.Strip.Cards[1].SkullCount.Text.text, "7", "Elite Bloonarius has 7 skulls");
        AssertEquals(section.Strip.Cards[3].SkullCount.Text.text, "?", "A Random tier's skulls aren't known yet");
        Assert(section.Strip.Cards[0].Skulls.gameObject.activeInHierarchy, "The skull count shows in Advanced");

        // A Random die glows behind it when the tier is Elite, on the card and in the picker
        GameObject? Glow(Component icon) => icon.transform.parent.Find(BossEditorSection.EliteGlowName + icon.name)?.gameObject;
        var randomCard = section.Strip.Cards[3].Portrait;
        Assert(Glow(randomCard) is not {activeSelf: true}, "A Random tier that isn't Elite has no glow");
        section.SetTierBoss(4, new TierBoss(TierBossMath.Random, true));
        Assert(Glow(randomCard) is {activeSelf: true}, "An Elite Random tier's die glows");
        Assert(Glow(randomCard)!.transform.GetSiblingIndex() < randomCard.transform.GetSiblingIndex(), "The glow is behind the die");
        var picker4 = section.OpenPicker(4)!;
        var die = picker4.Choices[TierBossMath.Random];
        Assert(Glow(die) is {activeSelf: true}, "The picker's die glows for an Elite tier");
        picker4.Elite.Toggle.isOn = false;
        Assert(Glow(die) is not {activeSelf: true} && Glow(randomCard) is not {activeSelf: true}, "Elite off takes the glow away");
        picker4.Hide();
        Assert(!section.Skulls.Row.activeInHierarchy && !section.SkullMode.gameObject.activeInHierarchy,
            "Advanced hides the Skulls rows and the Skull Preview");
        var picker2 = section.OpenPicker(2)!;
        yield return null;
        int Shown(SkullStrip strip) => strip.Markers.Count(marker => marker.Panel.gameObject.activeSelf);
        AssertEquals(Shown(picker2.Skulls), 7, "The picker's Skull Preview has Elite Bloonarius's 7 skulls");
        AssertEquals(picker2.Skulls.Tier.Text.text, "", "The picker names its tier in its title, not on the bar");
        Assert(!picker2.SkullCount.Row.activeSelf && !picker2.CustomSkulls.gameObject.activeInHierarchy,
            "Vanilla shows neither the count nor the list");
        var vanillaHeight = picker2.Height;
        picker2.SkullMode.Dropdown.value = (int) SkullMode.EvenlySpaced;
        Assert(picker2.SkullCount.Row.activeSelf, "Evenly Spaced shows the count");
        Assert(picker2.Height > vanillaHeight, $"The picker grows for the count: {vanillaHeight} -> {picker2.Height}");
        picker2.SkullCount.Step(1);
        AssertEquals(BossMode.Options.EntryOf(2).Skulls, SkullMode.EvenlySpaced, "The picker's skull mode is the tier's");
        AssertEquals(BossMode.Options.EntryOf(2).SkullCount, 4, "The picker's count is the tier's");
        AssertEquals(Shown(picker2.Skulls), 4, "The picker's Skull Preview follows the count");
        AssertEquals(section.Strip.Cards[1].SkullCount.Text.text, "4", "The card follows the tier's skulls");
        AssertEquals(section.Strip.Cards[0].SkullCount.Text.text, "3", "Other tiers keep their skulls");
        picker2.SkullMode.Dropdown.value = (int) SkullMode.Custom;
        Assert(picker2.CustomSkulls.gameObject.activeInHierarchy && !picker2.SkullCount.Row.activeSelf, "Custom shows the list");
        picker2.CustomSkulls.InputField.onEndEdit.Invoke("30, 60, junk");
        Assert(SameValues(BossMode.Options.TierSkullPercents(2), new[] {0.6f, 0.3f}),
            $"The picker's list is the tier's: {BossMode.Options.TierBosses}");
        AssertEquals(picker2.CustomSkulls.InputField.text, "60, 30", "The list shows what was understood");
        AssertEquals(Shown(picker2.Skulls), 2, "The picker's Skull Preview follows the list");
        Assert(BossMode.Options.TierBosses!.Split(',')[1].EndsWith("#60%;30%"), $"The code keeps the skulls: {BossMode.Options.TierBosses}");
        picker2.ResetSkulls.Button.onClick.Invoke();
        AssertEquals(BossMode.Options.EntryOf(2).Skulls, SkullMode.Vanilla, "The reset gives the tier its boss's skulls back");
        AssertEquals(picker2.Height, vanillaHeight, "The picker shrinks back");
        picker2.Hide();

        // Boss Speed and Boss Health are per tier in Advanced: the sliders are hidden, and each tier's picker has its own
        Assert(!section.BossSpeed.Row.activeInHierarchy && !section.BossHealth.Row.activeInHierarchy,
            "Advanced hides Boss Speed and Boss Health");
        var picker3 = section.OpenPicker(3)!;
        yield return null;
        Assert(picker3.Speed.Row.activeInHierarchy && picker3.Health.Row.activeInHierarchy, "The picker has speed and health");
        AssertEquals(picker3.Speed.Value, 100f, "A tier starts at 100 % speed");
        picker3.Speed.Step(1);
        picker3.Speed.Step(1);
        picker3.Health.Slider.value = 250;
        AssertEquals(BossMode.Options.EntryOf(3).Speed, 120, "The picker's speed is the tier's");
        AssertEquals(BossMode.Options.EntryOf(3).Health, 250, "The picker's health is the tier's");
        AssertEquals(section.Strip.Cards[2].Speed.Text.text.StartsWith("×1.2"), true,
            $"The card shows the tier's speed: {section.Strip.Cards[2].Speed.Text.text}");
        BossHistory.Undo(section.Editor);
        yield return null;
        AssertEquals(BossMode.Options.EntryOf(3).Health, 100, "Undo takes back the health");
        AssertEquals(BossMode.Options.EntryOf(3).Speed, 120, "Two speed steps in a row are one step for Undo, kept here");
        picker3 = section.OpenPicker(3)!;
        picker3.Pick(nameof(BossType.Phayze));
        AssertEquals(BossMode.Options.EntryOf(3), new TierBoss(nameof(BossType.Phayze), BossMode.Options.EntryOf(3).Elite, 120),
            "Picking another boss keeps the tier's speed and health");

        // Going to Advanced moves Boss Speed and Boss Health into every tier, and Standard brings them back
        var standardBefore = BossMode.Options;
        section.SetAdvanced(false);
        section.ResetAll();
        section.BossSpeed.Slider.value = 150;
        section.BossHealth.Slider.value = 300;
        section.SkullMode.Dropdown.value = (int) SkullMode.EvenlySpaced;
        section.SkullCount.Slider.value = 5;
        section.SetAdvanced(true);
        Assert(TierBossMath.Parse(BossMode.Options.TierBosses).All(t => t.Speed == 150 && t.Health == 300),
            $"Every tier starts with Boss Speed and Boss Health: {BossMode.Options.TierBosses}");
        Assert(TierBossMath.Parse(BossMode.Options.TierBosses).All(t => t is {Skulls: SkullMode.EvenlySpaced, SkullCount: 5}),
            $"Every tier starts with the skulls: {BossMode.Options.TierBosses}");
        AssertEquals(section.Editor.dcm.bloonModifiers.bossSpeedMultiplier, 1f, "Boss Speed is 100 % in Advanced");
        AssertEquals(section.Editor.dcm.bloonModifiers.healthMultipliers.boss, 1f, "Boss Health is 100 % in Advanced");
        AssertEquals(BossMode.Options.SkullMode, SkullMode.Vanilla, "The Skulls option is Vanilla in Advanced");
        AssertEquals(section.Strip.Cards[0].Speed.Text.text.StartsWith("×1.5"), true, "The cards still show 150 %");
        AssertEquals(section.Strip.Cards[0].SkullCount.Text.text, "5", "The cards show the skulls");
        section.SetAdvanced(false);
        AssertEquals(section.BossSpeed.Value, 150f, "Standard brings Boss Speed back");
        AssertEquals(section.Editor.dcm.bloonModifiers.healthMultipliers.boss, 3f, "Standard brings Boss Health back");
        AssertEquals((BossMode.Options.SkullMode, BossMode.Options.SkullCount), (SkullMode.EvenlySpaced, 5), "Standard brings the skulls back");
        Assert(section.SkullCount.Row.activeInHierarchy && section.Skulls.Row.activeInHierarchy, "Standard shows the skull rows again");

        // The editor's own Reset forgets a changed lineup and the Standard values too, like Reset Boss Options
        section.ResetAll();
        section.SetAdvanced(true);
        section.SetTierBoss(2, new TierBoss(nameof(BossType.Vortex), true, 200, 50));
        section.SetAdvanced(false);
        section.Editor.clearBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);
        section.SetAdvanced(true);
        Assert(TierBossMath.Parse(BossMode.Options.TierBosses).All(t => t == new TierBoss(nameof(BossType.Bloonarius), false)),
            $"Advanced after the editor's Reset starts with Bloonarius at 100 % on every tier: {BossMode.Options.TierBosses}");
        section.SetAdvanced(false);
        AssertEquals(section.BossSpeed.Value, 100f, "Standard after the editor's Reset has Boss Speed at 100 %");

        section.ResetAll();
        section.SetAdvanced(true);
        BossMode.Options = standardBefore;
        section.Load(section.Editor.dcm, BossMode.Options);

        // Random keeps the mode: Advanced gets new bosses per tier and stays Advanced
        var randomBefore = BossMode.Options.TierBosses;
        section.Editor.randomBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        Assert(BossMode.Options.Mixed && section.Mode.AdvancedPicked, "Random in Advanced stays Advanced");
        Assert(!section.BossTypeRow.activeInHierarchy, "Random in Advanced keeps the Boss Type hidden");
        Assert(BossMode.Options.TierBosses != randomBefore, "Random in Advanced picks new bosses per tier");
        BossHistory.Undo(section.Editor);
        yield return null;
        AssertEquals(BossMode.Options.TierBosses, randomBefore, "Undo of Random in Advanced");

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
        section.Editor.randomBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        Assert(!BossMode.Options.Mixed && !section.Mode.AdvancedPicked, "Random in Standard stays Standard");
        BossHistory.Undo(section.Editor);
        yield return null;
        section.SetAdvanced(true);
        AssertEquals(BossMode.Options.TierBosses, mixed, "On again: the bosses per tier come back");
        section.ResetBoss();
        Assert(!BossMode.Options.Mixed && !section.Mode.AdvancedPicked, "Reset of the boss goes back to Standard");

        // After a reset Advanced starts over from the Standard boss, Bloonarius, not the bosses from before
        section.SetAdvanced(true);
        AssertEquals(TierBossMath.Parse(BossMode.Options.TierBosses).All(t => t.Boss == nameof(BossType.Bloonarius)), true,
            "Advanced after a reset starts with Bloonarius on every tier");

        // Tiers left as they started follow the boss picked in Standard, and so does Advanced after Reset Boss Options
        section.SetAdvanced(false);
        section.BossButtons[BossType.Lych].Button.onClick.Invoke();
        section.SetAdvanced(true);
        AssertEquals(TierBossMath.Parse(BossMode.Options.TierBosses).All(t => t.Boss == nameof(BossType.Lych)), true,
            "Unchanged tiers follow the boss picked in Standard");
        section.SetTierBoss(2, new TierBoss(nameof(BossType.Vortex), false));
        section.SetAdvanced(false);
        section.ResetAll();
        section.SetAdvanced(true);
        AssertEquals(TierBossMath.Parse(BossMode.Options.TierBosses).All(t => t.Boss == nameof(BossType.Bloonarius)), true,
            "Advanced after Reset Boss Options starts with Bloonarius on every tier");
        section.ResetBoss();

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);
    }
}
