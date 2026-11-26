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
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
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
        Assert(!card2.Button!.interactable, "Cards can't be clicked while every tier has the Boss Type");
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
        Assert(card2.Button!.interactable && card2.Edit.gameObject.activeSelf, "Cards are buttons in Advanced Mode");
        AssertEquals(editor!.chalName.text, BossMode.MixedName, "The default name follows Advanced Mode");

        // A click on the card opens its picker with the tier's boss ticked; picking Lych closes it
        card2.Button!.onClick.Invoke();
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
        picker3.Health.DragTo(250);
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
        section.BossHealth.DragTo(300);
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
        // With a new setup's Boss Event Rounds (#251), the version that has both
        Assert(code.StartsWith(SetupCode.Version4Prefix), $"Advanced Mode with Boss Event Rounds gives a version 4 code: {code}");
        Assert(SetupCode.Encode(BossMode.Options with {BossEventRounds = false}, section.Editor.dcm).StartsWith(SetupCode.Version3Prefix),
            "Advanced Mode without Boss Event Rounds gives a version 3 code");

        // Off and on again brings the bosses back; the boss's reset turns it off
        var mixed = BossMode.Options.TierBosses;
        section.Mode.Standard.onClick.Invoke();
        Assert(!section.Mode.AdvancedPicked, "Clicking Standard picks its tab");
        AssertEquals(BossMode.Options.TierBosses, null, "Off: one boss for every tier");
        AssertEquals(BossMode.Options.BossId(2), "Vortex2", "Off: tier 2 has the Boss Type");
        Assert(!card2.Button!.interactable, "Off: the cards are a preview again");
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
        yield return NewSetupForgetsTabs(section);

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);
    }

    /// <summary>
    /// What the tabs remember belongs to one setup: a loaded, pasted or random setup starts over, and Undo brings back
    /// what the setup had then (#166)
    /// </summary>
    private IEnumerator NewSetupForgetsTabs(BossEditorSection section)
    {
        var editor = section.Editor;
        float Speed() => editor.dcm.bloonModifiers.bossSpeedMultiplier;
        bool AllTiers(BossType boss) => TierBossMath.Parse(BossMode.Options.TierBosses).All(t => t.Boss == boss.ToString());
        var lych = new BossOptions {Boss = BossType.Lych};

        // A: an edited lineup, Standard, another setup loaded, Advanced: the tiers are the loaded setup's
        section.ResetAll();
        section.SetAdvanced(true);
        section.SetTierBoss(2, new TierBoss(nameof(BossType.Vortex), false));
        var edited = BossMode.Options.TierBosses;
        section.SetAdvanced(false);
        BossMode.Replace(editor, lych, BossMode.DefaultDcm(lych));
        section.SetAdvanced(true);
        Assert(AllTiers(BossType.Lych), $"A: Advanced after loading a setup has its boss on every tier: {BossMode.Options.TierBosses}");

        // Undo puts back the setup from before the load, and with it the lineup it remembered
        BossHistory.Undo(editor);
        BossHistory.Undo(editor);
        Assert(!BossMode.Options.Mixed, "A: Undo goes back to Standard from before the load");
        section.SetAdvanced(true);
        AssertEquals(BossMode.Options.TierBosses, edited, "A: after Undo, Advanced brings back the edited lineup");

        // B: Boss Speed 200 % in Standard, Advanced, an Advanced code pasted, Standard: the pasted setup's own speed
        section.ResetAll();
        section.BossSpeed.Slider.value = 200;
        section.SetAdvanced(true);
        var advanced = new BossOptions {Boss = BossType.Phayze, TierBosses = "Phayze,Lych"};
        var code = SetupCode.Encode(advanced, BossMode.DefaultDcm(advanced));
        Assert(SetupCode.TryDecode(code, out var pasted, out var pastedDcm, out _) && pastedDcm != null, "B: the code reads");
        BossMode.Replace(editor, pasted, pastedDcm!);
        section.SetAdvanced(false);
        Assert(Approximately(Speed(), 1), $"B: Standard after pasting keeps the pasted setup's Boss Speed, not 200 %: {Speed()}");
        AssertEquals(section.BossSpeed.Value, 100f, "B: the slider shows the pasted setup's Boss Speed");

        // C: Random starts over too
        section.ResetAll();
        section.SetAdvanced(true);
        section.SetTierBoss(2, new TierBoss(nameof(BossType.Vortex), false));
        section.SetAdvanced(false);
        editor.randomBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        var randomBoss = BossMode.Options.Boss;
        section.SetAdvanced(true);
        Assert(AllTiers(randomBoss), $"C: Advanced after Random has the random boss on every tier: {BossMode.Options.TierBosses}");

        // C: Ctrl+Z across a tab switch brings back what the tabs remembered then, here Standard's 200 %
        section.ResetAll();
        section.BossSpeed.Slider.value = 200;
        section.SetAdvanced(true);
        section.SetTierBoss(2, new TierBoss(nameof(BossType.Vortex), false));
        section.SetAdvanced(false);
        Assert(Approximately(Speed(), 2), $"C: Standard brings back its 200 %: {Speed()}");
        BossHistory.Undo(editor);
        Assert(BossMode.Options.Mixed, "C: Undo goes back to Advanced");
        section.SetAdvanced(false);
        Assert(Approximately(Speed(), 2), $"C: Standard after Undo still brings back its 200 %: {Speed()}");

        // One setup still keeps its lineup while the tabs switch back and forth
        section.SetAdvanced(true);
        var lineup = BossMode.Options.TierBosses;
        Assert(TierBossMath.Parse(lineup)[1].Boss == nameof(BossType.Vortex), $"Tier 2 keeps Vortex: {lineup}");
        section.SetAdvanced(false);
        section.SetAdvanced(true);
        AssertEquals(BossMode.Options.TierBosses, lineup, "Switching tabs on one setup keeps the lineup");
        section.ResetAll();
    }
}

