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
                AssertEquals(model.GetBehavior<SpawnBloonsActionModel>()?.bloonType, Gildebloon.CollectorFor(elite, tier),
                    $"{id} sends its tier's Tax Collector");
                Assert(model.GetBehavior<IncreaseDamageFromTypeModel>() == null && model.GetBehavior<SpawnDeathAnimModel>() == null, $"{id} has the mod's own Tarnish and no Bloonarius death");
                Assert(Gildebloon.IsGildebloon(id), $"{id} is one of Gildebloon's tiers");
            }
        }

        foreach (var id in Gildebloon.CollectorKinds.Select(kind => kind.Id))
        {
            Assert(game.DoesBloonExist(id), $"{id} exists");
            AssertEquals(game.GetBloon(id).leakDamage, 0f, $"{id} takes no lives");
            Assert(Gildebloon.IsCollector(id) && !Gildebloon.IsGildebloon(id), $"{id} is a Tax Collector");
        }

        // Tiers above 5 are made from its tier 5, like the game's bosses'
        var options = new BossOptions {Boss = BossList.Gildebloon, Elite = true, Tiers = 7};
        AssertEquals(ExtraTiers.MissingVanillaTier(game, options), null, "Every tier has a source model");
        AssertEquals(options.SourceId(7), "GildebloonElite5", "Tier 7's source");

        // Share codes with it are version 8, by name, and read back the same (#25)
        var dcm = BossMode.DefaultDcm(options);
        var code = Persistence.SetupCode.Encode(options, dcm);
        Assert(code.StartsWith(Persistence.SetupCode.Version8Prefix), $"A code with Gildebloon is version 8: {code}");
        Assert(Persistence.CodeFormat.Contents(code).Contains("\"Gildebloon\""), "The code has it by name");
        Assert(Persistence.SetupCode.TryDecode(code, out var read, out _, out var problem), $"The code loads: {problem}");
        AssertEquals(read, options.Sanitized(), "Options read from the code");
        var mixed = new BossOptions {Tiers = 2, TierBosses = "Lych,GildebloonElite"};
        Assert(Persistence.SetupCode.Encode(mixed, dcm).StartsWith(Persistence.SetupCode.Version8Prefix), "A tier with it is version 8 too");
        Assert(!Persistence.SetupCode.Encode(new BossOptions {Tiers = 1, TierBosses = ",Gildebloon"}, dcm)
            .StartsWith(Persistence.SetupCode.Version8Prefix), "A tier that isn't played doesn't count");
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
[FullSuiteOnly("plays a three-tier game with Lych, Gildebloon and Bloonarius to victory (about 50 s)")]
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
            // Gildebloon's glow sits on the camera, which stays from match to match: it must not come along
            Assert(Visuals.GildebloonMapOoze.Shown == null, "No Gildebloon glow round a normal Boss Challenge's map");
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
[FullSuiteOnly("plays Toll, a Repossession and the Payout to victory (about 45 s)")]
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

        // Toll: two thefts later the cash went down by what the Vault holds; a Toll gives no shield
        var before = bridge.GetCash(-1);
        yield return WaitFor(() => GildebloonBrain.TotalStolen >= 2 * 20000 * GildebloonBrain.TollShare * 0.9, "Two thefts", 60);
        var stolen = GildebloonBrain.Stolen;
        Log($"stolen {stolen}, cash {before} -> {bridge.GetCash(-1)}, shield {boss.CurrentArmourAmount}");
        Assert(stolen > 0, "It stole cash");
        Assert(bridge.GetCash(-1) <= before - stolen + 1, $"Cash went down by what it stole: {before} -> {bridge.GetCash(-1)}, stolen {stolen}");
        // A share of the cash, no cap: two thefts of 2 % of $20,000
        Assert(stolen >= 2 * 20000 * GildebloonBrain.TollShare * 0.9, $"It took a share of the cash: {stolen}");
        AssertEquals(GildebloonBrain.Armour(boss), 0f, "A Toll gives no shield");
        AssertEquals(InGame.instance.bridge.Simulation.cashSpent.Value, 0.0, "Stealing isn't cash spent");
        yield return new WaitForSecondsRealtime(0.5f);
        AssertNotNull(Visuals.GildebloonHud.Panel, "The Vault is shown");

        // Repossession: a Banana Farm next to it is seized at its first skull
        var position = boss.Position;
        CreateTowerAt(bridge, new Vector2(position.X + 25, position.Y + 30), Game.instance.model.GetTowerFromId("BananaFarm"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X - 20, position.Y - 35), Game.instance.model.GetTowerFromId("DartMonkey"), costOverride: 0);
        yield return WaitFor(() => bridge.Simulation.towerManager.GetTowersByBaseId(GildebloonBrain.FarmBaseId).ToList().Count > 0, "The farm");
        var farm = bridge.Simulation.towerManager.GetTowersByBaseId(GildebloonBrain.FarmBaseId).ToList()[0];
        var beforeSkull = GildebloonBrain.Stolen;
        var cashBefore = bridge.GetCash(-1);
        boss.SetHealth((int) (boss.bloonModel.maxHealth * 0.79f));
        yield return WaitFor(() => GildebloonBrain.SeizedCount > 0, "A farm seized", 5);
        Assert(farm.IsAttackingDisabled, "The seized farm makes no money");
        Assert(farm.IsSellingBlocked, "The seized farm can't be sold");
        Log($"at the skull: vault {beforeSkull} -> {GildebloonBrain.Stolen}, cash {cashBefore} -> {bridge.GetCash(-1)}");
        Assert(GildebloonBrain.Stolen >= beforeSkull + cashBefore * GildebloonBrain.SkullShare - 1,
            "It seized a share of the cash at the skull, with no cap");
        // The skull's cash is its shield: times its tier's shield per dollar, at most SkullShield of its health
        var perCash = GildebloonBrain.ShieldPerCashFor(false, TierMath.TierOf(boss.bloonModel.id));
        var seized = GildebloonBrain.Stolen - beforeSkull;
        var shield = Math.Min(seized * perCash, Math.Ceiling(boss.bloonModel.maxHealth * GildebloonBrain.SkullShield));
        Log($"shield {GildebloonBrain.Armour(boss)} for {seized} seized, {GildebloonBrain.ShieldSecondsLeft:0.0} s left");
        Assert(Math.Abs(GildebloonBrain.Armour(boss) - shield) < 2 * perCash, $"Shield {GildebloonBrain.Armour(boss)}, expected {shield}");
        Assert(GildebloonBrain.ShieldSecondsLeft > GildebloonBrain.ShieldSeconds, "The shield lasts its seconds after the skull's invulnerability");
        // While it is up it moves at half speed
        var from = boss.DistanceTraveled;
        var startedAt = bridge.Simulation.time.elapsed;
        yield return new WaitForSecondsRealtime(0.5f);
        var speed = (boss.DistanceTraveled - from) / Math.Max(1, bridge.Simulation.time.elapsed - startedAt);
        Log($"shielded speed {speed:0.000} per frame, its own {boss.bloonModel.speedFrames:0.000}");
        Assert(speed < boss.bloonModel.speedFrames * 0.75f, $"It is slowed while shielded: {speed} per frame");
        Assert(Visuals.GildebloonHud.ShieldShown > 0.5f, $"The shield bar shows it: {Visuals.GildebloonHud.ShieldShown}");
        yield return new WaitForSecondsRealtime(0.1f);
        var folderNow = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        if (!string.IsNullOrEmpty(folderNow)) yield return ScreenshotTour.Capture(folderNow, "gildebloon-repossession-ring");
        yield return new WaitForSecondsRealtime(0.3f);
        Assert(Visuals.GildebloonHud.Repossessions > 0, "The Repossession was shown");
        var banner = AssertNotNull(Visuals.GildebloonHud.Banner, "The Repossession banner shows")!;
        Log($"banner: {banner.Shown().Replace('\n', '/')}; frozen {GildebloonBrain.FrozenCount}, padlocks {Visuals.GildebloonSeizeLook.PadlockCount}");
        // In the player's language, which need not be English, as the game shows it (#291)
        var repossessionTitle = Texts.GildebloonRepossessionTitle.T();
        var oneFarm = Texts.GildebloonFarmLocked.T(GildebloonBrain.SeizeSeconds);
        Assert(banner.Shown().StartsWith(ScreenText.Plain(repossessionTitle), StringComparison.Ordinal) && banner.Shows(oneFarm),
            $"Banner: {banner.Shown()}");
        Assert(Visuals.GildebloonHud.CashShown > 0, "What it took showed at the cash");
        Assert(Visuals.GildebloonSeizeLook.RingsShown > 0, "The ring showed how far it reached");
        AssertEquals(Visuals.GildebloonSeizeLook.PadlockCount, GildebloonBrain.SeizedCount + GildebloonBrain.FrozenCount, "A padlock over each seized tower");
        if (!string.IsNullOrEmpty(folderNow)) yield return ScreenshotTour.Capture(folderNow, "gildebloon-repossession");

        // The banner says what it took, a line each, in the right form
        string Lines(double cash, int frozen, int farms) =>
            string.Join(" / ", Visuals.GildebloonHud.BannerLines(new GildebloonBrain.Repossession(cash, frozen, farms, 50, boss)));
        AssertEquals(Lines(2400, 1, 0), $"-$2,400 / {Texts.GildebloonTowerFrozen.T(GildebloonBrain.FreezeSeconds)}", "One tower");
        AssertEquals(Lines(2400, 30, 2), $"-$2,400 / {Texts.GildebloonTowersFrozen.T(30, GildebloonBrain.FreezeSeconds)} / " +
            Texts.GildebloonFarmsLocked.T(2, GildebloonBrain.SeizeSeconds), "Many towers and farms");
        AssertEquals(Lines(80, 0, 1), $"-$80 / {oneFarm}", "One farm");
        AssertEquals(Visuals.GildebloonHud.BannerText(new GildebloonBrain.Repossession(80, 0, 0, 50, boss)),
            repossessionTitle + "\n<size=60%>-$80</size>", "Only cash");

        // The Vault lies under the tower menu, like the boss panel (#25)
        var menu = Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu.TowerSelectionMenu.instance;
        var farmView = InGame.instance.bridge.GetAllTowers().ToList().First(tower => tower.tower.Pointer == farm.Pointer);
        menu.SelectTower(farmView);
        yield return new WaitForSecondsRealtime(1);
        var line = AssertNotNull(Visuals.GildebloonHud.SeizedLine, "The selected farm's time left shows")!;
        Assert(Enumerable.Range(1, GildebloonBrain.SeizeSeconds).Any(seconds => line.ShowsExactly(Texts.GildebloonSeizedFor.T(seconds))),
            $"Seized line: {line.Shown()}");
        var folder = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, "gildebloon-tower-menu");
        Assert(!Visuals.GildebloonHud.Panel!.gameObject.activeSelf, "The Vault makes way for the tower menu");
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
        yield return null;
        AssertEquals(Visuals.GildebloonSeizeLook.PadlockCount, 0, "The padlocks are gone when it is beaten");

        // The victory screen names Gildebloon and shows its portrait, not Bloonarius's
        Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossVictoryScreen? screen = null;
        yield return WaitFor(() => (screen = UnityEngine.Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossVictoryScreen>()) != null,
            "The victory screen", 30);
        yield return new WaitForSecondsRealtime(2);
        // Gildebloon's name is the same in every language; the game's own text around it is not (#291)
        var title = screen!.defeatedBossTxt.Shown();
        Log($"victory screen: {title} (as held: {screen.defeatedBossTxt.text})");
        Assert(title.Contains(BossList.GildebloonName, StringComparison.OrdinalIgnoreCase), $"The victory screen names Gildebloon: {title}");
        Assert(!title.Contains(BossList.Name(BossList.Host(BossList.Gildebloon)), StringComparison.OrdinalIgnoreCase),
            $"The victory screen doesn't name Bloonarius: {title}");
        if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, "gildebloon-victory");
        yield return ToMainMenu();
    }
}

