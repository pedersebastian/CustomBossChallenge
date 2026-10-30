using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper.Api;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Creator;

/// <summary>
/// A setup worth trying that comes with the mod. Its name and description are localization keys; the English text is
/// kept too, for the website (see WebDataTest).
/// </summary>
public sealed record ExampleSetup(string NameKey, string DescriptionKey, string EnglishName, string EnglishDescription,
    BossOptions Options, Action<DailyChallengeModel> Rules)
{
    public string Name => NameKey.T();
    public string Description => DescriptionKey.T();
}

/// <summary>
/// The example setups, shown in the Boss Creator's Saved Setups list and on the website
/// </summary>
public static class ExampleSetups
{
    /// <summary>
    /// The examples' seed, so their codes stay the same every time they are made
    /// </summary>
    public const int Seed = 2026;

    public static readonly IReadOnlyList<ExampleSetup> All = new[]
    {
        Example("MarathonLych", "Marathon Lych", "Ten tiers of Elite Lych, each 30 % tougher than the last. Bring snacks.",
            new BossOptions {Boss = BossType.Lych, Elite = true, Tiers = 10, HealthPerTier = 30}, _ => { }),
        Example("SpeedDemonVortex", "Speed Demon Vortex",
            "Vortex gets 40 % faster every tier, with three evenly spaced skulls.",
            new BossOptions {Boss = BossType.Vortex, SpeedPerTier = 40, SkullMode = SkullMode.EvenlySpaced, SkullCount = 3},
            _ => { }),
        Example("GlassCannon", "Glass Cannon",
            "One Bloonarius at three times the speed and a tenth of the health. Blink and it's gone.",
            new BossOptions {Boss = BossType.Bloonarius, Tiers = 1}, dcm =>
            {
                dcm.bloonModifiers.bossSpeedMultiplier = 3;
                dcm.bloonModifiers.healthMultipliers.boss = 0.1f;
            }),
        Example("NoSkullsNoMercy", "No Skulls, No Mercy",
            "Elite Phayze without a single skull, every bloon fortified and no selling.",
            new BossOptions {Boss = BossType.Phayze, Elite = true, SkullMode = SkullMode.EvenlySpaced, SkullCount = 0}, dcm =>
            {
                dcm.bloonModifiers.allFortified = true;
                dcm.disableSelling = true;
            }),
        Example("SpeedrunDreadbloon", "Speedrun Dreadbloon",
            "All five tiers by round 40: a new Dreadbloon every five rounds from round 20.",
            new BossOptions {Boss = BossType.Dreadbloon, SpawnRounds = "20, 25, 30, 35, 40"}, _ => { }),
        Example("ParagonBan", "Paragon Ban", "Elite Blastapopoulos on Infernal, Hard, and not a single paragon allowed.",
            new BossOptions {Boss = BossType.Blastapopoulos, Elite = true}, dcm =>
            {
                dcm.maxParagons = 0;
                dcm.difficulty = "Hard";
                dcm.map = "Infernal";
            })
    };

    /// <summary>
    /// The example with this name, in the player's language or in English, or null
    /// </summary>
    public static ExampleSetup? Named(string? name) =>
        All.FirstOrDefault(example => name == example.Name || name == example.EnglishName);

    /// <summary>
    /// The example's setup, named in the player's language, or in English for the website
    /// </summary>
    public static (BossOptions Options, DailyChallengeModel Dcm) Build(ExampleSetup example, bool english = false)
    {
        var dcm = BossMode.DefaultDcm(example.Options);
        dcm.name = english ? example.EnglishName : example.Name;
        dcm.seed = Seed;
        example.Rules(dcm);
        return (example.Options, dcm);
    }

    private static ExampleSetup Example(string id, string name, string description, BossOptions options,
        Action<DailyChallengeModel> rules) =>
        new(ModContent.Localize<CustomBossChallenge>($"Example{id}", name),
            ModContent.Localize<CustomBossChallenge>($"Example{id}Info", description), name, description, options, rules);
}
