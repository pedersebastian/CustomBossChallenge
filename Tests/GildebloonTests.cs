using System;
using System.Collections;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors;
using Il2CppAssets.Scripts.Unity;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Base for the tests of the mod's own boss Gildebloon (#25): switches it on in the code and in Mod Settings for the
/// test, and back to what it was afterwards
/// </summary>
public abstract class GildebloonTest : CustomBossTest
{
    protected sealed override IEnumerator Run()
    {
        var flag = Experimental.Gildebloon;
        var setting = Experimental.GildebloonSetting;
        Experimental.Gildebloon = true;
        Experimental.GildebloonSetting = () => true;
        try
        {
            Gildebloon.Register(Game.instance.model);
            Visuals.GildebloonLook.Load();
            yield return RunOn();
        }
        finally
        {
            Experimental.Gildebloon = flag;
            Experimental.GildebloonSetting = setting;
        }
    }

    protected abstract IEnumerator RunOn();

    /// <summary>
    /// Plays until the game is won, checking that each tier spawns the boss the options give it, with the game's boss
    /// type and boss data set for it
    /// </summary>
    protected IEnumerator PlayToVictory(BossOptions options, float timeout = 300)
    {
        var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        var gameModel = InGame.instance.GetGameModel();
        ExtraTierGameTest.PlaceStrongTower(gameModel.GetTowerFromId("DartMonkey"));
        InGame.instance.bridge.SetFastForward(true);

        var highest = 0;
        var collectors = 0;
        var skulls = 0;
        var invulnerable = false;
        var start = Time.realtimeSinceStartup;
        while (!InGame.instance.bridge.Simulation.gameWon)
        {
            if (InGame.instance.bridge.Simulation.GameOver) Fail($"Lost on round {InGame.instance.bridge.GetCurrentRound() + 1}");
            if (Time.realtimeSinceStartup - start > timeout) Fail($"No victory after {timeout}s; highest tier {highest}");

            var boss = manager.CurrentBoss;
            if (manager.CurrentBossTier > highest && boss != null)
            {
                highest = manager.CurrentBossTier;
                var (type, elite) = options.BossOf(highest);
                AssertEquals(boss.bloonModel.id, options.BossId(highest), "Spawned boss");
                AssertEquals(manager.sim.model.bossBloonType, BossList.Name(type), $"Boss type of tier {highest}");
                AssertEquals(InGameData.CurrentGame.bossData.bossBloon, BossList.Host(type), $"Boss data of tier {highest}");
                Log($"tier {highest}: {boss.bloonModel.id}, {boss.health} health");

                var ui = UnityEngine.Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.InGame.Races.BossUI>();
                AssertNotNull(ui, "The game's boss panel");
            }

            if (boss != null && Gildebloon.IsGildebloon(boss.bloonModel.id))
            {
                skulls = Math.Max(skulls, InGame.instance.bridge.GetCurrentBossSkull());
                invulnerable |= boss.IsInvulnerable;
            }

            collectors = Math.Max(collectors, InGame.instance.GetBloons().Count(bloon => Gildebloon.IsCollector(bloon.bloonModel.id)));

            if (!InGame.instance.bridge.AreRoundsActive()) InGame.instance.bridge.StartRound();
            yield return null;
        }

        AssertEquals(highest, options.Tiers, "Won with the last tier");
        Log($"won; skulls used {skulls}, most Tax Collectors at once {collectors}, invulnerable at a skull {invulnerable}");
        Assert(skulls > 0, "Gildebloon used its skulls");
        Assert(collectors > 0, "Gildebloon sent Tax Collectors at a skull");
        Assert(invulnerable, "Gildebloon can't be hurt for a moment at a skull");
    }

    /// <summary>A custom game that starts on round 39 with weak bosses and lives to spare</summary>
    protected static Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel WeakBossDcm()
    {
        var dcm = BaseDcm();
        dcm.startRules.round = 39;
        dcm.startRules.lives = dcm.startRules.maxLives = 5000;
        dcm.bloonModifiers.healthMultipliers.boss = 0.05f;
        dcm.disableMK = true;
        return dcm;
    }
}

