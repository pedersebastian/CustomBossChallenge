global using BTD_Mod_Helper.Extensions;
using System;
using MelonLoader;
using BTD_Mod_Helper;
using CustomBossChallenge;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
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

    /// <summary>
    /// All patches are applied by now; a missing one is logged right away, not only when the Boss Creator is clicked,
    /// and so are other mods' patches on the guards
    /// </summary>
    public override void OnMainMenu()
    {
        _ = Patches.PatchCheck.AllInPlace;
        Patches.ConflictReport.LogOnce();
    }

    public override void OnUpdate()
    {
        Menus.MainMenuButton.UpdatePosition();
        Menus.BossCreatorKeys.Update();
    }

    public override void OnNewGameModel(GameModel result)
    {
        if (!CustomBossState.IsActive) return;

        CustomBossState.OnMatchStart();
        var options = CustomBossState.ActiveOptions!;
        var tiers = ExtraTiers.Add(result, options);

        // If a game update removed boss models, play the tiers that exist instead of failing when one is missing
        var available = ExtraTiers.AvailableTiers(result, options);
        if (available == 0)
        {
            // Not even tier 1 exists, so there is no boss to give custom tiers to; Play refuses this, a restart doesn't
            ModHelper.Warning<CustomBossChallenge>($"No model for {options.BossId(1)}, the boss options can't be applied");
            return;
        }

        if (available < TierMath.ClampTiers(options.Tiers))
        {
            ModHelper.Warning<CustomBossChallenge>(
                $"No model for {options.BossId(available + 1)}, playing {available} tiers instead of {options.Tiers}");
            options = CustomBossState.LimitTiers(available);
        }

        // The game multiplies the tiers by Boss Health after this, so their health must leave room for it. Without the
        // challenge, assume the highest Boss Health.
        var bossHealth = InGameData.CurrentGame?.dcModel?.bloonModifiers?.healthMultipliers?.boss ?? ChallengeLimits.MaxBossHealth;
        var scaled = TierScaling.Apply(result, options, bossHealth);
        var skulls = SkullModifier.Apply(result, options.SkullPercents);
        result.bossSpawnRounds = new Il2CppStructArray<int>(options.GameSpawnRounds);
        var bossData = InGameData.CurrentGame?.bossData;
        if (bossData != null) bossData.spawnRounds = new Il2CppStructArray<int>(options.GameSpawnRounds);

        ModHelper.Msg<CustomBossChallenge>(
            $"Custom {options.BossId(options.Tiers)}: added {tiers} extra tiers, scaled {scaled} tiers " +
            $"(speed {options.SpeedPerTier:+0;-0} %, health {options.HealthPerTier:+0;-0} % per tier), changed skulls of {skulls} boss models, " +
            $"spawn rounds {TierMath.FormatRounds(options.EffectiveSpawnRounds)}");
    }
}
