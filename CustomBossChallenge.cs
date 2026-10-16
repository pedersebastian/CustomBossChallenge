global using BTD_Mod_Helper.Extensions;
using MelonLoader;
using BTD_Mod_Helper;
using CustomBossChallenge;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

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

    public override void OnUpdate()
    {
        Menus.MainMenuButton.UpdatePosition();
    }

    public override void OnNewGameModel(GameModel result)
    {
        if (!CustomBossState.IsActive) return;

        CustomBossState.OnMatchStart();
        var options = CustomBossState.ActiveOptions!;
        var tiers = ExtraTiers.Add(result, options);
        var skulls = SkullModifier.Apply(result, options.SkullPercents);
        result.bossSpawnRounds = new Il2CppStructArray<int>(options.GameSpawnRounds);

        ModHelper.Msg<CustomBossChallenge>(
            $"Custom {options.BossId(options.Tiers)}: added {tiers} extra tiers, changed skulls of {skulls} boss models, " +
            $"spawn rounds {TierMath.FormatRounds(options.EffectiveSpawnRounds)}");
    }
}
