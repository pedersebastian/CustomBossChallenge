using BTD_Mod_Helper.Extensions;
using System.Linq;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors.Actions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CustomBossChallenge.Config;

/// <summary>
/// The models of the mod's own boss Gildebloon (#25): tiers 1-5, normal and Elite, made from Bloonarius's (a boss
/// without a panel of its own) with Bloonarius's own actions taken out and Gildebloon's put in. Its cash stealing,
/// Vault and payout are not in the models: Simulation.GildebloonBrain runs them.
/// <br/>
/// The models go into the game's model only while Gildebloon is switched on in the code
/// (<see cref="Experimental.Gildebloon"/>), and the game only ever spawns one when a custom game asks for it by
/// setting its boss type to "Gildebloon" (<see cref="Patches.MixedBosses"/>), so it can't show up in a real event.
/// </summary>
public static class Gildebloon
{
    /// <summary>The game boss the models are made from</summary>
    public const string Source = "Bloonarius";

    /// <summary>
    /// The Tax Collectors: MOAB-class bloons Gildebloon sends at each skull, which steal cash when they leak. A plain
    /// MOAB was no threat from tier 3 on (2026-11-22), so a tier sends bigger ones (<see cref="CollectorsFor"/>), and a
    /// bigger one takes more when it leaks.
    /// </summary>
    public const string CollectorId = "GildebloonCollector";

    public const string BfbCollectorId = "GildebloonCollectorBfb";
    public const string ZomgCollectorId = "GildebloonCollectorZomg";
    public const string FortifiedZomgCollectorId = "GildebloonCollectorZomgFortified";

    /// <summary>
    /// Every Tax Collector: its id, the game bloon it is made from, the share of the cash it takes when it leaks, and
    /// that bloon's size in its display (width, length and how far its middle is ahead, measured in the game
    /// 2026-11-22): its look covers the bloon it replaces, so the game's effects on that bloon (glue...) lie on it
    /// </summary>
    public static readonly (string Id, string Source, double Share, float Width, float Length, float Middle)[] CollectorKinds =
    [
        (CollectorId, "Moab", 0.05, 25.9f, 42.4f, 0),
        (BfbCollectorId, "Bfb", 0.08, 43.6f, 60.1f, -0.7f),
        (ZomgCollectorId, "Zomg", 0.1, 49.1f, 75.9f, -6.4f),
        (FortifiedZomgCollectorId, "ZomgFortified", 0.12, 49.1f, 75.9f, -6.4f)
    ];

    /// <summary>The Tax Collector each tier 1-5 sends: Elite one step up, fortified from tier 4</summary>
    internal static readonly string[] CollectorsFor = [CollectorId, CollectorId, BfbCollectorId, BfbCollectorId, ZomgCollectorId];

    internal static readonly string[] EliteCollectorsFor =
        [BfbCollectorId, BfbCollectorId, ZomgCollectorId, FortifiedZomgCollectorId, FortifiedZomgCollectorId];

    /// <summary>The Tax Collector a tier sends; tiers past 5 send tier 5's</summary>
    public static string CollectorFor(bool elite, int tier)
    {
        var table = elite ? EliteCollectorsFor : CollectorsFor;
        return table[System.Math.Clamp(tier, 1, table.Length) - 1];
    }

    /// <summary>What kind of Tax Collector a bloon is, or null if it isn't one</summary>
    public static (string Id, string Source, double Share, float Width, float Length, float Middle)? CollectorKind(string? id) =>
        CollectorKinds.Where(kind => kind.Id == id).Select(kind => ((string, string, double, float, float, float)?) kind).FirstOrDefault();

    // Health and speed of tiers 1-5; between Blastapopoulos and Dreadbloon, since the Vault adds to the health
    internal static readonly int[] Health = [12_000, 45_000, 200_000, 450_000, 1_900_000];
    internal static readonly int[] EliteHealth = [30_000, 180_000, 1_150_000, 4_700_000, 22_000_000];
    internal static readonly float[] Speed = [1.5f, 1.55f, 1.6f, 1.65f, 1.7f];

