using System.Collections;
using BTD_Mod_Helper.Api.Testing;
using CustomBossChallenge.Config;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The default config must not change any value of the challenge, but still marks it as custom
/// </summary>
public class ConfigApplierDefaultsTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var source = BaseDcm();
        var dcm = ConfigApplier.ApplyToClone(source, new BossChallengeConfig());
        var mods = dcm.bloonModifiers;

        Assert(Approximately(mods.bossSpeedMultiplier, 1), $"Boss speed was {mods.bossSpeedMultiplier}");
        Assert(Approximately(mods.healthMultipliers.boss, 1), $"Boss health was {mods.healthMultipliers.boss}");
        Assert(Approximately(mods.speedMultiplier, 1), $"Bloon speed was {mods.speedMultiplier}");
        Assert(Approximately(mods.moabSpeedMultiplier, 1), $"MOAB speed was {mods.moabSpeedMultiplier}");
        Assert(Approximately(mods.healthMultipliers.bloons, 1), $"Ceramic health was {mods.healthMultipliers.bloons}");
        Assert(Approximately(mods.healthMultipliers.moabs, 1), $"MOAB health was {mods.healthMultipliers.moabs}");
        Assert(Approximately(mods.regrowRateMultiplier, 1), $"Regrow rate was {mods.regrowRateMultiplier}");
        AssertEquals(dcm.startRules.cash, source.startRules.cash, "Start cash changed");
        AssertEquals(dcm.startRules.lives, source.startRules.lives, "Start lives changed");
        AssertEquals(dcm.startRules.round, source.startRules.round, "Start round changed");
        AssertEquals(dcm.startRules.endRound, source.startRules.endRound, "End round changed");
        Assert(!dcm.disableSelling && !dcm.disableMK && !dcm.disablePowers && !dcm.noContinues, "A rule was enabled");
        Assert(!mods.allCamo && !mods.allRegen && !mods.allFortified, "A bloon flag was enabled");
        Assert(dcm.disableSaves, "Saves must be disabled for custom challenges");
        Assert(ConfigApplier.IsMarked(dcm), $"Challenge was not marked as custom: '{dcm.name}'");
        yield break;
    }
}

/// <summary>
/// Every customized value ends up in the right challenge field
/// </summary>
public class ConfigApplierValuesTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var dcm = ConfigApplier.ApplyToClone(BaseDcm(), CustomConfig with
        {
            StartRound = 30, EndRound = 20, MaxLives = 10, NoMonkeyKnowledge = true, NoPowers = true,
            NoContinues = true, AllRegen = true, AllFortified = true
        });
        var mods = dcm.bloonModifiers;

        Assert(Approximately(mods.bossSpeedMultiplier, 2), $"Boss speed was {mods.bossSpeedMultiplier}");
        Assert(Approximately(mods.healthMultipliers.boss, 0.5f), $"Boss health was {mods.healthMultipliers.boss}");
        Assert(Approximately(mods.speedMultiplier, 1.5f), $"Bloon speed was {mods.speedMultiplier}");
        Assert(Approximately(mods.moabSpeedMultiplier, 1.25f), $"MOAB speed was {mods.moabSpeedMultiplier}");
        Assert(Approximately(mods.healthMultipliers.bloons, 3), $"Ceramic health was {mods.healthMultipliers.bloons}");
        Assert(Approximately(mods.healthMultipliers.moabs, 2.5f), $"MOAB health was {mods.healthMultipliers.moabs}");
        Assert(Approximately(mods.regrowRateMultiplier, 0.75f), $"Regrow rate was {mods.regrowRateMultiplier}");
        AssertEquals(dcm.startRules.cash, 5000, "Start cash");
        AssertEquals(dcm.startRules.lives, 50, "Start lives");
        AssertEquals(dcm.startRules.maxLives, 50, "Max lives must be raised to at least the start lives");
        AssertEquals(dcm.startRules.round, 30, "Start round");
        AssertEquals(dcm.startRules.endRound, 31, "End round must be after the start round");
        Assert(dcm.disableSelling && dcm.disableMK && dcm.disablePowers && dcm.noContinues, "A rule was not enabled");
        Assert(mods.allCamo && mods.allRegen && mods.allFortified, "A bloon flag was not enabled");
        yield break;
    }
}

/// <summary>
/// Applying a config never modifies the original challenge model, and marking is idempotent
/// </summary>
public class ConfigApplierDoesNotMutateSourceTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var source = BaseDcm();
        var originalName = source.name;
        var originalCash = source.startRules.cash;

        var custom = ConfigApplier.ApplyToClone(source, CustomConfig);
        var twice = ConfigApplier.ApplyToClone(custom, CustomConfig);

        Assert(Approximately(source.bloonModifiers.bossSpeedMultiplier, 1), "Source boss speed changed");
        AssertEquals(source.name, originalName, "Source name changed");
        AssertEquals(source.startRules.cash, originalCash, "Source cash changed");
        Assert(!source.disableSaves, "Source saves were disabled");
        Assert(!ConfigApplier.IsMarked(source), "Source was marked");
        AssertEquals(twice.name, custom.name, "Marking twice must not add the marker twice");
        yield break;
    }
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

        AssertEquals(new BossChallengeConfig().SkullPercents, null, "Vanilla skull mode must keep the skulls");
        Assert(SameValues(new BossChallengeConfig {SkullMode = SkullMode.Custom, CustomSkulls = "60, 30"}.SkullPercents,
            new[] {0.6f, 0.3f}), "Custom skull mode");
        yield break;
    }
}
