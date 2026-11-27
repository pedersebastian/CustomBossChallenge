using System;
using System.Collections;
using BTD_Mod_Helper.Api.Testing;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using System.Linq;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors.Actions;
using Il2CppAssets.Scripts.Unity;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Every boss (normal and elite) has skulls the mod can find and replace. This also catches game updates that
/// change how skulls work.
/// </summary>
public class SkullModifierTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var custom = new[] {0.9f, 0.1f};

        foreach (var boss in Enum.GetValues<BossType>())
        {
            foreach (var id in new[] {$"{boss}1", $"{boss}5", $"{boss}Elite1", $"{boss}Elite5"})
            {
                var original = AssertNotNull(Game.instance.model.GetBloon(id), $"No bloon model {id}");
                var vanillaSkulls = SkullModifier.GetSkulls(original);
                Assert(vanillaSkulls is {Length: > 0}, $"{id} has no skulls to modify");
                // The difficulty rating counts skulls from the boss's own (#304)
                AssertEquals(vanillaSkulls!.Length, DifficultyRating.OwnSkulls(boss, id.Contains("Elite")), $"{id}'s own skulls");

                // The mod changes the only non-repeating health trigger; a second one would make that a guess
                if (original.GetBehavior<LychBossSuperScriptModel>() == null)
                {
                    AssertEquals(original.GetBehaviors<HealthPercentTriggerModel>().Count(t => !t.repeatFirst), 1,
                        $"Skull triggers of {id}");
                }

                var clone = original.Clone().Cast<BloonModel>();
                Assert(SkullModifier.Apply(clone, custom), $"Skulls of {id} were not changed");
                Assert(SameValues(SkullModifier.GetSkulls(clone), custom),
                    $"{id} has skulls {Show(SkullModifier.GetSkulls(clone))}");

                Assert(SkullModifier.Apply(clone, Array.Empty<float>()), $"Skulls of {id} were not removed");
                AssertEquals(SkullModifier.GetSkulls(clone)!.Length, 0, $"{id} still has skulls");

                // What the boss keeps per skull fits any skull count (#304)
                foreach (var count in new[] {0, 1, 8, SkullMath.MaxSkulls})
                {
                    var fitted = original.Clone().Cast<BloonModel>();
                    SkullModifier.Apply(fitted, SkullMath.EvenlySpaced(count));
                    CheckPerSkullValues(id, original, fitted, count);
                }

                Assert(SameValues(SkullModifier.GetSkulls(original), vanillaSkulls), $"{id} original was modified");
            }
        }

        var segment = Game.instance.model.GetBloon("DiamondbackHead1").Clone().Cast<BloonModel>();
        Assert(!SkullModifier.Apply(segment, custom), "Diamondback segments must not be changed");
        Assert(!SkullModifier.Apply(Game.instance.model.GetBloon("Moab"), custom), "Non bosses must not be changed");
        yield break;
    }

    /// <summary>
    /// Dreadbloon has an immunity for its spawn and every skull, going round its own; Phayze a power level per
    /// skull that keeps growing; the boss's own model keeps its lists
    /// </summary>
    private void CheckPerSkullValues(string id, BloonModel original, BloonModel fitted, int count)
    {
        var vanillaImmunity = original.GetBehavior<ApplyTowerSetImmunityModel>();
        if (vanillaImmunity != null)
        {
            var own = vanillaImmunity.immunities.ToArray().Select(i => i.towerType).ToArray();
            var now = fitted.GetBehavior<ApplyTowerSetImmunityModel>()!.immunities.ToArray().Select(i => i.towerType).ToArray();
            Assert(now.Length >= count + 1 && now.Length >= own.Length, $"{id} with {count} skulls has {now.Length} immunities");
            for (var i = 0; i < now.Length; i++)
            {
                AssertEquals(now[i], own[i % own.Length], $"{id} with {count} skulls: immunity {i}");
            }
            AssertEquals(vanillaImmunity.immunities.Length, own.Length, $"{id}'s own immunities were changed");
        }

        var vanillaPhayze = original.GetBehavior<PhayzeBehaviorModel>();
        if (vanillaPhayze != null)
        {
            var own = vanillaPhayze.powerLevels.ToArray();
            var now = fitted.GetBehavior<PhayzeBehaviorModel>()!.powerLevels.ToArray();
            Assert(now.Length >= count && now.Length >= own.Length, $"{id} with {count} skulls has {now.Length} power levels");
            Assert(now.Take(own.Length).SequenceEqual(own), $"{id} with {count} skulls keeps its own power levels: {Show(now)}");
            for (var i = 1; i < now.Length; i++)
            {
                Assert(now[i] >= now[i - 1], $"{id} with {count} skulls gets weaker at skull {i + 1}: {Show(now)}");
            }
            AssertEquals(vanillaPhayze.powerLevels.Length, own.Length, $"{id}'s own power levels were changed");
        }
    }
}
