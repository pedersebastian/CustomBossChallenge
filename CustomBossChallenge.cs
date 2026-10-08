global using BTD_Mod_Helper.Extensions;
using MelonLoader;
using BTD_Mod_Helper;
using CustomBossChallenge;

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
}