/// <summary>
/// Gildebloon's own model from Bundles/gildebloon.bundle takes the place of its Bloonarius body, normal and Elite,
/// and its portraits come from Images/ (#25). With CBC_SCREENSHOTS set it takes a screenshot of each.
/// </summary>
[FullSuiteOnly("hurts each tier's look down to no health (about 35 s)")]
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
            if (!string.IsNullOrEmpty(folder))
            {
                yield return ScreenshotTour.Capture(folder, $"gildebloon-tier{tier}");
                // again once Spawn has played (3.5 s) and its eyes are open, going along the track
                yield return new WaitForSecondsRealtime(2.5f);
                yield return ScreenshotTour.Capture(folder, $"gildebloon-tier{tier}-awake");
            }

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
                Assert(longest <= 1, $"The Bloonarius body showed for {longest} frames in a row");
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
            .Select(text => text.Shown()).Where(text => !string.IsNullOrWhiteSpace(text)));
        Log($"defeat screen: {texts}");
        Assert(texts.Contains(BossList.GildebloonName, StringComparison.OrdinalIgnoreCase), $"The defeat screen names Gildebloon: {texts}");
        Assert(!texts.Contains(BossList.Name(BossList.Host(BossList.Gildebloon)), StringComparison.OrdinalIgnoreCase),
            $"The defeat screen doesn't name Bloonarius: {texts}");
        var folder = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, "gildebloon-defeat");
        yield return ToMainMenu();
    }
}

