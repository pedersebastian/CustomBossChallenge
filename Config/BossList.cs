using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAssets.Scripts.Data.Boss;

namespace CustomBossChallenge.Config;

/// <summary>
/// Every boss the mod knows: the game's own seven, and the mod's own Gildebloon (#25). The game's BossType can't get
/// a new value, so Gildebloon is a number the game doesn't use, with a name of its own. Anything that lists the bosses,
/// turns one into a name or reads one from a name goes through here instead of the enum, or Gildebloon would be
/// missing, or called "100". Kept free of the game's types for the unit tests.
/// </summary>
public static class BossList
{
    /// <summary>The mod's own boss, a cash-stealing gold vault (#25)</summary>
    public const BossType Gildebloon = (BossType) 100;

    public const string GildebloonName = "Gildebloon";

    /// <summary>The game's own bosses, in the game's order</summary>
    public static readonly BossType[] Vanilla = Enum.GetValues<BossType>();

    /// <summary>Every boss the mod knows, Gildebloon last; for reading setups, whether or not it can be played</summary>
    public static readonly BossType[] All = [..Vanilla, Gildebloon];

    /// <summary>The bosses that can be picked and played now: the game's, and Gildebloon when it is on</summary>
    public static BossType[] Available => Experimental.GildebloonAvailable ? All : Vanilla;

    /// <summary>
    /// The game boss that stands in for a mod boss wherever the game needs one of its own: its boss data, the boss
    /// event rounds. Gildebloon is made from Bloonarius, which has no boss panel of its own.
    /// </summary>
    public static BossType Host(BossType boss) => boss == Gildebloon ? BossType.Bloonarius : boss;

    /// <summary>Whether the boss is the mod's own instead of one of the game's</summary>
    public static bool IsModBoss(BossType boss) => boss == Gildebloon;

    /// <summary>Whether the mod knows the boss</summary>
    public static bool IsKnown(BossType boss) => boss == Gildebloon || Enum.IsDefined(boss);

    /// <summary>Whether the boss can be played now: a boss of the game, or Gildebloon when it is on</summary>
    public static bool IsAvailable(BossType boss) => Enum.IsDefined(boss) || boss == Gildebloon && Experimental.GildebloonAvailable;

    /// <summary>The boss's name, e.g. "Lych" or "Gildebloon"; the game's own name for its bosses</summary>
    public static string Name(BossType boss) => boss == Gildebloon ? GildebloonName : boss.ToString();

    /// <summary>The names of <see cref="All"/></summary>
    public static IEnumerable<string> AllNames => All.Select(Name);

    /// <summary>The boss with the name, any case; a number is not a name here</summary>
    public static bool TryParse(string? name, out BossType boss)
    {
        foreach (var known in All)
        {
            if (!string.Equals(Name(known), name?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            boss = known;
            return true;
        }

        boss = default;
        return false;
    }
}
