using System;
using System.Collections;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.Races;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// One part of the mod's frame update that throws doesn't stop the others, and is logged once, not every frame (#112)
/// </summary>
public class UpdateGuardTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 2, TierBosses = "Lych,Vortex"};
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(CustomEditorDcm(), options));

        CustomBossChallenge.FailedUpdates.Clear();
        var throws = 0;
        CustomBossChallenge.FailNextUpdate = () =>
        {
            throws++;
            throw new InvalidOperationException("test");
        };
        try
        {
            // Many frames: the part throws in every one, but is logged once
            yield return new WaitForSecondsRealtime(1.5f);
            Assert(throws > 10, $"The boss panel update ran in every frame: {throws}");
            AssertEquals(string.Join(", ", CustomBossChallenge.FailedUpdates), "boss panel", "Only the boss panel failed");
        }
        finally
        {
            CustomBossChallenge.FailNextUpdate = null;
            CustomBossChallenge.FailedUpdates.Clear();
        }

        // The parts after it still ran: the map effects are those of the first tier's boss
        AssertEquals(Patches.MixedBosses.Presented, 1, "The map effects are kept up after the failing part");
        yield return ToMainMenu();
    }
}

/// <summary>
/// The tier number on the boss panel doesn't throw when the panel's last star is missing (#112)
/// </summary>
public class TierStarsTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 7};
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(CustomEditorDcm(), options));

        BossUI? ui = null;
        yield return WaitFor(() => (ui = UnityEngine.Object.FindObjectOfType<BossUI>()) != null, "The boss panel");
        var stars = ui!.stars;
        var kept = new UnityEngine.UI.Image[stars.Length];
        for (var i = 0; i < stars.Length; i++) kept[i] = stars[i];
        try
        {
            stars[stars.Length - 1] = null!;
            Visuals.TierStars.Show(ui);
            for (var i = 0; i < stars.Length; i++) stars[i] = null!;
            Visuals.TierStars.Show(ui);
        }
        finally
        {
            for (var i = 0; i < stars.Length; i++) stars[i] = kept[i];
        }

        yield return ToMainMenu();
    }
}
