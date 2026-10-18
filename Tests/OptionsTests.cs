using System.Collections;
using System.Linq;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Preparing the editor's model for a custom game
/// </summary>
public class ConfigApplierTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var source = CustomEditorDcm();
        source.bloonModifiers.healthMultipliers.boss = 0;
        var originalName = source.name;

        var dcm = ConfigApplier.Prepare(source, CustomOptions);
        Assert(ConfigApplier.IsMarked(dcm), $"Challenge was not marked as custom: '{dcm.name}'");
        Assert(dcm.disableSaves, "Saves must be disabled for custom challenges");
        AssertEquals(dcm.chalType, ChallengeType.BossChallenge, "Challenge type");
        Assert(Approximately(dcm.bloonModifiers.bossSpeedMultiplier, 2), "Boss speed must come from the editor");
        Assert(Approximately(dcm.bloonModifiers.healthMultipliers.boss, ConfigApplier.MinMultiplier),
            $"Boss health must be floored, was {dcm.bloonModifiers.healthMultipliers.boss}");
        AssertEquals(dcm.startRules.cash, 5000, "Start cash must come from the editor");

        AssertEquals(source.name, originalName, "Editor model name changed");
        Assert(!source.disableSaves, "Editor model saves were disabled");
        Assert(Approximately(source.bloonModifiers.healthMultipliers.boss, 0), "Editor model health was changed");

        var twice = ConfigApplier.Prepare(dcm, CustomOptions);
        AssertEquals(twice.name, dcm.name, "Marking twice must not add the marker twice");

        AssertEquals(dcm.startRules.endRound, 140, "The default end round must leave room for the last tier");
        var late = BaseDcm();
        late.startRules.endRound = 50;
        late.startRules.round = 130;
        var lateDcm = ConfigApplier.Prepare(late, new BossOptions {Tiers = 3});
        AssertEquals(lateDcm.startRules.endRound, 100, "An end round before the last tier is moved after it");
        AssertEquals(lateDcm.startRules.round, -1, "A start round after the last tier is reset");
        var kept = BaseDcm();
        kept.startRules.endRound = 150;
        AssertEquals(ConfigApplier.Prepare(kept, CustomOptions).startRules.endRound, 150, "A later end round is kept");

        var unnamed = BaseDcm();
        unnamed.name = "";
        AssertEquals(ConfigApplier.Prepare(unnamed, new BossOptions {Boss = BossType.Vortex}).name,
            "Custom Vortex" + ConfigApplier.Marker, "Name of an unnamed challenge");
        yield break;
    }
}

/// <summary>
/// Tier and spawn round calculations
/// </summary>
public class TierMathTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        AssertEquals(Show(TierMath.DefaultSpawnRounds(5)), "40, 60, 80, 100, 120", "Vanilla spawn rounds");
        AssertEquals(Show(TierMath.DefaultSpawnRounds(7)), "40, 60, 80, 100, 120, 140, 160", "Extra tier rounds");
        AssertEquals(Show(TierMath.DefaultSpawnRounds(1)), "40", "One tier");
        AssertEquals(TierMath.DefaultSpawnRounds(99).Length, TierMath.MaxTiers, "Tiers must be capped");
        AssertEquals(TierMath.DefaultSpawnRounds(0).Length, TierMath.MinTiers, "At least one tier");

        AssertEquals(Show(TierMath.SpawnRounds(5, null)), "40, 60, 80, 100, 120", "No custom rounds");
        AssertEquals(Show(TierMath.SpawnRounds(3, "30, 50,70")), "30, 50, 70", "Custom rounds");
        AssertEquals(Show(TierMath.SpawnRounds(4, "30;45")), "30, 45, 65, 85", "Too few rounds are continued");
        AssertEquals(Show(TierMath.SpawnRounds(2, "30, 50, 70")), "30, 50", "Too many rounds are cut");
        AssertEquals(Show(TierMath.SpawnRounds(3, "50, 50, 20")), "50, 51, 52", "Rounds must ascend");
        AssertEquals(Show(TierMath.SpawnRounds(2, "x, -5, 0, 60")), "60, 80", "Junk is ignored");
        AssertEquals(Show(TierMath.SpawnRounds(1, "5000")), $"{TierMath.MaxRound}", "Rounds are capped");
        AssertEquals(Show(TierMath.SpawnRounds(2, "1000")), "999, 1000", "Capped tiers must not share a round");
        AssertEquals(Show(TierMath.SpawnRounds(3, "995, 2000")), "995, 999, 1000", "Capped later tiers");
        var capped = TierMath.SpawnRounds(TierMath.MaxTiers, "990");
        Assert(capped.Zip(capped.Skip(1)).All(pair => pair.First < pair.Second) && capped[^1] == TierMath.MaxRound,
            $"Ten capped tiers must ascend: {Show(capped)}");

        Assert(TierMath.HealthFactor(5, 150) == 1, "Tier 5 keeps its health");
        Assert(TierMath.HealthFactor(3, 150) == 1, "Lower tiers keep their health");
        Assert(System.Math.Abs(TierMath.HealthFactor(7, 150) - 2.25) < 1e-9, "Tier 7 at 150% per tier");
        AssertEquals(TierMath.ScaleHealth(3000000, 2.25), 6750000, "Scaled health");
        AssertEquals(TierMath.ScaleHealth(int.MaxValue, 1000), TierMath.MaxModelHealth, "Health must not overflow");

        AssertEquals(TierMath.BossId(BossType.Vortex, true, 7), "VortexElite7", "Elite boss id");
        AssertEquals(TierMath.TierOf("VortexElite7"), 7, "Tier of an elite boss id");
        AssertEquals(TierMath.TierOf("Bloonarius10"), 10, "Tier of a two digit tier");
        AssertEquals(TierMath.TierOf("Moab"), 0, "Tier of a bloon without one");
        AssertEquals(Show(TierMath.ToGameRounds(new[] {1, 40, 60})), "0, 39, 59", "The game counts rounds from 0");
        AssertEquals(new BossOptions {Boss = BossType.Lych}.BossId(2), "Lych2", "Boss id");
        AssertEquals(Show(new BossOptions {Tiers = 6, SpawnRounds = "45"}.EffectiveSpawnRounds),
            "45, 65, 85, 105, 125, 145", "Effective spawn rounds");
        yield break;
    }

    private static string Show(int[] rounds) => TierMath.FormatRounds(rounds);
}

