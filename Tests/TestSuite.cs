using System;
using System.Linq;
using System.Reflection;

namespace CustomBossChallenge.Tests;

/// <summary>
/// A test too slow for the quick suite (#246): it only runs in the full suite, or in a quick run that names it in
/// <c>CBC_TESTS</c> because its area changed. New tests are in the quick suite unless they get this mark; a test
/// that takes more than about 20 seconds should get it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FullSuiteOnlyAttribute : Attribute
{
    public string Reason { get; }

    public FullSuiteOnlyAttribute(string reason) => Reason = reason;
}

/// <summary>
/// Which tests a run plays (CONTRIBUTING.md, "Which tests to run"):
/// <list type="bullet">
/// <item><c>CBC_SUITE=quick</c>: every test without <see cref="FullSuiteOnlyAttribute"/>, about 8 minutes, plus
/// the ones <c>CBC_TESTS</c> names</item>
/// <item><c>CBC_TESTS=RetryTest,ContinueTest</c> alone: only those</item>
/// <item>neither: the full suite, every test, about 38 minutes; only its receipt counts for a release</item>
/// </list>
/// </summary>
public static class TestSuite
{
    public const string Full = "full";
    public const string Quick = "quick";
    public const string Selected = "selected";

    private static string[] Named =>
        (System.Environment.GetEnvironmentVariable("CBC_TESTS") ?? "").Split(',')
        .Select(name => name.Trim()).Where(name => name.Length > 0).ToArray();

    /// <summary>The kind of run: <see cref="Full"/>, <see cref="Quick"/> or <see cref="Selected"/></summary>
    public static string Name
    {
        get
        {
            var suite = System.Environment.GetEnvironmentVariable("CBC_SUITE")?.Trim().ToLowerInvariant();
            if (suite == Quick) return Quick;
            if (!string.IsNullOrEmpty(suite) && suite != Full)
            {
                throw new InvalidOperationException($"CBC_SUITE={suite} is not a suite: use {Quick} or {Full}");
            }

            return Named.Length > 0 ? Selected : Full;
        }
    }

    /// <summary>
    /// Why <paramref name="test"/> doesn't run in this run, or null when it does
    /// </summary>
    public static string? SkipReason(Type test)
    {
        var named = Named.Contains(test.Name);
        switch (Name)
        {
            case Quick:
                return named || test.GetCustomAttribute<FullSuiteOnlyAttribute>() == null ? null : "full suite only";
            case Selected:
                return named ? null : "not in CBC_TESTS";
            default:
                return null;
        }
    }
}
