using System;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Config;

/// <summary>
/// Turns the Challenge Editor's model into the model a custom Boss Challenge is played with
/// </summary>
public static class ConfigApplier
{
    /// <summary>
    /// Appended to the challenge name to recognise custom challenges, even after a restart
    /// </summary>
    public const string Marker = " (Custom)";

    /// <summary>
    /// The lowest boss speed/health multiplier allowed
    /// </summary>
    public const float MinMultiplier = 0.01f;

    public static bool IsMarked(DailyChallengeModel? dcm) => dcm?.name?.EndsWith(Marker) == true;

    /// <summary>
    /// Returns a prepared clone of <paramref name="editorDcm"/>; the editor's own model is left untouched
    /// </summary>
    public static DailyChallengeModel Prepare(DailyChallengeModel editorDcm, BossOptions options)
    {
        var dcm = editorDcm.Clone();

        var modifiers = dcm.bloonModifiers ??= new BloonModifiers();
        var health = modifiers.healthMultipliers ??= new BloonHealthMultipliers();
        modifiers.bossSpeedMultiplier = Math.Max(modifiers.bossSpeedMultiplier, MinMultiplier);
        health.boss = Math.Max(health.boss, MinMultiplier);

        dcm.chalType = ChallengeType.BossChallenge;

        // Tiers spawning on or after the end round never count: with the default end round (60 on medium) only the
        // first tier would be played. Like the weekly events, the last tier always gets 20 rounds to be beaten.
        var rules = dcm.startRules ??= new StartRules();
        rules.endRound = TierMath.EndRound(options.EffectiveSpawnRounds);

        // Starting on or after a tier's round skips it. Play refuses such a start round; this is the backstop for
        // any other way in, with the same rule as Play and the Random button.
        if (rules.round >= options.EffectiveSpawnRounds[0]) rules.round = -1;

        // Custom games never overwrite the player's Boss Challenge save and are never treated as a custom map
        dcm.disableSaves = true;
        dcm.customMapModel = null;

        if (!IsMarked(dcm))
        {
            var name = string.IsNullOrWhiteSpace(dcm.name) ? Creator.BossMode.DefaultName(options.Boss, options.Elite) : dcm.name;
            dcm.name = name + Marker;
        }

        return dcm;
    }

    /// <summary>
    /// Converts a whole percentage into a multiplier, never going below <see cref="MinMultiplier"/>
    /// </summary>
    public static float Multiplier(int percent) => Math.Max(percent / 100f, MinMultiplier);
}
