using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json;

namespace CustomBossChallenge.Config;

/// <summary>
/// The boss specific part of a Boss Creator setup. Everything else (map, rules, bloon and boss multipliers, towers)
/// lives in the Challenge Editor's DailyChallengeModel.
/// </summary>
public sealed record BossOptions
{
    public BossType Boss { get; init; } = BossType.Bloonarius;
    public bool Elite { get; init; }

    public SkullMode SkullMode { get; init; } = SkullMode.Vanilla;
    public int SkullCount { get; init; } = SkullMath.DefaultCount;
    public string CustomSkulls { get; init; } = SkullMath.DefaultCustom;

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
    /// The tiers that were Random before <see cref="ResolveRandom"/> picked their bosses, like "2,5", or null. The game
    /// keeps those bosses a secret until they arrive.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? RandomTiers { get; init; }

    /// <summary>Whether the game picked the boss of <paramref name="tier"/> at random when it started</summary>
    public bool WasRandom(int tier) => System.Array.IndexOf(TierBossMath.ParseTierList(RandomTiers), tier) >= 0;

    /// <summary>
    /// The boss events' own rounds instead of the default ones (#80), moved to the tiers' Spawn Rounds. In Advanced
    /// Mode (#84) the rounds follow the first tier's boss, and every tier arrives on the round made for its own boss.
    /// </summary>
    public bool BossEventRounds { get; init; }

    /// <summary>
    /// Which CHIMPS boss events' rounds Boss Event Rounds play in CHIMPS. Left out of saves and codes when it is the
    /// default, so the defaults the website knows stay the same.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ChimpsRounds ChimpsRounds { get; init; }

    /// <summary>Whether the game is played with the boss events' own rounds, in Standard and in Advanced Mode</summary>
    [JsonIgnore]
    public bool UsesBossEventRounds => BossEventRounds;

    /// <summary>
    /// The round set the game merges over the default rounds with Boss Event Rounds: the first tier's boss's
    /// </summary>
    [JsonIgnore]
    public string BaseEventRoundSet => BossEventRoundSet(BossOf(1).Boss);

    /// <summary>
    /// The name of a boss's own round set in the game's mergeable round sets, the same for Elite, e.g. "bloonarius"
    /// </summary>
    public static string BossEventRoundSet(BossType boss) => boss.ToString().ToLowerInvariant();

    /// <summary>The game mode of CHIMPS in the game's challenge models</summary>
    public const string ChimpsMode = "Clicks";

    /// <summary>
    /// The round set of the newest CHIMPS boss event, Diamondback8 (October 2026), for any boss. The game doesn't have
    /// it, the mod brings its own copy (<see cref="ChimpsRoundSet"/>).
    /// </summary>
    public const string ChimpsBossRoundSet = "chimpsboss";

    /// <summary>
    /// The round set of the earlier CHIMPS boss events, Bloonarius55 (2024) and Dreadbloon32New (2025), for any boss
    /// (#139). The game doesn't have it either.
    /// </summary>
    public const string ChimpsBloonariusRoundSet = "chimpsbloonarius";

    /// <summary>
    /// The round set of the CHIMPS boss events <paramref name="chimps"/> picks, or null for the boss's own event rounds
    /// (<see cref="ChimpsRounds.Own"/>)
    /// </summary>
    public static string? ChimpsEventRoundSet(ChimpsRounds chimps) => chimps switch
    {
        ChimpsRounds.Own => null,
        ChimpsRounds.Earlier => ChimpsBloonariusRoundSet,
        _ => ChimpsBossRoundSet
    };

    /// <summary>Whether <paramref name="name"/> is one of the CHIMPS boss events' round sets</summary>
    public static bool IsChimpsEventRoundSet(string? name) => name is ChimpsBossRoundSet or ChimpsBloonariusRoundSet;

