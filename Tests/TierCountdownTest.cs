using System;
using System.Collections;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Visuals;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.Races;
using Il2CppNinjaKiwi.Localization;
using UnityEngine;
using Object = UnityEngine.Object;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Between bosses the boss panel's countdown says which tier comes next and how many there are, e.g. "Tier 3/10
/// Appears In: 5 Rounds" (#307), inside the panel's background in every language: on one line, or broken after the colon. A normal Boss Challenge keeps the
/// game's own "Boss Appears In".
/// </summary>
public class TierCountdownTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        // TierLookScene plays all 10 tiers, with spawn rounds far away
        yield return TierLookScene.Start(new BossOptions {Boss = BossType.Bloonarius}, data => LoadIntoGame(data));
        var ui = AssertNotNull(Object.FindObjectOfType<BossUI>(), "No boss panel")!;
        var text = AssertNotNull(ui.bossArrivalTxt, "No countdown in the boss panel")!;

        // As the game sets it: tier 1 comes on round 500, and the game starts on round 1
        var round = InGame.instance.bridge.GetCurrentRound() + 1;
        Log($"On round {round} the game's countdown says '{text.Shown()}'");
        AssertEquals(text.Shown(), ScreenText.Plain(Texts.TierAppearsIn.T(1, 10, 500 - round)), "The game's own countdown before the first tier");

        ui.SetArriveInTxt(5);
        AssertEquals(text.Shown(), ScreenText.Plain(Texts.TierAppearsIn.T(1, 10, 5)), "Countdown before the first tier");

        CustomBossState.OnBossDefeated(2);
        ui.SetArriveInTxt(1);
        AssertEquals(text.Shown(), ScreenText.Plain(Texts.TierAppearsInOneRound.T(3, 10)), "Countdown after tier 2, one round left");

        CustomBossState.OnBossDefeated(9);
        ui.SetArriveInTxt(12);
        AssertEquals(text.Shown(), ScreenText.Plain(Texts.TierAppearsIn.T(10, 10, 12)), "Countdown to the last tier");

        // English stays on one line, also with three-digit rounds, and inside the background
        foreach (var (tier, rounds) in new[] {(1, 20), (10, 20), (10, 499)})
        {
            CustomBossState.ResumeWith(tier - 1);
            ui.SetArriveInTxt(rounds);
            CheckFits(ui, $"English, tier {tier}, {rounds} rounds");
            AssertEquals(text.textInfo.lineCount, 1, $"'{text.text}' is on one line");
            AssertEquals(text.Shown(), ScreenText.Plain(Texts.TierAppearsIn.T(tier, 10, rounds)), $"Countdown to tier {tier}");
        }

        // Every language, in its own font: inside the background, on one line or broken after the colon (#307)
        var original = LocalizationManager.Instance.ActiveLanguage;
        try
        {
            foreach (var name in LocalizationTest.Languages)
            {
                var task = LocalizationManager.Instance.UpdateLanguageAsync(Enum.Parse<Language>(name));
                yield return WaitFor(() => task.IsCompleted, $"Switching to {name}", 30);
                yield return null;
                ui.SetArriveInTxt(20);
                CheckFits(ui, name);
                Assert(text.textInfo.lineCount <= 2, $"{name}: '{text.text}' takes {text.textInfo.lineCount} lines");
                if (text.textInfo.lineCount == 2)
                {
                    var first = text.text.Split('\n')[0].TrimEnd();
                    Assert(first.EndsWith(':') || first.EndsWith('：'), $"{name}: '{text.text}' breaks somewhere else than after the colon");
                }
            }
        }
        finally
        {
            LocalizationManager.Instance.UpdateLanguageAsync(original);
        }
        yield return WaitFor(() => LocalizationManager.Instance.ActiveLanguage == original, "Going back to the player's language", 30);

        yield return ToMainMenu();

        // A normal Boss Challenge keeps the game's countdown
        yield return LoadIntoGame(SetupBossChallenge(BaseDcm(), BossType.Vortex));
        Assert(!CustomBossState.IsActive, "A normal Boss Challenge must not be custom");
        AssertEquals(BossHud.ArrivalText(5), null, "A normal Boss Challenge gets the tier countdown");
        ui = AssertNotNull(Object.FindObjectOfType<BossUI>(), "No boss panel in a normal Boss Challenge")!;
        ui.SetArriveInTxt(5);
        Assert(ui.bossArrivalTxt.Shown() != ScreenText.Plain(Texts.TierAppearsIn.T(1, 5, 5)),
            $"A normal Boss Challenge shows '{ui.bossArrivalTxt.Shown()}'");
        yield return ToMainMenu();
    }

    /// <summary>
    /// The countdown runs neither out of its box nor out of the background behind it, and keeps most of the game's size
    /// </summary>
    private void CheckFits(BossUI ui, string what)
    {
        var text = ui.bossArrivalTxt;
        text.ForceMeshUpdate();
        var bg = AssertNotNull(text.transform.parent.Find("Bg"), "No background behind the countdown")!.GetComponent<RectTransform>();
        var right = text.rectTransform.localPosition.x + text.textBounds.max.x;
        var edge = bg.localPosition.x + bg.rect.xMax;
        Log($"{what}: '{text.text.Replace("\n", " | ")}' in {text.font?.name} at size {text.fontSize:0.#} of {text.fontSizeMax:0.#}, " +
            $"{text.textInfo.lineCount} line(s), right edge {right:0} of the background's {edge:0}");
        Assert(!text.isTextOverflowing, $"{what}: '{text.text}' runs out of the countdown's box");
        Assert(right <= edge - BossHud.ArrivalPadding / 2, $"{what}: '{text.text}' reaches {right:0}, the background ends at {edge:0}");
        Assert(text.fontSize >= text.fontSizeMax * 0.6f, $"{what}: '{text.text}' shrinks to {text.fontSize:0.#} of {text.fontSizeMax:0.#}");
    }
}
