using System.Linq;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;

namespace CustomBossChallenge.Config;

/// <summary>
/// The game only has boss models for tiers 1-5. Tiers 6-10 are copies of tier 5 with more health (see
/// <see cref="TierScaling"/>).
/// <br/>
/// Related models that tier 5 refers to (Diamondback segments, Dreadbloon rocks, Lych's minions and tombstones) are
/// shared with the copies instead of being copied too.
/// </summary>
public static class ExtraTiers
{
    /// <summary>
    /// Creates the model for an extra tier from the tier 5 model
    /// </summary>
    public static BloonModel CreateTier(BloonModel tier5, BossOptions options, int tier)
    {
        var id = options.BossId(tier);
        var model = tier5.Clone().Cast<BloonModel>();
        model.id = id;
        model.name = id;
        model._name = id;
        TierScaling.Scale(model, TierScaling.Values.Of(tier5), options, tier);
        return model;
    }

    /// <summary>
    /// Adds the models for every tier above 5 that <paramref name="options"/> asks for to the match's game model.
    /// This must happen before the simulation starts, since it caches the bloon models.
    /// </summary>
    /// <returns>How many tiers were added</returns>
    public static int Add(GameModel gameModel, BossOptions options)
    {
        var tiers = TierMath.ClampTiers(options.Tiers);
        if (tiers <= TierMath.VanillaTiers) return 0;

        // Without a tier 5 model (e.g. after a game update renamed it) there is nothing to copy
        var tier5 = gameModel.DoesBloonExist(options.BossId(TierMath.VanillaTiers))
            ? gameModel.GetBloon(options.BossId(TierMath.VanillaTiers))
            : null;
        if (tier5 == null) return 0;

        var added = 0;
        for (var tier = TierMath.VanillaTiers + 1; tier <= tiers; tier++)
        {
            var model = CreateTier(tier5, options, tier);
            if (gameModel.bloonsByName.ContainsKey(model.id)) continue;

            gameModel.bloons = gameModel.bloons.AddTo(model);
            gameModel.AddChildDependant(model);
            gameModel.bloonsByName[model.id] = model;
            added++;
        }

        return added;
    }

    /// <summary>
    /// Whether the game model has a model for every tier of the boss
    /// </summary>
    public static bool HasAllTiers(GameModel gameModel, BossOptions options) =>
        AvailableTiers(gameModel, options) == TierMath.ClampTiers(options.Tiers);

    /// <summary>
    /// How many of the boss's tiers, counted from tier 1, have a model in the game model
    /// </summary>
    public static int AvailableTiers(GameModel gameModel, BossOptions options) =>
        Enumerable.Range(1, TierMath.ClampTiers(options.Tiers)).TakeWhile(tier => gameModel.DoesBloonExist(options.BossId(tier))).Count();

    /// <summary>
    /// The id of the first vanilla tier (1-5) the boss needs but the game doesn't have, or null if none is missing.
    /// Tiers above 5 are made from tier 5, so tier 5 is always needed for them.
    /// </summary>
    public static string? MissingVanillaTier(GameModel gameModel, BossOptions options)
    {
        var needed = TierMath.ClampTiers(options.Tiers) > TierMath.VanillaTiers
            ? TierMath.VanillaTiers
            : TierMath.ClampTiers(options.Tiers);
        return Enumerable.Range(1, needed).Select(options.BossId).FirstOrDefault(id => !gameModel.DoesBloonExist(id));
    }
}
