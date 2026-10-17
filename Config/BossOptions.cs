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
    public string CustomSkulls { get; init; } = "75,50,25";

    /// <summary>Number of boss tiers, 1-10. Tiers above 5 are stronger copies of tier 5.</summary>
    public int Tiers { get; init; } = TierMath.VanillaTiers;

    /// <summary>Health of each tier above 5, in percent of the tier before it</summary>
    public int ExtraTierHealth { get; init; } = 150;

    /// <summary>Comma separated rounds the tiers spawn on, or null for the defaults (40, 60, 80, ...)</summary>
    public string? SpawnRounds { get; init; }

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
            CustomSkulls = CustomSkulls ?? defaults.CustomSkulls,
            Tiers = TierMath.ClampTiers(Tiers),
            ExtraTierHealth = System.Math.Clamp(ExtraTierHealth, TierMath.MinExtraTierHealth, TierMath.MaxExtraTierHealth)
        };
    }

    /// <summary>
    /// The bloon id of this boss at the given tier, e.g. "VortexElite3"
    /// </summary>
    public string BossId(int tier) => TierMath.BossId(Boss, Elite, tier);
}
