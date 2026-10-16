using System.Collections;
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