/// <summary>
/// Gildebloon's models: every tier, normal and Elite, with its health, speed, skulls and behaviours, and its Tax
/// Collectors
/// </summary>
public class GildebloonModelTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var game = Game.instance.model;
        AssertEquals(Gildebloon.Register(game), 0, "Registering again adds nothing");
        foreach (var elite in new[] {false, true})
        {
            for (var tier = 1; tier <= TierMath.VanillaTiers; tier++)
            {
                var id = BossOptions.BossIdOf(BossList.Gildebloon, elite, tier);
                Assert(game.DoesBloonExist(id), $"{id} exists");
                var model = game.GetBloon(id);
                AssertEquals(model.maxHealth, (elite ? Gildebloon.EliteHealth : Gildebloon.Health)[tier - 1], $"{id} health");
                Assert(Approximately(model.speed, Gildebloon.Speed[tier - 1]), $"{id} speed {model.speed}");
                Assert(model.isBoss, $"{id} is a boss");
                Assert(SameValues(SkullModifier.GetSkulls(model), elite ? Gildebloon.EliteSkulls : Gildebloon.Skulls),
                    $"{id} skulls {Show(SkullModifier.GetSkulls(model))}");
                AssertEquals(model.GetBehaviors<SpawnBloonsActionModel>().Count(spawn => !Gildebloon.IsCollector(spawn.bloonType)), 0,
                    $"{id} has none of Bloonarius's spawns, only its Tax Collectors");
                AssertNotNull(model.GetBehavior<IncreaseDamageFromTypeModel>(), $"{id} takes more damage from acid");
                Assert(Gildebloon.IsGildebloon(id), $"{id} is one of Gildebloon's tiers");
            }
        }

        foreach (var id in new[] {Gildebloon.CollectorId, Gildebloon.EliteCollectorId})
        {
            Assert(game.DoesBloonExist(id), $"{id} exists");
            AssertEquals(game.GetBloon(id).leakDamage, 0f, $"{id} takes no lives");
            Assert(Gildebloon.IsCollector(id) && !Gildebloon.IsGildebloon(id), $"{id} is a Tax Collector");
        }

        // Tiers above 5 are made from its tier 5, like the game's bosses'
        var options = new BossOptions {Boss = BossList.Gildebloon, Elite = true, Tiers = 7};
        AssertEquals(ExtraTiers.MissingVanillaTier(game, options), null, "Every tier has a source model");
        AssertEquals(options.SourceId(7), "GildebloonElite5", "Tier 7's source");

        // Share codes with it are version 6, by name, and read back the same (#25)
        var dcm = BossMode.DefaultDcm(options);
        var code = Persistence.SetupCode.Encode(options, dcm);
        Assert(code.StartsWith(Persistence.SetupCode.Version6Prefix), $"A code with Gildebloon is version 6: {code}");
        Assert(Persistence.CodeFormat.Contents(code).Contains("\"Gildebloon\""), "The code has it by name");
        Assert(Persistence.SetupCode.TryDecode(code, out var read, out _, out var problem), $"The code loads: {problem}");
        AssertEquals(read, options.Sanitized(), "Options read from the code");
        var mixed = new BossOptions {Tiers = 2, TierBosses = "Lych,GildebloonElite"};
        Assert(Persistence.SetupCode.Encode(mixed, dcm).StartsWith(Persistence.SetupCode.Version6Prefix), "A tier with it is version 6 too");
        Assert(!Persistence.SetupCode.Encode(new BossOptions {Tiers = 1, TierBosses = ",Gildebloon"}, dcm)
            .StartsWith(Persistence.SetupCode.Version6Prefix), "A tier that isn't played doesn't count");
        yield break;
    }

    private const string Source = "Bloonarius";
}

/// <summary>
/// A whole custom game with Gildebloon: both tiers spawn as Gildebloon, use their skulls with the Repossession and
/// the Tax Collectors, and the game is won
/// </summary>
public class GildebloonGameTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 2, SpawnRounds = "40,42"};
        AssertEquals(Menus.BossEditorSection.PlayProblem(BaseDcm(), options), null, "The setup can be played");
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(WeakBossDcm(), options));
        Assert(ExtraTiers.HasAllTiers(InGame.instance.GetGameModel(), options), "The match has Gildebloon's tiers");
        yield return PlayToVictory(options);
        yield return ToMainMenu();
    }
}

