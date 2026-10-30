using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BTD_Mod_Helper;
using HarmonyLib;

namespace CustomBossChallenge.Patches;

/// <summary>
/// Which other mods patch the methods this mod patches. Two mods patching the same method is usually fine, but a
/// prefix of another mod that runs first can skip the method and our patches with it, and both mods may change the
/// same value. On a guarded method that could let a custom game reach the player's records, so those are logged at
/// startup, where they show up in the Latest.log of a bug report.
/// </summary>
internal static class ConflictReport
{
    /// <summary>
    /// Start of the lines the compatibility tests read from the log (.github/scripts/Test-ModCompatibility.ps1)
    /// </summary>
    public const string LogPrefix = "CBC-CONFLICT|";

    /// <param name="Method">The patched method, e.g. "Btd6Player.SetBossChallengeRecord"</param>
    /// <param name="Owner">The other mod's assembly, e.g. "TimeMachine"</param>
    /// <param name="HarmonyId">The Harmony instance that applied the patch</param>
    /// <param name="Kind">prefix, postfix, transpiler or finalizer</param>
    /// <param name="RunsFirst">A prefix of theirs that runs before ours, so it can skip the method and our patch</param>
    /// <param name="Guard">The method is one of our guards (<see cref="GuardPatchAttribute"/>)</param>
    public sealed record Overlap(string Method, string Owner, string HarmonyId, string Kind, int Priority, bool RunsFirst, bool Guard)
    {
        public string LogLine => $"{LogPrefix}{Method}|{Owner}|{HarmonyId}|{Kind}|{Priority}|{(RunsFirst ? "first" : "")}|{(Guard ? "guard" : "")}";

        public override string ToString() =>
            $"{Owner} also patches {Method} ({Kind}{(RunsFirst ? ", runs before ours" : "")})";
    }

    private static readonly Assembly Ours = typeof(ConflictReport).Assembly;

    private static bool reported;

    /// <summary>
    /// Every method this mod patches, with the patch class and whether it is a guard
    /// </summary>
    internal static IEnumerable<(MethodBase Target, Type Patch, bool Guard)> OurTargets() =>
        Ours.GetTypes()
            .Where(type => type.GetCustomAttributes<HarmonyPatch>().Any())
            .SelectMany(patch => PatchCheck.Targets(patch)
                .Where(target => target.Target != null)
                .Select(target => (target.Target!, patch, patch.GetCustomAttribute<GuardPatchAttribute>() != null)));

    /// <summary>
    /// Other mods' patches on the methods this mod patches
    /// </summary>
    public static List<Overlap> Find()
    {
        var found = new List<Overlap>();
        foreach (var group in OurTargets().GroupBy(target => target.Target))
        {
            var info = HarmonyLib.Harmony.GetPatchInfo(group.Key);
            if (info == null) continue;

            var method = $"{group.Key.DeclaringType?.Name}.{group.Key.Name}";
            var guard = group.Any(target => target.Guard);
            var ourPrefixes = info.Prefixes.Where(IsOurs).ToList();
            foreach (var (kind, patches) in new[]
                     {
                         ("prefix", info.Prefixes), ("postfix", info.Postfixes), ("transpiler", info.Transpilers),
                         ("finalizer", info.Finalizers)
                     })
            {
                foreach (var patch in patches.Where(patch => !IsOurs(patch)))
                {
                    var runsFirst = kind == "prefix" && ourPrefixes.Any(ours => RunsBefore(patch, ours));
                    found.Add(new Overlap(method, patch.PatchMethod.DeclaringType?.Assembly.GetName().Name ?? patch.owner,
                        patch.owner, kind, patch.priority, runsFirst, guard));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Harmony runs patches by priority, highest first, and in the order they were applied when equal
    /// </summary>
    internal static bool RunsBefore(Patch patch, Patch other) =>
        patch.priority > other.priority || (patch.priority == other.priority && patch.index < other.index);

    /// <summary>
    /// Our patch classes; tests that patch from other classes of this assembly stand in for other mods
    /// </summary>
    private static bool IsOurs(Patch patch) =>
        patch.PatchMethod.DeclaringType is { } type && type.Assembly == Ours && type.GetCustomAttributes<HarmonyPatch>().Any();

    /// <summary>
    /// Logs other mods' patches on guarded methods, once per start. Silent when there are none.
    /// </summary>
    public static void LogOnce()
    {
        if (reported) return;
        reported = true;

        try
        {
            foreach (var overlap in Find().Where(overlap => overlap.Guard))
            {
                ModHelper.Warning<CustomBossChallenge>(
                    $"{overlap}. If custom games change your records or Monkey Money, mention this in a bug report.");
            }
        }
        catch (Exception e)
        {
            // Only a report: it must never keep the mod from working
            ModHelper.Warning<CustomBossChallenge>($"Couldn't check other mods' patches: {e.Message}");
        }
    }
}
