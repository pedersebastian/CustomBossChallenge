using System.Collections;
using System.IO;
using System.Linq;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The example setups come with the mod: every one can be played and is translated, and the Saved Setups list shows
/// them after the player's own setups, marked as examples, to load but not to delete. They are never written to the
/// saved setups file until the player saves one as their own.
/// </summary>
public class ExampleSetupsTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        foreach (var example in ExampleSetups.All)
        {
            var (options, dcm) = ExampleSetups.Build(example);
            AssertEquals(BossEditorSection.PlayProblem(dcm, options), null, $"Example {example.EnglishName} can't be played");
            Assert(example.Name != example.NameKey && example.Description != example.DescriptionKey,
                $"Example {example.EnglishName} is not translated");
            AssertEquals(dcm.name, example.Name, $"Example {example.EnglishName} is named in the player's language");
        }

        var store = BossMode.Store;
        var saved = BossMode.Saved;
        BossMode.Store = new LastSetupStore(Path.Combine(TempFolder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(TempFolder, "SavedSetups.json"));
        try
        {
            yield return InTheList();
        }
        finally
        {
            BossMode.Store = store;
            BossMode.Saved = saved;
        }
    }

    private IEnumerator InTheList()
    {
        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitFor(() => (editor = Object.FindObjectOfType<ChallengeEditor>()) != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Challenge Editor");
        yield return new WaitForSecondsRealtime(1);
        var section = BossEditorSection.Current!;
        var dropdown = section.SavedList.Dropdown;
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup popup = null!;

        // Only the examples, each marked as one
        var label = Texts.ExampleLabel.T("").Trim();
        AssertEquals(dropdown.options.Count, ExampleSetups.All.Count, "The examples in an empty list");
        for (var i = 0; i < ExampleSetups.All.Count; i++)
        {
            var line = dropdown.options[i].text;
            Assert(line.Contains(label) && line.Contains(ExampleSetups.All[i].Name) && line.Contains(BossEditorSection.ExampleColor),
                $"Example line {i} must be marked as an example: {line}");
        }

        // Every example loads like a saved setup, showing its description first
        for (var i = 0; i < ExampleSetups.All.Count; i++)
        {
            var example = ExampleSetups.All[i];
            dropdown.value = i;
            AssertEquals(section.SelectedExample, example, $"Picked {example.EnglishName}");
            var caption = dropdown.captionText;
            caption.ForceMeshUpdate();
            Assert(caption.textBounds.size.x <= caption.rectTransform.rect.width + 1,
                $"{example.EnglishName} runs past the list: {caption.textBounds.size.x} > {caption.rectTransform.rect.width}");
            Assert(caption.fontSize >= BossEditorSection.SavedListMinFontSize, $"{example.EnglishName} is too small");
            Assert(section.LoadSetup.Button.interactable && !section.DeleteSetup.Button.interactable,
                $"{example.EnglishName} can be loaded but not deleted");
            section.LoadSetup.Button.onClick.Invoke();
            yield return WaitForPopup(p => popup = p);
            Assert(popup.body.text.Contains(example.Description), $"Load popup of {example.EnglishName}: {popup.body.text}");
            yield return ClosePopup(popup, true);
            AssertEquals(BossMode.Options, example.Options, $"Loaded options of {example.EnglishName}");
            AssertEquals(editor!.dcm.name, example.Name, $"Loaded name of {example.EnglishName}");
        }

        Assert(!File.Exists(BossMode.Saved.FilePath), "Loading examples must not write the saved setups file");

        // Saved as the player's own, it comes first; the examples stay
        section.SaveSetup.Button.onClick.Invoke();
        yield return WaitForPopup(p => popup = p);
        yield return ClosePopup(popup, true);
        var last = ExampleSetups.All[^1];
        AssertEquals(string.Join(", ", BossMode.Saved.All.Select(entry => entry.Name)), last.Name, "Saved setups");
        AssertEquals(dropdown.options.Count, 1 + ExampleSetups.All.Count, "The saved setup and the examples");
        AssertEquals(section.SelectedSetup, last.Name, "The saved setup is picked");
        Assert(section.DeleteSetup.Button.interactable, "The player's own setup can be deleted");
        Assert(!dropdown.options[0].text.Contains(BossEditorSection.ExampleColor), "The saved setup isn't marked as an example");

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return WaitFor(() => !BossMode.Active, "Leaving the Boss Creator");
        yield return ToMainMenu();
    }
}