/// <summary>
/// No sign of Bloonarius in a Gildebloon match (#25): not its slime on the map (a pile of money instead), not its body
/// as the Elite is beaten down past every skull and dies, not its skin on the Tax Collectors. In Advanced Mode the
/// map's effects follow the boss: a game boss's come back after Gildebloon's pile.
/// </summary>
[FullSuiteOnly("plays two games scanning every renderer for Bloonarius (about 30 s)")]
public class GildebloonNoBloonariusTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var folder = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        var options = new BossOptions {Boss = BossList.Gildebloon, Elite = true, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.bloonModifiers.healthMultipliers.boss = 1;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        yield return WaitFor(() => Visuals.GildebloonMapLook.Pile != null, "The pile of money on the map", 10);
        Assert(Visuals.GildebloonMapOoze.Shown != null, "The gold round the map is there");
        Log($"gold round the map over {Visuals.GildebloonMapOoze.Area.size}");
        Assert(Visuals.GildebloonMapLook.Modelled, "The pile is the one modelled in Blender (gildebloon-pile.mesh.gz)");
        Assert(Visuals.BossPresentation.MapEffects.All(name => !name.Contains("Bloonarius") ||
                UnityEngine.GameObject.Find(name) == null), $"Bloonarius's map effects are hidden: {string.Join(", ", Visuals.BossPresentation.MapEffects)}");
        AssertNoBloonarius("before the boss");
        if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, "gildebloon-pile");

        var manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        manager.SpawnBoss(1, false);
        yield return WaitFor(() => Visuals.GildebloonLook.Look != null, "The look", 10);
        var boss = manager.CurrentBoss!;

        // A tower that beats it in some 25 hits, so it goes through every skull and damage state
        var tower = InGame.instance.GetGameModel().GetTowerFromId("DartMonkey");
        ExtraTierGameTest.MakeStrong(tower);
        foreach (var attack in tower.GetAttackModels())
        {
            foreach (var weapon in attack.weapons)
            {
                weapon.rate = 0.05f;
                if (weapon.projectile.GetDamageModel() is { } damage) damage.damage = boss.bloonModel.maxHealth / 25f;
            }
        }
        CreateTowerAt(InGame.instance.bridge, Vector2.zero, tower, costOverride: 0);
        InGame.instance.bridge.SetFastForward(true);

        var frames = 0;
        var collectors = 0;
        var start = Time.realtimeSinceStartup;
        var shotHurt = false;
        var shotCollector = false;
        var collectorLooks = 0;
        var seen = new System.Collections.Generic.Dictionary<IntPtr, int>();
        while (!boss.IsDestroyed && boss.health > 0)
        {
            if (Time.realtimeSinceStartup - start > 150) Fail($"Not beaten after 150s, {boss.health} health left");
            frames++;
            Assert(!Visuals.GildebloonLook.BodyShows(boss), $"The Bloonarius body shows at {boss.health} health");
            foreach (var bloon in InGame.instance.GetBloons().Where(bloon => Gildebloon.IsCollector(bloon.bloonModel.id)))
            {
                collectors = Math.Max(collectors, 1);
                // The look takes the skin off the frame after the game sends it
                seen.TryGetValue(bloon.Pointer, out var seenFrames);
                seen[bloon.Pointer] = ++seenFrames;
                if (seenFrames < 3) continue;
                if (Visuals.GildebloonLook.HasLook(bloon)) Assert(!Visuals.GildebloonLook.BodyShows(bloon), "A Tax Collector's MOAB body doesn't show");
                Assert(bloon.mutators == null || !bloon.mutators.ToArray().Any(m => m.mutator?.id == Visuals.GildebloonLook.BossSkinMutator),
                    "A Tax Collector has no boss skin");
            }
            if (frames % 5 == 0) AssertNoBloonarius($"at {boss.health} health");
            collectorLooks = Math.Max(collectorLooks, Visuals.GildebloonLook.CollectorLooks);
            if (!shotCollector && Visuals.GildebloonLook.CollectorLooks > 0 && !string.IsNullOrEmpty(folder))
            {
                shotCollector = true;
                yield return ScreenshotTour.Capture(folder, "gildebloon-collector");
                // and once it has come out of the boss and gone ahead
                yield return new WaitForSecondsRealtime(1.5f);
                yield return ScreenshotTour.Capture(folder, "gildebloon-collector-out");
            }
            if (!shotHurt && boss.health < boss.bloonModel.maxHealth * 0.3f && !string.IsNullOrEmpty(folder))
            {
                shotHurt = true;
                yield return ScreenshotTour.Capture(folder, "gildebloon-elite-hurt");
            }
            yield return null;
        }

        Log($"beaten after {frames} frames; skulls {InGame.instance.bridge.GetCurrentBossSkull()}, body parts hidden again {Visuals.GildebloonLook.BodyShownAgain}, " +
            $"collectors seen {collectors}, collector looks now {Visuals.GildebloonLook.CollectorLooks}");
        Assert(collectors > 0, "It sent Tax Collectors");
        Assert(collectorLooks > 0, "A Tax Collector had its own look");

        // Its end: it bursts into coins, and nothing of Bloonarius's death shows
        var end = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - end < 4)
        {
            AssertNoBloonarius("as it dies");
            yield return null;
        }
        Assert(Visuals.GildebloonLook.BurstCount > 0, "It burst into coins");
        yield return ToMainMenu();

        // Advanced Mode: Lych's effects, then Gildebloon's pile, then Lych's again
        options = new BossOptions {Tiers = 3, SpawnRounds = "40,41,42", TierBosses = "Lych,Gildebloon,Lych"};
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(WeakBossDcm(), options));
        manager = InGame.instance.bridge.Simulation.map.spawner.bossBloonManager;
        foreach (var tier in new[] {1, 2, 3})
        {
            manager.SpawnBoss(tier, false);
            yield return new WaitForSecondsRealtime(1.5f);
            var effects = string.Join(", ", Visuals.BossPresentation.MapEffects);
            var active = Visuals.BossPresentation.MapEffects.Count(name => UnityEngine.GameObject.Find(name) != null);
            Log($"tier {tier}: effects {effects} ({active} shown), pile {Visuals.GildebloonMapLook.Pile != null}");
            if (tier == 2)
            {
                AssertNotNull(Visuals.GildebloonMapLook.Pile, "Gildebloon's pile");
                AssertNotNull(Visuals.GildebloonMapOoze.Shown, "Gildebloon's glow round the map");
                AssertNoBloonarius("at Gildebloon's tier");
            }
            else
            {
                Assert(Visuals.GildebloonMapLook.Pile == null, $"No pile at tier {tier}");
                Assert(Visuals.GildebloonMapOoze.Shown == null, $"No Gildebloon glow round the map at tier {tier}, a game boss's");
                Assert(effects.Length == 0 || active > 0, $"Lych's effects show at tier {tier}");
            }
            manager.CurrentBoss?.Destroy();
            yield return new WaitForSecondsRealtime(1);
        }
        yield return ToMainMenu();
    }

    private void AssertNoBloonarius(string when)
    {
        var showing = Visuals.GildebloonLook.BloonariusShowing();
        Assert(showing.Count == 0, $"Bloonarius shows {when}: {string.Join(", ", showing.Take(5))}");
    }
}