    /// <summary>
    /// The event round set a tier with <paramref name="boss"/> plays in <paramref name="mode"/>: the CHIMPS events'
    /// set <paramref name="chimps"/> picks in CHIMPS, the boss's own set in every other mode and in CHIMPS with
    /// <see cref="ChimpsRounds.Own"/>
    /// </summary>
    public static string EventRoundSet(BossType boss, string? mode, ChimpsRounds chimps = ChimpsRounds.Newest) =>
        (mode == ChimpsMode ? ChimpsEventRoundSet(chimps) : null) ?? BossEventRoundSet(boss);

    /// <summary>The event round set <paramref name="tier"/> plays in <paramref name="mode"/></summary>
    public string EventRoundSetIn(int tier, string? mode) => EventRoundSet(BossOf(tier).Boss, mode, ChimpsRounds);

    /// <summary>The event round set the match plays in <paramref name="mode"/>, from the first tier's boss</summary>
    public string BaseEventRoundSetIn(string? mode) => EventRoundSetIn(1, mode);

    /// <summary>
    /// The skull thresholds (0-1, descending) these options ask for, or null to keep the vanilla skulls
    /// </summary>
    [JsonIgnore]
    public float[]? SkullPercents => SkullMath.Percents(SkullMode, SkullCount, CustomSkulls);

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
            ChimpsRounds = System.Enum.IsDefined(ChimpsRounds) ? ChimpsRounds : defaults.ChimpsRounds,
            SkullCount = System.Math.Clamp(SkullCount, 0, SkullMath.MaxSkulls),
            CustomSkulls = SkullMath.NormalizeCustom(CustomSkulls, defaults.CustomSkulls),
            Tiers = TierMath.ClampTiers(Tiers),
            SpeedPerTier = TierMath.ClampSpeedPerTier(SpeedPerTier),
            HealthPerTier = TierMath.ClampHealthPerTier(ExtraTierHealth is { } legacy && HealthPerTier == 0
                ? TierMath.HealthPerTierFromExtraTierHealth(legacy)
                : HealthPerTier),
            ExtraTierHealth = null,
            TierBosses = TierBossMath.Normalize(TierBosses),
            RandomTiers = TierBossMath.ParseTierList(RandomTiers) is {Length: > 0} random ? string.Join(",", random) : null
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

    /// <summary>
    /// The skull thresholds (0-1, descending) the given tier asks for, or null to keep its boss's own: in Advanced Mode
    /// the tier's own skulls, and <see cref="SkullPercents"/> for a tier that keeps Vanilla
    /// </summary>
    public float[]? TierSkullPercents(int tier) => EntryOf(tier).SkullPercents ?? SkullPercents;

    /// <summary>Whether a tier will get a random boss when the game starts</summary>
    [JsonIgnore]
    public bool HasRandom => Mixed && TierBossMath.HasRandom(TierBosses, Tiers);

    /// <summary>
    /// These options with a concrete boss for every tier: Random tiers get a boss picked by <paramref name="random"/>,
    /// and <see cref="RandomTiers"/> remembers which they were.
    /// Options without different bosses per tier come back as they are.
    /// </summary>
    public BossOptions ResolveRandom(System.Random random) => Mixed
        ? this with
        {
            TierBosses = TierBossMath.Resolve(TierBosses, Boss.ToString(), Elite, Tiers, random.Next),
            RandomTiers = TierBossMath.RandomTiers(TierBosses, Tiers)
        }
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

/// <summary>
/// The round sets Boss Event Rounds can play in CHIMPS (#139): one of the CHIMPS boss events', or the boss's own. New
/// values go at the end, so the numbers in saves and codes keep their meaning.
/// </summary>
public enum ChimpsRounds
{
    /// <summary>The newest CHIMPS boss event's rounds, "chimpsboss" (Diamondback, 2026)</summary>
    Newest,

    /// <summary>The earlier CHIMPS boss events' rounds, "chimpsbloonarius" (Bloonarius 2024, Dreadbloon 2025)</summary>
    Earlier,

    /// <summary>
    /// The boss's own event rounds, as in the other modes and in CHIMPS before 2.11; in Advanced Mode every tier gets the
    /// round made for its own boss
    /// </summary>
    Own
}
