using System;
using System.Collections;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The whole Boss Creator flow in the real Challenge Editor: the boss section edits the setup and the editor's
/// model, leaving saves the setup without touching the player's own editor draft, the normal editor shows no boss
/// section, and Play starts the custom game
/// </summary>
public class BossCreatorTest : CustomBossTest
{
    private string folder = "";

    protected override IEnumerator Run()
    {
        folder = Path.Combine(Path.GetTempPath(), "CustomBossChallengeTest-" + Guid.NewGuid().ToString("N"));
        var store = BossMode.Store;
        var saved = BossMode.Saved;
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(folder, "SavedSetups.json"));
        try
        {
            yield return EditSetup();
            yield return ShareCodes();
            yield return SavedSetups();
            yield return NormalEditorHasNoBossSection();
            yield return PlayLaunchesTheGame();
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
            Menus.Clipboard.Reset();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private IEnumerator EditSetup()
    {
        var userDraft = Game.Player.Data.challengeEditorModel;

        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitForEditor(e => editor = e);
        Assert(BossMode.Active, "Boss mode is not active");

        // Undo goes back to the setup the editor was opened with, boss options included
        var opened = BossMode.Options;
        var openedCash = editor!.dcm.startRules.cash;
        editor.dcm.startRules.cash = 1234;
        editor.ApplyDcmSettingsToUI();
        editor.Randomize();
        BossEditorSection.Current!.BossButtons[opened.Boss == BossType.Vortex ? BossType.Lych : BossType.Vortex].Button.onClick.Invoke();
        Assert(BossMode.Options != opened, "The boss options must have changed before Undo");
        editor.Undo();
        yield return null;
        AssertEquals(editor.dcm.startRules.cash, openedCash, "Undo must restore the editor's own values");
        AssertEquals(BossMode.Options, opened, "Undo must restore the boss options");

        // Esc (the menu's Back) in one of the editor's panels closes that panel, not the whole Boss Creator
        yield return BackClosesPanel(editor!.TowerRestrictionClicked, () => editor.towerRestrictionMenu, "Tower Restrictions");
        yield return BackClosesPanel(editor.TowerSelectClicked, () => editor.towerSelectObject, "Add towers");
        yield return BackClosesPanel(editor.MapSelectClicked, () => editor.mapSelectPanel.gameObject, "Map select");
        yield return BackClosesPanel(editor.modifierSettings.OpenEditPopup, () => editor.modifierSettings.gameObject, "Modifiers");

        var section = AssertNotNull(BossEditorSection.Current, "No boss section")!;
        Assert(section.Section.activeInHierarchy, "The boss section is hidden");
        Assert(section.Play.activeInHierarchy, "The Play button is hidden");
        // GetWorldCorners can't fill a managed array through Il2Cpp, so the edges are worked out from the rects
        float Edge(Component holder, bool right)
        {
            var rect = holder.GetComponentInChildren<Button>().GetComponent<RectTransform>();
            return rect.TransformPoint(new Vector3(right ? rect.rect.xMax : rect.rect.xMin, 0, 0)).x;
        }

        var playRight = Edge(section.Play.transform, true);
        var resetLeft = Edge(editor!.transform.Find("Reset")!, false);
        Assert(playRight < resetLeft, $"Play (right edge {playRight}) overlaps Reset (left edge {resetLeft})");
        AssertEquals(section.BossButtons.Count, Enum.GetValues<BossType>().Length, "Boss buttons");
        AssertEquals(editor!.chalName.text, BossMode.DefaultName(BossType.Bloonarius), "Default challenge name");
        // The end round always follows the last tier, so the editor's own field is hidden
        Assert(!editor.endRound.gameObject.activeSelf, "The Boss Creator shows the End Round field");
        Assert(!editor.endRound.transform.parent.Find("EndRoundLbl").gameObject.activeSelf, "The Boss Creator shows the End Round label");

        // Every boss option explains itself through an info button
        var infoButtons = 0;
        foreach (var info in section.Section.GetComponentsInChildren<Button>(true))
        {
            if (info.name == EditorRows.InfoName) infoButtons++;
        }
        AssertEquals(infoButtons, 17, "Info buttons in the boss section");

        section.Tiers.Row.transform.Find($"Image/{EditorRows.InfoName}").GetComponent<Button>().onClick.Invoke();
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitFor(() => (popup = Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()) != null,
            "Opening the info popup");
        Assert(popup!.body.text.Contains(Texts.BossTiersInfo.T()), $"Info popup text: {popup.body.text}");
        popup.TryHidePop();
        yield return new WaitForSecondsRealtime(1);

        AssertEquals(section.MaxParagons.Value, section.MaxParagons.Slider.maxValue, "Max paragons must default to unlimited");
        AssertEquals(editor.dcm.maxParagons, ParagonMath.NoLimit, "No paragon limit in a new setup");

        section.BossButtons[BossType.Vortex].Button.onClick.Invoke();
        section.Elite.SetChecked(true, true);
        section.BossSpeed.Slider.value = 150;
        section.BossHealth.Slider.value = 300;
        // All Fortified is among the editor's own modifiers, first in the excluded list
        var modifiers = AssertNotNull(FortifiedModifier.Settings(editor), "No modifier settings")!;
        var fortified = AssertNotNull(FortifiedModifier.Find(modifiers), "No All Fortified modifier")!;
        AssertEquals(fortified.transform.parent.Pointer, modifiers.excludedContainer.Pointer, "All Fortified starts excluded");
        AssertEquals(fortified.transform.GetSiblingIndex(), 0, "All Fortified comes first");
        foreach (var label in fortified.GetComponentsInChildren<TMP_Text>(true))
        {
            AssertEquals(label.text, Texts.AllFortified.T(), "All Fortified label");
        }
        modifiers.OpenEditPopup();
        yield return new WaitForSecondsRealtime(1);
        // The panels grow by a row, so all nine modifiers fit without scrolling
        var scrollArea = modifiers.excludedContainer.parent.parent.GetComponent<RectTransform>();
        var excludedSlots = modifiers.excludedContainer.GetComponent<RectTransform>();
        Assert(FortifiedModifier.ExtraHeight(modifiers) > 0, "The modifier panels must grow");
        Assert(scrollArea.rect.height >= excludedSlots.rect.height - 1,
            $"All excluded modifiers must fit: area {scrollArea.rect.height}, modifiers {excludedSlots.rect.height}");
        fortified.Select();
        AssertEquals(modifiers.descriptionTitle.text, Texts.AllFortified.T(), "All Fortified info title");
        fortified.includeToggle.isOn = true;
        yield return new WaitForSecondsRealtime(0.5f);
        AssertEquals(fortified.transform.parent.Pointer, modifiers.includedContainer.Pointer, "All Fortified moves to included");
        modifiers.FillMsbLists();
        Assert(modifiers.includedMSB.Contains(fortified), "All Fortified is listed as included");
        modifiers.CloseEditPopup();
        yield return new WaitForSecondsRealtime(0.5f);
        section.MaxParagons.Slider.value = 1;
        section.Tiers.Slider.value = 7;
        section.SpeedPerTier.Slider.value = 20;
        section.HealthPerTier.Slider.value = 50;
        section.SkullMode.Dropdown.value = (int) SkullMode.EvenlySpaced;
        section.SkullCount.Slider.value = 5;
        section.SpawnRounds.InputField.text = "30, 50";
        section.SpawnRounds.InputField.onEndEdit.Invoke("30, 50");
        yield return null;

        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Vortex, BossMode.Options.Elite), "The name must follow the boss");
        // The tier preview shows every tier with its round, speed and health, colored against the normal game
        var cards = section.Strip.Cards;
        AssertEquals(cards.Count(card => card.Panel.gameObject.activeSelf), 7, "Tier cards shown");
        AssertEquals(cards[0].Round.Text.text, "R30", "Round of tier 1");
        AssertEquals(cards[0].Speed.Text.text, "×1.5", "Speed of tier 1 (Boss Speed 150 %)");
        AssertEquals(cards[0].Health.Text.text, "125k", "Health of Vortex Elite tier 1 at 300 %");
        AssertEquals(cards[2].Speed.Text.text, "×2.1", "Speed of tier 3 (+20 % per tier)");
        AssertEquals(cards[6].Speed.Text.text, "×3.3", "Speed of tier 7");
        var tier7 = Game.instance.model.GetBloon("VortexElite5").maxHealth * TierMath.ExtraTierFactor(7) * TierMath.HealthFactor(7, 50);
        AssertEquals(cards[6].Health.Text.text, TierStrip.FormatHealth(Math.Min(tier7, TierMath.MaxModelHealth(3)) * 3), "Health of tier 7");

