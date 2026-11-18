using System;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Tests that switch <see cref="Experimental"/> on run alone: it is shared by every test that lists the bosses
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExperimentalCollection
{
    public const string Name = "Experimental";
}

/// <summary>
/// The mod's own boss Gildebloon (#25) next to the game's: its name, when it can be picked, and how it is stored
/// </summary>
[Collection(ExperimentalCollection.Name)]
public sealed class BossListTests : IDisposable
{
    private readonly bool flag = Experimental.Gildebloon;
    private readonly Func<bool> setting = Experimental.GildebloonSetting;

    public void Dispose()
    {
        Experimental.Gildebloon = flag;
        Experimental.GildebloonSetting = setting;
    }

    private static void TurnOn(bool code = true, bool setting = true)
    {
        Experimental.Gildebloon = code;
        Experimental.GildebloonSetting = () => setting;
    }

    [Fact]
    public void Released_versions_have_it_off()
    {
        Assert.False(flag, "Experimental.Gildebloon must stay off until the boss is finished");
    }

    [Fact]
    public void Gildebloon_has_a_name_of_its_own()
    {
        Assert.Equal("Gildebloon", BossList.Name(BossList.Gildebloon));
        Assert.Equal("Lych", BossList.Name(BossType.Lych));
        Assert.True(BossList.TryParse("gildebloon", out var boss));
        Assert.Equal(BossList.Gildebloon, boss);
        Assert.True(BossList.TryParse("Vortex", out boss));
        Assert.Equal(BossType.Vortex, boss);
        Assert.False(BossList.TryParse("100", out _), "A number is not a boss name");
        Assert.False(BossList.TryParse("Gilde", out _));
        Assert.False(Enum.IsDefined(BossList.Gildebloon), "Gildebloon must not be a value the game uses");
    }

    [Fact]
    public void Its_models_are_named_like_the_games()
    {
        Assert.Equal("Gildebloon3", BossOptions.BossIdOf(BossList.Gildebloon, false, 3));
        Assert.Equal("GildebloonElite5", BossOptions.BossIdOf(BossList.Gildebloon, true, 5));
        Assert.Equal("GildebloonElite5", new BossOptions {Boss = BossList.Gildebloon, Elite = true, Tiers = 8}.SourceId(8));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void It_can_only_be_picked_when_both_switches_are_on(bool code, bool setting)
    {
        TurnOn(code, setting);
        Assert.False(Experimental.GildebloonAvailable);
        Assert.Equal(BossList.Vanilla, BossList.Available);
        Assert.Equal(TierBossMath.VanillaBosses, TierBossMath.Bosses);
        Assert.DoesNotContain("Gildebloon", TierBossMath.Choices);
        Assert.False(BossList.IsAvailable(BossList.Gildebloon));
        Assert.True(BossList.IsKnown(BossList.Gildebloon), "A setup with it is still read");
    }

    [Fact]
    public void When_on_it_comes_after_the_games_bosses()
    {
        TurnOn();
        Assert.True(Experimental.GildebloonAvailable);
        Assert.Equal([..BossList.Vanilla, BossList.Gildebloon], BossList.Available);
        Assert.Equal([..TierBossMath.VanillaBosses, "Gildebloon"], TierBossMath.Bosses);
        Assert.Equal("Random", TierBossMath.Choices[^1]);
        Assert.Equal("Gildebloon", TierBossMath.Choices[^2]);

        // Random tiers can get it, and stepping through the bosses passes it
        var resolved = TierBossMath.Resolve("Random", "Lych", false, 1, count => count - 1);
        Assert.Equal("Gildebloon", resolved);
        Assert.Equal("Gildebloon", TierBossMath.Step(new TierBoss("Diamondback", false), "Lych", false, 1).Boss);
        Assert.Equal("Random", TierBossMath.Step(new TierBoss("Gildebloon", false), "Lych", false, 1).Boss);
    }

    [Fact]
    public void Random_tiers_never_get_it_when_it_is_off()
    {
        TurnOn(setting: false);
        Assert.Equal("Diamondback", TierBossMath.Resolve("Random", "Lych", false, 1, count => count - 1));
        Assert.Equal("Random", TierBossMath.Step(new TierBoss("Diamondback", false), "Lych", false, 1).Boss);
    }

    [Fact]
    public void A_setup_with_it_keeps_it_even_when_it_is_off()
    {
        TurnOn(false, false);
        var options = new BossOptions {Boss = BossList.Gildebloon, Elite = true}.Sanitized();
        Assert.Equal(BossList.Gildebloon, options.Boss);
        Assert.True(options.UsesGildebloon);

        var mixed = new BossOptions {Tiers = 3, TierBosses = "Lych,GildebloonElite@120/80"}.Sanitized();
        Assert.Equal("Lych,GildebloonElite@120/80", mixed.TierBosses);
        Assert.Equal((BossList.Gildebloon, true), mixed.BossOf(2));
        Assert.True(mixed.UsesGildebloon);
        Assert.False(new BossOptions {Tiers = 1, TierBosses = ",GildebloonElite"}.UsesGildebloon, "Tier 2 isn't played");
        Assert.False(new BossOptions().UsesGildebloon);
    }

    [Fact]
    public void Files_store_it_by_name()
    {
        var options = new BossOptions {Boss = BossList.Gildebloon, SkullMode = SkullMode.EvenlySpaced};
        var json = JsonConvert.SerializeObject(options, FormatJson.WithEnumNames);
        Assert.Contains("\"Boss\": \"Gildebloon\"", json);
        Assert.Contains("\"SkullMode\": \"EvenlySpaced\"", json);
        Assert.Equal(options, JsonConvert.DeserializeObject<BossOptions>(json, FormatJson.WithEnumNames));

        // The game's bosses are written and read as before
        var lych = JsonConvert.SerializeObject(new BossOptions {Boss = BossType.Lych}, FormatJson.WithEnumNames);
        Assert.Contains("\"Boss\": \"Lych\"", lych);
        Assert.Equal(BossType.Lych, JsonConvert.DeserializeObject<BossOptions>(lych, FormatJson.WithEnumNames)!.Boss);
        Assert.Equal(BossType.Vortex,
            JsonConvert.DeserializeObject<BossOptions>("{\"Boss\": 2}", FormatJson.WithEnumNames)!.Boss);
    }
}