    internal static readonly float[] Skulls = [0.8f, 0.6f, 0.4f, 0.2f];
    internal static readonly float[] EliteSkulls = [0.875f, 0.75f, 0.625f, 0.5f, 0.375f, 0.25f, 0.125f];

    /// <summary>How many Tax Collectors come at each skull, per tier</summary>
    internal static readonly int[] Collectors = [2, 2, 3, 3, 4];

    /// <summary>Acid does this much more damage to it: gold tarnishes (#25)</summary>
    public const float AcidDamage = 1.5f;

    /// <summary>
    /// Taking this share of its health within <see cref="SkittishSeconds"/> makes it dash ahead, like a Golden Bloon
    /// (GildebloonBrain: the game's DamageTakenTriggerModel and DashForwardsActionModel do nothing on a boss)
    /// </summary>
    public const float SkittishShare = 0.1f;

    public const float SkittishSeconds = 1;

    /// <summary>How far it dashes, and how long until it can dash again</summary>
    public const float DashDistance = 60;

    public const float DashCooldownSeconds = 3;

    /// <summary>The towers whose attacks count as acid for Tarnish: the Alchemist's potions and the Glue Gunner's glue</summary>
    public static readonly string[] AcidTowers = ["Alchemist", "GlueGunner"];

    /// <summary>The tag Gildebloon's models carry, which the acid towers' attacks do more damage to</summary>
    public const string Tag = "Gildebloon";

    internal const string Tarnish = "GildeTarnish";

    /// <summary>How long it can't be hurt at a skull: the Repossession</summary>
    public const float SkullSeconds = 3;

    // Action ids of its behaviours
    internal const string SkullInvulnerable = "GildeInvulnOn";
    internal const string SkullVulnerable = "GildeInvulnOff";
    internal const string SkullTimer = "GildeSkullTimer";
    internal const string SkullCollectors = "GildeCollectors";
    internal const string Dash = "GildeDash";

    /// <summary>
    /// Adds the models to <paramref name="gameModel"/> if Gildebloon is switched on in the code and they aren't there
    /// yet. Safe to call again.
    /// </summary>
    /// <returns>How many models were added</returns>
    public static int Register(GameModel gameModel)
    {
        if (!Experimental.Gildebloon || gameModel.DoesBloonExist(BossOptions.BossIdOf(BossList.Gildebloon, false, 1))) return 0;

        var added = 0;
        foreach (var kind in CollectorKinds)
        {
            if (CreateCollector(gameModel, kind.Id, kind.Source) is { } made) added += Add(gameModel, made);
        }

        foreach (var elite in new[] {false, true})
        {
            for (var tier = 1; tier <= TierMath.VanillaTiers; tier++)
            {
                var source = BossOptions.BossIdOf(Il2CppAssets.Scripts.Data.Boss.BossType.Bloonarius, elite, tier);
                if (!gameModel.DoesBloonExist(source)) continue;
                var collector = CollectorFor(elite, tier);
                var model = Create(gameModel.GetBloon(source), elite, tier,
                    gameModel.DoesBloonExist(collector) ? collector : gameModel.DoesBloonExist(CollectorId) ? CollectorId : null,
                    Invulnerability(gameModel));
                Unbloonarius(model, gameModel);
                added += Add(gameModel, model);
            }
        }

        AddTarnish(gameModel);
        AddCashResistance(gameModel);
        return added;
    }

    private static int Add(GameModel gameModel, BloonModel model)
    {
        if (gameModel.bloonsByName.ContainsKey(model.id)) return 0;
        gameModel.bloons = gameModel.bloons.AddTo(model);
        gameModel.AddChildDependant(model);
        gameModel.bloonsByName[model.id] = model;
        return 1;
    }