/// <summary>
/// After a game with Random tiers the Boss Creator plays the setup again with the bosses Random picked, and copies a
/// code with them (#270). The setup keeps its Random tiers; a pasted code shows those tiers as Random, Play keeps their
/// bosses, and picking a boss for such a tier makes it a normal tier.
/// </summary>
public class LastGameBossesCreatorTest : CustomBossTest
{
    private string clipboard = "";

    protected override IEnumerator Run()
    {
        var folder = Path.Combine(TempFolder, "LastGame");
        var store = BossMode.Store;
        var saved = BossMode.Saved;
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(folder, "SavedSetups.json"));
        Clipboard.Read = () => clipboard;
        Clipboard.Write = text => clipboard = text;
        try
        {
            yield return PlayAgain();
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
            Clipboard.Reset();
            LastGame.Clear();
            TierBossPicker.Current?.Hide();
        }

        yield return ToMainMenu();
    }

    private IEnumerator OpenCreator()
    {
        yield return ToMainMenu();
        BossMode.Enter();
        yield return WaitFor(() => BossEditorSection.Current?.Editor != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Boss Creator", 30);
        yield return new WaitForSecondsRealtime(1);
    }

    /// <summary>Presses a button that starts a game, and quits it back to the Boss Creator</summary>
    private IEnumerator PlayAndQuit(UnityEngine.UI.Button button, Action check)
    {
        button.onClick.Invoke();
        yield return WaitFor(() => InGame.instance?.bridge?.Simulation != null && !InGame.instance.quitting &&
                                   MenuManager.instance?.IsTransitioning == false, "Loading the game", LoadTimeout);
        yield return new WaitForSecondsRealtime(1);
        check();
        InGame.instance.QuitToMainMenu();
        yield return WaitFor(() => InGame.instance == null && BossEditorSection.Current?.Editor != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor) &&
                                   !MenuManager.instance.IsTransitioning, "Back in the Boss Creator", 60);
        yield return new WaitForSecondsRealtime(1);
    }

    private IEnumerator PlayAgain()
    {
        LastGame.Clear();
        yield return OpenCreator();
        var section = BossEditorSection.Current!;
        section.ResetAll();
        section.SetAdvanced(true);
        section.SetTierBoss(2, new TierBoss(TierBossMath.Random, true));
        section.SetTierBoss(4, new TierBoss(TierBossMath.Random, false));
        section.Editor.dcm.disableSaves = true;
        var setup = BossMode.Options;
        Assert(!section.LastGameRow.activeSelf, "No last game's bosses before a game");

        // Play: the Random tiers get their bosses, and back in the Boss Creator the row offers them again
        BossOptions? played = null;
        yield return PlayAndQuit(section.PlayButton, () => played = CustomBossState.ActiveOptions);
        Assert(played is {HasRandom: false} && played.RandomTiers == "2,4", $"The game picked tiers 2 and 4: {played?.TierBosses}, {played?.RandomTiers}");
        section = BossEditorSection.Current!;
        AssertEquals(BossMode.Options, setup, "The setup keeps its Random tiers");
        Assert(section.LastGameRow.activeSelf, "After a game with Random tiers the row is shown");
        AssertEquals(section.FocusStops.Any(stop => stop.Name == "ReplayLineup"), true, "Tab reaches Play Again");

        // Copy: a version 7 code with those bosses
        section.CopyLineup.Button.onClick.Invoke();
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitForPopup(found => popup = found);
        var code = clipboard;
        // Version 8 when Random picked the mod's own Gildebloon, which the player may have switched on (#25)
        Assert(code.StartsWith(SetupCode.PrefixFor(played!)) && CodeFormat.HoldsRandomTiers(code), $"A version 7 code was copied: {code}");
        Assert(SetupCode.TryDecode(code, out var fromCode, out _, out _), "The copied code loads");
        AssertEquals(fromCode, played, "The copied code has the bosses the game picked");
        yield return ClosePopup(popup!, true);
        section.CopyCode.Button.onClick.Invoke();
        yield return WaitForPopup(found => popup = found);
        Assert(SetupCode.TryDecode(clipboard, out var plain, out _, out _) && plain == setup, "Copy still copies the setup, Random and all");
        yield return ClosePopup(popup!, true);

        // Play Again: the same bosses, and the setup is still the one with Random tiers
        yield return PlayAndQuit(section.ReplayLineup.Button,
            () => AssertEquals(CustomBossState.ActiveOptions, played, "Play Again has the same bosses"));
        section = BossEditorSection.Current!;
        AssertEquals(BossMode.Options, setup, "Play Again leaves the setup as it was");
        Assert(section.LastGameRow.activeSelf, "The row stays after playing again");

        // A changed setup is another setup: the row goes, and Undo brings it back
        section.SetTierBoss(1, new TierBoss(nameof(BossType.Lych), false));
        Assert(!section.LastGameRow.activeSelf, "A changed setup hides the last game's bosses");
        BossHistory.Undo(section.Editor);
        yield return null;
        Assert(BossEditorSection.Current!.LastGameRow.activeSelf, "Undo brings the row back");

        // The pasted code: its tiers show as Random, Play keeps their bosses
        section = BossEditorSection.Current!;
        clipboard = code;
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(found => popup = found);
        yield return ClosePopup(popup!, true);
        AssertEquals(BossMode.Options, played, "The pasted code's bosses");
        Assert(!section.LastGameRow.activeSelf, "The pasted setup isn't the one the last game came from");
        AssertEquals(section.Strip.Cards[1].SkullCount.Text.text, "?", "A tier Random picked keeps its skulls a secret");
        AssertEquals(BossGameLauncher.Resolve(BossMode.Options), played, "Play keeps the pasted bosses");

        // The picker shows Random ticked; picking a boss makes it a normal tier, the other stays Random
        var picker = TierBossPicker.Open(section, 2);
        var marker = picker.Choices[TierBossMath.Random].transform.Find(BossEditorSection.SelectedMarker);
        Assert(marker != null && marker.gameObject.activeSelf, "The picker of a tier Random picked ticks Random");
        picker.Pick(nameof(BossType.Phayze));
        Assert(!BossMode.Options.WasRandom(2) && BossMode.Options.WasRandom(4), $"Tier 2 is a normal tier now: {BossMode.Options.RandomTiers}");
        AssertEquals(BossMode.Options.BossId(2), "PhayzeElite2", "The picked boss keeps the tier's Elite");
    }
}
