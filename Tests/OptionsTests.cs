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
        AssertEquals(ConfigApplier.Prepare(unnamed, new BossOptions {Boss = BossType.Lych, Elite = true}).name,
            "Custom Elite Lych" + ConfigApplier.Marker, "Name of an unnamed Elite challenge");

        // Starting between two tiers skips the earlier ones, so it falls back to the default start round
        var between = BaseDcm();
        between.startRules.round = 50;
        AssertEquals(ConfigApplier.Prepare(between, CustomOptions).startRules.round, -1, "A start round after the first tier is reset");
        yield break;
    }
}

/// <summary>
/// Tier options that use the game's boss types. The calculations themselves are unit tested in UnitTests.
/// </summary>
public class TierMathTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        AssertEquals(new BossOptions {ExtraTierHealth = 225}.Sanitized(), new BossOptions {HealthPerTier = 10},
            "Old Extra Tier Health becomes Health per Tier");
        AssertEquals(new BossOptions {ExtraTierHealth = 225, HealthPerTier = 10}.Sanitized(), new BossOptions {HealthPerTier = 10},
            "A Health per Tier that is set wins over the old setting");

        AssertEquals(new BossOptions {Boss = BossType.Vortex, Elite = true}.BossId(7), "VortexElite7", "Elite boss id");
        AssertEquals(new BossOptions {Boss = BossType.Lych}.BossId(2), "Lych2", "Boss id");
        AssertEquals(Show(new BossOptions {Tiers = 6, SpawnRounds = "45"}.EffectiveSpawnRounds),
            "45, 65, 85, 105, 125, 145", "Effective spawn rounds");
        yield break;
    }

    private static string Show(int[] rounds) => TierMath.FormatRounds(rounds);
}

/// <summary>
/// Skull options. The calculations themselves are unit tested in UnitTests.
/// </summary>
public class SkullMathTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
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
                Assert(options.SpeedPerTier is >= 0 and <= BossRandomizer.MaxRandomSpeedPerTier && options.SpeedPerTier % TierMath.PerTierStep == 0,
                    $"Speed per tier {options.SpeedPerTier}");
                Assert(options.HealthPerTier is >= 0 and <= BossRandomizer.MaxRandomHealthPerTier && options.HealthPerTier % TierMath.PerTierStep == 0,
                    $"Health per tier {options.HealthPerTier}");

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
