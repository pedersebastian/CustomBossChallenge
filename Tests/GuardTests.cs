using System;
using System.Collections;
using System.Linq;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Patches;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.Player;
using Il2CppAssets.Scripts.Unity.UI_New.Main;
using UnityEngine.UI;
using Object = UnityEngine.Object;
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

/// <summary>
/// Every patch of the mod is in place; one a game update left out keeps the Boss Creator closed with a message instead
/// </summary>
public class PatchCheckTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        PatchCheck.Missing = null;
        AssertEquals(string.Join(", ", PatchCheck.Missing!), "", "Missing patches");
        var record = AccessTools.Method(typeof(Btd6Player), nameof(Btd6Player.SetBossChallengeRecord));
        Assert(PatchCheck.IsPatchedBy(record, typeof(Btd6Player_SetBossChallengeRecord)), "The record guard must count as patched");
        Assert(!PatchCheck.IsPatchedBy(record, typeof(PatchCheckTest)), "Another class must not count as the patch");
        Assert(PatchCheck.Checked > 20, $"Only {PatchCheck.Checked} patches were checked");

        // What a game update looks like: a renamed method is gone, and a patch that wasn't applied is reported too
        AssertEquals(string.Join(", ", PatchCheck.Problems(typeof(PatchCheckTest),
                new HarmonyMethod {declaringType = typeof(Btd6Player), methodName = "SetBossChallengeRecordRenamed"})),
            "Btd6Player.SetBossChallengeRecordRenamed (gone)", "A gone method");
        AssertEquals(string.Join(", ", PatchCheck.Problems(typeof(PatchCheckTest),
                new HarmonyMethod {declaringType = typeof(Btd6Player), methodName = nameof(Btd6Player.SetBossChallengeRecord)})),
            "Btd6Player.SetBossChallengeRecord (not patched)", "A method that isn't patched");

        var mainMenu = AssertNotNull(Object.FindObjectOfType<MainMenu>(), "No main menu")!;
        var button = mainMenu.transform.Find(MainMenuButton.Name).GetComponentInChildren<Button>(true);
        try
        {
            PatchCheck.Missing = new[] {"Btd6Player.SetBossChallengeRecord (gone)"};
            button.onClick.Invoke();
            Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
            yield return WaitForPopup(p => popup = p);
            Assert(popup!.body.text.Contains("Btd6Player.SetBossChallengeRecord"), $"Popup: {popup.body.text}");
            yield return ClosePopup(popup, true);
            Assert(!BossMode.Active, "The Boss Creator must stay closed while a patch is missing");
            Assert(MenuManager.instance.GetCurrentMenu()?.Is<MainMenu>() == true, "The main menu must stay open");
        }
        finally
        {
            PatchCheck.Missing = null;
        }
    }
}
