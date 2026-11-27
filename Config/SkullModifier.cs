using System.Linq;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors.Actions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CustomBossChallenge.Config;

/// <summary>
/// Changes the skull thresholds of boss bloons.
/// <br/>
/// Most bosses fire one non-repeating <see cref="HealthPercentTriggerModel"/> at each skull; its actionIds are the
/// actions run at every threshold, so the percentages are replaced and the actions kept. Bloonarius also has a
/// repeating 1% bleed trigger that must be left alone. Lych instead goes ethereal at the thresholds of its
/// <see cref="LychBossSuperScriptModel"/>. Dreadbloon and Phayze also keep a value per skull, made to fit the skull
/// count (<see cref="FitPerSkullValues"/>).
/// </summary>
public static class SkullModifier
{
    /// <summary>
    /// Applies the skulls to every boss in the game model
    /// </summary>
    /// <returns>How many boss models were changed</returns>
    public static int Apply(GameModel gameModel, float[]? percents)
    {
        if (percents == null) return 0;

        return gameModel.bloons.Count(bloon => Apply(bloon, percents));
    }

    /// <summary>
    /// Applies each tier's own skulls to its boss, for a setup with a different boss per tier. Every tier has a model
    /// of its own, so tiers with the same boss can have different skulls.
    /// </summary>
    /// <returns>How many boss models were changed</returns>
    public static int ApplyTiers(GameModel gameModel, BossOptions options) =>
        Enumerable.Range(1, TierMath.ClampTiers(options.Tiers)).Count(tier =>
            options.TierSkullPercents(tier) is { } percents && gameModel.DoesBloonExist(options.BossId(tier)) &&
            Apply(gameModel.GetBloon(options.BossId(tier)), percents));

    /// <summary>
    /// Applies the skulls to a single boss model
    /// </summary>
    /// <returns>Whether the bloon had skulls that were changed</returns>
    public static bool Apply(BloonModel bloon, float[] percents)
    {
        if (!bloon.isBoss || bloon.isBossSegment) return false;

        var lych = bloon.GetBehavior<LychBossSuperScriptModel>();
        if (lych != null)
        {
            lych.etherealHealthPercentageValues = new Il2CppStructArray<float>(percents);
            return true;
        }

        var trigger = GetSkullTrigger(bloon);
        if (trigger == null) return false;

        trigger.percentageValues = new Il2CppStructArray<float>(percents);
        FitPerSkullValues(bloon, percents.Length);
        return true;
    }

    /// <summary>
    /// Makes what a boss keeps per skull long enough for <paramref name="skulls"/> skulls (#304). The game looks
    /// it up by the skull's number and does nothing past the end, so more skulls than the boss's own lost it:
    /// <list type="bullet">
    /// <item>Dreadbloon's armour is immune to a tower set at spawn and at every skull (Primary, Military, Magic,
    /// Support); they go round, as on Elite Dreadbloon, whose list has them twice for its 7 skulls.</item>
    /// <item>Phayze gets stronger at every skull, a power level per skull; past its own they keep growing by the
    /// last step, as Elite Phayze's go on from the normal one's.</item>
    /// </list>
    /// </summary>
    private static void FitPerSkullValues(BloonModel bloon, int skulls)
    {
        foreach (var immunity in bloon.GetBehaviors<ApplyTowerSetImmunityModel>())
        {
            // One for the armour it spawns with, then one per skull
            immunity.immunities = new Il2CppReferenceArray<ApplyTowerSetImmunityModel.TowerTypeImmunity>(
                SkullMath.Cycle(immunity.immunities.ToArray(), skulls + 1));
        }

        foreach (var phayze in bloon.GetBehaviors<PhayzeBehaviorModel>())
        {
            phayze.powerLevels = new Il2CppStructArray<float>(SkullMath.ContinueSteps(phayze.powerLevels.ToArray(), skulls));
        }
    }

    /// <summary>
    /// The current skull thresholds of a boss model, or null if it doesn't have any
    /// </summary>
    public static float[]? GetSkulls(BloonModel bloon)
    {
        var lych = bloon.GetBehavior<LychBossSuperScriptModel>();
        if (lych != null) return lych.etherealHealthPercentageValues?.ToArray();

        return GetSkullTrigger(bloon)?.percentageValues?.ToArray();
    }

    private static HealthPercentTriggerModel? GetSkullTrigger(BloonModel bloon) =>
        bloon.GetBehaviors<HealthPercentTriggerModel>().FirstOrDefault(trigger => !trigger.repeatFirst);
}