        // A tier above the game's limit is held to it, with a line under the cards saying so
        section.BossHealth.Slider.value = 2000;
        section.HealthPerTier.Slider.value = 200;
        section.Tiers.Slider.value = 10;
        // The game's text may put a space before the unit
        AssertEquals(string.Concat(cards[9].Health.Text.text.Where(c => !char.IsWhiteSpace(c))), "2B",
            "Health of tier 10, capped like in the game");
        Assert(section.Strip.Note.Text.text.Contains("2B"), $"The cap line shows the game's limit: {section.Strip.Note.Text.text}");
        Assert(section.Strip.Note.gameObject.activeSelf, "The cap line shows while a tier reaches the cap");
        AssertEquals(section.Strip.Row.GetComponent<RectTransform>().sizeDelta.y, TierStrip.CappedHeight, "The strip grows for the cap line");
        section.BossHealth.Slider.value = 300;
        section.HealthPerTier.Slider.value = 50;
        section.Tiers.Slider.value = 3;
        Assert(!section.Strip.Note.gameObject.activeSelf, "The cap line is hidden while no tier reaches the cap");
        AssertEquals(section.Strip.Row.GetComponent<RectTransform>().sizeDelta.y, TierStrip.Height, "The strip shrinks back without the cap line");
        section.Tiers.Slider.value = 7;
        AssertEquals(cards[0].Speed.Text.color, TierStrip.Higher, "Faster than the normal game is orange");
        section.BossSpeed.Slider.value = 50;
        AssertEquals(cards[0].Speed.Text.text, "×0.5", "The preview follows Boss Speed");
        AssertEquals(cards[0].Speed.Text.color, TierStrip.Lower, "Slower than the normal game is blue");
        section.BossSpeed.Slider.value = 150;
        section.Elite.SetChecked(false, true);
        AssertEquals(cards[0].Health.Text.text, "60k", "The preview follows Elite");
        section.Elite.SetChecked(true, true);
        Assert(section.SkullCount.Row.activeSelf, "Skull count must show for evenly spaced skulls");

