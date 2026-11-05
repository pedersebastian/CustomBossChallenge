using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// What <see cref="RandomNames"/> looks at in a setup: the boss options and the few challenge values that make a
/// setup stand out. Speed and health are in percent, 100 is the normal game.
/// </summary>
public sealed record NameTraits
{
    /// <summary>The boss as the name shows it, e.g. "Lych" or "Elite Lych"; empty when the tiers have different bosses</summary>
    public string Boss { get; init; } = "";

    /// <summary>Whether every tier is Elite</summary>
    public bool Elite { get; init; }

    public int Tiers { get; init; } = TierMath.VanillaTiers;
    public int BossSpeed { get; init; } = 100;
    public int BossHealth { get; init; } = 100;
    public int SpeedPerTier { get; init; }
    public int HealthPerTier { get; init; }

    /// <summary>Whether no tier has a single skull</summary>
    public bool NoSkulls { get; init; }

    public bool AllFortified { get; init; }

    /// <summary>In Advanced Mode: the boss of every tier when they are all the same one (without Elite), or null</summary>
    public string? SameBoss { get; init; }
}

/// <summary>
/// Fun names that describe a setup the Random button made, like "The Unkillable Snail" for a slow boss with lots of
/// health. The rules are checked in order, so the most striking trait wins; a setup without one gets no name here and
/// keeps the Boss Creator's default name. The names are English only.
/// </summary>
public static class RandomNames
{
    /// <summary>
    /// The game's challenge name field holds 40 characters, so every name the rules can make fits in it (and in
    /// ChallengeLimits.MaxNameLength)
    /// </summary>
    public const int MaxLength = 40;

    /// <summary>
    /// Besides ASCII letters and digits, the only characters the game's name field keeps when the name is put into it:
    /// it drops others like '-', ':' or '(' and turns 'Å' into 'A', so a name using them would no longer match its
    /// rule. RandomNamesFieldTest checks every name against the real field.
    /// </summary>
    public const string FieldPunctuation = " !?;/#%',.";

    /// <summary>From how far off the normal 100 % a boss speed or health counts as striking</summary>
    public const int High = 250;
    public const int Low = 60;

    public const int ManyTiers = 8;
    public const int FastPerTier = 20;
    public const int ToughPerTier = 30;

    /// <summary>
    /// A rule: when it applies, and the names it picks from. {0} is <see cref="NameTraits.Boss"/> and {1}
    /// <see cref="NameTraits.SameBoss"/>; a name with {0} is left out for a setup with different bosses.
    /// </summary>
    private sealed record Rule(Func<NameTraits, bool> Applies, params string[] Names);

    private static readonly Rule[] Rules =
    [
        new(t => t.SameBoss != null && t.Tiers >= 3, "{1}, {1}, {1}...", "Nothing but {1}", "{1} on Repeat"),
        new(t => t.BossSpeed <= Low && t.BossHealth >= High, "The Unkillable Snail", "Slow but Unstoppable"),
        new(t => t.BossSpeed >= High && t.BossHealth <= Low, "Fast and Fragile", "Paper Rocket"),
        new(t => t.BossSpeed >= High && t.Tiers == 1, "Blink and It's Gone", "Gone in a Flash"),
        new(t => t.Tiers >= ManyTiers && t.HealthPerTier >= ToughPerTier, "The Long Haul", "It Just Keeps Going"),
        new(t => t.AllFortified && t.Elite, "Armoured and Angry", "Elite and Armoured"),
        new(t => t.NoSkulls, "No Skulls Allowed", "No Skulls Attached"),
        new(t => t.BossSpeed >= High, "Turbo {0}", "{0} in a Hurry"),
        new(t => t.BossHealth >= High, "Heavyweight {0}", "{0} the Unbreakable"),
        new(t => t.BossSpeed <= Low, "Sleepy {0}", "{0} in Slow Motion"),
        new(t => t.BossHealth <= Low, "Paper {0}", "Fragile {0}"),
        new(t => t.Tiers == 1, "One and Done", "Just the One"),
        new(t => t.Tiers >= ManyTiers, "Tier After Tier", "The Long Road"),
        new(t => t.SpeedPerTier >= FastPerTier, "Faster Every Time", "Picking Up Speed"),
        new(t => t.HealthPerTier >= ToughPerTier, "Tougher Every Time", "Growing Pains"),
        new(t => t.AllFortified, "Hard Shell", "Fortified Everything")
    ];

    /// <summary>
    /// A name that describes the setup, one of the first rule's names picked by <paramref name="random"/>, or null when
    /// nothing about the setup stands out
    /// </summary>
    public static string? For(NameTraits traits, Random random)
    {
        var names = NamesFor(traits);
        return names.Count > 0 ? names[random.Next(names.Count)] : null;
    }

    /// <summary>
    /// Whether <paramref name="name"/> is one the rules would still give the setup, so it can stay
    /// </summary>
    public static bool Fits(string? name, NameTraits traits) => name != null && NamesFor(traits).Contains(name);

    /// <summary>
    /// Whether the rules can make <paramref name="name"/> for some setup, so it's a name the Boss Creator gave
    /// </summary>
    public static bool IsGenerated(string? name) => name != null && All.Value.Contains(name);

    /// <summary>
    /// Every name the rules can make, with every boss
    /// </summary>
    public static IEnumerable<string> AllNames => All.Value;

    private static readonly Lazy<HashSet<string>> All = new(() =>
    {
        var bosses = TierBossMath.Bosses.Concat(TierBossMath.Bosses.Select(boss => $"Elite {boss}")).ToArray();
        return Rules.SelectMany(rule => rule.Names)
            .SelectMany(name => name.Contains("{0}") ? bosses.Select(boss => Fill(name, boss, ""))
                : name.Contains("{1}") ? TierBossMath.Bosses.Select(boss => Fill(name, "", boss))
                : [name])
            .Where(name => name.Length <= MaxLength)
            .ToHashSet();
    });

    /// <summary>
    /// The names of the first rule that applies and has a name for the setup
    /// </summary>
    private static List<string> NamesFor(NameTraits traits)
    {
        foreach (var rule in Rules)
        {
            if (!rule.Applies(traits)) continue;
            var names = rule.Names
                .Where(name => !name.Contains("{0}") || traits.Boss.Length > 0)
                .Select(name => Fill(name, traits.Boss, traits.SameBoss ?? ""))
                // e.g. "Blastapopoulos, Blastapopoulos, Blastapopoulos...", which the name field would cut off
                .Where(name => name.Length <= MaxLength)
                .ToList();
            if (names.Count > 0) return names;
        }

        return [];
    }

    private static string Fill(string name, string boss, string sameBoss) =>
        name.Replace("{0}", boss).Replace("{1}", sameBoss);
}
