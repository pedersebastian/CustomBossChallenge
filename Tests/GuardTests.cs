using System;
using System.Collections;
using System.Linq;
using CustomBossChallenge.Patches;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.Player;
using BTD_Mod_Helper.Api.Testing;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// A custom challenge is always set up unranked, and normal or co-op challenges never become custom
/// </summary>
public class RankedGuardTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        try
        {
            var custom = CustomBossState.Prepare(BaseDcm(), CustomOptions);
            var data = SetupBossChallenge(custom, BossType.Vortex, ranked: true);
            Assert(!data.bossData.bossRankedMode, "Custom challenge was set up as ranked");
            AssertEquals(CustomBossState.ActiveOptions, CustomOptions, "Custom challenge did not become active");

            data = SetupBossChallenge(BaseDcm(), BossType.Vortex, ranked: true);
            Assert(data.bossData.bossRankedMode, "Vanilla ranked challenge must stay ranked");
            AssertEquals(CustomBossState.ActiveOptions, null, "Vanilla challenge became custom");

            var coop = CustomBossState.Prepare(BaseDcm(), CustomOptions);
            Assert(!CustomBossState.OnSetupBossChallenge(coop, coop: true), "Co-op challenge became custom");
            AssertEquals(CustomBossState.ActiveOptions, null, "Co-op challenge has an active config");
        }
        finally
        {
            CustomBossState.Clear();
        }
        yield break;
    }
}

/// <summary>
/// Every method that would give a custom game records, boss progress, badges or event completion still exists and is
/// patched. A game update renaming one would otherwise let custom games count silently.
/// </summary>
public class ProgressGuardTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        AssertPatched(nameof(Btd6Player.SetBossChallengeRecord), typeof(Btd6Player_SetBossChallengeRecord));
        foreach (var name in Btd6Player_BossProgress.MethodNames)
        {
            AssertPatched(name, typeof(Btd6Player_BossProgress));
        }

        AssertEquals(Btd6Player_BossProgress.TargetMethods().Count(), Btd6Player_BossProgress.MethodNames.Length,
            "Guarded progress methods");
        yield break;
    }

    private void AssertPatched(string name, Type patch)
    {
        var method = AssertNotNull(AccessTools.Method(typeof(Btd6Player), name), $"Btd6Player.{name} is gone");
        var prefixes = HarmonyLib.Harmony.GetPatchInfo(method)?.Prefixes;
        Assert(prefixes != null && prefixes.Any(prefix => prefix.PatchMethod.DeclaringType == patch),
            $"Btd6Player.{name} is not guarded");
    }
}
