using System.Collections;
using BTD_Mod_Helper.Api.Components;
using CustomBossChallenge.Patches;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The Customize button is created exactly once next to the Start button and reflects whether custom rules are
/// enabled. The real Boss Event screen needs online event data, so a stand-in hierarchy is used.
/// </summary>
public class BossScreenButtonTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var root = new GameObject("BossChallengeTestPanel", Il2CppType.Of<RectTransform>());
        var wasEnabled = (bool) Settings.Enabled;
        try
        {
            var start = new GameObject("StartBossChallenge", Il2CppType.Of<RectTransform>())
                .GetComponent<RectTransform>();
            start.SetParent(root.transform, false);
            start.anchoredPosition = new Vector2(400, -300);

            Settings.Enabled.SetValue(false);
            var button = BossScreenButton.CreateOrUpdate(start);
            AssertEquals(button.transform.parent, root.transform, "Button parent");
            Assert(button.RectTransform.anchoredPosition == start.anchoredPosition + BossScreenButton.Offset,
                $"Button position {button.RectTransform.anchoredPosition}");
            AssertEquals(Label(button), "Customize", "Label while disabled");

            Settings.Enabled.SetValue(true);
            var again = BossScreenButton.CreateOrUpdate(start);
            Assert(again == button, "A second button was created instead of updating the first");
            AssertEquals(root.transform.childCount, 2, "Children after updating");
            AssertEquals(Label(button), "Custom Rules: ON", "Label while enabled");
        }
        finally
        {
            Settings.Enabled.SetValue(wasEnabled);
            Object.Destroy(root);
        }

        yield break;
    }

    private string Label(ModHelperButton button) =>
        AssertNotNull(button.GetComponentInChildren<ModHelperText>(), "Button has no text").Text.text;
}
