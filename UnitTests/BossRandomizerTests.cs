using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The Boss Creator's Random gives the same values as the website's Studio for the same random numbers, from
/// web/test/random-cases.json, which web/test/random.test.ts reads too. Randomize itself needs the game's challenge
/// and is tested in-game (OptionsTests).
/// </summary>
[Collection(nameof(BossRandomizerTests))]
public class BossRandomizerTests
{
    private static JObject Cases()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "web", "test", "random-cases.json"))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("web/test/random-cases.json not found above " + AppContext.BaseDirectory);
        return JObject.Parse(File.ReadAllText(Path.Combine(folder.FullName, "web", "test", "random-cases.json")));
    }

    /// <summary>
    /// Gives the listed numbers, and Next(n) as the website's next(): the number times n, rounded down
    /// </summary>
    private sealed class ListedRandom(IReadOnlyList<double> rolls) : Random
    {
        private int next;

        public int Used => next;

        public override double NextDouble() =>
            next < rolls.Count ? rolls[next++] : throw new InvalidOperationException($"used more than the {rolls.Count} rolls");

        public override int Next(int maxValue) => (int) Math.Floor(NextDouble() * maxValue);

        public override int Next(int minValue, int maxValue) => minValue + (int) Math.Floor(NextDouble() * (maxValue - minValue));

        public override int Next() => throw new InvalidOperationException("the website has no Next() without a limit");

        protected override double Sample() => NextDouble();
    }

    private static T With<T>(Random random, Func<T> pick)
    {
        var before = BossRandomizer.Random;
        BossRandomizer.Random = random;
        try
        {
            return pick();
        }
        finally
        {
            BossRandomizer.Random = before;
        }
    }

    public static IEnumerable<object[]> PercentWeights() =>
        ((JArray) Cases()["percentWeights"]!).Select(c => new object[] {(int) c[0]!, (double) c[1]!});

    public static IEnumerable<object[]> TierWeights() =>
        ((JArray) Cases()["tierWeights"]!).Select(c => new object[] {(int) c[0]!, (double) c[1]!});

    [Theory]
    [MemberData(nameof(PercentWeights))]
    public void A_percent_is_as_likely_as_on_the_website(int percent, double weight) =>
        Assert.Equal(weight, BossRandomizer.PercentWeight(percent), 1e-12);

    [Theory]
    [MemberData(nameof(TierWeights))]
    public void A_number_of_tiers_is_as_likely_as_on_the_website(int tiers, double weight) =>
        Assert.Equal(weight, BossRandomizer.TierWeight(tiers), 1e-12);

    public static IEnumerable<object[]> WeightedPercents() =>
        ((JArray) Cases()["weightedPercent"]!).Select(c => new object[] {(double) c["roll"]!, (int) c["value"]!});

    public static IEnumerable<object[]> WeightedTiers() =>
        ((JArray) Cases()["weightedTiers"]!).Select(c => new object[] {(double) c["roll"]!, (int) c["value"]!});

    [Theory]
    [MemberData(nameof(WeightedPercents))]
    public void A_roll_picks_the_same_percent_as_on_the_website(double roll, int value) =>
        Assert.Equal(value, With(new ListedRandom([roll]), BossRandomizer.WeightedPercent));

    [Theory]
    [MemberData(nameof(WeightedTiers))]
    public void A_roll_picks_the_same_tiers_as_on_the_website(double roll, int value) =>
        Assert.Equal(value, With(new ListedRandom([roll]), BossRandomizer.WeightedTiers));

    public static IEnumerable<object[]> TierBosses() =>
        ((JArray) Cases()["tierBosses"]!).Select(c =>
            new object[] {c["rolls"]!.Select(r => (double) r).ToArray(), (string) c["expected"]!});

    [Theory]
    [MemberData(nameof(TierBosses))]
    public void The_same_rolls_give_the_same_bosses_per_tier_as_on_the_website(double[] rolls, string expected)
    {
        var random = new ListedRandom(rolls);
        Assert.Equal(expected, With(random, BossRandomizer.RandomTierBosses));
        Assert.Equal(rolls.Length, random.Used);
    }

    [Fact]
    public void Random_bosses_per_tier_are_ten_valid_tiers()
    {
        var random = new Random(4321);
        for (var i = 0; i < 500; i++)
        {
            var list = With(random, BossRandomizer.RandomTierBosses);
            Assert.Equal(list, TierBossMath.Normalize(list));
            var tiers = TierBossMath.Parse(list);
            Assert.Equal(TierMath.MaxTiers, tiers.Length);
            Assert.All(tiers, tier => Assert.NotNull(tier.Boss));
            // Each tier's own speed and health are from the randomizer's range, or the normal 100 %
            Assert.All(tiers, tier => Assert.InRange(tier.Speed, BossRandomizer.MinPercent, BossRandomizer.MaxPercent));
            Assert.All(tiers, tier => Assert.InRange(tier.Health, BossRandomizer.MinPercent, BossRandomizer.MaxPercent));
        }
    }
}
