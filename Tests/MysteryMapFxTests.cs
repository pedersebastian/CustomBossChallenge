using System.Collections;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Visuals;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The mystery map effects of a boss Random picked that is still a secret (#285): the haze round the screen and the
/// dice at the spawn point take the place of the game's effects on two maps, the dice roll, they cost next to nothing a
/// frame, they go when the boss arrives and when the match is left, nothing is left behind from match to match, and a
/// game without a secret boss has none
/// </summary>
public class MysteryMapFxTest : CustomBossTest
{
    private const double MaxMsPerFrame = 0.1;

    protected override IEnumerator Run()
    {
        Assert(MysteryMapFx.Ready, "The mystery effects were read from the mod");
        var options = BossGameLauncher.Resolve(new BossOptions {Tiers = 2, TierBosses = "Random,Lych", SpawnRounds = "40, 43"}.Sanitized());
        Assert(options.WasRandom(1), "Tier 1 is Random");

        int? meshes = null, materials = null;
        var maps = new[] {TestUtils.Map, "Logs", TestUtils.Map};
        for (var game = 0; game < maps.Length; game++)
        {
            var dcm = BaseDcm();
            dcm.map = maps[game];
            dcm.startRules.round = 38;
            yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
            yield return WaitFor(() => MysteryMapFx.Ambient != null && MysteryMapFx.Track != null, $"The mystery effects on {maps[game]}", 10);
            var problem = ShownProblem(maps[game]);
            Assert(problem == null, problem ?? "");

            if (ScreenshotTour.Folder is { Length: > 0 } folder)
            {
                yield return new WaitForSecondsRealtime(1);
                yield return ScreenshotTour.Capture(folder, $"mystery-{maps[game]}");
            }

            if (game == 0) yield return RollsAndCost();
            if (game == 1)
            {
                // The boss arrives: its own effects come back, ours go
                InGame.instance.bridge.Simulation.map.spawner.bossBloonManager.SpawnBoss(1, false);
                yield return WaitFor(() => MysteryMapFx.Track == null && MysteryMapFx.Ambient == null, "The mystery effects going when tier 1 arrives", 5);
                Assert(!Patches.MixedBosses.PresentedHidden && BossPresentation.MapEffects.Length > 0,
                    $"The game's effects are back once tier 1 arrives: {BossPresentationTest.Names()}");
            }

            yield return ToMainMenu();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert(MysteryMapFx.Ambient == null && MysteryMapFx.Track == null, $"The mystery effects are gone after leaving {maps[game]}");
            var left = Object.FindObjectsOfType<Transform>().Where(t => t.name.StartsWith("Mystery")).Select(t => t.name).ToList();
            Assert(left.Count == 0, $"Nothing of them is left at the main menu: {string.Join(", ", left)}");

            // Made once, kept: the same meshes and materials after each match (each match's dice meshes are destroyed)
            var meshCount = Resources.FindObjectsOfTypeAll<Mesh>().Count(mesh => mesh.name.StartsWith("Mystery"));
            var materialCount = Resources.FindObjectsOfTypeAll<Material>().Count(material => material.name.StartsWith("Mystery"));
            Log($"after {maps[game]}: {meshCount} mystery meshes, {materialCount} materials");
            if (meshes is { } before) AssertEquals(meshCount, before, $"Mystery meshes after match {game + 1}");
            if (materials is { } had) AssertEquals(materialCount, had, $"Mystery materials after match {game + 1}");
            meshes = meshCount;
            materials = materialCount;
        }

        // A game with no secret boss has none
        var normal = BaseDcm();
        normal.startRules.round = 38;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(normal, BossGameLauncher.Resolve(new BossOptions {Tiers = 2, TierBosses = "Lych,Bloonarius"})));
        yield return new WaitForSecondsRealtime(3);
        Assert(MysteryMapFx.Ambient == null && MysteryMapFx.Track == null, "A game without a secret boss has no mystery effects");
        Assert(BossPresentation.MapEffects.Length > 0, $"It has the game's own: {BossPresentationTest.Names()}");
        yield return ToMainMenu();
    }

    private IEnumerator RollsAndCost()
    {
        // Each die rolls within a few seconds, and lands back on its spot
        yield return WaitFor(() => MysteryMapFx.Rolls >= 3, "The dice rolling", 15);
        var watch = new System.Diagnostics.Stopwatch();
        const int frames = 240;
        for (var i = 0; i < frames; i++)
        {
            watch.Start();
            MysteryMapFx.Update();
            watch.Stop();
            yield return null;
        }
        var ms = watch.Elapsed.TotalMilliseconds / frames;
        Log($"MysteryMapFx.Update: {ms:0.000} ms a frame, {MysteryMapFx.Rolls} rolls");
        Assert(ms < MaxMsPerFrame, $"The mystery effects take {ms:0.000} ms a frame, more than {MaxMsPerFrame}");

        var track = MysteryMapFx.Track!.transform;
        for (var i = 0; i < 3; i++)
        {
            var die = track.Find($"DieSpot{i}");
            Assert(die != null && die.localPosition.magnitude < 3, $"Die {i} stays at the spawn point: {die?.localPosition}");
        }
    }

    /// <summary>What is wrong with the mystery effects shown on <paramref name="map"/>, or null when they are right</summary>
    internal static string? ShownProblem(string map)
    {
        if (!Patches.MixedBosses.PresentedHidden) return $"{map}: the boss isn't a secret";
        if (BossPresentationTest.Names() != "") return $"{map}: the game's effects are shown: {BossPresentationTest.Names()}";
        var ambient = MysteryMapFx.Ambient;
        var track = MysteryMapFx.Track;
        if (ambient == null || track == null || !ambient.activeInHierarchy || !track.activeInHierarchy)
            return $"{map}: the mystery effects aren't shown (haze {ambient != null}, dice {track != null})";
        // On the map's camera: on the defeat screen Camera.main is another one, and the haze stays on the map's
        if (ambient.transform.parent?.GetComponent<Camera>() is not {orthographic: true}) return $"{map}: the haze isn't on the map's camera";
        if (ambient.GetComponentsInChildren<MeshRenderer>().Length <= 5) return $"{map}: the haze has no pips";
        if (Enumerable.Range(0, 3).Count(i => track.transform.Find($"DieSpot{i}") != null) != 3) return $"{map}: not three dice";
        // Drawn like most of the game's track effects (Bloonarius's, Diamondback's, Lych's: render queue 1999-2000)
        if (MysteryMapFx.DiceQueue != (int) UnityEngine.Rendering.RenderQueue.Geometry)
            return $"{map}: the dice are drawn at queue {MysteryMapFx.DiceQueue}";
        if (BossPresentation.TrackEffectPosition is not { } at) return $"{map}: the game's track effect is gone";
        // On the track, where the game's track effect is
        var off = Vector3.Distance(track.transform.position, at);
        return off < 1 ? null : $"{map}: the dice are {off:0.0} away from the game's track effect";
    }
}

