global using BTD_Mod_Helper.Extensions;
using MelonLoader;
using BTD_Mod_Helper;
using CustomBossChallenge;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models;

[assembly: MelonInfo(typeof(CustomBossChallenge.CustomBossChallenge), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6-Epic")]

namespace CustomBossChallenge;

public class CustomBossChallenge : BloonsTD6Mod
{
    public override void OnApplicationStart()
    {
        ModHelper.Msg<CustomBossChallenge>("CustomBossChallenge loaded!");
    }

    public override void OnNewGameModel(GameModel result)
    {
        if (!CustomBossState.IsActive) return;

        var changed = SkullModifier.Apply(result, CustomBossState.ActiveOptions!.SkullPercents);
        if (changed > 0)
        {
            ModHelper.Msg<CustomBossChallenge>($"Applied custom skulls to {changed} boss models");
        }
    }
}