/// <summary>
/// Gildebloon as one tier of a setup with a boss per tier: the game's boss type goes from Lych to Gildebloon and back
/// </summary>
public class GildebloonMixedTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Tiers = 3, SpawnRounds = "40,42,44", TierBosses = "Lych,GildebloonElite,Bloonarius"};
        Assert(options.UsesGildebloon, "The setup has Gildebloon");
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(WeakBossDcm(), options));
        yield return PlayToVictory(options);
        yield return ToMainMenu();
    }
}

/// <summary>
/// With Gildebloon off nothing offers it, and a setup with it can't be played; a normal Boss Challenge never gets it,
/// also while it is on
/// </summary>
public class GildebloonOffTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var flag = Experimental.Gildebloon;
        var setting = Experimental.GildebloonSetting;
        try
        {
            var options = new BossOptions {Boss = BossList.Gildebloon};
            foreach (var (code, wanted) in new[] {(false, false), (false, true), (true, false)})
            {
                Experimental.Gildebloon = code;
                Experimental.GildebloonSetting = () => wanted;
                AssertEquals(BossList.Available.Length, Enum.GetValues<BossType>().Length, $"Bosses with code {code}, setting {wanted}");
                AssertEquals(Menus.BossEditorSection.PlayProblem(BaseDcm(), options),
                    code ? Menus.Texts.GildebloonOff.T() : Menus.Texts.CodeTooNew.T(), "Why it can't be played");
            }

            // A normal Boss Challenge plays the game's own boss, with Gildebloon's models in the game
            Experimental.Gildebloon = true;
            Experimental.GildebloonSetting = () => true;
            Gildebloon.Register(Game.instance.model);
            yield return LoadIntoGame(SetupBossChallenge(BaseDcm(), BossType.Bloonarius));
            var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
            manager.SpawnBoss(1, false);
            yield return new WaitForSecondsRealtime(1);
            AssertEquals(InGame.instance.bridge.Simulation.model.bossBloonType, "Bloonarius", "Boss type of a normal Boss Challenge");
            AssertEquals(manager.CurrentBoss?.bloonModel.id, "Bloonarius1", "A normal Boss Challenge's boss");
            yield return ToMainMenu();
        }
        finally
        {
            Experimental.Gildebloon = flag;
            Experimental.GildebloonSetting = setting;
        }
    }
}

