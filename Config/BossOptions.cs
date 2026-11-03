using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json;

namespace CustomBossChallenge.Config;

/// <summary>
/// How the skull thresholds of the boss should be determined
/// </summary>
public enum SkullMode
{
    /// <summary>Keep the skulls the boss normally has</summary>
    Vanilla,

    /// <summary>Place <see cref="BossOptions.SkullCount"/> skulls evenly across the health bar</summary>
    EvenlySpaced,

    /// <summary>Use the comma separated percentages in <see cref="BossOptions.CustomSkulls"/></summary>
    Custom
}

/// <summary>
/// The boss specific part of a Boss Creator setup. Everything else (map, rules, bloon and boss multipliers, towers)
/// lives in the Challenge Editor's DailyChallengeModel.
/// </summary>
public sealed record BossOptions
{
    public BossType Boss { get; init; } = BossType.Bloonarius;
    public bool Elite { get; init; }

    public SkullMode SkullMode { get; init; } = SkullMode.Vanilla;
    public int SkullCount { get; init; } = 3;
    public string CustomSkulls { get; init; } = "75, 50, 25";

    /// <summary>Number of boss tiers, 1-10. Tiers above 5 are stronger copies of tier 5.</summary>
    public int Tiers { get; init; } = TierMath.VanillaTiers;

    /// <summary>How much faster each tier is than the one before it, in percent on top of the game's own step</summary>
    public int SpeedPerTier { get; init; }

    /// <summary>
    /// How much more health each tier has than the one before it, in percent on top of the game's own step. Tiers
    /// above 5 have no step of their own; there the game's step is <see cref="TierMath.ExtraTierStep"/>.
    /// </summary>
    public int HealthPerTier { get; init; }

    /// <summary>
    /// Setups and codes from before 2.3 stored the health of tiers above 5 here, in percent of the tier before it.
    /// <see cref="Sanitized"/> turns it into <see cref="HealthPerTier"/>.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? ExtraTierHealth { get; init; }

    /// <summary>Comma separated rounds the tiers spawn on, or null for the defaults (40, 60, 80, ...)</summary>
    public string? SpawnRounds { get; init; }

    /// <summary>
    /// The boss of each tier when "Different boss per tier" is on, or null when every tier has <see cref="Boss"/>.
    /// A comma separated token per tier, like "Lych,,RandomElite"; see <see cref="TierBossMath"/>.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? TierBosses { get; init; }

    /// <summary>Whether the tiers can have different bosses</summary>
    [JsonIgnore]
    public bool Mixed => TierBosses != null;

    /// <summary>
    /// The skull thresholds (0-1, descending) these options ask for, or null to keep the vanilla skulls
    /// </summary>
    [JsonIgnore]
    public float[]? SkullPercents => SkullMode switch
    {
        SkullMode.EvenlySpaced => SkullMath.EvenlySpaced(SkullCount),
        SkullMode.Custom => SkullMath.ParseCustom(CustomSkulls),
        _ => null
    };

    /// <summary>
    /// The round each tier spawns on, always exactly <see cref="Tiers"/> ascending rounds
    /// </summary>
    [JsonIgnore]
    public int[] EffectiveSpawnRounds => TierMath.SpawnRounds(Tiers, SpawnRounds);

    /// <summary>
    /// The spawn rounds the way the game counts them: rounds start at 0 there, so round 40 is 39
    /// </summary>
    [JsonIgnore]
    public int[] GameSpawnRounds => TierMath.ToGameRounds(EffectiveSpawnRounds);

    /// <summary>
    /// A copy with every value in the range the Boss Creator allows, for options read from a file
    /// </summary>
    public BossOptions Sanitized()
    {
        var defaults = new BossOptions();
        return this with
        {
            Boss = System.Enum.IsDefined(Boss) ? Boss : defaults.Boss,
            SkullMode = System.Enum.IsDefined(SkullMode) ? SkullMode : defaults.SkullMode,
            SkullCount = System.Math.Clamp(SkullCount, 0, SkullMath.MaxSkulls),
            CustomSkulls = SkullMath.NormalizeCustom(CustomSkulls, defaults.CustomSkulls),
            Tiers = TierMath.ClampTiers(Tiers),
            SpeedPerTier = TierMath.ClampSpeedPerTier(SpeedPerTier),
            HealthPerTier = TierMath.ClampHealthPerTier(ExtraTierHealth is { } legacy && HealthPerTier == 0
                ? TierMath.HealthPerTierFromExtraTierHealth(legacy)
                : HealthPerTier),
            ExtraTierHealth = null,
            TierBosses = TierBossMath.Normalize(TierBosses)
        };
    }

    /// <summary>
    /// What the given tier was set to: the setup's boss, a boss of its own, or Random
    /// </summary>
    public TierBoss EntryOf(int tier) => Mixed ? TierBossMath.Entry(TierBosses, tier) : TierBoss.Follows;

    /// <summary>
    /// The boss and Elite of the given tier. A Random tier has the setup's boss here; games always start with
    /// <see cref="ResolveRandom"/>'s options, which have no Random tiers left.
    /// </summary>
    public (BossType Boss, bool Elite) BossOf(int tier)
    {
        var entry = EntryOf(tier);
        if (entry.FollowsSetup || entry.IsRandom || !System.Enum.TryParse<BossType>(entry.Boss, out var boss))
        {
            return (Boss, Elite);
        }

        return (boss, entry.Elite);
    }

    /// <summary>
    /// A tier's own speed and health compared with the normal game, in Advanced Mode in place of Boss Speed and Boss
    /// Health; 1 without a boss per tier
    /// </summary>
    public double TierSpeedFactor(int tier) => EntryOf(tier).SpeedFactor;

    public double TierHealthFactor(int tier) => EntryOf(tier).HealthFactor;

    /// <summary>Whether a tier will get a random boss when the game starts</summary>
    [JsonIgnore]
    public bool HasRandom => Mixed && TierBossMath.HasRandom(TierBosses, Tiers);

    /// <summary>
    /// These options with a concrete boss for every tier: Random tiers get a boss picked by <paramref name="random"/>.
    /// Options without different bosses per tier come back as they are.
    /// </summary>
    public BossOptions ResolveRandom(System.Random random) => Mixed
        ? this with {TierBosses = TierBossMath.Resolve(TierBosses, Boss.ToString(), Elite, Tiers, random.Next)}
        : this;

    /// <summary>
    /// The bloon id of the boss at the given tier, e.g. "VortexElite3"
    /// </summary>
    public string BossId(int tier)
    {
        var (boss, elite) = BossOf(tier);
        return BossIdOf(boss, elite, tier);
    }

    /// <summary>
    /// The id of the game's own model the given tier is made from: the tier itself up to tier 5, and tier 5 of the
    /// tier's boss above that
    /// </summary>
    public string SourceId(int tier)
    {
        var (boss, elite) = BossOf(tier);
        return BossIdOf(boss, elite, System.Math.Min(tier, TierMath.VanillaTiers));
    }

    /// <summary>
    /// The bloon id of a boss at the given tier, e.g. "VortexElite3"
    /// </summary>
    public static string BossIdOf(BossType boss, bool elite, int tier) => $"{boss}{(elite ? "Elite" : "")}{tier}";
}
