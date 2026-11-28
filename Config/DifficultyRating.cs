using System;
using System.Linq;
using Il2CppAssets.Scripts.Data.Boss;

namespace CustomBossChallenge.Config;

/// <summary>
/// A rough difficulty score for a setup, 1 to 10 stars (#263), shown next to the Tier Preview in the Boss Creator and on
/// the website. It is an estimate from the setup's numbers, not from playing it: it helps players compare setups and
/// pick one to try.
/// <br/>
/// The score is in points, roughly doublings of how hard the setup is compared with a normal boss event: the game's own
/// five Bloonarius tiers on Medium with the boss events' rounds score about 0. Twice the boss's health and speed, or
/// twice the bloons' health, adds about 1 point, and so does a rule that takes a big tool away from the player.
/// <see cref="StarSteps"/> turns the points into stars.
/// <br/>
/// The website has the same formula in web/packages/share-code/src/math/difficulty.ts; web/test/difficulty-cases.json
/// keeps the two the same.
/// </summary>
public static class DifficultyRating
{
    public const int MaxStars = 10;

    /// <summary>
    /// The points at which 2 to 10 stars begin: most normal boss events get 1 to 3 stars, most Elite ones 5 to 7, and 10
    /// is left for setups harder than any event so far
    /// </summary>
    public static readonly double[] StarSteps = [0.5, 1.5, 2.5, 3.25, 4, 5, 6.5, 8, 10];

    /// <summary>
    /// How much harder each boss is than Bloonarius, in points: Phayze and Lych count for more, as their abilities
    /// take towers or buffs away
    /// </summary>
    public static double BossWeight(BossType boss) => boss switch
    {
        BossType.Bloonarius => 0,
        BossType.Vortex => 0.25,
        BossType.Dreadbloon => 0.5,
        BossType.Blastapopoulos => 0.5,
        BossType.Diamondback => 0.5,
        BossType.Lych => 0.75,
        BossType.Phayze => 1,
        // It takes cash, freezes towers and locks farms: as hard as Lych, whose abilities take buffs away (#25)
        BossList.Gildebloon => 0.75,
        _ => RandomBossWeight
    };

    /// <summary>A Random tier's boss isn't known before the game, so it counts as an average boss</summary>
    public const double RandomBossWeight = 0.5;

    /// <summary>
    /// What an Elite tier adds to its boss: the Elite bosses have several times the health and are faster
    /// </summary>
    public const double EliteWeight = 3;

    /// <summary>What each tier more than the game's five adds, and each tier fewer takes away</summary>
    public const double TierCountWeight = 0.25;

    /// <summary>
    /// What halving the rounds between tiers adds, against the normal 20: tiers every round add about 2.2 points, as
    /// each tier must be beaten before the next one arrives
    /// </summary>
    public const double GapWeight = 0.5;

    /// <summary>
    /// What each skull more than the boss's own adds (#304): every skull uses the boss's ability once more, and some
    /// abilities hurt more than others. Phayze counts the most, as it also gets stronger at every skull; Bloonarius's
    /// skulls only send bloons.
    /// </summary>
    public static double SkullWeight(BossType boss) => boss switch
    {
        BossType.Bloonarius => 0.05,
        BossType.Diamondback => 0.1,
        BossType.Vortex => 0.15,
        BossType.Blastapopoulos => 0.15,
        BossType.Dreadbloon => 0.2,
        BossType.Lych => 0.2,
        BossType.Phayze => 0.3,
        // Each skull is a Repossession, a bigger share of the cash each time
        BossList.Gildebloon => 0.2,
        _ => RandomSkullWeight
    };

    /// <summary>A Random tier's boss isn't known before the game, so its skulls count as an average boss's</summary>
    public const double RandomSkullWeight = 0.15;

    /// <summary>What each skull fewer than the boss's own adds: fewer skulls, fewer breathers, but not much</summary>
    public const double FewerSkullWeight = 0.1;

