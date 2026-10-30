using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace CustomBossChallenge.Tests;

/// <summary>
/// What happens to our patches when another mod's prefix skips the method, in the Harmony that MelonLoader ships. The
/// guards rely on it, so the compatibility tests (issue #24) check it instead of assuming what Harmony's docs say.
/// </summary>
public class HarmonyBehaviourTest : CustomBossTest
{
    private const string HarmonyId = "cbc.harmony-behaviour";

    private static readonly List<string> Calls = new();

    protected override IEnumerator Run()
    {
        foreach (var rivalPriority in new[] {Priority.First, Priority.Last})
        {
            var calls = RunWithRival(rivalPriority);
            Log($"rival prefix with priority {rivalPriority} returns false: {string.Join(", ", calls)}");

            // HarmonyX runs every prefix and postfix even when one returns false; only the method itself is skipped.
            // So another mod can't switch our guards off this way, only change values after or before we do.
            Assert(calls.Contains("our prefix"), $"Priority {rivalPriority}: our prefix must still run");
            Assert(calls.Contains("our postfix"), $"Priority {rivalPriority}: our postfix must still run");
            Assert(!calls.Contains("original"), $"Priority {rivalPriority}: the method itself is skipped");
            Assert(calls.Contains("result 2"), $"Priority {rivalPriority}: the rival's result is used");
        }

        // Which prefix runs first only follows the priority
        var first = RunWithRival(Priority.First);
        Assert(first.IndexOf("rival prefix") < first.IndexOf("our prefix"), "Priority.First runs before ours");
        var last = RunWithRival(Priority.Last);
        Assert(last.IndexOf("our prefix") < last.IndexOf("rival prefix"), "Priority.Last runs after ours");

        yield break;
    }

    private List<string> RunWithRival(int rivalPriority)
    {
        var original = AccessTools.Method(typeof(HarmonyBehaviourTest), nameof(Original));
        var harmony = new HarmonyLib.Harmony(HarmonyId);
        try
        {
            harmony.Patch(original, new HarmonyMethod(typeof(HarmonyBehaviourTest), nameof(RivalPrefix)) {priority = rivalPriority});
            harmony.Patch(original, new HarmonyMethod(typeof(HarmonyBehaviourTest), nameof(OurPrefix)),
                new HarmonyMethod(typeof(HarmonyBehaviourTest), nameof(OurPostfix)));
            Calls.Clear();
            Calls.Add($"result {Original()}");
            return new List<string>(Calls);
        }
        finally
        {
            harmony.UnpatchSelf();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Original()
    {
        Calls.Add("original");
        return 1;
    }

    private static bool RivalPrefix(ref int __result)
    {
        Calls.Add("rival prefix");
        __result = 2;
        return false;
    }

    private static void OurPrefix() => Calls.Add("our prefix");

    private static void OurPostfix() => Calls.Add("our postfix");
}