/// <summary>
/// Skull threshold calculations
/// </summary>
public class SkullMathTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        Assert(SameValues(SkullMath.EvenlySpaced(3), new[] {0.75f, 0.5f, 0.25f}),
            $"EvenlySpaced(3) = {Show(SkullMath.EvenlySpaced(3))}");
        Assert(SameValues(SkullMath.EvenlySpaced(4), new[] {0.8f, 0.6f, 0.4f, 0.2f}),
            $"EvenlySpaced(4) = {Show(SkullMath.EvenlySpaced(4))}");
        AssertEquals(SkullMath.EvenlySpaced(0).Length, 0, "EvenlySpaced(0) must be empty");
        AssertEquals(SkullMath.EvenlySpaced(99).Length, SkullMath.MaxSkulls, "EvenlySpaced must be capped");

        Assert(SameValues(SkullMath.ParseCustom("75,50,25"), new[] {0.75f, 0.5f, 0.25f}), "Simple list");
        Assert(SameValues(SkullMath.ParseCustom(" 25 ; 90%, 50, 50 "), new[] {0.9f, 0.5f, 0.25f}),
            $"Messy list = {Show(SkullMath.ParseCustom(" 25 ; 90%, 50, 50 "))}");
        Assert(SameValues(SkullMath.ParseCustom("abc, 0, 100, 150, 33.5"), new[] {0.335f}),
            $"Invalid values must be ignored = {Show(SkullMath.ParseCustom("abc, 0, 100, 150, 33.5"))}");
        AssertEquals(SkullMath.ParseCustom("nothing valid"), null, "Junk must give null");
        AssertEquals(SkullMath.ParseCustom(""), null, "Empty must give null");

        AssertEquals(SkullMath.NormalizeCustom("25,25,50", "x"), "50, 25", "Duplicates are shown merged");
        AssertEquals(SkullMath.NormalizeCustom(" 33.5% ; 90 abc", "x"), "90, 33.5", "Decimals and junk");
        AssertEquals(SkullMath.NormalizeCustom("33,5", "x"), "33, 5", "A decimal comma separates two skulls");
        AssertEquals(SkullMath.NormalizeCustom("junk, 0, 100", "75, 50, 25"), "75, 50, 25", "Nothing valid keeps the fallback");
        AssertEquals(SkullMath.NormalizeCustom(new BossOptions().CustomSkulls, "x"), new BossOptions().CustomSkulls,
            "The default skulls are already normalized");

        AssertEquals(new BossOptions().SkullPercents, null, "Vanilla skull mode must keep the skulls");
        Assert(SameValues(new BossOptions {SkullMode = SkullMode.Custom, CustomSkulls = "60, 30"}.SkullPercents,
            new[] {0.6f, 0.3f}), "Custom skull mode");
        yield break;
    }
}

