using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// The boss of one tier in a setup with a different boss per tier
/// </summary>
/// <param name="Boss">A boss name from <see cref="TierBossMath.Bosses"/>, <see cref="TierBossMath.Random"/>, or null
/// when the tier follows the setup's own boss and Elite</param>
/// <param name="Elite">Whether the tier is Elite; ignored when the tier follows the setup's boss</param>
/// <param name="Speed">The tier's own boss speed in percent, in Advanced Mode in place of Boss Speed</param>
/// <param name="Health">The tier's own boss health in percent, in Advanced Mode in place of Boss Health</param>
/// <param name="Skulls">The tier's own skulls, in Advanced Mode in place of the Skulls option</param>
/// <param name="SkullCount">The number of skulls for <see cref="SkullMode.EvenlySpaced"/></param>
/// <param name="CustomSkulls">The skulls' health percentages for <see cref="SkullMode.Custom"/>, like "75, 50, 25"</param>
public readonly record struct TierBoss(
    string? Boss,
    bool Elite,
    int Speed = TierBossMath.Normal,
    int Health = TierBossMath.Normal,
    SkullMode Skulls = SkullMode.Vanilla,
    int SkullCount = SkullMath.DefaultCount,
    string CustomSkulls = SkullMath.DefaultCustom)
{
    public static readonly TierBoss Follows = new(null, false);

    public bool FollowsSetup => Boss == null;
    public bool IsRandom => Boss == TierBossMath.Random;

    /// <summary>
    /// The tier's speed and health compared with the normal game; 1 is normal
    /// </summary>
    public double SpeedFactor => Speed / 100.0;
    public double HealthFactor => Health / 100.0;

    /// <summary>
    /// The tier's skull thresholds (0-1, descending), or null to keep the boss's own
    /// </summary>
    public float[]? SkullPercents => SkullMath.Percents(Skulls, SkullCount, CustomSkulls);

    /// <summary>
    /// The token this tier has in <see cref="BossOptions.TierBosses"/>: "", "Lych", "LychElite", "RandomElite",
    /// "LychElite@120/80" for 120 % speed and 80 % health, "Lych#4" for 4 evenly spaced skulls, "Lych#60%;30%" for
    /// skulls at 60 % and 30 % health, ...
    /// </summary>
    public override string ToString() =>
        (Boss == null ? "" : Boss + (Elite ? TierBossMath.EliteSuffix : "")) +
        (Speed == TierBossMath.Normal && Health == TierBossMath.Normal ? "" : $"{TierBossMath.ValuesMark}{Speed}/{Health}") +
        TierBossMath.FormatSkulls(this);
}

/// <summary>
/// Pure helpers for the boss of each tier (<see cref="BossOptions.TierBosses"/>): a comma separated list with a token
/// per tier, like "Lych,,RandomElite,VortexElite@120/80#4". An empty token follows the setup's own boss and Elite,
/// "@speed/health" gives the tier its own speed and health in percent, and "#" its own skulls: a count of evenly
/// spaced skulls ("#4") or their health percentages ("#75%;50%;25%"). Kept free of
/// the game's types, so the unit and property tests can run it; BossNamesTest checks <see cref="Bosses"/> against
/// the game's BossType.
/// </summary>
public static class TierBossMath
{
    public const string Random = "Random";
    public const string EliteSuffix = "Elite";

    /// <summary>
    /// Between a tier's boss and its speed and health, e.g. "Lych@120/80"
    /// </summary>
    public const char ValuesMark = '@';

    /// <summary>
    /// Before a tier's own skulls, e.g. "Lych#4" or "Lych#75%;50%"
    /// </summary>
    public const char SkullsMark = '#';

    /// <summary>
    /// A tier's speed and health in percent, with the range and step of Boss Speed and Boss Health
    /// </summary>
    public const int Normal = 100;
    public const int MinSpeed = 10;
    public const int MaxSpeed = 1000;
    public const int MinHealth = 10;
    public const int MaxHealth = 2000;
    public const int ValueStep = 10;

