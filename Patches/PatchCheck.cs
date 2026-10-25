using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BTD_Mod_Helper;
using HarmonyLib;

namespace CustomBossChallenge.Patches;

/// <summary>
/// Marks a patch that only changes how something looks, so the Boss Creator still opens without it
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class OptionalPatchAttribute : Attribute
{
}

/// <summary>
/// Checks that every patch of the mod is in place. A game update that renames or removes a patched method leaves that
/// patch out without any error in the game, and some of them keep custom games from touching the player's records,
/// achievements, saves and Monkey Money. If one is missing, the Boss Creator stays closed until the mod is updated.
/// </summary>
internal static class PatchCheck
{
    /// <summary>
    /// Methods looked up by name for a patch with several targets that could not be found
    /// </summary>
    private static readonly HashSet<string> NotFound = new();

    private static IReadOnlyList<string>? missing;

    /// <summary>
    /// How many patch classes the last check looked at
    /// </summary>
    public static int Checked { get; private set; }

    /// <summary>
    /// The patched methods that are missing or not patched, e.g. "Btd6Player.SetBossChallengeRecord". Tests can set
    /// it to pretend that a patch is missing; null works it out again.
    /// </summary>
    public static IReadOnlyList<string>? Missing
    {
        get => missing ??= Find();
        set => missing = value;
    }

    public static bool AllInPlace => Missing!.Count == 0;

    /// <summary>
    /// Looks up a target of a patch with several targets, remembering it when a game update removed it
    /// </summary>
    internal static MethodBase? Target(Type type, string name)
    {
        var method = AccessTools.Method(type, name);
        if (method == null)
        {
            NotFound.Add($"{type.Name}.{name}");
            ModHelper.Warning<CustomBossChallenge>($"{type.Name}.{name} not found, custom games can't guard it");
        }

        return method;
    }

    private static List<string> Find()
    {
        var problems = new List<string>(NotFound);
        Checked = 0;
        foreach (var patch in typeof(PatchCheck).Assembly.GetTypes())
        {
            if (patch.GetCustomAttributes<HarmonyPatch>().FirstOrDefault() == null) continue;

            Checked++;
            var found = Problems(patch);
            if (found.Count == 0) continue;

            if (patch.GetCustomAttribute<OptionalPatchAttribute>() != null)
            {
                ModHelper.Warning<CustomBossChallenge>($"Optional patch {patch.Name} is missing: {string.Join(", ", found)}");
                continue;
            }

            problems.AddRange(found);
        }

        problems = problems.Distinct().ToList();
        if (problems.Count > 0)
        {
            ModHelper.Error<CustomBossChallenge>(
                $"The Boss Creator is turned off, this version of BTD6 changed patched methods: {string.Join(", ", problems)}");
        }

        return problems;
    }

    /// <summary>
    /// The targets of <paramref name="patch"/> that are gone or not patched by it. Tests pass the target in
    /// <paramref name="info"/>, since a class with a patch attribute is always applied.
    /// </summary>
    internal static List<string> Problems(Type patch, HarmonyMethod? info = null)
    {
        var problems = new List<string>();
        foreach (var (target, name) in Targets(patch, info))
        {
            if (target == null) problems.Add($"{name} (gone)");
            else if (!IsPatchedBy(target, patch)) problems.Add($"{target.DeclaringType?.Name}.{target.Name} (not patched)");
        }

        return problems;
    }

    private static IEnumerable<(MethodBase? Target, string Name)> Targets(Type patch, HarmonyMethod? info)
    {
        var targetMethods = patch.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method => method.GetCustomAttribute<HarmonyTargetMethods>() != null);
        if (targetMethods != null && info == null)
        {
            var targets = targetMethods.Invoke(null, null) as IEnumerable<MethodBase> ?? Enumerable.Empty<MethodBase>();
            return targets.Select(target => ((MethodBase?) target, target.Name)).ToList();
        }

        info ??= HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patch));
        var name = $"{info.declaringType?.Name}.{info.methodName}";
        MethodBase? method;
        try
        {
            method = info.declaringType == null ? null : AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
        }
        catch (AmbiguousMatchException)
        {
            method = null;
        }

        return new[] {(method, name)};
    }

    internal static bool IsPatchedBy(MethodBase target, Type patch)
    {
        var info = HarmonyLib.Harmony.GetPatchInfo(target);
        return info != null && info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
            .Any(applied => applied.PatchMethod.DeclaringType == patch);
    }
}