    /// <summary>
    /// Gildebloon's model for a tier, from Bloonarius's model of the same tier
    /// </summary>
    public static BloonModel Create(BloonModel source, bool elite, int tier, string? collector,
        (SetInvulnerableActionModel On, SetInvulnerableActionModel Off)? invulnerability)
    {
        var id = BossOptions.BossIdOf(BossList.Gildebloon, elite, tier);
        var model = source.Clone().Cast<BloonModel>();
        model.id = id;
        model.name = id;
        model._name = id;
        model.baseId = BossList.GildebloonName;

        var index = tier - 1;
        model.maxHealth = (elite ? EliteHealth : Health)[index];
        model.speed = Speed[index];
        model.speedFrames = Speed[index] / 60f;

        // Bloonarius's spew, spawns and bleed go; what plays its look and sounds stays
        model.RemoveBehaviors<HealthPercentTriggerModel>();
        model.RemoveBehaviors<PlayAnimTriggerActionModel>();
        model.RemoveBehaviors<SpawnBloonsActionModel>();

        // Repossession at each skull: it can't be hurt for a moment and sends its Tax Collectors. Like the game's bosses,
        // no hit takes it past a skull.
        var skulls = elite ? EliteSkulls : Skulls;
        var actions = new[] {SkullInvulnerable, SkullTimer, SkullCollectors}
            .Where(action => (action != SkullCollectors || collector != null) && (action == SkullCollectors || invulnerability != null));
        model.AddBehavior(new HealthPercentTriggerModel("GildeSkulls", false, new Il2CppStructArray<float>(skulls),
            new Il2CppStringArray(actions.ToArray()), true));
        if (invulnerability is var (on, off))
        {
            var invulnerable = on.Clone().Cast<SetInvulnerableActionModel>();
            invulnerable.actionId = SkullInvulnerable;
            var vulnerable = off.Clone().Cast<SetInvulnerableActionModel>();
            vulnerable.actionId = SkullVulnerable;
            model.AddBehavior(invulnerable);
            model.AddBehavior(new WaitForSecondsActionModel("GildeSkullTimer", SkullSeconds, SkullTimer,
                new Il2CppStringArray(new[] {SkullVulnerable})));
            model.AddBehavior(vulnerable);
        }
        if (collector != null)
        {
            // The game looks the boss name up in its own boss data for the spawned bloons' skin; Gildebloon and an empty
            // name aren't there, so they get the skin of the boss it is made from
            model.AddBehavior(new SpawnBloonsActionModel("GildeCollectors", SkullCollectors, collector, Collectors[index],
                1.0f, 30, 0, 0, new Il2CppStringArray(0), new Il2CppStringArray(0), 0, false, 0, 0, Source));
        }

        // Skittish (a big hit sends it ahead) is GildebloonBrain's: the game's DamageTakenTriggerModel and
        // DashForwardsActionModel do nothing on a boss (tested 2026-11-18). Tarnish (acid hurts gold) goes by its tag:
        // the game's IncreaseDamageFromTypeModel did nothing either, so the acid towers get a bonus against the tag.
        model.tags = new Il2CppStringArray(model.tags.ToArray().Append(Tag).ToArray());
        model.typedTagsBitmaskInitialized = false;
        return model;
    }

    /// <summary>The game bloon whose pops, sounds and end Gildebloon gets in place of Bloonarius's</summary>
    public const string Presentation = "Zomg";

    /// <summary>
    /// Takes out what would show or sound like Bloonarius (#25): its pop effect and sounds become a ZOMG's, and its
    /// death animation goes (the look bursts into coins instead, Visuals.GildebloonLook)
    /// </summary>
    private static void Unbloonarius(BloonModel model, GameModel gameModel)
    {
        model.RemoveBehaviors<SpawnDeathAnimModel>();
        var other = gameModel.DoesBloonExist(Presentation) ? gameModel.GetBloon(Presentation) : null;
        if (other == null) return;
        Swap<PopEffectModel>(model, other);
        Swap<CreateSoundOnDamageBloonModel>(model, other);
        Swap<CreateSoundOnSpawnBloonModel>(model, other);
    }

