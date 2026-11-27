using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Simulation.Bloons;
using Il2CppAssets.Scripts.Simulation.Bloons.Behaviors;
using Il2CppAssets.Scripts.Simulation.Bloons.Behaviors.Actions;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using UnityEngine;

namespace CustomBossChallenge.Tests;

/// <summary>
/// What custom skulls do to every boss in a game (#304): each boss, normal and Elite, is pushed past every skull of
/// its vanilla skulls, 8 and 10 evenly spaced, skulls close together and one at the very end. Every skull is logged
/// with what the boss did, and the boss must get through all of them: none skipped, none twice, never stuck.
/// Dreadbloon's armour must go round its immunities at every skull, however many there are.
/// </summary>
[FullSuiteOnly("pushes every boss through 10 skull setups (about 3 min)")]
public class SkullBehaviourTest : CustomBossTest
{
    /// <summary>
    /// The skulls each tier of a boss's game gets, normal on tiers 1-5 and Elite on 6-10
    /// </summary>
    public static readonly string[] Settings = ["", "#8", "#10", "#95%;94%;93%", "#1%"];

    /// <summary>
    /// The longest a skull may take to come after the boss's health was pushed past it
    /// </summary>
    private const float SkullTimeout = 20;

    /// <summary>
    /// The tower sets Dreadbloon's armour is immune to, at spawn and then at every skull: they go round (#304)
    /// </summary>
    public static readonly string[] DreadbloonImmunities = ["Primary", "Military", "Magic", "Support"];

    protected override IEnumerator Run()
    {
        foreach (var boss in TierLookScene.Bosses)
        {
            var tiers = new[] {false, true}.SelectMany(elite => Settings.Select(skulls => $"{boss}{(elite ? "Elite" : "")}{skulls}"));
            var options = new BossOptions {Boss = boss, Tiers = 10, TierBosses = string.Join(",", tiers)};
            yield return TierLookScene.Start(options, data => LoadIntoGame(data));
            for (var tier = 1; tier <= 10; tier++)
            {
                yield return PushThroughSkulls(options, tier);
            }
            yield return ToMainMenu();
        }
    }

    private IEnumerator PushThroughSkulls(BossOptions options, int tier)
    {
        var gameModel = InGame.instance.bridge.Model;
        var skulls = SkullModifier.GetSkulls(gameModel.GetBloon(options.BossId(tier)))!.OrderByDescending(p => p).ToArray();
        var name = $"{options.BossId(tier)} {Settings[(tier - 1) % Settings.Length] switch {"" => "vanilla", var s => s}}";

        Bloon? boss = null;
        yield return TierLookScene.Spawn(options, tier, spawned => boss = spawned);
        var dreadbloon = options.BossOf(tier).Boss == BossType.Dreadbloon;
        Log($"{name}: {skulls.Length} skulls, at spawn {State(boss!)}");
        if (dreadbloon) AssertEquals(Immunity(boss!), DreadbloonImmunities[0], $"{name}'s immunity at spawn");

        var passed = 0;
        var waited = 0f;
        while (passed < skulls.Length)
        {
            if (boss!.IsDestroyed || boss.health <= 0) Fail($"{name} died after skull {passed} of {skulls.Length}");

            // Push the health just past the next skull; the game holds it at the skull for the skull's effect
            var target = skulls[passed] - 0.002f;
            var damage = boss.health - target * boss.bloonModel.maxHealth;
            if (damage > 0)
            {
                boss.ClearArmour();
                boss.Damage(damage, null, false, false, false, null, Il2Cpp.BloonProperties.None,
                    Il2Cpp.BloonProperties.None, false, true, true, true, new Il2CppSystem.Nullable<int>());
            }

            // What the skull brought with it (rock bloons, souls, ...) is popped so the next skull can come
            foreach (var bloon in InGame.instance.GetBloons().ToArray())
            {
                if (!bloon.bloonModel.isBoss && !bloon.bloonModel.isBossSegment) bloon.Destroy();
            }

            yield return new WaitForSecondsRealtime(0.25f);
            waited += 0.25f;

            var skull = InGame.instance.bridge.GetCurrentBossSkull();
            if (skull > passed)
            {
                Assert(skull == passed + 1, $"{name} went from skull {passed} to {skull}: a skull was skipped");
                passed = skull;
                waited = 0;
                Log($"{name}: skull {passed} of {skulls.Length} at {boss.health / (double) boss.bloonModel.maxHealth:P1}: {State(boss)}");
                if (dreadbloon)
                {
                    AssertEquals(Immunity(boss), DreadbloonImmunities[passed % DreadbloonImmunities.Length],
                        $"{name}'s immunity at skull {passed}");
                }
            }
            else if (waited > SkullTimeout)
            {
                Fail($"{name} is stuck after skull {passed} of {skulls.Length} at " +
                     $"{boss.health / (double) boss.bloonModel.maxHealth:P1} health: {State(boss)}");
            }
        }

        TierLookScene.RemoveBosses();
        yield return new WaitForSecondsRealtime(0.25f);
    }

    /// <summary>
    /// What the boss is doing now that a skull can change: its immunity, armour, speed and heat
    /// </summary>
    private static string State(Bloon boss)
    {
        var parts = new List<string>
        {
            $"immune to {Immunity(boss)}",
            boss.HasArmour ? $"armour {boss.CurrentArmourAmount:0}" : "no armour",
            $"speed x{boss.trackSpeedMultiplier:0.###}",
        };
        if (boss.IsInvulnerable) parts.Add("invulnerable");
        foreach (var behavior in boss.bloonBehaviors.list)
        {
            if (behavior.TryCast<PhayzeBehavior>() is { } phayze) parts.Add($"Phayze skulls {phayze.skulls}");
        }
        return string.Join(", ", parts);
    }

    public static string Immunity(Bloon boss) => boss.TowerSetImmunity.ToString();
}