    /// <summary>
    /// How many skulls the boss has in the game, normal and Elite; SkullModifierTest checks them against the game's
    /// models.
    /// </summary>
    public static int OwnSkulls(BossType boss, bool elite) => boss switch
    {
        BossType.Lych => 5,
        BossType.Phayze => elite ? 5 : 3,
        BossType.Blastapopoulos => elite ? 7 : 4,
        BossType.Diamondback => elite ? 7 : 5,
        BossType.Bloonarius or BossType.Vortex or BossType.Dreadbloon => elite ? 7 : 3,
        BossList.Gildebloon => elite ? 7 : 4,
        _ => RandomOwnSkulls(elite)
    };

    /// <summary>The skulls a Random tier counts as having, about the bosses' average</summary>
    public static int RandomOwnSkulls(bool elite) => elite ? 6 : 4;

    /// <summary>
    /// What a tier's skulls add: each one more than the boss's own adds the boss's <see cref="SkullWeight"/>, each
    /// one fewer <see cref="FewerSkullWeight"/>
    /// </summary>
    public static double SkullPoints(int skulls, int own, double weight) =>
        skulls >= own ? weight * (skulls - own) : FewerSkullWeight * (own - skulls);

    /// <summary>The stars for <paramref name="facts"/>, 1 to <see cref="MaxStars"/></summary>
    public static int Stars(SetupFacts facts) => StarsOf(Points(facts));

    /// <summary>The stars for a score in points</summary>
    public static int StarsOf(double points) => 1 + StarSteps.Count(step => points >= step);

    /// <summary>The score in points, the sum of <see cref="BossPoints"/>, <see cref="RoundPoints"/> and <see cref="RulePoints"/></summary>
    public static double Points(SetupFacts facts) => BossPoints(facts) + RoundPoints(facts) + RulePoints(facts);

    /// <summary>
    /// The bosses: each tier's health × speed compared with the normal game, its boss and Elite, weighted by the tier's
    /// number so the last tiers count the most; and the number of tiers
    /// </summary>
    public static double BossPoints(SetupFacts facts)
    {
        var options = facts.Options;
        var tiers = TierMath.ClampTiers(options.Tiers);
        double sum = 0, weights = 0;
        for (var tier = 1; tier <= tiers; tier++)
        {
            var strength = TierMath.HealthFactor(tier, options.HealthPerTier) * options.TierHealthFactor(tier) *
                           TierMath.ExtraTierFactor(tier) * Math.Max(facts.BossHealth, 0.01) *
                           TierMath.SpeedFactor(tier, options.SpeedPerTier) * options.TierSpeedFactor(tier) *
                           Math.Max(facts.BossSpeed, 0.01);
            var entry = options.EntryOf(tier);
            var (boss, elite) = options.BossOf(tier);
            var bossWeight = entry.IsRandom ? RandomBossWeight : BossWeight(boss);
            var isElite = entry.IsRandom ? entry.Elite : elite;
            sum += tier * (Log(strength) + bossWeight + (isElite ? EliteWeight : 0));
            weights += tier;
        }

        return sum / weights + TierCountWeight * (tiers - TierMath.VanillaTiers);
    }

    /// <summary>
    /// The rounds: tiers arriving earlier than in the normal game (40, 60, 80, ...) leave less time to build up, weighted
    /// like <see cref="BossPoints"/>; skulls other than the boss's own (<see cref="SkullPoints"/>); and the boss events' rounds are harder than
    /// the normal ones, the CHIMPS events' much harder
    /// </summary>
    public static double RoundPoints(SetupFacts facts)
    {
        var options = facts.Options;
        var rounds = options.EffectiveSpawnRounds;
        double early = 0, skulls = 0, weights = 0;
        for (var tier = 1; tier <= rounds.Length; tier++)
        {
            var normal = TierMath.FirstSpawnRound + TierMath.RoundsBetweenTiers * (tier - 1);
            early += tier * Math.Clamp(Math.Log2((double) normal / Math.Max(rounds[tier - 1], 1)), -1, 2);
            weights += tier;
            if (options.TierSkullPercents(tier) is { } percents)
            {
                var entry = options.EntryOf(tier);
                var (boss, elite) = options.BossOf(tier);
                skulls += entry.IsRandom
                    ? SkullPoints(percents.Length, RandomOwnSkulls(entry.Elite), RandomSkullWeight)
                    : SkullPoints(percents.Length, OwnSkulls(boss, elite), SkullWeight(boss));
            }
        }

        var points = early / weights + skulls / rounds.Length + GapPoints(rounds);
        if (options.UsesBossEventRounds)
            points += facts.Mode == BossOptions.ChimpsMode && BossOptions.IsChimpsEventRoundSet(options.BaseEventRoundSetIn(facts.Mode))
                ? 1
                : 0.25;
        return points;
    }

