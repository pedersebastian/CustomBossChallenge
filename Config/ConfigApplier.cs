using System;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Config;

/// <summary>
/// Applies a <see cref="BossChallengeConfig"/> to a Boss Challenge <see cref="DailyChallengeModel"/>
/// </summary>
public static class ConfigApplier
{
    /// <summary>
    /// Appended to the challenge name to recognise custom challenges, even after a restart
    /// </summary>
    public const string Marker = " (Custom)";

    public static bool IsMarked(DailyChallengeModel? dcm) => dcm?.name?.EndsWith(Marker) == true;

    /// <summary>
    /// Returns a modified clone of <paramref name="source"/>; the source itself is left untouched
    /// </summary>
    public static DailyChallengeModel ApplyToClone(DailyChallengeModel source, BossChallengeConfig config)
    {
        var dcm = source.Clone();
        Apply(dcm, config);
        return dcm;
    }

    /// <summary>
    /// Mutates <paramref name="dcm"/> so that it uses the values of <paramref name="config"/>
    /// </summary>
    public static void Apply(DailyChallengeModel dcm, BossChallengeConfig config)
    {
        var modifiers = dcm.bloonModifiers ??= new BloonModifiers();
        var health = modifiers.healthMultipliers ??= new BloonHealthMultipliers();

        modifiers.bossSpeedMultiplier = Multiplier(config.BossSpeed);
        health.boss = Multiplier(config.BossHealth);

        modifiers.speedMultiplier = Multiplier(config.BloonSpeed);
        modifiers.moabSpeedMultiplier = Multiplier(config.MoabSpeed);
        modifiers.regrowRateMultiplier = Multiplier(config.RegrowRate);
        health.bloons = Multiplier(config.CeramicHealth);
        health.moabs = Multiplier(config.MoabHealth);

        // Flags are only ever switched on, so a base challenge that already has them keeps them
        modifiers.allCamo |= config.AllCamo;
        modifiers.allRegen |= config.AllRegen;
        modifiers.allFortified |= config.AllFortified;
        dcm.disableMK |= config.NoMonkeyKnowledge;
        dcm.disableSelling |= config.NoSelling;
        dcm.disablePowers |= config.NoPowers;
        dcm.noContinues |= config.NoContinues;

        ApplyStartRules(dcm.startRules ??= new StartRules(), config);

        // Custom games never overwrite the player's vanilla Boss Challenge save
        dcm.disableSaves = true;

        if (!IsMarked(dcm))
        {
            dcm.name = (string.IsNullOrEmpty(dcm.name) ? "Boss Challenge" : dcm.name) + Marker;
        }
    }

    private static void ApplyStartRules(StartRules rules, BossChallengeConfig config)
    {
        if (config.StartCash > 0) rules.cash = config.StartCash;
        if (config.StartLives > 0) rules.lives = config.StartLives;
        if (config.MaxLives > 0) rules.maxLives = config.MaxLives;
        if (config.StartRound > 0) rules.round = config.StartRound;
        if (config.EndRound > 0) rules.endRound = config.EndRound;

        // Keep the rules consistent if only some of them were customized
        if (rules.maxLives > 0 && rules.lives > rules.maxLives) rules.maxLives = rules.lives;
        if (rules.endRound > 0 && rules.round > 0 && rules.endRound <= rules.round) rules.endRound = rules.round + 1;
    }

    /// <summary>
    /// Converts a whole percentage into a multiplier, never going below 1%
    /// </summary>
    public static float Multiplier(int percent) => Math.Max(percent, 1) / 100f;
}