    private static void Swap<T>(BloonModel model, BloonModel other) where T : Il2CppAssets.Scripts.Models.Model
    {
        var theirs = other.GetBehaviors<T>().ToList();
        model.RemoveBehaviors<T>();
        foreach (var behavior in theirs) model.AddBehavior(behavior.Clone().Cast<T>());
    }

    /// <summary>
    /// Blastapopoulos's actions that make it unhurtable at a skull and hurtable again, sounds and all, or null if a game
    /// update took them away
    /// </summary>
    private static (SetInvulnerableActionModel, SetInvulnerableActionModel)? Invulnerability(GameModel gameModel)
    {
        if (!gameModel.DoesBloonExist("Blastapopoulos1")) return null;
        var actions = gameModel.GetBloon("Blastapopoulos1").GetBehaviors<SetInvulnerableActionModel>().ToList();
        var on = actions.FirstOrDefault(action => action.set);
        var off = actions.FirstOrDefault(action => !action.set);
        return on != null && off != null ? (on, off) : null;
    }

    /// <summary>
    /// The Tax Collector: a MOAB (a BFB for Elite) that Gildebloon sends at each skull. When one leaks it steals cash
    /// instead of lives (Simulation.GildebloonBrain).
    /// </summary>
    /// <summary>
    /// Tarnish: every attack of the acid towers (<see cref="AcidTowers"/>), at every upgrade, does
    /// <see cref="AcidDamage"/> times its damage to bloons with Gildebloon's tag. Safe to call again.
    /// </summary>
    /// <summary>
    /// Gildebloon guards its gold against the towers that make money (#25): towers that can make cash do this share of
    /// their damage to it, and so do towers in a Monkey Town's reach (a Monkey Village x-x-3 or more, whose pops are worth
    /// more). Only the upgrades that make cash count: a Sniper from x-4-x (Supply Drop), a Heli Pilot from x-4-x, an
    /// Engineer from x-x-4 (Bloon Trap), a Buccaneer from x-x-3 (Merchantman), an Alchemist from x-x-3 (Rubber to Gold), a
    /// Super Monkey from x-4-x, a Druid from x-4-x, a Desperado's x-4-x, their paragons that make cash, Benjamin and
    /// Obyn's levels that do; found by what their models have (<see cref="CashBehaviours"/>), so a new tower that makes
    /// cash counts too.
    /// </summary>
    public const float CashTowerDamage = 0.5f;

    /// <summary>The model behaviours that make a tower one that makes cash (seen in 57.4's towers)</summary>
    internal static readonly string[] CashBehaviours =
    [
        "CashModel", "PerRoundCashBonusTowerModel", "GiveCashOnPopModel", "CashbackZoneModel", "BonusCashZoneModel",
        "CashPerTowerInRangeModel", "BountyHunterZoneModel", "BananaCashIncreaseSupportModel", "IncreaseBloonWorthWithTierModel",
        "MonkeyCityIncomeSupportModel"
    ];

    internal const string CashResistance = "GildeCashGuard";

    /// <summary>The towers that make cash in a game model, by name, for tests</summary>
    public static System.Collections.Generic.List<string> CashTowers(GameModel gameModel) =>
        gameModel.towers.Where(MakesCash).Select(tower => tower.name).ToList();

    private static bool MakesCash(Il2CppAssets.Scripts.Models.Towers.TowerModel tower) =>
        tower.GetDescendants<Model>().ToList().Any(model => System.Array.IndexOf(CashBehaviours, model.GetIl2CppType().Name) >= 0);

