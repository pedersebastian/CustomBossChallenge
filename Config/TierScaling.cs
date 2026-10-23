using System;
using System.Collections.Generic;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;

namespace CustomBossChallenge.Config;

/// <summary>
/// Gives every boss tier its speed and health from Speed per tier and Health per tier.
/// <br/>
/// The game has already applied the challenge's own multipliers to the match's tier models when the mod sees them,
/// and a restart reuses those models. So the values a model had before it was first scaled are remembered per match,
/// and scaling always starts from them: it keeps the game's multipliers and can't be applied twice.
/// </summary>
public static class TierScaling
{
    public readonly record struct Values(float Speed, float SpeedFrames, int MaxHealth)
    {
        public static Values Of(BloonModel model) => new(model.speed, model.speedFrames, model.maxHealth);
    }

    /// <summary>
    /// The match the originals belong to. Holding the model itself, not just its address, keeps it from being freed,
    /// so a later match can never get the same address and be taken for this one.
    /// </summary>
    private static GameModel? originalsMatch;

    private static readonly Dictionary<string, Values> Originals = new();

    private static bool IsOriginalsMatch(GameModel match) => originalsMatch != null && originalsMatch.Pointer == match.Pointer;

    /// <summary>
    /// Sets the speed and health of a tier from <paramref name="source"/>: the tier's values before scaling, or tier
    /// 5's for the tiers above it. Health is held to what the game can take with <paramref name="bossHealth"/>.
    /// </summary>
    public static void Scale(BloonModel model, Values source, BossOptions options, int tier, double bossHealth = 1)
    {
        var speed = (float) TierMath.SpeedFactor(tier, options.SpeedPerTier);
        model.speed = source.Speed * speed;
        model.speedFrames = source.SpeedFrames * speed;
        model.maxHealth = TierMath.ScaleHealth(source.MaxHealth,
            TierMath.ExtraTierFactor(tier) * TierMath.HealthFactor(tier, options.HealthPerTier), bossHealth);
    }

    /// <summary>
    /// Scales every tier of the boss in the match's game model, for a challenge with <paramref name="bossHealth"/>
    /// </summary>
    /// <returns>How many tier models were set</returns>
    public static int Apply(GameModel match, BossOptions options, double bossHealth)
    {
        if (!IsOriginalsMatch(match))
        {
            originalsMatch = match;
            Originals.Clear();
        }

        var scaled = 0;
        for (var tier = TierMath.MinTiers; tier <= TierMath.ClampTiers(options.Tiers); tier++)
        {
            var id = options.BossId(tier);
            var sourceId = options.BossId(Math.Min(tier, TierMath.VanillaTiers));
            if (!match.DoesBloonExist(id) || !match.DoesBloonExist(sourceId)) continue;

            Scale(match.GetBloon(id), Original(match, sourceId), options, tier, bossHealth);
            scaled++;
        }

        return scaled;
    }

    /// <summary>
    /// The values a tier model of the match had before it was first scaled
    /// </summary>
    public static Values Original(GameModel match, string id)
    {
        if (IsOriginalsMatch(match) && Originals.TryGetValue(id, out var values)) return values;

        values = Values.Of(match.GetBloon(id));
        if (IsOriginalsMatch(match)) Originals[id] = values;
        return values;
    }
}