    /// <summary>
    /// The bosses in the order the Boss Creator shows them, which is the order of the game's BossType
    /// </summary>
    public static readonly string[] Bosses =
        ["Bloonarius", "Lych", "Vortex", "Dreadbloon", "Phayze", "Blastapopoulos", "Diamondback"];

    /// <summary>
    /// What a tier can be stepped through: every boss, then Random
    /// </summary>
    public static readonly string[] Choices = [..Bosses, Random];

    /// <summary>
    /// The tiers' bosses in <paramref name="list"/>, at most <see cref="TierMath.MaxTiers"/>. Unknown tokens follow
    /// the setup's boss. Case doesn't matter.
    /// </summary>
    public static TierBoss[] Parse(string? list)
    {
        if (string.IsNullOrWhiteSpace(list)) return [];
        return list.Split(',').Take(TierMath.MaxTiers).Select(ParseToken).ToArray();
    }

    private static TierBoss ParseToken(string token)
    {
        token = token.Trim();
        var skulls = TierBoss.Follows;
        var skullsMark = token.IndexOf(SkullsMark);
        if (skullsMark >= 0)
        {
            skulls = ParseSkulls(token[(skullsMark + 1)..]);
            token = token[..skullsMark].Trim();
        }

        var (speed, health) = (Normal, Normal);
        var mark = token.IndexOf(ValuesMark);
        if (mark >= 0)
        {
            var values = token[(mark + 1)..].Split('/');
            speed = ParseValue(values[0], MinSpeed, MaxSpeed);
            health = ParseValue(values.Length > 1 ? values[1] : "", MinHealth, MaxHealth);
            token = token[..mark].Trim();
        }

        var elite = token.EndsWith(EliteSuffix, StringComparison.OrdinalIgnoreCase);
        var name = elite ? token[..^EliteSuffix.Length] : token;
        var known = Choices.FirstOrDefault(choice => string.Equals(choice, name, StringComparison.OrdinalIgnoreCase));
        return skulls with {Boss = known, Elite = known != null && elite, Speed = speed, Health = health};
    }

