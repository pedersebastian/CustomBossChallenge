using System;
using System.Linq;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Config;

/// <summary>
/// What the Challenge Editor's Random button does to the boss options. It works like the game's own randomizer: each
/// option has a chance to change and otherwise keeps its default. A changed value is spread over a range that stays
/// playable, with values close to the normal game more likely than extreme ones (110 % far more often than 400 %).
/// In Advanced Mode every tier gets a boss of its own, and the setup stays in Advanced Mode; Standard stays Standard.
/// The chances, weights and the bosses per tier are in BossRandomizer.Weights.cs, which is built without the game.
/// </summary>
public static partial class BossRandomizer
{
    /// <summary>
    /// New random boss options, with the boss values in the challenge randomized to match. Fixes what the game's
    /// randomizer picks that a boss game can't use: unsupported game modes and a start round after the first tier.
    /// </summary>
    /// <param name="advanced">Whether the setup is in Advanced Mode, which random setups then are too</param>
    public static BossOptions Randomize(DailyChallengeModel dcm, string[] unsupportedModes, bool advanced = false)
    {
        var bosses = BossList.Available;
        // Like a new setup, with Boss Event Rounds on (#251)
        var options = BossOptions.NewSetup with
        {
            Boss = bosses[Random.Next(bosses.Length)],
            Elite = Chance(EliteChance),
            Tiers = WeightedTiers()
        };

        if (advanced) options = options with {TierBosses = RandomTierBosses()};

        if (Chance(SpeedPerTierChance))
        {
            options = options with {SpeedPerTier = Step(TierMath.PerTierStep, MaxRandomSpeedPerTier, TierMath.PerTierStep)};
        }

        if (Chance(HealthPerTierChance))
        {
            options = options with {HealthPerTier = Step(TierMath.PerTierStep, MaxRandomHealthPerTier, TierMath.PerTierStep)};
        }

        // In Advanced every tier has its own skulls, and the Skulls option stays Vanilla
        if (!advanced)
        {
            var skulls = RandomSkullsOf(TierBoss.Follows);
            options = options with {SkullMode = skulls.Skulls, SkullCount = skulls.SkullCount, CustomSkulls = skulls.CustomSkulls};
        }

        var modifiers = dcm.bloonModifiers ??= new BloonModifiers();
        var health = modifiers.healthMultipliers ??= new BloonHealthMultipliers();
        // In Advanced every tier has its own speed and health, and Boss Speed and Boss Health stay at 100 %
        modifiers.bossSpeedMultiplier = !advanced && Chance(SpeedChance) ? WeightedPercent() / 100f : 1;
        health.boss = !advanced && Chance(HealthChance) ? WeightedPercent() / 100f : 1;
        modifiers.allFortified = Chance(FortifiedChance);
        dcm.maxParagons = Chance(ParagonChance) ? Random.Next(0, MaxRandomParagons + 1) : ParagonMath.NoLimit;

        if (unsupportedModes.Contains(dcm.mode)) dcm.mode = "Standard";

        // Starting after the first tier's round would skip it
        var rules = dcm.startRules ??= new StartRules();
        var firstSpawn = options.EffectiveSpawnRounds[0];
        if (rules.round >= firstSpawn) rules.round = Random.Next(1, firstSpawn);

        return options.Sanitized();
    }
}