/// <summary>
/// Gildebloon's own rounds, Gold Rush (#25): with Boss Event Rounds a Gildebloon match plays all 140 of them, as the
/// mod brings them, and freeplay after; without, the default rounds; in CHIMPS the CHIMPS events' rounds, unless the
/// setup picks the boss's own. In Advanced Mode a Gildebloon tier gets its own boss round.
/// </summary>
[FullSuiteOnly("compares Gold Rush's rounds with the game's in five games (about 25 s)")]
public class GildebloonRoundsTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var defaults = Il2CppAssets.Scripts.Data.GameData.Instance.RoundSetByName("DefaultRoundSet").rounds;
        var gold = ChimpsRoundSet.Rounds(BossOptions.GildebloonRoundSet);
        string Mine(ChimpsRoundSet.Round round) => string.Join(" ", round.Groups.Select(g => $"{g.Count}x{g.Bloon}@{g.Start * 60f:0}"));
        string Game(Il2CppAssets.Scripts.Models.Rounds.RoundModel round) => string.Join(" ", round.groups.Select(g => $"{g.count}x{g.bloon}@{g.start:0}"));
        Il2CppAssets.Scripts.Models.Rounds.RoundModel[] Match() => InGame.instance.GetGameModel().roundSet.rounds.ToArray();

        var on = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40", BossEventRounds = true};
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(BaseDcm(), on));
        var rounds = Match();
        AssertEquals(rounds.Length, defaults.Length, "As many rounds as the default, freeplay after them");
        foreach (var number in new[] {11, 39, 63, 79, 119, 139})
        {
            AssertEquals(Game(rounds[number - 1]), Mine(gold[number - 1]), $"Round {number} is Gold Rush's");
        }
        Log($"round 39: {Game(rounds[38])}");
        yield return ToMainMenu();

        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(BaseDcm(), on with {BossEventRounds = false}));
        AssertEquals(Game(Match()[38]), Game(defaults[38]), "Without Boss Event Rounds the default round 39");
        yield return ToMainMenu();

        var chimps = BaseDcm();
        chimps.mode = BossOptions.ChimpsMode;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(chimps, on));
        AssertEquals(Game(Match()[38]), Mine(ChimpsRoundSet.Rounds(BossOptions.ChimpsBossRoundSet)[38]), "CHIMPS plays the CHIMPS events' rounds");
        yield return ToMainMenu();
        chimps = BaseDcm();
        chimps.mode = BossOptions.ChimpsMode;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(chimps, on with {ChimpsRounds = ChimpsRounds.Own}));
        AssertEquals(Game(Match()[38]), Mine(gold[38]), "CHIMPS with the boss's own rounds plays Gold Rush");
        yield return ToMainMenu();

        // Advanced Mode: Lych's rounds, and Gildebloon's own boss round for its tier
        var mixed = new BossOptions {Tiers = 2, SpawnRounds = "40,60", TierBosses = "Lych,Gildebloon", BossEventRounds = true};
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(BaseDcm(), mixed));
        AssertEquals(Game(Match()[59]), Mine(gold[59]), "Gildebloon's tier has its own boss round");
        yield return ToMainMenu();
    }
}

/// <summary>
/// Tarnish shows (#25): an Alchemist's potions hitting Gildebloon make its modelled body play TarnishHit. The game's
/// damage can't be hooked, so the look counts a hit right after an acid projectile was at it.
/// </summary>
[FullSuiteOnly("plays a game until an Alchemist has hit it (about 30 s)")]
public class GildebloonTarnishTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 20000;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        Assert(Visuals.GildebloonBody.Ready, "The modelled body loaded");

        var bridge = InGame.instance.bridge;
        var manager = bridge.Simulation.map.spawner.bossBloonManager;
        bridge.SetFastForward(true);
        bridge.StartRound();
        yield return WaitFor(() => manager.CurrentBoss != null, "Gildebloon arriving", 90);
        var before = Visuals.GildebloonLook.TarnishHits;
        var position = manager.CurrentBoss!.Position;
        CreateTowerAt(bridge, new Vector2(position.X + 20, position.Y + 25), Game.instance.model.GetTowerFromId("Alchemist"), costOverride: 0);
        yield return WaitFor(() => Visuals.GildebloonLook.TarnishHits > before, "An Alchemist's hit tarnishing it", 40);
        Log($"tarnished {Visuals.GildebloonLook.TarnishHits - before} times, last animation {Visuals.GildebloonLook.LastAnimation}");
        yield return ToMainMenu();
    }
}

