using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.Achievements;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;

namespace CustomBossChallenge.Patches;

/// <summary>
/// Custom games must not count towards achievements: a boss with 1 % health would otherwise unlock them, in the game
/// and on Steam/Epic. XP, Monkey Money and the like are still awarded.
/// </summary>
[HarmonyPatch]
internal static class AchievementPatches
{
    /// <summary>
    /// Where achievement progress is worked out, set and passed on to the platform. The achievements' own virtual
    /// methods (UpdateProgress, OnShowVictory) are not patched: the game shares their native code with other
    /// methods, and patching them crashes it on start.
    /// </summary>
    internal static readonly (System.Type Type, string Name)[] Methods =
    {
        (typeof(AchievementManager), nameof(AchievementManager.UpdateWithPlayerData)),
        (typeof(AchievementManager), nameof(AchievementManager.SetAchievementProgress)),
        (typeof(AchievementManager), nameof(AchievementManager.IncreaseAchievementProgress)),
        (typeof(ActiveAchievement), nameof(ActiveAchievement.UpdateExternalComplete)),
        (typeof(ActiveAchievement), nameof(ActiveAchievement.UpdateExternalStat))
    };

    /// <summary>
    /// Only while the custom game itself runs: after leaving it, progress made elsewhere (e.g. in the store) counts as
    /// usual, even though the game still remembers the custom challenge until the next game
    /// </summary>
    internal static bool Blocked => CustomBossState.IsActive && InGame.instance != null;

    /// <summary>
    /// A method a game update renamed or removed is skipped, so the others are still guarded; <see cref="PatchCheck"/>
    /// then keeps the Boss Creator closed
    /// </summary>
    [HarmonyTargetMethods]
    internal static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        Methods.Select(method => PatchCheck.Target(method.Type, method.Name)).Where(method => method != null)!;

    [HarmonyPrefix]
    internal static bool Prefix() => !Blocked;
}