/// <summary>A continued game whose next boss is a secret Random one shows the mystery effects (#285)</summary>
public class MysteryMapFxContinueTest : ContinueTestBase
{
    protected override IEnumerator Run()
    {
        var options = BossGameLauncher.Resolve(new BossOptions {Tiers = 2, SpawnRounds = "40, 60", TierBosses = "Lych,Random"}.Sanitized());
        Assert(options.WasRandom(2), "Tier 2 is Random");
        yield return StartCustomGame(options, Challenge(28).Build());
        var json = Persistence.MapSaveJson.Serialize(
            InGame.instance.CreateCurrentMapSave(InGame.instance.bridge.GetCurrentRound(), CustomBossRetries.SaveId));
        var dcm = Il2CppAssets.Scripts.Unity.UI_New.InGame.InGameData.CurrentGame.dcModel;
        yield return CloseGame();

        // Saved after tier 1 was beaten: tier 2 is still a secret
        Store.Save(options, dcm, new[] {new Persistence.RoundSnapshot(41, 1, json)});
        var game = AssertNotNull(Store.TryLoad(), "The saved game did not load")!;
        BossGameLauncher.Continue(game);
        yield return WaitForLoadedGame();
        yield return WaitFor(() => MysteryMapFx.Ambient != null && MysteryMapFx.Track != null, "The mystery effects after Continue", 10);
        var problem = MysteryMapFxTest.ShownProblem("Continued");
        Assert(problem == null, problem ?? "");
        yield return ToMainMenu();
    }
}