    /// <summary>Whether a Monkey Village upgrade is a Monkey Town or more: its pops are worth more</summary>
    private static bool IsMonkeyTown(Il2CppAssets.Scripts.Models.Towers.TowerModel tower) =>
        tower.baseId == "MonkeyVillage" && tower.HasBehavior<Il2CppAssets.Scripts.Models.Towers.Behaviors.CashIncreaseModel>();

    /// <summary>See <see cref="CashTowerDamage"/>. Safe to call again.</summary>
    private static void AddCashResistance(GameModel gameModel)
    {
        foreach (var tower in gameModel.towers)
        {
            if (IsMonkeyTown(tower))
            {
                if (tower.GetBehaviors<Il2CppAssets.Scripts.Models.Towers.Behaviors.DamageModifierSupportModel>().Any(model => model.name.EndsWith(CashResistance)))
                    continue;
                // the towers in its reach do less to Gildebloon; one Monkey Town's is enough (unique)
                tower.AddBehavior(new Il2CppAssets.Scripts.Models.Towers.Behaviors.DamageModifierSupportModel(CashResistance, true,
                    CashResistance, new Il2CppReferenceArray<Il2CppAssets.Scripts.Models.Towers.TowerFilters.TowerFilterModel>(0), false,
                    new Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.DamageModifierForTagModel(CashResistance, Tag, CashTowerDamage, 0, false, true, false),
                    false, 0, false));
                continue;
            }

            if (!MakesCash(tower)) continue;
            foreach (var projectile in tower.GetDescendants<Il2CppAssets.Scripts.Models.Towers.Projectiles.ProjectileModel>().ToList())
            {
                if (!projectile.HasBehavior<Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.DamageModel>()) continue;
                if (projectile.GetBehaviors<Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.DamageModifierForTagModel>()
                    .Any(modifier => modifier.name.EndsWith(CashResistance))) continue;
                projectile.AddBehavior(new Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.DamageModifierForTagModel(
                    CashResistance, Tag, CashTowerDamage, 0, false, true, false));
            }
        }
    }

    private static void AddTarnish(GameModel gameModel)
    {
        foreach (var tower in gameModel.towers)
        {
            if (System.Array.IndexOf(AcidTowers, tower.baseId) < 0) continue;
            foreach (var projectile in tower.GetDescendants<Il2CppAssets.Scripts.Models.Towers.Projectiles.ProjectileModel>().ToList())
            {
                if (!projectile.HasBehavior<Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.DamageModel>()) continue;
                if (projectile.GetBehaviors<Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.DamageModifierForTagModel>()
                    .Any(modifier => modifier.name.EndsWith(Tarnish))) continue;
                projectile.AddBehavior(new Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.DamageModifierForTagModel(
                    Tarnish, Tag, AcidDamage, 0, false, true, false));
            }
        }
    }

    private static BloonModel? CreateCollector(GameModel gameModel, string id, string source)
    {
        // a game without the fortified one gets the plain one
        if (!gameModel.DoesBloonExist(source)) source = source.Replace("Fortified", "");
        if (!gameModel.DoesBloonExist(source)) return null;
        var model = gameModel.GetBloon(source).Clone().Cast<BloonModel>();
        model.id = id;
        model.name = id;
        model._name = id;
        // A leak takes cash, no lives: not its own, nor what the bloons inside it would cost
        model.leakDamage = 0;
        model.totalLeakDamage = new Il2CppSystem.Nullable<float>(0f);
        model.leakDamageMultiplier = 0;
        model.AddBehavior(new ColorModel("GildeGold", 1f, 0.8f, 0.2f, 1f));
        return model;
    }

    /// <summary>Whether the bloon is one of Gildebloon's Tax Collectors</summary>
    public static bool IsCollector(string? id) => CollectorKind(id) != null;

    /// <summary>Whether a model id is one of Gildebloon's tiers, e.g. "GildebloonElite3"</summary>
    public static bool IsGildebloon(string? id) =>
        id != null && id.StartsWith(BossList.GildebloonName) && !IsCollector(id) && TierMath.TierOf(id) > 0;
}