/// <summary>
/// The Max Paragons slider: its last position means no limit
/// </summary>
public class ParagonMathTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        AssertEquals(DailyChallengeModel.CreateDefaultEditorModel().maxParagons, ParagonMath.NoLimit,
            "The game's own default must be no limit");
        Assert(ParagonMath.Available >= 14, $"Paragons in the game: {ParagonMath.Available}");

        AssertEquals(ParagonMath.ToSlider(ParagonMath.NoLimit, 14), 14, "No limit is the last position");
        AssertEquals(ParagonMath.ToSlider(0, 14), 0, "No paragons");
        AssertEquals(ParagonMath.ToSlider(3, 14), 3, "A limit");
        AssertEquals(ParagonMath.ToSlider(14, 14), 14, "A limit of every paragon is no limit");
        AssertEquals(ParagonMath.ToSlider(500, 14), 14, "A limit above every paragon is no limit");
        AssertEquals(ParagonMath.ToSlider(-7, 14), 14, "Negative values are no limit");

        AssertEquals(ParagonMath.FromSlider(14, 14), ParagonMath.NoLimit, "The last position is no limit");
        AssertEquals(ParagonMath.FromSlider(0, 14), 0, "No paragons");
        AssertEquals(ParagonMath.FromSlider(13, 14), 13, "The highest limit");
        AssertEquals(ParagonMath.FromSlider(-1, 14), 0, "Below the slider");
        yield break;
    }
}

/// <summary>
/// The Random button's boss options are always playable, and spread like the game's own randomizer
/// </summary>
public class BossRandomizerTest : CustomBossTest
{
    public const int Runs = 1000;

    protected override IEnumerator Run()
    {
        var random = BossRandomizer.Random;
        BossRandomizer.Random = new System.Random(1234);
        var modes = ChallengeLimits.Modes;
        var unsupported = Menus.BossEditorSection.UnsupportedModes;
        var bosses = new System.Collections.Generic.HashSet<BossType>();
        int defaultSpeed = 0, defaultHealth = 0, elite = 0, defaultTiers = 0, limited = 0;
        try
        {
            for (var i = 0; i < Runs; i++)
            {
                // Like the game's randomizer: any mode, and a start round up to 50
                var dcm = BaseDcm();
                dcm.mode = modes[i % modes.Length];
                dcm.startRules.round = i % 50 + 1;
                var options = BossRandomizer.Randomize(dcm, unsupported);

                AssertEquals(options, options.Sanitized(), "Random options must already be in range");
                Assert(!unsupported.Contains(dcm.mode), $"Random game mode {dcm.mode} can't be played with a boss");
                Assert(dcm.startRules.round < options.EffectiveSpawnRounds[0],
                    $"Start round {dcm.startRules.round} skips the first tier on {options.EffectiveSpawnRounds[0]}");
                AssertEquals(Menus.BossEditorSection.PlayProblem(dcm, options), null, "A random setup must be playable");
                var speed = dcm.bloonModifiers.bossSpeedMultiplier;
                var health = dcm.bloonModifiers.healthMultipliers.boss;
                Assert(speed >= 0.5f && speed <= 2f, $"Boss speed {speed}");
                Assert(health >= 0.5f && health <= 2f, $"Boss health {health}");
                Assert(dcm.maxParagons == ParagonMath.NoLimit || dcm.maxParagons is >= 0 and <= BossRandomizer.MaxRandomParagons,
                    $"Max paragons {dcm.maxParagons}");

                bosses.Add(options.Boss);
                if (Approximately(speed, 1)) defaultSpeed++;
                if (Approximately(health, 1)) defaultHealth++;
                if (options.Elite) elite++;
                if (options.Tiers == TierMath.VanillaTiers) defaultTiers++;
                if (dcm.maxParagons != ParagonMath.NoLimit) limited++;
            }
        }
        finally
        {
            BossRandomizer.Random = random;
        }

        Log($"default speed {defaultSpeed}, default health {defaultHealth}, elite {elite}, 5 tiers {defaultTiers}, " +
            $"paragon limit {limited} of {Runs}");
        AssertEquals(bosses.Count, System.Enum.GetValues<BossType>().Length, "Every boss can come up");
        AssertBetween(defaultSpeed, 0.55, 0.7, "Boss speed left at 100 %");
        AssertBetween(defaultHealth, 0.55, 0.7, "Boss health left at 100 %");
        AssertBetween(elite, 0.25, 0.35, "Elite");
        AssertBetween(defaultTiers, 0.5, 0.6, "5 tiers");
        AssertBetween(limited, 0.15, 0.25, "Paragon limit");
        yield break;
    }

    private void AssertBetween(int count, double min, double max, string what) =>
        Assert(count >= min * Runs && count <= max * Runs, $"{what}: {count} of {Runs}");
}