/// <summary>
/// Gildebloon's own abilities (#25): it steals cash into a shield, seizes a Banana Farm next to it at a skull, and
/// pays back what it stole, with interest, when it is beaten
/// </summary>
public class GildebloonBrainTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 20000;
        dcm.bloonModifiers.healthMultipliers.boss = 1;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));

        var bridge = InGame.instance.bridge;
        var manager = bridge.Simulation.map.spawner.bossBloonManager;
        bridge.SetFastForward(true);
        bridge.StartRound();
        yield return WaitFor(() => manager.CurrentBoss != null, "Gildebloon arriving", 90);
        var boss = manager.CurrentBoss!;

        // Toll: two thefts later the cash went down by what the Vault holds, and the shield holds the same
        var before = bridge.GetCash(-1);
        yield return WaitFor(() => GildebloonBrain.TotalStolen >= 2 * 20000 * GildebloonBrain.TollShare * 0.9 ||
                                   GildebloonBrain.TotalStolen >= 2 * 150, "Two thefts", 60);
        var stolen = GildebloonBrain.Stolen;
        Log($"stolen {stolen}, cash {before} -> {bridge.GetCash(-1)}, shield {boss.CurrentArmourAmount}");
        Assert(stolen > 0, "It stole cash");
        Assert(bridge.GetCash(-1) <= before - stolen + 1, $"Cash went down by what it stole: {before} -> {bridge.GetCash(-1)}, stolen {stolen}");
        Assert(boss.HasArmour && Math.Abs(boss.CurrentArmourAmount - stolen) < 2, $"Shield {boss.CurrentArmourAmount} for {stolen} stolen");
        AssertEquals(InGame.instance.bridge.Simulation.cashSpent.Value, 0.0, "Stealing isn't cash spent");
        yield return new WaitForSecondsRealtime(0.5f);
        AssertNotNull(Visuals.GildebloonHud.Panel, "The Vault is shown");

        // Repossession: a Banana Farm next to it is seized at its first skull
        var position = boss.Position;
        CreateTowerAt(bridge, new Vector2(position.X + 10, position.Y + 10), Game.instance.model.GetTowerFromId("BananaFarm"), costOverride: 0);
        yield return WaitFor(() => bridge.Simulation.towerManager.GetTowersByBaseId(GildebloonBrain.FarmBaseId).ToList().Count > 0, "The farm");
        var farm = bridge.Simulation.towerManager.GetTowersByBaseId(GildebloonBrain.FarmBaseId).ToList()[0];
        var beforeSkull = GildebloonBrain.Stolen;
        var cashBefore = bridge.GetCash(-1);
        boss.SetHealth((int) (boss.bloonModel.maxHealth * 0.79f));
        yield return WaitFor(() => GildebloonBrain.SeizedCount > 0, "A farm seized", 5);
        Assert(farm.IsAttackingDisabled, "The seized farm makes no money");
        Assert(farm.IsSellingBlocked, "The seized farm can't be sold");
        Log($"at the skull: vault {beforeSkull} -> {GildebloonBrain.Stolen}, cash {cashBefore} -> {bridge.GetCash(-1)}");
        Assert(GildebloonBrain.Stolen >= beforeSkull + Math.Min(cashBefore * GildebloonBrain.SkullShare, 150 * GildebloonBrain.SkullCapTimes) - 1,
            "It seized a share of the cash at the skull");
        yield return new WaitForSecondsRealtime(0.3f);
        Assert(Visuals.GildebloonHud.Repossessions > 0, "The Repossession was shown");
        var banner = AssertNotNull(Visuals.GildebloonHud.Banner, "The Repossession banner shows")!;
        Assert(banner.Contains("Gildebloon"), $"Banner: {banner}");

        // The Vault lies under the tower menu, like the boss panel (#25)
        var menu = Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu.TowerSelectionMenu.instance;
        var farmView = InGame.instance.bridge.GetAllTowers().ToList().First(tower => tower.tower.Pointer == farm.Pointer);
        menu.SelectTower(farmView);
        yield return new WaitForSecondsRealtime(1);
        var folder = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, "gildebloon-tower-menu");
        var panel = AssertNotNull(Visuals.GildebloonHud.Panel, "The Vault")!.transform;
        var bossUi = UnityEngine.Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.InGame.Races.BossUI>()!.transform;
        while (bossUi.parent != null && bossUi.parent != panel.parent) bossUi = bossUi.parent;
        AssertEquals(panel.GetSiblingIndex(), bossUi.GetSiblingIndex() + 1, "The Vault comes right after the boss panel");
        menu.DeselectTower();

        // Payout: beaten, it pays back what it stole, with interest
        var vault = GildebloonBrain.Stolen;
        ExtraTierGameTest.PlaceStrongTower(InGame.instance.GetGameModel().GetTowerFromId("DartMonkey"));
        var start = Time.realtimeSinceStartup;
        while (!bridge.Simulation.gameWon)
        {
            if (Time.realtimeSinceStartup - start > 240) Fail("Gildebloon wasn't beaten");
            if (!bridge.AreRoundsActive()) bridge.StartRound();
            yield return null;
        }

        Log($"payout {GildebloonBrain.LastPayout} for a vault of at least {vault}, farm released {!farm.IsAttackingDisabled}");
        Assert(GildebloonBrain.LastPayout >= Math.Floor(vault * GildebloonBrain.PayoutFactor), "It paid back what it stole, with interest");
        Assert(!farm.IsAttackingDisabled && !farm.IsSellingBlocked, "The farm is released when it is beaten");

        // The victory screen names Gildebloon and shows its portrait, not Bloonarius's
        Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossVictoryScreen? screen = null;
        yield return WaitFor(() => (screen = UnityEngine.Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossVictoryScreen>()) != null,
            "The victory screen", 30);
        yield return new WaitForSecondsRealtime(2);
        var title = screen!.defeatedBossTxt.text;
        Log($"victory screen: {title}");
        Assert(title.Contains("Gildebloon", StringComparison.OrdinalIgnoreCase), $"The victory screen names Gildebloon: {title}");
        Assert(!title.Contains("Bloonarius", StringComparison.OrdinalIgnoreCase), $"The victory screen doesn't name Bloonarius: {title}");
        if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, "gildebloon-victory");
        yield return ToMainMenu();
    }
}

