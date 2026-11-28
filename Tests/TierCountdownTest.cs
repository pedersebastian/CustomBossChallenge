using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Visuals;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.Races;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Between bosses the boss panel's countdown says which tier comes next and how many there are, e.g. "Tier 3/10
/// Appears In: 5 Rounds" (#307), and the longest translation still fits its box. A normal Boss Challenge keeps the
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

        // The longest translation, with two-digit numbers, fits the box: the text shrinks rather than runs out of it
        var longest = LocalizationTest.Languages.Select(Translation)
            .Where(value => value != null && value.All(c => c < 0x2E80))
            .OrderByDescending(value => value!.Length).First()!;
        text.SetText(string.Format(longest, 10, 10, 10));
        text.ForceMeshUpdate();
        Log($"Longest countdown '{text.text}' at size {text.fontSize:0.#} of {text.fontSizeMax:0.#}");
        Assert(!text.isTextOverflowing, $"'{text.text}' runs out of the countdown's box");

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

    /// <summary>The countdown as a language of the mod writes it</summary>
    private static string? Translation(string language)
    {
        using var stream = typeof(TierCountdownTest).Assembly.GetManifestResourceStream(LocalizationTest.ResourcePrefix + language + ".json");
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd())?.GetValueOrDefault(Texts.TierAppearsIn);
    }
}