    /// <summary>
    /// The rounds between tiers: each gap shorter than <see cref="TierMath.RoundsBetweenTiers"/> leaves less time to beat a
    /// tier before the next one comes, <see cref="GapWeight"/> for each halving; longer gaps take a little away
    /// </summary>
    public static double GapPoints(int[] rounds)
    {
        if (rounds.Length < 2) return 0;
        double sum = 0;
        for (var i = 1; i < rounds.Length; i++)
        {
            var gap = Math.Max(rounds[i] - rounds[i - 1], 1);
            sum += Math.Clamp(Math.Log2((double) TierMath.RoundsBetweenTiers / gap), -1, Math.Log2(TierMath.RoundsBetweenTiers));
        }

        return GapWeight * sum / (rounds.Length - 1);
    }

    /// <summary>What the map adds, by its group in the game's map list: harder maps have shorter paths and less room</summary>
    public static double MapWeight(string mapDifficulty) => mapDifficulty switch
    {
        "Intermediate" => 0.25,
        "Advanced" => 0.5,
        "Expert" => 1,
        _ => 0
    };

    /// <summary>
    /// The Challenge Editor's rules: the map, the bloons' health and speed, lives, cash, towers, paragons, the rules that take a
    /// tool away, the difficulty and the mode
    /// </summary>
    public static double RulePoints(SetupFacts facts)
    {
        var points = MapWeight(facts.MapDifficulty) +
                     Log(facts.BloonHealth) + Log(facts.MoabHealth) + Log(facts.BloonSpeed) + Log(facts.MoabSpeed);
        points += facts.Lives switch
        {
            < 0 => 0,
            <= 1 => 2,
            <= 10 => 1,
            <= 50 => 0.5,
            _ => 0
        };
        points += facts.Cash is >= 0 and < 400 ? 1 : 0;
        points += facts.MaxTowers is >= 0 and <= 5 ? 1.5 : facts.MaxTowers is > 5 and <= 15 ? 0.5 : 0;
        points += facts.MaxParagons == 0 ? 0.5 : 0;
        points += facts.AllTowers > 0 ? 2.0 * Math.Clamp(facts.BannedTowers, 0, facts.AllTowers) / facts.AllTowers : 0;
        points += (facts.NoSelling ? 1 : 0) + (facts.NoKnowledge ? 0.5 : 0) + (facts.NoPowers ? 0.25 : 0);
        points += (facts.AllCamo ? 0.5 : 0) + (facts.AllRegrow ? 0.5 : 0) + (facts.AllFortified ? 1 : 0);
        points += facts.Difficulty switch
        {
            "Easy" => -0.5,
            "Hard" => 0.5,
            _ => 0
        };
        points += facts.Mode switch
        {
            BossOptions.ChimpsMode => 1.5,
            "Impoppable" => 1.5,
            "HalfCash" or "DoubleMoabHealth" or "PrimaryOnly" or "MilitaryOnly" or "MagicOnly" => 1,
            "Apopalypse" or "Deflation" or "AlternateBloonsRounds" => 0.5,
            _ => 0
        };
        return points;
    }

    private static double Log(double factor) => Math.Log2(Math.Max(factor, 0.01));
}