/// <summary>
/// What towers put on a boss shows on Gildebloon (#25): the game draws it in Bloonarius's overlays (Gildebloon's
/// overlay class is Bloonarius's), which the look used to hide with Bloonarius's body. A Ninja's sticky bomb shows,
/// on Bloonarius's rig shrunk onto Gildebloon; a Glue Gunner's green glue and a Wizard's fire show as Gildebloon's
/// own coats in place of the game's, which are shaped for Bloonarius, and so does a Dartling's Laser Shock. Tax
/// Collectors are spawned for a look at their size.
/// </summary>
[FullSuiteOnly("plays a game until glue, fire, laser shock and a sticky bomb are on it (about 35 s)")]
public class GildebloonOverlayTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 20000;
        dcm.bloonModifiers.healthMultipliers.boss = 30;
        dcm.bloonModifiers.bossSpeedMultiplier = 0.2f;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        Assert(Visuals.GildebloonBody.Ready, "The modelled body loaded");

        var bridge = InGame.instance.bridge;
        var manager = bridge.Simulation.map.spawner.bossBloonManager;
        bridge.StartRound();
        yield return WaitFor(() => manager.CurrentBoss != null, "Gildebloon arriving", 90);
        // Tax Collectors are as big as the MOAB or BFB they replace: a look at them before the towers come, without the
        // round's own MOAB in the way
        foreach (var other in InGame.instance.GetBloons().Where(bloon => !bloon.bloonModel.isBoss).ToList()) other.Destroy();
        InGame.instance.SpawnBloons(Gildebloon.CollectorId, 1, 0);
        InGame.instance.SpawnBloons(Gildebloon.BfbCollectorId, 1, 0);
        yield return WaitFor(() => Visuals.GildebloonLook.CollectorLooks >= 2, "Two Tax Collectors with their look", 10);
        for (var wait = 0f; wait < 4; wait += Time.unscaledDeltaTime)
        {
            foreach (var other in InGame.instance.GetBloons().Where(bloon => !bloon.bloonModel.isBoss && !Gildebloon.IsCollector(bloon.bloonModel.id)).ToList())
                other.Destroy();
            yield return null;
        }
        var shots = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        if (!string.IsNullOrEmpty(shots)) yield return ScreenshotTour.Capture(shots, "gildebloon-collectors-sized");

        var position = manager.CurrentBoss!.Position;
        CreateTowerAt(bridge, new Vector2(position.X + 22, position.Y + 22), Game.instance.model.GetTowerFromId("GlueGunner-300"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X - 22, position.Y + 22), Game.instance.model.GetTowerFromId("NinjaMonkey-004"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X, position.Y - 28), Game.instance.model.GetTowerFromId("WizardMonkey-030"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X + 30, position.Y - 10), Game.instance.model.GetTowerFromId("DartlingGunner-200"), costOverride: 0);

        Transform? Overlay(string name)
        {
            var display = manager.CurrentBoss?.GetUnityDisplayNode();
            return display == null ? null
                : display.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name && t.parent?.name == Visuals.GildebloonLook.OverlaysName);
        }

        // A sticky bomb is the game's own, drawn on it
        yield return WaitFor(() => Overlay("BloonariusStickyBomb")?.gameObject.activeInHierarchy == true, "A sticky bomb on Gildebloon", 40);
        var bomb = Overlay("BloonariusStickyBomb")!.GetComponentsInChildren<Renderer>().ToList();
        Assert(bomb.Count > 0 && bomb.All(r => !r.forceRenderingOff), "The sticky bomb is drawn on Gildebloon");

        // Glue and fire coat the whole boss: Gildebloon's own coat shows, the game's (Bloonarius-shaped) doesn't
        foreach (var (name, coat) in new[] {("BloonariusGlueDissolve", "GlueDissolve"), ("BloonariusFire", "Fire"), ("BloonariusLaserShockEffect", "LaserShock")})
        {
            yield return WaitFor(() => Overlay(name)?.gameObject.activeInHierarchy == true && Visuals.GildebloonLook.CoatsShown.Contains(coat),
                $"{coat} on Gildebloon", 40);
            // The look hides the game's coat when it comes on and then every few frames, so parts the game adds a moment
            // later are hidden at the next look
            yield return WaitFor(() => Overlay(name)?.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff) == true,
                $"The game's {name} is hidden", 2);
        }
        Log($"coats shown: {string.Join(", ", Visuals.GildebloonLook.CoatsShown)}");

        // On its body: the overlays' rig is shrunk onto it
        var rig = Overlay("BloonariusGlueDissolve")!.parent.parent;
        AssertEquals(rig.localScale, Visuals.GildebloonLook.OverlayScale, "Bloonarius's rig shrunk onto Gildebloon");
        Assert(Visuals.GildebloonLook.OverlaysOn >= 2, $"Effects on it: {Visuals.GildebloonLook.OverlaysOn}");
        var folder = System.Environment.GetEnvironmentVariable(ScreenshotTour.EnvironmentVariable);
        if (!string.IsNullOrEmpty(folder)) yield return ScreenshotTour.Capture(folder, "gildebloon-overlays");
        yield return ToMainMenu();
    }
}

/// <summary>
/// Gildebloon never stands still for a player with a lot of cash and weak towers (#25): its health goes down between
/// skulls. A shield from every Toll once made tier 3 stand at 195,001 of 200,000 (2026-11-22); Tolls give none now.
/// </summary>
[FullSuiteOnly("plays a rich game with one weak tower until its health drops (about 25 s)")]
public class GildebloonNoStallTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 1_000_000;
        dcm.bloonModifiers.healthMultipliers.boss = 1;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));

        var bridge = InGame.instance.bridge;
        var manager = bridge.Simulation.map.spawner.bossBloonManager;
        bridge.SetFastForward(true);
        bridge.StartRound();
        yield return WaitFor(() => manager.CurrentBoss != null, "Gildebloon arriving", 90);
        var boss = manager.CurrentBoss!;
        var position = boss.Position;
        CreateTowerAt(bridge, new Vector2(position.X + 20, position.Y + 20), Game.instance.model.GetTowerFromId("DartMonkey"), costOverride: 0);

        // Three Tolls of 2 % of a million go by, and still every hit takes health
        var start = boss.health;
        yield return WaitFor(() => GildebloonBrain.TotalStolen >= 3 * 1_000_000 * GildebloonBrain.TollShare * 0.85, "Three Tolls", 60);
        Log($"health {start} -> {boss.health}, shield {GildebloonBrain.Armour(boss)}, stolen {GildebloonBrain.TotalStolen}");
        AssertEquals(GildebloonBrain.Armour(boss), 0f, "Tolls give no shield, however rich the player");
        Assert(boss.health < start, $"A Dart Monkey hurts it while the player is rich: {start} -> {boss.health}");
        yield return ToMainMenu();
    }
}

/// <summary>
/// The shield from a skull runs out (#25): after the skull's invulnerability and its <see cref="GildebloonBrain.ShieldSeconds"/>
/// it is gone even if no tower broke it, the look plays VaultBreak, and Gildebloon moves at its own speed again
/// </summary>
[FullSuiteOnly("waits out a skull's shield (about 20 s)")]
public class GildebloonShieldTimeTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 50_000;
        dcm.bloonModifiers.healthMultipliers.boss = 1;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));

        var bridge = InGame.instance.bridge;
        var manager = bridge.Simulation.map.spawner.bossBloonManager;
        bridge.SetFastForward(true);
        bridge.StartRound();
        yield return WaitFor(() => manager.CurrentBoss != null, "Gildebloon arriving", 90);
        var boss = manager.CurrentBoss!;
        boss.SetHealth((int) (boss.bloonModel.maxHealth * 0.79f));
        yield return WaitFor(() => GildebloonBrain.Armour(boss) > 0, "The skull's shield", 10);
        var shieldAt = bridge.Simulation.time.elapsed;
        Log($"shield {GildebloonBrain.Armour(boss)}, {GildebloonBrain.ShieldSecondsLeft:0.0} s");

        yield return WaitFor(() => GildebloonBrain.Armour(boss) <= 0, "The shield running out", 30);
        var lasted = (bridge.Simulation.time.elapsed - shieldAt) / (float) GildebloonBrain.FramesPerSecond;
        Log($"the shield lasted {lasted:0.0} s; last animation {Visuals.GildebloonLook.LastAnimation}");
        Assert(lasted >= GildebloonBrain.ShieldSeconds + Gildebloon.SkullSeconds - 0.5f &&
               lasted <= GildebloonBrain.ShieldSeconds + Gildebloon.SkullSeconds + 0.5f, $"It lasted its seconds: {lasted:0.0}");
        yield return WaitFor(() => Visuals.GildebloonLook.LastAnimation == "VaultBreak", "VaultBreak as it goes", 3);

        // its own speed again
        var from = boss.DistanceTraveled;
        var frames = bridge.Simulation.time.elapsed;
        yield return new WaitForSecondsRealtime(0.5f);
        var speed = (boss.DistanceTraveled - from) / System.Math.Max(1, bridge.Simulation.time.elapsed - frames);
        Assert(speed > boss.bloonModel.speedFrames * 0.85f, $"Its own speed without the shield: {speed} per frame of {boss.bloonModel.speedFrames}");
        yield return ToMainMenu();
    }
}

