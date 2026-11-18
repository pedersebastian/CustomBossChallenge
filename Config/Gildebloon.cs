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

    /// <summary>The MOAB-class bloons Gildebloon sends at each skull, which steal cash when they leak</summary>
    public const string CollectorId = "GildebloonCollector";

    public const string EliteCollectorId = "GildebloonCollectorElite";

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

    /// <summary>A hit taking this share of its health at once makes it dash ahead, like a Golden Bloon</summary>
    public const float SkittishShare = 0.1f;

    /// <summary>How far it dashes</summary>
    public const float DashDistance = 60;

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
        foreach (var elite in new[] {false, true})
        {
            var collector = CreateCollector(gameModel, elite);
            if (collector != null) added += Add(gameModel, collector);

            for (var tier = 1; tier <= TierMath.VanillaTiers; tier++)
            {
                var source = BossOptions.BossIdOf(Il2CppAssets.Scripts.Data.Boss.BossType.Bloonarius, elite, tier);
                if (!gameModel.DoesBloonExist(source)) continue;
                var model = Create(gameModel.GetBloon(source), elite, tier, collector?.id, Invulnerability(gameModel));
                Unbloonarius(model, gameModel);
                added += Add(gameModel, model);
            }
        }

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

        // Skittish: a big hit sends it ahead along the track
        model.AddBehavior(new DamageTakenTriggerModel("GildeSkittish", Dash, (int) (model.maxHealth * SkittishShare), -1, 1));
        model.AddBehavior(new DashForwardsActionModel("GildeDash", Dash, DashDistance, new Il2CppStringArray(0), 4, "", false));

        // Tarnish: acid hurts gold
        model.AddBehavior(new IncreaseDamageFromTypeModel("GildeTarnish", AcidDamage, new Il2CppStringArray(new[] {"Acid"}), false));
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
    private static BloonModel? CreateCollector(GameModel gameModel, bool elite)
    {
        var source = elite ? "Bfb" : "Moab";
        if (!gameModel.DoesBloonExist(source)) return null;
        var id = elite ? EliteCollectorId : CollectorId;
        var model = gameModel.GetBloon(source).Clone().Cast<BloonModel>();
        model.id = id;
        model.name = id;
        model._name = id;
        model.leakDamage = 0;
        model.AddBehavior(new ColorModel("GildeGold", 1f, 0.8f, 0.2f, 1f));
        return model;
    }

    /// <summary>Whether the bloon is one of Gildebloon's Tax Collectors</summary>
    public static bool IsCollector(string? id) => id is CollectorId or EliteCollectorId;

    /// <summary>Whether a model id is one of Gildebloon's tiers, e.g. "GildebloonElite3"</summary>
    public static bool IsGildebloon(string? id) =>
        id != null && id.StartsWith(BossList.GildebloonName) && !IsCollector(id) && TierMath.TierOf(id) > 0;
}
