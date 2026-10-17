using System;
using System.Collections;
using BTD_Mod_Helper.Api.Testing;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using System.Linq;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors;
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

                Assert(SameValues(SkullModifier.GetSkulls(original), vanillaSkulls), $"{id} original was modified");
            }
        }

        var segment = Game.instance.model.GetBloon("DiamondbackHead1").Clone().Cast<BloonModel>();
        Assert(!SkullModifier.Apply(segment, custom), "Diamondback segments must not be changed");
        Assert(!SkullModifier.Apply(Game.instance.model.GetBloon("Moab"), custom), "Non bosses must not be changed");
        yield break;
    }
}
