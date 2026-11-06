using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors.Actions;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Config;

/// <summary>
/// All Camo, All Regrow and All Fortified for the bloons a boss spawns itself (#90). The game gives the rounds' bloons
/// these modifiers when it builds the match, by changing the bloon ids of the rounds, but the bosses keep the ids of
/// the bloons they spawn in their own behaviours, which it leaves alone. So those ids are changed here, the same way:
/// Bloonarius's Ceramic becomes a camo Ceramic, Diamondback's MOAB a fortified MOAB, and so on.
/// <br/>
/// The game's own rule decides which bloons can be camo, regrow or fortified, and a bloon without such a variant
/// (Lych's minis and tombstones, Dreadbloon's rocks) keeps its id. Bloon health and speed need nothing: the game
/// scales the bloon models themselves, so the spawned bloons have them already.
/// </summary>
public static class SpawnedBloonModifiers
{
    /// <summary>
    /// Gives the bloons every boss in <paramref name="gameModel"/> spawns the modifiers that are on
    /// </summary>
    /// <returns>How many spawned bloon ids were changed</returns>
    public static int Apply(GameModel gameModel, BloonModifiers? modifiers)
    {
        if (modifiers == null || !(modifiers.allCamo || modifiers.allRegen || modifiers.allFortified)) return 0;

        string Modified(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var modified = DailyChallengeModel.AddCamoRegenToBloonString(id, modifiers.allCamo, modifiers.allRegen,
                modifiers.allFortified);
            return modified != id && gameModel.GetBloon(modified) != null ? modified : id;
        }

        var changed = 0;
        string Change(string id)
        {
            var modified = Modified(id);
            if (modified != id) changed++;
            return modified;
        }

        foreach (var bloon in gameModel.bloons)
        {
            if (!bloon.isBoss) continue;
            foreach (var behaviour in bloon.behaviors)
            {
                if (behaviour.Is<SpawnBloonsActionModel>(out var spawn)) spawn.bloonType = Change(spawn.bloonType);
                else if (behaviour.Is<LychTombstoneSpawnActionModel>(out var tombstone)) tombstone.bloonType = Change(tombstone.bloonType);
                else if (behaviour.Is<SpawnBloonsUntilArmourBreaksActionModel>(out var rocks)) rocks.bloonType = Change(rocks.bloonType);
                else if (behaviour.Is<SpawnTombstoneBloonsActionModel>(out var undead))
                {
                    var ids = undead.bloonTypePerTombstoneTier;
                    for (var i = 0; i < ids.Length; i++) ids[i] = Change(ids[i]);
                }
            }
        }

        return changed;
    }

    /// <summary>The ids of the bloons a boss model spawns, for the tests and the log</summary>
    public static string[] SpawnedIds(BloonModel boss)
    {
        var ids = new System.Collections.Generic.List<string>();
        foreach (var behaviour in boss.behaviors)
        {
            if (behaviour.Is<SpawnBloonsActionModel>(out var spawn)) ids.Add(spawn.bloonType);
            else if (behaviour.Is<LychTombstoneSpawnActionModel>(out var tombstone)) ids.Add(tombstone.bloonType);
            else if (behaviour.Is<SpawnBloonsUntilArmourBreaksActionModel>(out var rocks)) ids.Add(rocks.bloonType);
            else if (behaviour.Is<SpawnTombstoneBloonsActionModel>(out var undead)) ids.AddRange(undead.bloonTypePerTombstoneTier);
        }
        return ids.ToArray();
    }
}