        // The skull preview puts a skull at every percentage, from full health on the left to none on the right
        AssertEquals(ShownSkulls(section), "83.33%, 66.67%, 50%, 33.33%, 16.67%", "Evenly spaced skulls in the preview");
        var markers = section.Skulls.Markers.Where(marker => marker.Panel.gameObject.activeSelf).ToList();
        Assert(markers.Zip(markers.Skip(1)).All(pair =>
                pair.First.Panel.RectTransform.anchoredPosition.x < pair.Second.Panel.RectTransform.anchoredPosition.x),
            "Skulls must go from left to right");
        AssertEquals(markers[2].Panel.RectTransform.anchoredPosition.x, SkullStrip.BarWidth / 2, "50 % is in the middle of the bar");
        AssertEquals(section.Skulls.Empty.Text.text, "", "No empty text while there are skulls");
        section.SkullCount.Slider.value = 0;
        AssertEquals(ShownSkulls(section), "", "No skulls in the preview");
        AssertEquals(section.Skulls.Empty.Text.text, Texts.NoSkulls.T(), "The preview says there are no skulls");
        section.SkullCount.Slider.value = 5;

        section.SkullMode.Dropdown.value = (int) SkullMode.Custom;
        section.CustomSkulls.InputField.onEndEdit.Invoke("50, 75, 51");
        AssertEquals(ShownSkulls(section), "75%, 51%, 50%", "Custom skulls in the preview");
        var close = section.Skulls.Markers.Take(3).Select(marker => marker.Percent.RectTransform.anchoredPosition.y).ToList();
        AssertEquals(close[1], SkullStrip.TextY, "51 % far from 75 % stays on the upper line");
        AssertEquals(close[2], SkullStrip.LowerTextY, "50 % right next to 51 % goes on the lower line");
        section.CustomSkulls.InputField.onEndEdit.Invoke("75, 50, 25");

        // Vanilla shows the boss's own skulls, following the boss
        section.SkullMode.Dropdown.value = (int) SkullMode.Vanilla;
        foreach (var boss in new[] {BossType.Lych, BossType.Bloonarius, BossType.Vortex})
        {
            section.BossButtons[boss].Button.onClick.Invoke();
            var own = SkullModifier.GetSkulls(Game.instance.model.GetBloon(BossMode.Options.BossId(1)))!;
            AssertEquals(ShownSkulls(section), string.Join(", ", own.OrderByDescending(p => p).Select(SkullStrip.Format)),
                $"{boss} own skulls in the preview");
            Log($"{boss} own skulls {ShownSkulls(section)}{(section.Skulls.Tier.Text.text == "" ? "" : ", differ between tiers")}");
        }
        section.SkullMode.Dropdown.value = (int) SkullMode.EvenlySpaced;
        AssertEquals(section.SpawnRounds.InputField.text, "30, 50, 70, 90, 110, 130, 150", "Normalized spawn rounds");

        var expected = new BossOptions
        {
            Boss = BossType.Vortex, Elite = true, Tiers = 7, SpeedPerTier = 20, HealthPerTier = 50,
            SkullMode = SkullMode.EvenlySpaced, SkullCount = 5, SpawnRounds = "30, 50"
        };
        AssertEquals(BossMode.Options, expected, "Options from the boss section");

        // The two round sliders show the typed rounds, and fill them in evenly when moved
        AssertEquals(section.FirstSpawnRound.Value, 30f, "First boss round from the typed rounds");
        AssertEquals(section.RoundsBetween.Value, 20f, "Rounds between tiers from the typed rounds");
        section.FirstSpawnRound.Slider.value = 25;
        section.RoundsBetween.Slider.value = 10;
        AssertEquals(section.SpawnRounds.InputField.text, "25, 35, 45, 55, 65, 75, 85", "Spawn rounds from the sliders");
        AssertEquals(section.Strip.Cards[1].Round.Text.text, "R35", "Round of tier 2 from the sliders");
        section.SpawnRounds.InputField.onEndEdit.Invoke("30, 50");
        AssertEquals(BossMode.Options, expected, "Typed rounds replace the sliders' rounds");

