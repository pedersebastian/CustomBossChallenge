using System;
using System.Collections;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using Object = UnityEngine.Object;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The bosses per tier know every boss of the game, in the game's order, and give each tier its own models
/// </summary>
public class MixedBossModelTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        AssertEquals(string.Join(",", TierBossMath.Bosses), string.Join(",", Enum.GetNames<BossType>()),
            "TierBossMath.Bosses must be the game's bosses in order");

        var options = new BossOptions
            {Boss = BossType.Vortex, Elite = true, Tiers = 8, TierBosses = "Lych,,Random,PhayzeElite,,Diamondback,LychElite"}.Sanitized();
        AssertEquals(options.BossId(1), "Lych1", "Tier with a boss of its own");
        AssertEquals(options.BossId(2), "VortexElite2", "Tier that follows the setup");
        AssertEquals(options.BossId(3), "VortexElite3", "A Random tier has the setup's boss before the game starts");
        AssertEquals(options.BossId(4), "PhayzeElite4", "Elite tier");
        AssertEquals(options.SourceId(7), "LychElite5", "An extra tier is made from tier 5 of its own boss");
        AssertEquals(options.SourceId(8), "VortexElite5", "Extra tiers past the list follow the setup");
        Assert(options.HasRandom, "The setup has a Random tier");

        var game = Game.instance.model;
        AssertEquals(ExtraTiers.MissingVanillaTier(game, options), null, "Every tier has a source model");
        var tier7 = ExtraTiers.CreateTier(game.GetBloon(options.SourceId(7)), options, 7);
        AssertEquals(tier7.id, "LychElite7", "Extra tier id");
        AssertEquals(tier7.baseId, game.GetBloon("LychElite5").baseId, "Extra tier base");

        var resolved = options.ResolveRandom(new System.Random(1));
        Assert(!resolved.HasRandom, $"Resolved options still have Random: {resolved.TierBosses}");
        AssertEquals(resolved.BossId(1), "Lych1", "Resolving keeps chosen bosses");
        AssertEquals(resolved.BossId(2), "VortexElite2", "Resolving fills in the setup's boss");
        Assert(resolved.BossId(3).EndsWith("3") && !resolved.BossId(3).Contains("Elite"), $"Random keeps its Elite: {resolved.BossId(3)}");
        AssertEquals(new BossOptions().ResolveRandom(new System.Random(1)), new BossOptions(), "A single boss setup stays as it is");

        // Each tier's own speed and health go on top of Speed per Tier and Health per Tier
        var values = new BossOptions {Tiers = 2, SpeedPerTier = 10, TierBosses = "Lych@150/200,Lych"}.Sanitized();
        var source = game.GetBloon("Lych1");
        var scaled = source.Clone().Cast<Il2CppAssets.Scripts.Models.Bloons.BloonModel>();
        TierScaling.Scale(scaled, TierScaling.Values.Of(source), values, 1);
        Assert(Approximately(scaled.speed, source.speed * 1.5f), $"Tier 1 at 150 % speed: {scaled.speed} vs {source.speed}");
        AssertEquals(scaled.maxHealth, source.maxHealth * 2, "Tier 1 at 200 % health");
        AssertEquals(values.TierSpeedFactor(2), 1.0, "A tier without its own values is normal");
        AssertEquals(new BossOptions().TierHealthFactor(1), 1.0, "Without a boss per tier there are no tier values");

        AssertEquals(BossMode.DefaultName(options), BossMode.MixedName, "Name of a mixed setup");
        Assert(BossMode.IsDefaultName(BossMode.MixedName), "The mixed name is a default name");
        yield break;
    }
}

