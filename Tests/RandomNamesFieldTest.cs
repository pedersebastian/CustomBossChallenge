using System.Collections;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The game's challenge name field drops some characters and cuts long names off, and a name Random gave that comes
/// back changed no longer matches its rule. Every name the rules can make must come back from the real field as it
/// went in, and <see cref="RandomNames.MaxLength"/> must still be the field's limit.
/// </summary>
public class RandomNamesFieldTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        BossMode.Enter();
        ChallengeEditor? editor = null;
        yield return WaitFor(() => (editor = Object.FindObjectOfType<ChallengeEditor>()) != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Challenge Editor");
        yield return new WaitForSecondsRealtime(1);

        var field = editor!.chalName;
        AssertEquals(field.characterLimit, RandomNames.MaxLength, "The name field's length");
        var changed = RandomNames.AllNames.Where(name =>
        {
            field.text = name;
            return field.text != name;
        }).Select(name => $"{name} -> {field.text}").ToList();
        Assert(changed.Count == 0, $"The name field changes {changed.Count} name(s): {string.Join(", ", changed.Take(5))}");

        // The rule the setup names follow (NameField, #218): what it keeps stays, what it doesn't is changed
        var kept = "Az09" + NameField.Punctuation;
        field.text = kept;
        AssertEquals(field.text, kept, "The name field keeps NameField's characters");
        foreach (var text in new[] {"nåde", "Élite", "Marathon-Lych", "a:b", "März", "年", "ปีศาจ", "Пример"})
        {
            Assert(!NameField.Fits(text), $"NameField says the field keeps {text}");
            field.text = text;
            Assert(field.text != text, $"The name field keeps {text}, which NameField says it changes");
        }

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return WaitFor(() => !BossMode.Active, "Leaving the Boss Creator");
    }
}
