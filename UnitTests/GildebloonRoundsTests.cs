using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Gildebloon's own rounds, Gold Rush (#25): 140 rounds the mod brings, each worth about what the game's default round
/// is worth, with a heist before each default boss round. Checked against the game's rounds and bloon RBE in
/// tools/data (written by the in-game RoundDataTest).
/// </summary>
public class GildebloonRoundsTests
{
    private static readonly string Root = FindRoot();
    private static readonly JObject Rbe = JObject.Parse(File.ReadAllText(Path.Combine(Root, "tools", "data", "bloonrbe.json")));
    private static readonly IReadOnlyList<ChimpsRoundSet.Round> Defaults =
        ChimpsRoundSet.Parse(File.ReadAllText(Path.Combine(Root, "tools", "data", "defaultroundset.json")));
    private static readonly IReadOnlyList<ChimpsRoundSet.Round> Rounds = ChimpsRoundSet.Rounds(BossOptions.GildebloonRoundSet);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "tools", "data", "bloonrbe.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("tools/data not found above " + AppContext.BaseDirectory);
    }

    private static double Worth(ChimpsRoundSet.Round round) => round.Groups.Sum(group => Rbe[group.Bloon]!.Value<double>() * group.Count);

    [Fact]
    public void HasEveryRoundOnce()
    {
        Assert.Equal(Enumerable.Range(1, 140), Rounds.Select(round => round.Number));
        Assert.All(Rounds, round => Assert.False(round.AddToRound));
        Assert.All(Rounds, round => Assert.NotEmpty(round.Groups));
    }

    [Fact]
    public void HasOnlyBloonsTheGameHas()
    {
        var unknown = Rounds.SelectMany(round => round.Groups).Select(group => group.Bloon).Where(bloon => Rbe[bloon] == null).Distinct();
        Assert.Empty(unknown);
        // The game's own round 86 starts a group a moment early (-0.15 s); none starts earlier than that
        Assert.All(Rounds.SelectMany(round => round.Groups), group => Assert.True(group.Count > 0 && group.Duration >= 0 && group.Start >= -0.15f));
    }

    [Fact]
    public void IsAsHardAsTheDefaultRounds()
    {
        foreach (var (round, normal) in Rounds.Zip(Defaults))
        {
            var share = Worth(round) / Worth(normal);
            Assert.True(share is >= 0.9 and <= 1.1, $"Round {round.Number} is worth {Worth(round)}, the default {Worth(normal)}");
        }
    }

    [Fact]
    public void StartsAndBossRoundsLikeTheDefault()
    {
        foreach (var number in Enumerable.Range(1, 10).Concat(new[] {40, 60, 80, 100, 120}))
        {
            Assert.Equal(Defaults[number - 1].Groups, Rounds[number - 1].Groups);
        }
    }

    [Fact]
    public void HasAHeistBeforeEachBossRound()
    {
        foreach (var number in new[] {39, 59, 79, 99, 119})
        {
            var heist = Rounds[number - 1];
            var end = heist.Groups.Max(group => group.End);
            Assert.True(end < Defaults[number - 1].Groups.Max(group => group.End), $"Round {number} is a short rush");
            Assert.NotEqual(Defaults[number - 1].Groups, heist.Groups);
        }
    }

    [Fact]
    public void HasMoreMetal()
    {
        bool Metal(string bloon) => bloon.StartsWith("Lead") || bloon.StartsWith("Zebra") || bloon.StartsWith("Purple");
        double Share(IEnumerable<ChimpsRoundSet.Round> rounds) =>
            rounds.SelectMany(round => round.Groups).Where(group => Metal(group.Bloon)).Sum(group => group.Count) /
            (double) rounds.SelectMany(round => round.Groups).Where(group => !group.Bloon.StartsWith("Moab")).Sum(group => group.Count);
        Assert.True(Share(Rounds.Skip(10).Take(50)) > Share(Defaults.Skip(10).Take(50)) * 1.5);
    }

    [Fact]
    public void IsGildebloonsOwnSet()
    {
        Assert.Equal("gildebloon", BossOptions.BossEventRoundSet(BossList.Gildebloon));
        Assert.True(BossOptions.IsModRoundSet("gildebloon"));
        Assert.False(BossOptions.IsChimpsEventRoundSet("gildebloon"));
        Assert.Equal("chimpsboss", BossOptions.EventRoundSet(BossList.Gildebloon, BossOptions.ChimpsMode));
        Assert.Equal("gildebloon", BossOptions.EventRoundSet(BossList.Gildebloon, BossOptions.ChimpsMode, ChimpsRounds.Own));
        Assert.Equal("bloonarius", BossOptions.BossEventRoundSet(Il2CppAssets.Scripts.Data.Boss.BossType.Bloonarius));
    }
}