/// <summary>
/// Setups with a different boss per tier are shared as version 3 codes and read back the same
/// </summary>
public class MixedBossCodeTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 4, TierBosses = "LychElite,,RandomElite,Diamondback", SpeedPerTier = 10};
        var dcm = BossMode.DefaultDcm(options);
        var code = SetupCode.Encode(options, dcm);
        Assert(code.StartsWith(SetupCode.Version3Prefix), $"A mixed setup must be a version 3 code: {code}");
        Assert(SetupCode.TryDecode(code, out var read, out _, out var problem), $"The code did not load: {problem}");
        AssertEquals(read, options, "Options read from the code");

        var empty = options with {TierBosses = ""};
        Assert(SetupCode.TryDecode(SetupCode.Encode(empty, dcm), out var readEmpty, out _, out _), "Code with every tier following");
        AssertEquals(readEmpty.TierBosses, "", "\"Different boss per tier\" stays on with every tier following the setup");

        Assert(SetupCode.Encode(options with {TierBosses = null}, dcm).StartsWith(SetupCode.Version2Prefix),
            "Without bosses per tier the code stays version 2");

        // A game with bosses per tier is continued with the same bosses, from a file older versions leave alone
        var store = new ContinueStore(System.IO.Path.Combine(TempFolder, "Mixed", "Continue.json"));
        var resolved = options.ResolveRandom(new System.Random(7));
        store.Save(resolved, dcm, new[] {new RoundSnapshot(35, 1, "not json")});
        Assert(System.IO.File.ReadAllText(store.FilePath).Contains($"\"Version\": {ContinueStore.Version}"),
            "A game with bosses per tier is saved as version 2");
        var load = store.Load();
        AssertEquals(load.Lost?.Options ?? load.Game?.Options, resolved, "Bosses per tier after reading the file back");
        store.Save(options with {TierBosses = null}, dcm, Array.Empty<RoundSnapshot>());
        Assert(System.IO.File.ReadAllText(store.FilePath).Contains($"\"Version\": {ContinueStore.SingleBossVersion}"),
            "A game with one boss stays version 1, which older versions can continue");
        yield break;
    }
}

/// <summary>
/// A real game with a different boss per tier, an Elite one, one that follows the setup and extra tiers: each tier
/// spawns its own boss, the game's boss type follows the tiers, and the game is won with the last one
/// </summary>
public class MixedBossGameTest : CustomBossTest
{
    public const float TimeoutSeconds = 420;

    protected override IEnumerator Run()
    {
        // The strong test tower can't damage Dreadbloon, so it isn't in the line-up. Some tiers have skulls of their own.
        var options = new BossOptions
        {
            Tiers = 6, HealthPerTier = 10, SpawnRounds = "40,43,46,49,52,55",
            TierBosses = "Lych#2,,Diamondback#60%;30%,VortexElite,Phayze,Blastapopoulos#0"
        };
        var dcm = BaseDcm();
        dcm.startRules.round = 39;
        dcm.startRules.lives = dcm.startRules.maxLives = 5000;
        dcm.bloonModifiers.healthMultipliers.boss = 0.05f;
        dcm.disableMK = true;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, BossGameLauncher.Resolve(options)));

        var gameModel = InGame.instance.GetGameModel();
        Assert(ExtraTiers.HasAllTiers(gameModel, options), "The match is missing boss tier models");
        AssertScaledTiers(gameModel, options);
        AssertEquals(gameModel.bossBloonType, "Lych", "The game starts as the first tier's boss");
        float[]? Skulls(int tier) => SkullModifier.GetSkulls(gameModel.GetBloon(options.BossId(tier)));
        Assert(SameValues(Skulls(1), SkullMath.EvenlySpaced(2)), $"Tier 1 has its own 2 skulls: {Show(Skulls(1))}");
        AssertEquals(Skulls(2)?.Length, 3, "Tier 2 keeps Bloonarius's own skulls");
        Assert(SameValues(Skulls(3), new[] {0.6f, 0.3f}), $"Tier 3 has its own list: {Show(Skulls(3))}");
        AssertEquals(Skulls(4)?.Length, 7, "Tier 4 keeps Elite Vortex's own skulls");
        AssertEquals(Skulls(6)?.Length, 0, "The extra tier 6 has no skulls");
        AssertEquals(SkullModifier.GetSkulls(gameModel.GetBloon("Lych2"))?.Length, 5, "Lych's other tiers keep their skulls");

        var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        ExtraTierGameTest.PlaceStrongTower(gameModel.GetTowerFromId("DartMonkey"));
        InGame.instance.bridge.SetFastForward(true);

        var highestTier = 0;
        var start = Time.realtimeSinceStartup;
        while (!InGame.instance.bridge.Simulation.gameWon)
        {
            if (InGame.instance.bridge.Simulation.GameOver) Fail($"The game was lost on round {InGame.instance.bridge.GetCurrentRound() + 1}, tier {highestTier}");
            if (Time.realtimeSinceStartup - start > TimeoutSeconds) Fail($"No victory after {TimeoutSeconds}s; highest tier {highestTier}");

            if (manager.CurrentBossTier > highestTier && manager.CurrentBoss != null)
            {
                highestTier = manager.CurrentBossTier;
                var (boss, elite) = options.BossOf(highestTier);
                AssertEquals(manager.CurrentBoss.bloonModel.id, options.BossId(highestTier), "Spawned boss");
                AssertEquals(manager.sim.model.bossBloonType, boss.ToString(), $"Boss type during tier {highestTier}");
                AssertEquals(manager.sim.model.bossEliteMode, elite, $"Elite during tier {highestTier}");
                AssertEquals(InGameData.CurrentGame.bossData.bossBloon, boss, $"Boss data during tier {highestTier}");
                Log($"tier {highestTier}: {manager.CurrentBoss.bloonModel.id}");
                CheckHud(options, highestTier, boss, elite);
            }

            if (!InGame.instance.bridge.AreRoundsActive()) InGame.instance.bridge.StartRound();
            yield return new WaitForSecondsRealtime(0.25f);
        }

        AssertEquals(highestTier, options.Tiers, "Victory must come with the last chosen tier");
        yield return ToMainMenu();
        AssertEquals(Visuals.BossHud.Lineup, null, "The line-up is gone outside the game");
    }

    /// <summary>
    /// The boss panel's portrait, the banner and the line-up all show the tier that just spawned
    /// </summary>
    private void CheckHud(BossOptions options, int tier, BossType boss, bool elite)
    {
        var ui = AssertNotNull(Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.InGame.Races.BossUI>(), "No boss panel")!;
        Visuals.BossHud.ShowPortraits(ui);
        Visuals.BossHud.Update(now: true);
        var lineup = AssertNotNull(Visuals.BossHud.Lineup, $"No line-up during tier {tier}")!;
        Assert(lineup.gameObject.activeInHierarchy, "The line-up is shown");
        AssertEquals(Visuals.BossHud.Cells.Count, options.Tiers, "A line-up cell per tier");

        var banner = AssertNotNull(Visuals.BossHud.Banner, $"No banner for tier {tier}")!;
        Assert(banner.gameObject.activeSelf, $"The banner shows when tier {tier} arrives");
        AssertEquals(Visuals.BossHud.BannerText!.Text.text, Texts.TierArrives.T(tier, Visuals.BossHud.Name(boss, elite)),
            "Banner text");

        if (!Visuals.BossHud.LineupShown) return;
        for (var i = 0; i < Visuals.BossHud.Cells.Count; i++)
        {
            var cell = Visuals.BossHud.Cells[i];
            AssertEquals(cell.Tick.gameObject.activeSelf, i + 1 <= CustomBossState.HighestDefeatedTier, $"Tick of tier {i + 1} during tier {tier}");
            AssertEquals(cell.Frame.gameObject.activeSelf, i + 1 == tier, $"Frame of tier {i + 1} during tier {tier}");
        }
    }
}

