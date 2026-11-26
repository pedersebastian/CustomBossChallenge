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

    /// <summary>The options of the custom game with a boss per tier being played, or null</summary>
    internal static BossOptions? Options => MixedOptions;

    /// <summary>
    /// The options of a custom game whose boss type has to be set before each tier: one with a boss per tier, or with
    /// the mod's own Gildebloon, which the game only spawns when the type says so (#25)
    /// </summary>
    private static BossOptions? MixedOptions =>
        CustomBossState.IsActive && CustomBossState.ActiveOptions is { } options && SetsType(options) ? options : null;

    /// <summary>Whether the game's boss type is set per tier for these options</summary>
    public static bool SetsType(BossOptions options) => options.Mixed || options.UsesGildebloon;

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
    /// The tier whose boss's map effects a match shows when it is loaded or restarted, or a boss is beaten (#85): the
    /// one alive, or else the last one beaten. The next boss's effects come when the next round starts, see
    /// <see cref="OnRoundStart"/>.
    /// </summary>
    public static int PresentedTier(BossBloonManager manager, BossOptions options)
    {
        var alive = manager.CurrentBoss;
        var tier = alive != null && TierMath.TierOf(alive.bloonModel.id) is > 0 and var own
            ? own
            : CustomBossState.HighestDefeatedTier;
        return Math.Clamp(tier, TierMath.MinTiers, TierMath.ClampTiers(options.Tiers));
    }

    /// <summary>
    /// The highest tier whose boss has arrived: the one alive, or else the last one beaten; 0 before the first
    /// </summary>
    public static int ArrivedTier(BossBloonManager manager)
    {
        var alive = manager.CurrentBoss;
        return alive != null && TierMath.TierOf(alive.bloonModel.id) is > 0 and var own
            ? Math.Max(own, CustomBossState.HighestDefeatedTier)
            : CustomBossState.HighestDefeatedTier;
    }

    /// <summary>
    /// Whether the boss of <paramref name="tier"/> is still a secret: the game picked it at random, and it hasn't
    /// arrived yet. Its portraits show a die, on the defeat screen too, and the map has no boss effects from the round
    /// after the boss before it is beaten, or from the start for a first tier like that, until it arrives (#283).
    /// </summary>
    public static bool IsSecret(BossBloonManager manager, BossOptions options, int tier) =>
        options.IsSecret(tier, ArrivedTier(manager));

    /// <summary>
    /// Makes the game model and the game's boss data say the boss of <paramref name="tier"/>
    /// </summary>
    public static void SetType(GameModel? model, BossOptions options, int tier)
    {
        if (!SetsType(options)) return;

        var (boss, elite) = options.BossOf(tier);
        if (model != null)
        {
            model.bossBloonType = BossList.Name(boss);
            model.bossEliteMode = elite;
        }

        // The game's boss data only knows its own bosses; a mod boss is played as the game boss it is made from
        var data = InGameData.CurrentGame?.bossData;
        if (data != null)
        {
            data.bossBloon = BossList.Host(boss);
            data.bossEliteMode = elite;
        }
    }

    internal static void BeforeSpawn(BossBloonManager manager, int tier)
    {
        if (MixedOptions is { } options) SetType(manager.sim?.model, options, tier);
    }

    internal static void AfterSpawn(BossBloonManager manager, int tier)
    {
        if (MixedOptions is not { } options) return;
        Present(options, tier, false);
        Spawned?.Invoke(tier);
    }

    /// <summary>The tier whose boss's map effects are shown now, or 0 before a custom game with tier bosses</summary>
    public static int Presented { get; private set; }

    /// <summary>
    /// Whether the map shows no boss effects, because the boss of <see cref="Presented"/> is still a secret: a tier the
    /// game picked at random that hasn't arrived yet (#283)
    /// </summary>
    public static bool PresentedHidden { get; private set; }

    private static void Present(BossOptions options, int tier, bool hidden)
    {
        Presented = tier;
        PresentedHidden = hidden;
        ApplyPresentation(options);
    }

    private static void ApplyPresentation(BossOptions options)
    {
        if (PresentedHidden) Visuals.BossPresentation.HideMapEffects();
        else Visuals.BossPresentation.ShowMapEffects(options.BossOf(Presented).Boss);
    }

    /// <summary>Shows the effects of <paramref name="tier"/>'s boss, or none while that boss is a secret</summary>
    private static void Present(BossBloonManager manager, BossOptions options, int tier) =>
        Present(options, tier, IsSecret(manager, options, tier));

    /// <summary>
    /// A round has started: once a boss is beaten, the map shows the next boss's effects from the next round on, even
    /// when that boss comes rounds later (#85). A boss the game picked at random keeps its secret until it arrives, so
    /// the map shows no boss effects until then (#283).
    /// </summary>
    internal static void OnRoundStart()
    {
        if (MixedOptions is not { } options) return;
        var manager = InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager;
        if (manager == null) return;
        Present(manager, options, ShownTier(manager, options));
    }

    /// <summary>
    /// Keeps the map's boss effects those of the boss shown, also when the game adds its own a moment after the map
    /// loads, as it does when a game is continued, and without any while the boss is a secret
    /// </summary>
    internal static void KeepPresentation()
    {
        if (MixedOptions is not { } options) return;
        if (Presented > 0) ApplyPresentation(options);
        // Before the first tier is shown its boss's effects are the ones the game put there, except for Gildebloon's
        // (#25), and a first boss the game picked at random stays a secret (#283)
        else if (!options.WasRandom(1)) Visuals.BossPresentation.ShowMapEffects(options.BossOf(1).Boss);
    }

    /// <summary>
    /// Puts the type back to the boss alive or the next one, after a defeat, a restart or loading a saved round. A
    /// restart or a loaded round also shows the map effects of the boss alive or beaten last, or none while the next one
    /// is a secret (#283); a defeat keeps them until the next round starts.
    /// </summary>
    internal static void Sync(BossBloonManager manager, bool loaded = true)
    {
        if (MixedOptions is not { } options) return;
        var next = ShownTier(manager, options);
        SetType(manager.sim?.model, options, next);
        if (loaded) Present(manager, options, IsSecret(manager, options, next) ? next : PresentedTier(manager, options));
    }
}