/// <summary>
/// Gildebloon's own model from Bundles/gildebloon.bundle takes the place of its Bloonarius body, normal and Elite,
/// and its portraits come from Images/ (#25). With CBC_SCREENSHOTS set it takes a screenshot of each.
/// </summary>
public class GildebloonLookTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        Assert(Menus.BossEditorSection.GildebloonPortrait("Gildebloon") != BTD_Mod_Helper.Api.Enums.VanillaSprites.GoldenBloonIcon,
            "Gildebloon's portrait is in the mod");
        Assert(Menus.BossEditorSection.GildebloonPortrait("GildebloonElite") != BTD_Mod_Helper.Api.Enums.VanillaSprites.GoldenBloonIcon,
            "Elite Gildebloon's portrait is in the mod");

        var options = new BossOptions {Tiers = 2, SpawnRounds = "40,41", TierBosses = "Gildebloon,GildebloonElite"};
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(WeakBossDcm(), options));
        var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        var folder = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        foreach (var tier in new[] {1, 2})
        {
            manager.SpawnBoss(tier, false);
            yield return WaitFor(() => Visuals.GildebloonLook.Look != null || Visuals.GildebloonLook.Tinted, $"Tier {tier}'s look", 10);
            AssertEquals(Visuals.GildebloonLook.Problem, null, "The bundle loads");
            Assert(!Visuals.GildebloonLook.Tinted, $"Tier {tier} has its own model");
            var renderers = Visuals.GildebloonLook.Look!.GetComponentsInChildren<Renderer>().Length;
            Log($"tier {tier}: {manager.CurrentBoss?.bloonModel.id} with {renderers} renderers");
            Assert(renderers > 10, "The model has its parts");
            yield return new WaitForSecondsRealtime(2);
            if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, $"gildebloon-tier{tier}");

            // Hurt, the game swaps the body's parts (Elite's damage states); the Bloonarius body may show for a moment
            // until the next check, but never longer
            if (tier == 2)
            {
                ExtraTierGameTest.PlaceStrongTower(InGame.instance.GetGameModel().GetTowerFromId("DartMonkey"));
                var boss = manager.CurrentBoss!;
                var run = 0;
                var longest = 0;
                var start = Time.realtimeSinceStartup;
                while (!boss.IsDestroyed && boss.health > boss.bloonModel.maxHealth * 0.1f && Time.realtimeSinceStartup - start < 60)
                {
                    run = Visuals.GildebloonLook.BodyShows(boss) ? run + 1 : 0;
                    longest = Math.Max(longest, run);
                    yield return null;
                }
                Log($"tier {tier}: hurt to {boss.health}, body shown again {Visuals.GildebloonLook.BodyShownAgain} times, at most {longest} frames");
                Assert(longest <= Visuals.GildebloonLook.BodyCheckFrames + 2, $"The Bloonarius body showed for {longest} frames in a row");
            }
            if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, $"gildebloon-tier{tier}-hurt");
            manager.CurrentBoss?.Destroy();
            yield return new WaitForSecondsRealtime(1);
        }

        yield return ToMainMenu();
    }
}

/// <summary>
/// Losing to Gildebloon: the defeat screen names it and shows its portrait, not Bloonarius's (#25)
/// </summary>
public class GildebloonDefeatTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Elite = true, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.lives = dcm.startRules.maxLives = 1;
        dcm.bloonModifiers.bossSpeedMultiplier = 10;
        dcm.bloonModifiers.healthMultipliers.boss = 1;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossDefeatScreen? found = null;
        yield return LoseAndOpenDefeatScreen(screen => found = screen);
        var texts = string.Join(" | ", found!.GetComponentsInChildren<Il2CppTMPro.TMP_Text>(true)
            .Select(text => text.text).Where(text => !string.IsNullOrWhiteSpace(text)));
        Log($"defeat screen: {texts}");
        Assert(!texts.Contains("Bloonarius", StringComparison.OrdinalIgnoreCase), $"The defeat screen doesn't name Bloonarius: {texts}");
        var folder = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, "gildebloon-defeat");
        yield return ToMainMenu();
    }
}