/// <summary>
/// Random tiers get their boss once, when the game is launched: a restart keeps them and goes back to tier 1's boss
/// </summary>
public class MixedBossRandomTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 3, TierBosses = "Random,RandomElite,Random", SpawnRounds = "45"};
        var resolved = BossGameLauncher.Resolve(options);
        Assert(!resolved.HasRandom, $"Random tiers left: {resolved.TierBosses}");
        AssertEquals(resolved.BossOf(2).Elite, true, "A Random Elite tier stays Elite");
        AssertEquals(resolved.BossOf(1).Elite, false, "A Random tier stays normal");

        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(BaseDcm(), resolved));
        AssertEquals(CustomBossState.ActiveOptions, resolved, "The game is played with the resolved bosses");
        var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        manager.SpawnBoss(2, false);
        yield return new WaitForSecondsRealtime(1);
        AssertEquals(manager.CurrentBoss?.bloonModel.id, resolved.BossId(2), "Spawned tier 2");

        var simulation = InGame.instance.bridge.Simulation.Pointer;
        InGame.instance.RunRestart();
        var start = Time.realtimeSinceStartup;
        while (InGame.instance == null || InGame.instance.bridge?.Simulation == null ||
               InGame.instance.bridge.Simulation.Pointer == simulation)
        {
            if (Time.realtimeSinceStartup - start > 30) Fail("The game did not restart");
            yield return new WaitForSecondsRealtime(0.25f);
        }
        yield return new WaitForSecondsRealtime(1);

        AssertEquals(CustomBossState.ActiveOptions, resolved, "A restart keeps the bosses");
        var model = InGame.instance.bridge.Simulation.model;
        AssertEquals(model.bossBloonType, resolved.BossOf(1).Boss.ToString(), "After a restart the game is tier 1's boss again");
        AssertEquals(model.bossEliteMode, resolved.BossOf(1).Elite, "Elite after a restart");
        yield return ToMainMenu();
    }
}
