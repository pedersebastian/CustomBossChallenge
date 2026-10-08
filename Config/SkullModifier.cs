using System.Linq;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CustomBossChallenge.Config;

/// <summary>
/// Changes the skull thresholds of boss bloons.
/// <br/>
/// Most bosses fire one non-repeating <see cref="HealthPercentTriggerModel"/> at each skull; its actionIds are the
/// actions run at every threshold, so only the percentages need replacing. Bloonarius also has a repeating 1% bleed
/// trigger that must be left alone. Lych instead goes ethereal at the thresholds of its
/// <see cref="LychBossSuperScriptModel"/>.
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
        return true;
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