/// <summary>
/// A Tax Collector that leaks takes its kind's share of the cash and no lives (#25): a MOAB 5 %, a BFB 8 %, a ZOMG 10 %,
/// a fortified ZOMG 12 %
/// </summary>
public class GildebloonCollectorLeakTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 100_000;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        var bridge = InGame.instance.bridge;

        foreach (var kind in Gildebloon.CollectorKinds)
        {
            InGame.instance.SpawnBloons(kind.Id, 1, 0);
            yield return WaitFor(() => InGame.instance.GetBloons().Any(bloon => bloon.bloonModel.id == kind.Id), $"A {kind.Source} Tax Collector", 5);
            var collector = InGame.instance.GetBloons().First(bloon => bloon.bloonModel.id == kind.Id);
            var cash = bridge.GetCash(-1);
            var lives = InGame.instance.GetHealth();
            collector.Leaked();
            yield return null;
            var taken = cash - bridge.GetCash(-1);
            Log($"{kind.Id} leaked: took {taken} of {cash}, lives {lives} -> {InGame.instance.GetHealth()}");
            AssertEquals(taken, System.Math.Floor(cash * kind.Share), $"A leaking {kind.Source} takes {kind.Share:P0}");
            AssertEquals(InGame.instance.GetHealth(), lives, $"A leaking {kind.Source} takes no lives");
        }
        yield return ToMainMenu();
    }
}

/// <summary>
/// Elite's Interest (#25): while nothing hits it its Vault grows, which is paid back, and gives it no shield (the
/// shield comes only at a skull)
/// </summary>
[FullSuiteOnly("waits for an Elite's Interest (about 20 s)")]
public class GildebloonInterestTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Elite = true, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 50_000;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));

        var bridge = InGame.instance.bridge;
        var manager = bridge.Simulation.map.spawner.bossBloonManager;
        var interest = 0.0;
        void Added(double amount) => interest += amount;
        GildebloonBrain.InterestAdded += Added;
        try
        {
            bridge.SetFastForward(true);
            bridge.StartRound();
            yield return WaitFor(() => manager.CurrentBoss != null, "Elite Gildebloon arriving", 90);
            yield return WaitFor(() => GildebloonBrain.Stolen > 0, "A Toll", 20);
            yield return WaitFor(() => interest > 0, "Interest while nothing hits it", 20);
            var stolen = GildebloonBrain.TotalStolen;
            Log($"interest {interest}, vault {GildebloonBrain.Stolen}, stolen from the cash {stolen}");
            Assert(GildebloonBrain.Stolen >= stolen + interest - 1, "The Vault holds what it stole and its Interest");
            AssertEquals(GildebloonBrain.Armour(manager.CurrentBoss!), 0f, "Interest gives no shield");
        }
        finally
        {
            GildebloonBrain.InterestAdded -= Added;
        }
        yield return ToMainMenu();
    }
}

/// <summary>
/// The coats GildebloonOverlayTest doesn't put on it (#25): plain glue, the pink glue with bubbles and Unstable
/// Concoction's acid. Each tower in a game of its own: while
/// the game's overlay is on, Gildebloon's own coat shows and the game's (shaped for Bloonarius) doesn't draw. The game
/// switches its overlays to what is on the boss every frame, so it takes the real towers.
/// </summary>
[FullSuiteOnly("plays three games, a tower's coat in each (about 60 s)")]
public class GildebloonCoatsTest : GildebloonTest
{
    private static readonly (string Tower, string Overlay, string Coat)[] Steps =
    [
        ("GlueGunner-200", "BloonariusGlueBasic", "GlueBasic"),
        // On a boss the pink glue with bubbles comes from 2-0-5; 0-0-4 and 0-0-5 glue no boss. Plain pink glue
        // (GlueStronger) is 2-0-3's and 2-0-4's in the models, but neither glued it in 25 s (2026-11-22), so it isn't here
        ("GlueGunner-205", "BloonariusGlueStrongerDot", "GlueStrongerDot"),
        ("Alchemist-030", "BloonariusUnstableConcoction", "UnstableConcoction")
    ];

    protected override IEnumerator RunOn()
    {
        // a game each: a glued bloon isn't glued again by another Glue Gunner
        var failed = new System.Collections.Generic.List<string>();
        foreach (var (tower, overlay, coat) in Steps)
        {
            var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
            var dcm = WeakBossDcm();
            dcm.startRules.cash = 20000;
            dcm.bloonModifiers.healthMultipliers.boss = 30;
            dcm.bloonModifiers.bossSpeedMultiplier = 0.2f;
            yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
            Assert(Visuals.GildebloonBody.Ready, "The modelled body loaded");
            var bridge = InGame.instance.bridge;
            var manager = bridge.Simulation.map.spawner.bossBloonManager;
            bridge.StartRound();
            yield return WaitFor(() => manager.CurrentBoss != null, "Gildebloon arriving", 90);
            yield return new WaitForSecondsRealtime(1);

            Transform? Overlay(string name)
            {
                var display = manager.CurrentBoss?.GetUnityDisplayNode();
                return display == null ? null
                    : display.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name && t.parent?.name == Visuals.GildebloonLook.OverlaysName);
            }

            AssertEquals(Visuals.GildebloonLook.CoatsShown.Count, 0, "No coat without an effect on it");
            var position = manager.CurrentBoss!.Position;
            CreateTowerAt(bridge, new Vector2(position.X + 22, position.Y + 22), Game.instance.model.GetTowerFromId(tower), costOverride: 0);
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var waited = 0f; waited < 25 && !(Overlay(overlay)?.gameObject.activeInHierarchy == true && Visuals.GildebloonLook.CoatsShown.Contains(coat));
                 waited += Time.unscaledDeltaTime)
            {
                var group = Overlay(overlay)?.parent;
                for (var i = 0; group != null && i < group.childCount; i++)
                {
                    if (group.GetChild(i).gameObject.activeSelf && seen.Add(group.GetChild(i).name)) Log($"{tower}: on {group.GetChild(i).name}");
                }
                yield return null;
            }
            if (!(Overlay(overlay)?.gameObject.activeInHierarchy == true && Visuals.GildebloonLook.CoatsShown.Contains(coat)))
                failed.Add($"{coat} from a {tower}; on: {string.Join(", ", seen)}");
            else if (!Overlay(overlay)!.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff)) failed.Add($"The game's {overlay} draws");
            Log($"{tower}: coats shown {string.Join(", ", Visuals.GildebloonLook.CoatsShown)}");
            yield return ToMainMenu();
        }
        AssertEquals(string.Join("; ", failed), "", "Every coat showed");
    }
}