    /// <summary>
    /// A tier's own skulls after <see cref="SkullsMark"/>: a whole number is that many evenly spaced skulls, a list
    /// with "%" their health percentages; junk keeps the boss's own
    /// </summary>
    private static TierBoss ParseSkulls(string text)
    {
        text = text.Trim();
        if (text.Contains('%'))
        {
            return SkullMath.ParseCustom(text) is { } percents
                ? TierBoss.Follows with {Skulls = SkullMode.Custom, CustomSkulls = SkullMath.FormatCustom(percents)}
                : TierBoss.Follows;
        }

        return int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
            out var count)
            ? TierBoss.Follows with {Skulls = SkullMode.EvenlySpaced, SkullCount = Math.Clamp(count, 0, SkullMath.MaxSkulls)}
            : TierBoss.Follows;
    }

    /// <summary>
    /// A tier's own skulls as they follow <see cref="SkullsMark"/>, or "" for the boss's own
    /// </summary>
    public static string FormatSkulls(TierBoss tier)
    {
        if (tier.Skulls == SkullMode.EvenlySpaced) return $"{SkullsMark}{Math.Clamp(tier.SkullCount, 0, SkullMath.MaxSkulls)}";
        if (tier.Skulls != SkullMode.Custom || SkullMath.ParseCustom(tier.CustomSkulls) is not { } percents) return "";
        return SkullsMark + string.Join(";", percents.Select(SkullMath.FormatPercent));
    }

    /// <summary>
    /// A speed or health in percent, held to its range and step; junk is the normal 100
    /// </summary>
    private static int ParseValue(string text, int min, int max) =>
        int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? ClampValue(value, min, max)
            : Normal;

    /// <summary>
    /// A speed or health held to its range, on a multiple of <see cref="ValueStep"/>
    /// </summary>
    public static int ClampValue(int value, int min, int max) =>
        Math.Clamp((int) Math.Round(value / (double) ValueStep, MidpointRounding.AwayFromZero) * ValueStep, min, max);

    /// <summary>
    /// The list for <paramref name="tiers"/>, without the tiers at the end that follow the setup's boss
    /// </summary>
    public static string Format(IEnumerable<TierBoss> tiers)
    {
        var tokens = tiers.Take(TierMath.MaxTiers).Select(tier => tier.ToString()).ToList();
        while (tokens.Count > 0 && tokens[^1] == "") tokens.RemoveAt(tokens.Count - 1);
        return string.Join(",", tokens);
    }

    /// <summary>
    /// The list in its canonical form. Null (a single boss for every tier) stays null.
    /// </summary>
    public static string? Normalize(string? list) => list == null ? null : Format(Parse(list));

    /// <summary>
    /// The boss of a 1-based tier in <paramref name="list"/>
    /// </summary>
    public static TierBoss Entry(string? list, int tier)
    {
        var tiers = Parse(list);
        return tier >= 1 && tier <= tiers.Length ? tiers[tier - 1] : TierBoss.Follows;
    }

    /// <summary>
    /// <paramref name="list"/> with the boss of a 1-based tier replaced
    /// </summary>
    public static string With(string? list, int tier, TierBoss boss)
    {
        if (tier < 1 || tier > TierMath.MaxTiers) return Normalize(list) ?? "";
        var tiers = Parse(list).ToList();
        while (tiers.Count < tier) tiers.Add(TierBoss.Follows);
        tiers[tier - 1] = boss;
        return Format(tiers);
    }

    /// <summary>
    /// The next (<paramref name="direction"/> 1) or previous (-1) of <see cref="Choices"/>, wrapping around. A tier
    /// that follows the setup's boss steps from that boss, and keeps the setup's Elite.
    /// </summary>
    public static TierBoss Step(TierBoss current, string setupBoss, bool setupElite, int direction)
    {
        var from = current.Boss ?? setupBoss;
        var index = Array.IndexOf(Choices, from);
        if (index < 0) index = 0;
        var next = ((index + Math.Sign(direction)) % Choices.Length + Choices.Length) % Choices.Length;
        return current with {Boss = Choices[next], Elite = current.FollowsSetup ? setupElite : current.Elite};
    }

    /// <summary>
    /// The concrete boss of every tier from 1 to <paramref name="tiers"/>: tiers that follow the setup get its boss and
    /// Elite, and Random tiers get a boss picked with <paramref name="pick"/> (given the number of bosses, it returns
    /// an index), keeping their own Elite. Every tier keeps its speed, health and skulls; tiers without an entry get
    /// those of <paramref name="standard"/>.
    /// </summary>
    public static string Resolve(string? list, string setupBoss, bool setupElite, int tiers, Func<int, int> pick,
        TierBoss? standard = null)
    {
        var parsed = Parse(list);
        var missing = (standard ?? TierBoss.Follows) with {Boss = null, Elite = false};
        var resolved = Enumerable.Range(1, TierMath.ClampTiers(tiers)).Select(tier =>
        {
            var entry = tier <= parsed.Length ? parsed[tier - 1] : missing;
            if (entry.FollowsSetup) return entry with {Boss = setupBoss, Elite = setupElite};
            if (!entry.IsRandom) return entry;
            return entry with {Boss = Bosses[Math.Clamp(pick(Bosses.Length), 0, Bosses.Length - 1)]};
        });
        return string.Join(",", resolved.Select(tier => tier.ToString()));
    }

    /// <summary>
    /// Whether any of the first <paramref name="tiers"/> tiers is Random
    /// </summary>
    public static bool HasRandom(string? list, int tiers) => Parse(list).Take(tiers).Any(tier => tier.IsRandom);
}
