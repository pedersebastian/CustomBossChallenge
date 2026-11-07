using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.Testing;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Writes TestResults/ingame.json when the game quits after a run of every test, so a release can show that the
/// in-game tests passed for its code: Check-Release.ps1 refuses a tag without a passing run of the same source.
/// Only Debug builds know their source and the repo (<c>CBC.*</c> metadata from CustomBossChallenge.csproj).
/// <br/>
/// Mod Helper quits with <c>-q</c> right after the last test, so the report is written then, when every test's
/// result is known. A run of single tests, or one stopped early, leaves tests not completed and writes nothing.
/// </summary>
public static class TestRunReport
{
    public const string RelativePath = "TestResults/ingame.json";

    private static readonly SortedDictionary<string, string> Skipped = new(StringComparer.Ordinal);
    private static readonly SortedDictionary<string, string> NeedsOwnRun = new(StringComparer.Ordinal);
    private static bool watching;

    /// <summary>
    /// Called as tests start; the first call waits for the game to quit
    /// </summary>
    public static void Watch()
    {
        if (watching) return;
        watching = true;
        MelonEvents.OnApplicationQuit.Subscribe(Write, unsubscribeOnFirstInvocation: true);
    }

    /// <summary>
    /// A test that didn't check what it normally checks; a run with skipped tests doesn't count for a release
    /// </summary>
    public static void Skip(string test, string reason) => Skipped[test] = reason;

    /// <summary>
    /// A test that only runs when asked, in runs of its own (e.g. the two game starts of a crash). It isn't counted
    /// as passed, and doesn't keep a normal run from counting for a release.
    /// </summary>
    public static void OnRequest(string test, string reason) => NeedsOwnRun[test] = reason;

    private static void Write()
    {
        try
        {
            var metadata = typeof(TestRunReport).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(attribute => attribute.Key.StartsWith("CBC."))
                .ToDictionary(attribute => attribute.Key, attribute => attribute.Value ?? "");
            if (!metadata.TryGetValue("CBC.RepoRoot", out var root) || string.IsNullOrEmpty(root))
            {
                ModHelper.Msg<CustomBossChallenge>($"{RelativePath} not written: only Debug builds know their source");
                return;
            }

            var tests = ModContent.GetInstance<CustomBossChallenge>().Content.OfType<ModTest>()
                .Where(test => test.IsAvailable).ToList();
            var notRun = tests.Count(test => !test.Completed);
            if (notRun > 0)
            {
                ModHelper.Msg<CustomBossChallenge>($"{RelativePath} not written: {notRun} of {tests.Count} test(s) didn't run");
                return;
            }

            var report = new JObject
            {
                ["modVersion"] = ModHelperData.Version,
                ["gameVersion"] = UnityEngine.Application.version,
                ["commit"] = metadata.GetValueOrDefault("CBC.Commit", ""),
                ["dirty"] = metadata.GetValueOrDefault("CBC.Dirty", "") == "true",
                ["sourceHash"] = metadata.GetValueOrDefault("CBC.SourceHash", ""),
                ["passed"] = tests.Count(test => test.Suceeded && !Skipped.ContainsKey(test.Name) && !NeedsOwnRun.ContainsKey(test.Name)),
                ["failed"] = new JArray(tests.Where(test => test.Failed).Select(test => (object) test.Name)),
                ["skipped"] = new JObject(Skipped.Select(skip => new JProperty(skip.Key, skip.Value))),
                ["onRequest"] = new JObject(NeedsOwnRun.Select(test => new JProperty(test.Key, test.Value))),
                ["finished"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var path = Path.Combine(root, RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, report.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
            ModHelper.Msg<CustomBossChallenge>($"wrote {path}");
        }
        catch (Exception e)
        {
            ModHelper.Error<CustomBossChallenge>($"{RelativePath} not written: {e}");
        }
    }
}