/// <summary>
/// Tax Collectors are as big as the bloon they replace (#25): each kind's look covers that bloon's length and width
/// (measured from the game's MOAB, BFB and ZOMG), within a tenth, and the bloon's own body doesn't draw
/// </summary>
public class GildebloonCollectorSizeTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        Assert(Visuals.GildebloonBody.Ready, "The modelled body loaded");

        foreach (var kind in Gildebloon.CollectorKinds)
        {
            InGame.instance.SpawnBloons(kind.Id, 1, 0);
            Il2CppAssets.Scripts.Simulation.Bloons.Bloon? collector = null;
            yield return WaitFor(() =>
            {
                collector = InGame.instance.GetBloons().FirstOrDefault(bloon => bloon.bloonModel.id == kind.Id);
                return collector != null && Visuals.GildebloonLook.HasLook(collector);
            }, $"A {kind.Source} Tax Collector with its look", 5);
            yield return null;

            var node = collector!.GetUnityDisplayNode().transform;
            var look = node.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith(Visuals.GildebloonLook.RootName) && t.parent == node);
            var points = look.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).SelectMany(f =>
                new[] {-1, 1}.SelectMany(x => new[] {-1, 1}.SelectMany(y => new[] {-1, 1}.Select(z =>
                    node.InverseTransformPoint(f.transform.TransformPoint(f.sharedMesh.bounds.center +
                                                                          Vector3.Scale(f.sharedMesh.bounds.extents, new Vector3(x, y, z)))))))).ToList();
            var width = points.Max(p => p.x) - points.Min(p => p.x);
            var length = points.Max(p => p.z) - points.Min(p => p.z);
            Log($"{kind.Id}: {width:0.0} x {length:0.0}, its bloon {kind.Width} x {kind.Length}");
            Assert(System.Math.Abs(length - kind.Length) <= kind.Length * 0.1f, $"{kind.Id} is as long as a {kind.Source}: {length:0.0} of {kind.Length}");
            Assert(System.Math.Abs(width - kind.Width) <= kind.Width * 0.1f, $"{kind.Id} is as wide as a {kind.Source}: {width:0.0} of {kind.Width}");
            Assert(!Visuals.GildebloonLook.BodyShows(collector), $"The {kind.Source}'s own body doesn't draw");
            collector.Destroy();
            yield return null;
        }
        yield return ToMainMenu();
    }
}

/// <summary>
/// Gildebloon guards its gold against towers that make money (#25): the upgrades that make cash do
/// <see cref="Gildebloon.CashTowerDamage"/> of their damage to it, and so do towers in a Monkey Town's reach. Two
/// Sniper Monkeys 2-0-0 shoot it, one beside a Monkey Village 0-0-3: that one does about half the damage.
/// </summary>
[FullSuiteOnly("two Snipers shoot it, one in a Monkey Town (about 25 s)")]
public class GildebloonCashGuardTest : GildebloonTest
{
    protected override IEnumerator RunOn()
    {
        var game = Game.instance.model;
        var cashTowers = Gildebloon.CashTowers(game);
        foreach (var (tower, makesCash) in new[]
                 {
                     ("SniperMonkey-030", false), ("SniperMonkey-040", true), ("SniperMonkey-050", true), ("SniperMonkey-Paragon", true),
                     ("HeliPilot-030", false), ("HeliPilot-040", true), ("EngineerMonkey-003", false), ("EngineerMonkey-004", true),
                     ("MonkeyBuccaneer-002", false), ("MonkeyBuccaneer-003", true), ("Alchemist-002", false), ("Alchemist-003", true),
                     ("DartMonkey-502", false), ("SuperMonkey-400", true), ("Druid-040", true)
                 })
            AssertEquals(cashTowers.Contains(tower), makesCash, $"{tower} makes cash");

        var options = new BossOptions {Boss = BossList.Gildebloon, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 20000;
        dcm.bloonModifiers.healthMultipliers.boss = 30;
        dcm.bloonModifiers.bossSpeedMultiplier = 0.2f;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        var bridge = InGame.instance.bridge;
        var manager = bridge.Simulation.map.spawner.bossBloonManager;
        bridge.StartRound();
        yield return WaitFor(() => manager.CurrentBoss != null, "Gildebloon arriving", 90);
        var position = manager.CurrentBoss!.Position;

        // a Sniper far from the Monkey Town, and one beside it
        CreateTowerAt(bridge, new Vector2(position.X + 30, position.Y - 10), game.GetTowerFromId("SniperMonkey-200"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X - 22, position.Y + 22), game.GetTowerFromId("MonkeyVillage-003"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X - 22, position.Y + 50), game.GetTowerFromId("SniperMonkey-200"), costOverride: 0);
        yield return WaitFor(() => bridge.Simulation.towerManager.GetTowersByBaseId("SniperMonkey").ToList().Count == 2 &&
                                   bridge.Simulation.towerManager.GetTowersByBaseId("MonkeyVillage").ToList().Count == 1, "Two Snipers and a Monkey Town", 10);
        var town = bridge.Simulation.towerManager.GetTowersByBaseId("MonkeyVillage").ToList()[0];
        var snipers = bridge.Simulation.towerManager.GetTowersByBaseId("SniperMonkey").ToList();
        var near = snipers.OrderBy(s => (s.Position.X - town.Position.X) * (s.Position.X - town.Position.X) + (s.Position.Y - town.Position.Y) * (s.Position.Y - town.Position.Y)).First();
        var far = snipers.First(s => s.Pointer != near.Pointer);

        // only Gildebloon to shoot at
        var start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 12)
        {
            foreach (var other in InGame.instance.GetBloons().Where(bloon => !bloon.bloonModel.isBoss).ToList()) other.Destroy();
            yield return null;
        }

        Log($"damage: beside the Monkey Town {near.damageDealt}, away from it {far.damageDealt}");
        Assert(far.damageDealt > 0, "The Sniper away from the Monkey Town hurt it");
        var share = near.damageDealt / (double) far.damageDealt;
        Assert(share > Gildebloon.CashTowerDamage * 0.7 && share < Gildebloon.CashTowerDamage * 1.3,
            $"The Sniper in the Monkey Town does about {Gildebloon.CashTowerDamage:P0}: {share:P0}");
        yield return ToMainMenu();
    }
}

