using System.Collections;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Patches;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Nothing the mod only does for a custom game goes on in the menus after leaving it (#109): the guards on boss
/// records, boss progress and achievements, and the upkeep of a mixed game's map effects and boss panel. The game
/// forgets the challenge when the match is destroyed, which these all rely on, so this checks it still does.
/// </summary>
public class MenuGuardTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var options = new BossOptions {Tiers = 2, TierBosses = "Lych,Vortex"};
        yield return LoadIntoGame(BossGameLauncher.BuildInGameData(CustomEditorDcm(), options));

        // In the match, everything is guarded
        Assert(CustomBossState.IsActive, "The match is a custom game");
        Assert(!Btd6Player_BossProgress.Prefix(), "Boss progress is blocked in the match");
        Assert(!Btd6Player_SetBossChallengeRecord.Allowed, "Boss records are blocked in the match");
        Assert(AchievementPatches.Blocked, "Achievements are blocked in the match");
        AssertNotNull(MixedBosses.Options, "The boss per tier is kept up in the match");

        yield return ToMainMenu();
        Assert(InGame.instance == null, "Back in the menus");

        // The game forgot the challenge, so the custom game is over for every guard
        AssertEquals(InGameData.CurrentGame, null, "The game forgets the match when it is left");
        Assert(!CustomBossState.IsActive, "No custom game in the menus");
        Assert(Btd6Player_BossProgress.Prefix(), "Boss progress made in the menus counts, e.g. badges of a real event");
        Assert(Btd6Player_SetBossChallengeRecord.Allowed, "Boss records made in the menus count");
        Assert(!AchievementPatches.Blocked, "Achievements made in the menus count");
        AssertEquals(MixedBosses.Options, null, "No map effects or boss panel are kept up in the menus");
    }
}
