using System;
using System.Collections;
using System.Linq;
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

/// <summary>
/// A round whose snapshot can't be written doesn't stop the later rounds from being saved for Continue (#112)
/// </summary>
public class FailedSnapshotTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 3, SpawnRounds = "40, 43, 46"};
        yield return StartCustomGame(options, Challenge(20).Build());
        NoAutoPlay();
        yield return PlayRound();
        AssertEquals(AssertNotNull(Store.TryLoad(), "The game after round 21").Latest.Snapshot.Round, 21, "Saved round");

        CustomBossRetries.FailNextSnapshot = true;
        yield return PlayRound();
        Assert(!CustomBossRetries.FailNextSnapshot, "The snapshot of round 22 was taken");
        Assert(CustomBossRetries.HasLastRound(22), "A round whose JSON failed can still be retried in this session");
        AssertEquals(AssertNotNull(Store.TryLoad(), "The game after round 22").Latest.Snapshot.Round, 21,
            "A round whose JSON failed isn't in the file");

        yield return PlayRound();
        AssertEquals(AssertNotNull(Store.TryLoad(), "The game after round 23").Latest.Snapshot.Round, 23,
            "The rounds after a failed snapshot are still saved");
        yield return ToMainMenu();
    }
}

/// <summary>
/// When the game has no model for the later tiers, the match ends after the last tier it has, not after the ones it
/// lacks (#112). A game model can't be made to lack a tier, so this calls the same code on a running match.
/// </summary>
public class LimitTiersTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 5, SpawnRounds = "40, 50, 60, 70, 80"};
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(CustomEditorDcm(), options));

        var model = InGame.instance.GetGameModel();
        var dcm = InGameData.CurrentGame.dcModel;
        var modelEnd = model.endRound;
        var dcmEnd = dcm.startRules.endRound;
        Log($"End round of 5 tiers: game model {modelEnd}, challenge {dcmEnd}");

        var limited = CustomBossChallenge.LimitTiers(model, 3);
        AssertEquals(limited.Tiers, 3, "Tiers played");
        var shift = TierMath.EndRound(limited.EffectiveSpawnRounds) - TierMath.EndRound(options.EffectiveSpawnRounds);
        Assert(shift < 0, $"Three tiers end earlier than five: {shift}");
        AssertEquals(model.endRound, modelEnd + shift, "The match ends after the last tier it has");
        AssertEquals(dcm.startRules.endRound, dcmEnd + shift, "The challenge ends after the last tier it has");
        yield return ToMainMenu();
    }
}

/// <summary>
/// In Advanced Mode with Boss Event Rounds, each tier's own boss round is merged over the match's own round set, so
/// AlternateBloonsRounds is used for every tier, not only the first one's (#112)
/// </summary>
public class AlternateRoundsTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var alternate = Il2CppAssets.Scripts.Data.GameData.Instance.RoundSetByName("AlternateRoundSet").rounds;
        var checkedRounds = 0;
        foreach (var boss in TierBossMath.Bosses)
        {
            if (boss == "Bloonarius") continue;
            var options = new BossOptions {Tiers = 2, TierBosses = $"Bloonarius,{boss}", BossEventRounds = true};
            var dcm = CustomEditorDcm();
            dcm.mode = "AlternateBloonsRounds";
            yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));

            var rounds = InGame.instance.GetGameModel().roundSet.rounds;
            foreach (var (round, tier, eventRound) in BossRoundsMath.TierEventRounds(options.EffectiveSpawnRounds, rounds.Length))
            {
                var set = BossOptions.BossEventRoundSet(options.BossOf(tier).Boss);
                if (set == options.BaseEventRoundSet || eventRound > alternate.Length) continue;
                var expected = BossEventRounds.RoundOf(set, eventRound, alternate[eventRound - 1]);
                if (expected == null) continue;
                AssertEquals(Groups(rounds[round - 1]), Groups(expected),
                    $"Round {round} of tier {tier} ({boss}) is its event round {eventRound} over the alternate rounds");
                checkedRounds++;
            }
            yield return ToMainMenu();
        }
        Assert(checkedRounds > 0, "No tier had a round of its own boss");
    }

    private static string Groups(Il2CppAssets.Scripts.Models.Rounds.RoundModel round) =>
        string.Join(";", round.groups.ToArray().Select(group => $"{group.bloon}x{group.count}@{group.start}-{group.end}"));
}