/// <summary>
/// What Gildebloon costs (#25): a busy Elite match (glue, fire, a sticky bomb, a skull with padlocks, Tax Collectors and
/// coins flying) a few times over. Logs what each of its per-frame parts takes, and checks that nothing piles up from
/// match to match: the game's meshes, materials, textures, sprites and objects, and memory.
/// </summary>
[FullSuiteOnly("eight busy matches, Gildebloon's and Bloonarius's, measuring time and memory (about 3 min)")]
public class GildebloonCostTest : GildebloonTest
{
    private const int Games = 3;

    protected override IEnumerator RunOn()
    {
        var gildebloon = 0.0;
        yield return Matches(BossList.Gildebloon, grown => gildebloon = grown);
        // the same with the game's own boss: the game's memory grows from match to match by itself
        var bloonarius = 0.0;
        yield return Matches(BossType.Bloonarius, grown => bloonarius = grown);
        // Only logged: the game's (Il2Cpp) memory swings by several MB from run to run (Bloonarius 11.6 MB a match in one
        // run, 3.6 in the next, 2026-11-22), too much to tell 1 MB; what is checked is what can't swing: the counts of
        // the game's objects, managed memory, and the HUD's template made once
        Log($"the game's memory a match: Gildebloon {gildebloon:0.00} MB, Bloonarius {bloonarius:0.00} MB");
    }

    /// <summary>A match to fill the caches (meshes, materials, the HUD's templates), then <see cref="Games"/> more, measured</summary>
    private IEnumerator Matches(BossType boss, System.Action<double> gameGrown)
    {
        yield return Match(boss, boss == BossList.Gildebloon);
        var templates = Visuals.GildebloonHud.TemplatesMade;
        var before = new System.Collections.Generic.Dictionary<string, int>();
        var (managed, game) = (0.0, 0.0);
        yield return Measure(before, (m, g) => (managed, game) = (m, g));
        for (var i = 0; i < Games; i++) yield return Match(boss, false);
        var after = new System.Collections.Generic.Dictionary<string, int>();
        var (managedAfter, gameAfter) = (0.0, 0.0);
        yield return Measure(after, (m, g) => (managedAfter, gameAfter) = (m, g));

        var grown = before.Keys.Select(key => (key, per: (after[key] - before[key]) / (double) Games)).ToList();
        Log($"{boss} per match: {string.Join(", ", grown.Select(g => $"{g.key} {g.per:+0.0;-0.0;0}"))}; " +
            $"{(managedAfter - managed) / Games:0.000} MB managed, {(gameAfter - game) / Games:0.00} MB game memory");
        gameGrown((gameAfter - game) / Games);
        if (boss != BossList.Gildebloon) yield break;
        // the HUD is copied from a template made once, not made with Mod Helper each match (about 1.2 MB a match)
        AssertEquals(Visuals.GildebloonHud.TemplatesMade, templates, "The HUD's template isn't made again each match");
        foreach (var (key, per) in grown)
            Assert(per <= (key == "GameObject" ? 5 : 1), $"{key}s pile up: {per:0.0} more after each match");
        Assert((managedAfter - managed) / Games < 0.5, $"Managed memory grows by {(managedAfter - managed) / Games:0.00} MB a match");
    }

    private IEnumerator Match(BossType bossType, bool timeIt)
    {
        var options = new BossOptions {Boss = bossType, Elite = true, Tiers = 1, SpawnRounds = "40"};
        var dcm = WeakBossDcm();
        dcm.startRules.cash = 50000;
        dcm.bloonModifiers.healthMultipliers.boss = 20;
        dcm.bloonModifiers.bossSpeedMultiplier = 0.3f;
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(dcm, options));
        var bridge = InGame.instance.bridge;
        var manager = bridge.Simulation.map.spawner.bossBloonManager;
        bridge.StartRound();
        yield return WaitFor(() => manager.CurrentBoss != null, $"Elite {bossType} arriving", 90);
        var boss = manager.CurrentBoss!;
        var position = boss.Position;
        var model = Game.instance.model;
        CreateTowerAt(bridge, new Vector2(position.X + 22, position.Y + 22), model.GetTowerFromId("GlueGunner-300"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X - 22, position.Y + 22), model.GetTowerFromId("NinjaMonkey-004"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X, position.Y - 28), model.GetTowerFromId("WizardMonkey-030"), costOverride: 0);
        CreateTowerAt(bridge, new Vector2(position.X + 25, position.Y - 25), model.GetTowerFromId("BananaFarm"), costOverride: 0);
        yield return new WaitForSecondsRealtime(3);
        var repossessions = Visuals.GildebloonHud.Repossessions;
        boss.SetHealth((int) (boss.bloonModel.maxHealth * 0.86f));
        if (bossType == BossList.Gildebloon)
            yield return WaitFor(() => Visuals.GildebloonHud.Repossessions > repossessions && Visuals.GildebloonSeizeLook.PadlockCount > 0, "A Repossession", 10);
        yield return new WaitForSecondsRealtime(1);

        if (timeIt)
        {
            var parts = new[] {"Gildebloon vault", "Gildebloon look", "Gildebloon's money", "Gildebloon's Repossession"};
            var start = parts.ToDictionary(part => part, part => CustomBossChallenge.UpdateTicks.TryGetValue(part, out var t) ? t : 0);
            var frames = 0;
            var began = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - began < 4)
            {
                frames++;
                yield return null;
            }
            var ms = parts.ToDictionary(part => part, part =>
                (CustomBossChallenge.UpdateTicks[part] - start[part]) * 1000.0 / System.Diagnostics.Stopwatch.Frequency / frames);
            Log($"a frame ({frames} frames): {string.Join(", ", ms.Select(m => $"{m.Key} {m.Value:0.000} ms"))}; together {ms.Values.Sum():0.000} ms");
            // 2026-11-22: 0.9 ms before looking through the body only now and then and posing only parts that move
            Assert(ms.Values.Sum() < 0.7, $"Gildebloon takes {ms.Values.Sum():0.000} ms a frame");
        }
        yield return ToMainMenu();
    }

    private static IEnumerator Measure(System.Collections.Generic.Dictionary<string, int> counts, System.Action<double, double> found)
    {
        yield return new WaitForSecondsRealtime(1);
        var unload = Resources.UnloadUnusedAssets();
        while (!unload.isDone) yield return null;
        for (var i = 0; i < 6; i++)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            Il2CppSystem.GC.Collect();
            yield return null;
        }
        counts["Mesh"] = Resources.FindObjectsOfTypeAll<Mesh>().Length;
        counts["Material"] = Resources.FindObjectsOfTypeAll<Material>().Length;
        counts["Texture2D"] = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
        counts["Sprite"] = Resources.FindObjectsOfTypeAll<Sprite>().Length;
        counts["GameObject"] = Resources.FindObjectsOfTypeAll<GameObject>().Length;
        found(System.GC.GetTotalMemory(true) / 1048576.0, Il2CppSystem.GC.GetTotalMemory(false) / 1048576.0);
    }
}
