global using BTD_Mod_Helper.Extensions;
using System;
using MelonLoader;
using BTD_Mod_Helper;
using CustomBossChallenge;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

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
        Visuals.TierLookApplier.Prepare();
    }

    public override void OnUpdate()
    {
        // Each part on its own, so one that throws doesn't stop the others for the rest of the match (#112)
        Guarded("main menu button", Menus.MainMenuButton.UpdatePosition);
        Guarded("Boss Creator keys", Menus.BossCreatorKeys.Update);
        Guarded("tier look", Visuals.TierLookApplier.Update);
        Guarded("boss panel", () =>
        {
            FailNextUpdate?.Invoke();
            Visuals.BossHud.Update();
        });
        if (Time.realtimeSinceStartup >= nextPresentationCheck)
        {
            nextPresentationCheck = Time.realtimeSinceStartup + Visuals.BossPresentation.CheckInterval;
            Guarded("map effects", Patches.MixedBosses.KeepPresentation);
        }
    }

    private float nextPresentationCheck;

    /// <summary>
    /// The parts of <see cref="OnUpdate"/> that threw this session, so each is logged once and not every frame
    /// </summary>
    internal static readonly System.Collections.Generic.HashSet<string> FailedUpdates = new();

    /// <summary>
    /// Tests only: runs before the boss panel's update, to make it throw
    /// </summary>
    internal static Action? FailNextUpdate { get; set; }

    private static void Guarded(string part, Action update)
    {
        try
        {
            update();
        }
        catch (Exception e)
        {
            if (FailedUpdates.Add(part)) ModHelper.Warning<CustomBossChallenge>($"The {part} update failed: {e}");
        }
    }

    public override void OnNewGameModel(GameModel result)
    {
        if (!CustomBossState.IsActive) return;

        CustomBossState.OnMatchStart();
        var options = CustomBossState.ActiveOptions!;
        var tiers = ExtraTiers.Add(result, options);
        // All Camo, All Regrow and All Fortified also for the bloons the bosses spawn, extra tiers included (#90)
        var spawned = SpawnedBloonModifiers.Apply(result, InGameData.CurrentGame?.dcModel?.bloonModifiers);

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
        var skulls = options.Mixed ? SkullModifier.ApplyTiers(result, options) : SkullModifier.Apply(result, options.SkullPercents);
        result.bossSpawnRounds = new Il2CppStructArray<int>(options.GameSpawnRounds);
        var eventRounds = options.UsesBossEventRounds ? BossEventRounds.Apply(result, options) : "off";
        Patches.MixedBosses.SetType(result, options, 1);
        var bossData = InGameData.CurrentGame?.bossData;
        if (bossData != null) bossData.spawnRounds = new Il2CppStructArray<int>(options.GameSpawnRounds);

        ModHelper.Msg<CustomBossChallenge>(
            $"Custom {options.BossId(options.Tiers)}: added {tiers} extra tiers, scaled {scaled} tiers " +
            $"(speed {options.SpeedPerTier:+0;-0} %, health {options.HealthPerTier:+0;-0} % per tier), changed skulls of {skulls} boss models, " +
            $"spawn rounds {TierMath.FormatRounds(options.EffectiveSpawnRounds)}, boss event rounds {eventRounds}, " +
            $"modified {spawned} bloons the bosses spawn");
    }
}