        editor.ApplyUiSettingsToDCM();
        var dcm = editor.dcm;
        Assert(Approximately(dcm.bloonModifiers.bossSpeedMultiplier, 1.5f), "Boss speed in the editor's model");
        Assert(Approximately(dcm.bloonModifiers.healthMultipliers.boss, 3), "Boss health in the editor's model");
        Assert(dcm.bloonModifiers.allFortified, "All fortified in the editor's model");
        AssertEquals(dcm.maxParagons, 1, "Max paragons in the editor's model");

        section.BossSpeed.Show(100);
        editor.ApplyDcmSettingsToUI();
        AssertEquals(section.BossSpeed.Value, 150f, "The section must show the model's boss speed");

        // Loading a setup with another boss also follows it in the name, unless the player named the challenge
        editor.chalName.text = "My boss";
        section.Load(editor.dcm, BossMode.Options with {Boss = BossType.Lych});
        AssertEquals(editor.chalName.text, "My boss", "A loaded boss must keep the player's own name");
        editor.chalName.text = BossMode.DefaultName(BossType.Vortex, BossMode.Options.Elite);
        section.Load(editor.dcm, BossMode.Options with {Boss = BossType.Lych});
        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Lych, BossMode.Options.Elite), "The name must follow a loaded boss");
        section.BossButtons[BossType.Vortex].Button.onClick.Invoke();
        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Vortex, BossMode.Options.Elite), "The name must follow the boss back");

        // The name says Elite, and follows the Elite box too
        Assert(BossMode.Options.Elite, "Elite is on here");
        AssertEquals(editor.chalName.text, "Custom Elite Vortex", "The name of an Elite boss");
        section.Elite.SetChecked(false, true);
        AssertEquals(editor.chalName.text, "Custom Vortex", "The name must follow Elite off");
        section.Elite.SetChecked(true, true);
        AssertEquals(editor.chalName.text, "Custom Elite Vortex", "The name must follow Elite on");
        editor.chalName.text = "My boss";
        section.Elite.SetChecked(false, true);
        AssertEquals(editor.chalName.text, "My boss", "Elite must keep the player's own name");
        section.Elite.SetChecked(true, true);
        editor.chalName.text = editor.dcm.name = BossMode.DefaultName(BossType.Vortex, true);

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return WaitFor(() => !BossMode.Active && MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name != nameof(ChallengeEditor),
            "Leaving the Boss Creator");

        var draft = Game.Player.Data.challengeEditorModel;
        Assert(draft == null && userDraft == null || draft != null && userDraft != null && draft.Pointer == userDraft.Pointer,
            "The player's own Challenge Editor draft was replaced");

        Assert(BossMode.Store.TryLoad(out var saved, out var savedDcm), "The setup was not saved");
        AssertEquals(saved, expected, "Saved options");
        Assert(Approximately(savedDcm!.bloonModifiers.bossSpeedMultiplier, 1.5f), "Saved boss speed");
        AssertEquals(savedDcm.maxParagons, 1, "Saved max paragons");
        yield return ToMainMenu();
    }

    /// <summary>
    /// Copy puts the setup on the clipboard, and Paste loads a shared setup after asking, or explains what is wrong
    /// </summary>
    private IEnumerator ShareCodes()
    {
        var clipboard = "";
        Clipboard.Read = () => clipboard;
        Clipboard.Write = text => clipboard = text;

        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitForEditor(e => editor = e);
        var section = BossEditorSection.Current!;
        var mine = BossMode.Options;

        section.CopyCode.Button.onClick.Invoke();
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup popup = null!;
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(clipboard.Length.ToString()), $"Copied popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        var myCode = clipboard;
        Assert(SetupCode.TryDecode(myCode, out var copied, out var copiedDcm, out _), $"Copied code: {myCode}");
        AssertEquals(copied, mine, "Copied options");
        AssertEquals(copiedDcm!.maxParagons, 1, "Copied max paragons");

        // A setup from someone else
        var theirs = new BossOptions {Boss = BossType.Lych, Tiers = 3};
        var theirDcm = Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel.CreateDefaultEditorModel();
        theirDcm.name = "Shared Lych";
        theirDcm.map = "Logs";
        theirDcm.bloonModifiers.bossSpeedMultiplier = 2.5f;
        clipboard = SetupCode.Encode(theirs, theirDcm);

        // Cancel keeps the current setup
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains("Shared Lych"), $"Paste popup: {popup.body.text}");
        yield return ClosePopup(popup, false);
        AssertEquals(BossMode.Options, mine, "Cancel must keep the options");
        AssertEquals(editor!.dcm.name, BossMode.DefaultName(BossType.Vortex, BossMode.Options.Elite), "Cancel must keep the challenge");

        // Load replaces it, in the section, the editor and the saved setup
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        AssertEquals(BossMode.Options, theirs, "Loaded options");
        Assert(BossMode.Dcm != null && editor.dcm.Pointer == BossMode.Dcm.Pointer, "The editor must edit the loaded setup");
        AssertEquals(editor.dcm.name, "Shared Lych", "Loaded challenge name");
        AssertEquals(editor.dcm.map, "Logs", "Loaded map");
        AssertEquals(section.BossSpeed.Value, 250f, "Loaded boss speed shown");
        AssertEquals(section.Tiers.Value, 3f, "Loaded tiers shown");
        AssertEquals(section.MaxParagons.Value, section.MaxParagons.Slider.maxValue, "Loaded max paragons shown");
        Assert(BossMode.Store.TryLoad(out var saved, out _) && saved == theirs, "The loaded setup was not saved");

        // A broken code and one from a newer version explain themselves
        clipboard = "not a code";
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(Texts.CodeInvalid.T()), $"Invalid code popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        clipboard = "CBC7:abc";
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(Texts.CodeTooNew.T()), $"Newer code popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        AssertEquals(BossMode.Options, theirs, "A bad code must not change the setup");

        // The editor's Random button randomizes the boss options too, into a setup that can be played
        var random = BossRandomizer.Random;
        BossRandomizer.Random = new System.Random(7);
        try
        {
            var before = BossMode.Options;
            for (var i = 0; i < 10 && BossMode.Options == before; i++)
            {
                editor.randomBtn.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.5f);
            }

            Assert(BossMode.Options != before, "Random must change the boss options");
            AssertEquals(section.Tiers.Value, (float) BossMode.Options.Tiers, "Random tiers shown");
            AssertEquals(section.SpeedPerTier.Value, (float) BossMode.Options.SpeedPerTier, "Random speed per tier shown");
            AssertEquals(section.HealthPerTier.Value, (float) BossMode.Options.HealthPerTier, "Random health per tier shown");
            AssertEquals(section.SkullMode.Dropdown.value, (int) BossMode.Options.SkullMode, "Random skull mode shown");
            AssertEquals(FortifiedModifier.Find(FortifiedModifier.Settings(editor))!.includeToggle.isOn,
                editor.dcm.bloonModifiers.allFortified, "Random All Fortified shown among the modifiers");
            editor.ApplyUiSettingsToDCM();
            AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), null, "A random setup must be playable");
            Assert(Approximately(editor.dcm.bloonModifiers.bossSpeedMultiplier, section.BossSpeed.Value / 100f),
                "Random boss speed shown");
        }
        finally
        {
            BossRandomizer.Random = random;
        }

        // Back to the setup from before, for the rest of the test
        clipboard = myCode;
        section.PasteCode.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        AssertEquals(BossMode.Options, mine, "Options pasted back");
        AssertEquals(editor.dcm.maxParagons, 1, "Max paragons pasted back");

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return WaitFor(() => !BossMode.Active, "Leaving the Boss Creator");
        yield return ToMainMenu();
    }

    /// <summary>
    /// Saving setups by the challenge's name, loading one back after asking, and deleting one
    /// </summary>
    private IEnumerator SavedSetups()
    {
        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitForEditor(e => editor = e);
        var section = BossEditorSection.Current!;
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup popup = null!;
        editor!.ApplyUiSettingsToDCM();
        var before = SetupCode.Encode(BossMode.Options, editor.dcm);

        // Without saved setups the list says so, and only Save can be pressed
        AssertEquals(section.SavedList.Dropdown.options.Count, 1, "One line in an empty list");
        Assert(section.SavedList.Dropdown.captionText.text.Contains(Texts.NoSavedSetups.T()), "The empty list says so");
        Assert(!section.LoadSetup.Button.interactable && !section.DeleteSetup.Button.interactable, "Nothing to load or delete");
        AssertEquals(section.SelectedSetup, null, "Nothing picked");

        // Save stores the setup under the challenge's name
        section.BossButtons[BossType.Lych].Button.onClick.Invoke();
        section.Tiers.Slider.value = 4;
        editor.chalName.text = "My Lych";
        var lych = BossMode.Options;
        section.SaveSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains("My Lych"), $"Saved popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        Assert(BossMode.Saved.Contains("My Lych"), "The setup was not saved under the challenge's name");
        AssertEquals(section.SelectedSetup, "My Lych", "The saved setup is picked");
        Assert(section.LoadSetup.Button.interactable && section.DeleteSetup.Button.interactable, "Load and Delete work now");

        // Saving the same name again asks first; Cancel keeps the saved one
        section.Tiers.Slider.value = 6;
        section.SaveSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains("My Lych"), $"Replace popup: {popup.body.text}");
        yield return ClosePopup(popup, false);
        Assert(BossMode.Saved.TryLoad("My Lych", out var kept, out _, out _) && kept == lych, "Cancel must keep the saved setup");

        // A second setup
        section.BossButtons[BossType.Vortex].Button.onClick.Invoke();
        editor.chalName.text = "Speedy Vortex";
        var vortex = BossMode.Options;
        section.SaveSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        AssertEquals(string.Join(", ", BossMode.Saved.All.Select(entry => entry.Name)), "My Lych, Speedy Vortex", "Saved setups");
        AssertEquals(section.SavedList.Dropdown.options.Count, 2, "Both setups in the list");
        AssertEquals(section.SavedList.TemplatePanel.RectTransform.sizeDelta.y, 2 * section.SavedList.RectTransform.sizeDelta.y,
            "The opened list must have room for both setups");

        // Opening the list shows both setups, and clicking one picks it
        section.SavedList.Dropdown.Show();
        yield return new WaitForSecondsRealtime(0.5f);
        var items = section.SavedList.GetComponentsInChildren<Toggle>().Where(toggle => toggle.gameObject.activeInHierarchy).ToList();
        AssertEquals(items.Count, 2, "Setups in the opened list");
        Assert(items[0].GetComponent<RectTransform>().rect.height > 0, "The setups in the opened list must be visible");
        items[0].isOn = true;
        yield return new WaitForSecondsRealtime(0.5f);
        AssertEquals(section.SelectedSetup, "My Lych", "Clicked in the opened list");
        section.SavedList.Dropdown.value = 1;
        AssertEquals(section.SelectedSetup, "Speedy Vortex", "The new setup is picked");

        // Picking the first one and loading it asks first; Cancel keeps the current setup
        section.SavedList.Dropdown.value = 0;
        AssertEquals(section.SelectedSetup, "My Lych", "Picked in the list");
        section.LoadSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains("My Lych") && popup.body.text.Contains(Texts.TierCount.T(4)), $"Load popup: {popup.body.text}");
        yield return ClosePopup(popup, false);
        AssertEquals(BossMode.Options, vortex, "Cancel must keep the current setup");

        section.LoadSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        AssertEquals(BossMode.Options, lych, "Loaded options");
        AssertEquals(editor.dcm.name, "My Lych", "Loaded challenge name");
        AssertEquals(section.Tiers.Value, 4f, "Loaded tiers shown");

        // Delete asks first, then removes the setup from the list
        section.DeleteSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains("My Lych"), $"Delete popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        Assert(!BossMode.Saved.Contains("My Lych"), "The setup was not deleted");
        AssertEquals(section.SavedList.Dropdown.options.Count, 1, "One setup left in the list");
        AssertEquals(section.SelectedSetup, "Speedy Vortex", "The setup left is picked");

        // A damaged saved setup says so when loaded, instead of talking about the clipboard
        var store = BossMode.Saved;
        var savedFile = File.ReadAllText(store.FilePath);
        File.WriteAllText(store.FilePath, "{\"Version\":1,\"Setups\":[{\"Name\":\"Damaged\",\"Code\":\"not a code\"}]}");
        section.ShowSavedSetups();
        AssertEquals(section.SelectedSetup, "Damaged", "The damaged setup is picked");
        section.LoadSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(Texts.SetupUnreadable.T()), $"Damaged setup popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        File.WriteAllText(store.FilePath, savedFile);

        // A file that can't be written tells the player, not only the log: the folder is a file here
        BossMode.Saved = new SavedSetupsStore(Path.Combine(store.FilePath, "SavedSetups.json"));
        section.SaveSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        Assert(popup.body.text.Contains(Texts.SetupFileError.T()), $"File error popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        BossMode.Saved = store;
        section.ShowSavedSetups();
        AssertEquals(section.SelectedSetup, "Speedy Vortex", "The setups are back");

        // Back to the setup from before, for the rest of the test
        Assert(SetupCode.TryDecode(before, out var beforeOptions, out var beforeDcm, out _), "The setup from before");
        BossMode.Replace(editor, beforeOptions, beforeDcm!);

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return WaitFor(() => !BossMode.Active, "Leaving the Boss Creator");
        yield return ToMainMenu();
    }

    private IEnumerator NormalEditorHasNoBossSection()
    {
        var model = Game.Player.Data.challengeEditorModel ??
                    Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel.CreateDefaultEditorModel();
        MenuManager.instance.OpenMenu(SceneNames.ChallengeEditor, model);
        yield return WaitForEditor(_ => { });

        Assert(!BossMode.Active, "Boss mode must be off in the normal editor");
        var editor = Object.FindObjectOfType<ChallengeEditor>();
        Assert(!BossEditorSection.IsShowing(editor), "The normal Challenge Editor shows the boss section");
        var play = editor.transform.Find(BossEditorSection.PlayName);
        Assert(play == null || !play.gameObject.activeSelf, "The normal Challenge Editor shows the boss Play button");
        var title = editor.transform.Find("TopInfo/Title")?.GetComponent<TMP_Text>();
        Assert(title == null || title.text != BossEditorSection.Title, "The normal Challenge Editor is titled Boss Creator");
        AssertEquals(FortifiedModifier.Find(FortifiedModifier.Settings(editor)), null, "The normal Challenge Editor has the All Fortified modifier");
        AssertEquals(FortifiedModifier.ExtraHeight(FortifiedModifier.Settings(editor)!), 0f, "The normal editor's modifier panels must keep their size");
        Assert(editor.endRound.gameObject.activeSelf, "The normal Challenge Editor hides the End Round field");
        Assert(editor.endRound.transform.parent.Find("EndRoundLbl").gameObject.activeSelf, "The normal Challenge Editor hides the End Round label");

        MenuManager.instance.Back();
        yield return ToMainMenu();
    }

    private IEnumerator PlayLaunchesTheGame()
    {
        BossMode.Enter();
        yield return WaitForEditor(_ => { });
        Assert(BossMode.Options.Boss == BossType.Vortex, "The last setup was not loaded");

        var play = AssertNotNull(BossEditorSection.Current, "No boss section")!.Play.GetComponentInChildren<Button>();
        yield return PlayIsBlocked(play);
        play.onClick.Invoke();
        yield return WaitFor(() => InGame.instance != null && InGame.instance.bridge != null, "Loading the game", 60);
        yield return new WaitForSecondsRealtime(1);

        Assert(!BossMode.Active, "Boss mode must end when the game starts");
        Assert(CustomBossState.IsActive, "The game is not a custom boss game");
        var data = InGameData.CurrentGame;
        AssertEquals(data.bossData.bossBloon, BossType.Vortex, "Boss");
        Assert(data.bossData.bossEliteMode, "Elite");
        Assert(!data.bossData.bossRankedMode, "Ranked");
        AssertEquals(data.dcModel.maxParagons, 1, "Max paragons in the game");
        AssertEquals(TierMath.FormatRounds(InGame.instance.bridge.Simulation.map.spawner.bossBloonManager.spawnRounds),
            "29, 49, 69, 89, 109, 129, 149", "Spawn rounds (the game counts rounds from 0)");
        Assert(InGame.instance.GetGameModel().DoesBloonExist("VortexElite7"), "Tier 7 model");

        // Leaving the game returns to the editor it was started from, which must still be the Boss Creator
        InGame.instance.QuitToMainMenu();
        yield return WaitForEditor(_ => { });
        Assert(BossMode.Active, "Returning from the game must continue the Boss Creator");
        var editor = Object.FindObjectOfType<ChallengeEditor>();
        Assert(BossEditorSection.IsShowing(editor), "The boss section is missing after the game");
        AssertEquals(BossMode.Options.Boss, BossType.Vortex, "Boss after the game");
        AssertEquals(BossEditorSection.Current!.Tiers.Value, 7f, "Tiers shown after the game");

        yield return ResetClearsBossOptions(editor);

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return ToMainMenu();
    }

    /// <summary>
    /// Play explains instead of starting a game when the mode can't be used or the boss doesn't exist
    /// </summary>
    private IEnumerator PlayIsBlocked(Button play)
    {
        var editor = Object.FindObjectOfType<ChallengeEditor>();
        var mode = editor.dcm.mode;
        foreach (var unsupported in BossEditorSection.UnsupportedModes)
        {
            editor.dcm.mode = unsupported;
            AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), Texts.UnsupportedMode.T(unsupported),
                $"Playing {unsupported}");
        }
        editor.dcm.mode = mode;
        AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), null, "Playing the setup");

        // A start round on or after the first tier's round would skip it
        var startRound = editor.dcm.startRules.round;
        var firstSpawn = BossMode.Options.EffectiveSpawnRounds[0];
        editor.dcm.startRules.round = firstSpawn;
        AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), Texts.StartRoundAfterBoss.T(firstSpawn, firstSpawn),
            "Starting on the first tier's round");
        editor.dcm.startRules.round = firstSpawn - 1;
        AssertEquals(BossEditorSection.PlayProblem(editor.dcm, BossMode.Options), null, "Starting the round before the first tier");
        editor.dcm.startRules.round = startRound;

        // The unsupported modes the editor offers for this difficulty, through the real mode dropdown
        var original = editor.gameModeDropdown.value;
        foreach (var unsupported in BossEditorSection.UnsupportedModes)
        {
            var index = editor.gameModes.IndexOf(unsupported);
            if (index < 0) continue;

            editor.gameModeDropdown.value = index;
            yield return PlayShowsPopup(play, Texts.UnsupportedMode.T(unsupported));
        }
        editor.gameModeDropdown.value = original;
        editor.ApplyUiSettingsToDCM();

        var options = BossMode.Options;
        BossMode.Options = options with {Boss = (BossType) 99};
        yield return PlayShowsPopup(play, Texts.BossUnavailable.T(BossMode.Options.BossId(1)));
        BossMode.Options = options;
    }

    private IEnumerator PlayShowsPopup(Button play, string text)
    {
        play.onClick.Invoke();
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitFor(() => (popup = Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()) != null,
            $"Showing \"{text}\"");
        Assert(popup!.body.text.Contains(text), $"Popup text: {popup.body.text}");
        popup.TryHidePop();
        yield return new WaitForSecondsRealtime(1);
        Assert(InGame.instance == null && BossMode.Active, "Play must not start a game it can't play");
    }

    /// <summary>
    /// The editor's reset button also resets the boss options, back to a new setup with Bloonarius
    /// </summary>
    private IEnumerator ResetClearsBossOptions(ChallengeEditor editor)
    {
        var section = BossEditorSection.Current!;
        var fortified = FortifiedModifier.Find(FortifiedModifier.Settings(editor))!;
        fortified.includeToggle.isOn = true;
        section.MaxParagons.Slider.value = 2;
        section.SkullMode.Dropdown.value = (int) SkullMode.Custom;

        // The skull field shows what it understood, and a list without valid values keeps the previous skulls
        section.CustomSkulls.InputField.text = "25,25,50";
        section.CustomSkulls.InputField.onEndEdit.Invoke("25,25,50");
        AssertEquals(section.CustomSkulls.InputField.text, "50, 25", "Skulls shown after editing");
        AssertEquals(BossMode.Options.CustomSkulls, "50, 25", "Skulls in the setup");
        section.CustomSkulls.InputField.text = "junk";
        section.CustomSkulls.InputField.onEndEdit.Invoke("junk");
        AssertEquals(section.CustomSkulls.InputField.text, "50, 25", "Skulls shown after invalid input");
        AssertEquals(BossMode.Options.CustomSkulls, "50, 25", "Skulls in the setup after invalid input");

        editor.clearBtn.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);

        AssertEquals(BossMode.Options, new BossOptions(), "Options after reset");
        AssertEquals(editor.chalName.text, BossMode.DefaultName(BossType.Bloonarius), "Name after reset");
        Assert(section.BossButtons[BossType.Bloonarius].transform.Find(BossEditorSection.SelectedMarker).gameObject.activeSelf,
            "Bloonarius must be shown as chosen after reset");
        Assert(!section.BossButtons[BossType.Vortex].transform.Find(BossEditorSection.SelectedMarker).gameObject.activeSelf,
            "The previous boss must not be shown as chosen after reset");
        AssertEquals(section.SpeedPerTier.Value, 0f, "Speed per tier shown after reset");
        AssertEquals(section.BossSpeed.Value, 100f, "Boss speed shown after reset");
        AssertEquals(section.BossHealth.Value, 100f, "Boss health shown after reset");
        AssertEquals(section.Tiers.Value, 5f, "Tiers shown after reset");
        AssertEquals(section.CustomSkulls.InputField.text, new BossOptions().CustomSkulls, "Skulls shown after reset");
        Assert(!fortified.includeToggle.isOn, "All fortified shown after reset");
        Assert(Approximately(editor.dcm.bloonModifiers.bossSpeedMultiplier, 1), "Boss speed in the model after reset");
        Assert(Approximately(editor.dcm.bloonModifiers.healthMultipliers.boss, 1), "Boss health in the model after reset");
        Assert(!editor.dcm.bloonModifiers.allFortified, "All fortified in the model after reset");
        AssertEquals(section.MaxParagons.Value, section.MaxParagons.Slider.maxValue, "Max paragons shown after reset");
        AssertEquals(editor.dcm.maxParagons, ParagonMath.NoLimit, "Max paragons in the model after reset");
    }

    private IEnumerator BackClosesPanel(Action open, Func<GameObject> panel, string name)
    {
        open();
        yield return new WaitForSecondsRealtime(1);
        Assert(panel().activeInHierarchy, $"{name} did not open");

        MenuManager.instance.Back();
        yield return new WaitForSecondsRealtime(1);
        Assert(!panel().activeInHierarchy, $"Back did not close {name}");
        Assert(BossMode.Active && MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            $"Back in {name} left the Boss Creator");
    }

    private IEnumerator WaitForEditor(Action<ChallengeEditor> found)
    {
        ChallengeEditor? editor = null;
        yield return WaitFor(() => (editor = Object.FindObjectOfType<ChallengeEditor>()) != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Challenge Editor");
        yield return new WaitForSecondsRealtime(1);
        found(editor!);
    }

    private static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;

    /// <summary>
    /// The percentages of the skulls the preview shows, e.g. "75%, 50%"
    /// </summary>
    private static string ShownSkulls(BossEditorSection section) => string.Join(", ", section.Skulls.Markers
        .Where(marker => marker.Panel.gameObject.activeSelf)
        .Select(marker => marker.Percent.Text.text));
}
