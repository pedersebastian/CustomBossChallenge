using System;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Simulation.Track;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;

namespace CustomBossChallenge.Patches;

/// <summary>
/// A different boss per tier. The game spawns the boss whose type the game model has when the tier spawns, and its
/// boss panel (Lych's regrow, Blastapopoulos' heat, Diamondback's tail, the armour bars) follows that type too. So the
/// type is switched to each tier's boss right before it spawns, and to the next tier's boss once one is defeated, so
/// the panel between bosses already counts down to the right one. The portraits the panel shows are set by
/// Visuals.BossPortraits.
/// <br/>
/// Bosses are always looked up by tier, never as "the" boss of the game, which leaves room for more than one boss at
/// a time later.
/// </summary>
public static class MixedBosses
{
    /// <summary>
    /// A boss of a custom game with a different boss per tier has spawned, with its tier
    /// </summary>
    public static event Action<int>? Spawned;

    private static BossOptions? MixedOptions =>
        CustomBossState.IsActive && CustomBossState.ActiveOptions is {Mixed: true} options ? options : null;

    /// <summary>
    /// The tier whose boss the game should show now: the one alive, or else the next one to come
    /// </summary>
    public static int ShownTier(BossBloonManager manager, BossOptions options)
    {
        var alive = manager.CurrentBoss;
        var tier = alive != null && TierMath.TierOf(alive.bloonModel.id) is > 0 and var own
            ? own
            : CustomBossState.HighestDefeatedTier + 1;
        return Math.Clamp(tier, TierMath.MinTiers, TierMath.ClampTiers(options.Tiers));
    }

    /// <summary>
    /// Makes the game model and the game's boss data say the boss of <paramref name="tier"/>
    /// </summary>
    public static void SetType(GameModel? model, BossOptions options, int tier)
    {
        if (!options.Mixed) return;

        var (boss, elite) = options.BossOf(tier);
        if (model != null)
        {
            model.bossBloonType = boss.ToString();
            model.bossEliteMode = elite;
        }

        var data = InGameData.CurrentGame?.bossData;
        if (data != null)
        {
            data.bossBloon = boss;
            data.bossEliteMode = elite;
        }
    }

    internal static void BeforeSpawn(BossBloonManager manager, int tier)
    {
        if (MixedOptions is { } options) SetType(manager.sim?.model, options, tier);
    }

    internal static void AfterSpawn(BossBloonManager manager, int tier)
    {
        if (MixedOptions != null) Spawned?.Invoke(tier);
    }

    /// <summary>
    /// Puts the type back to the boss alive or the next one, after a defeat, a restart or loading a saved round
    /// </summary>
    internal static void Sync(BossBloonManager manager)
    {
        if (MixedOptions is { } options) SetType(manager.sim?.model, options, ShownTier(manager, options));
    }
}
