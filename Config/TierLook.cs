using System;
using System.Collections.Generic;
using System.Globalization;

namespace CustomBossChallenge.Config;

/// <summary>
/// A colour with channels from 0 to 1, kept free of the game's types so the unit tests can use it
/// </summary>
public readonly record struct TierColor(float R, float G, float B)
{
    /// <summary>
    /// A colour from a hex code like "#9BFF2E"
    /// </summary>
    public static TierColor FromHex(string hex)
    {
        var value = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new TierColor(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f);
    }

    public string Hex => $"#{Channel(R):X2}{Channel(G):X2}{Channel(B):X2}";

    private static int Channel(float value) => (int) Math.Round(Math.Clamp(value, 0, 1) * 255);
}

/// <summary>
/// How tiers above 5 look: every boss has a colour theme of its own (Elite shares it), and the effect gets stronger
/// with each tier. Tiers 1-5 keep the game's look.
/// </summary>
public static class TierLook
{
    /// <summary>
    /// The number of tiers that get a look of their own, 6 to 10
    /// </summary>
    public const int Steps = TierMath.MaxTiers - TierMath.VanillaTiers;

    /// <summary>
    /// The colours of tiers 6 to 10 per boss, named as in the game's BossType
    /// </summary>
    private static readonly Dictionary<string, TierColor[]> Themes = new()
    {
        // Toxic slime that heats up
        ["Bloonarius"] = Colors("#9BFF2E", "#E8FF1A", "#FFAA00", "#FF3B1F", "#E0119D"),
        // Ghostly green turning to cursed violet
        ["Lych"] = Colors("#5CFFA8", "#2EE6FF", "#3D7BFF", "#A64DFF", "#FF2ED1"),
        // Storm clouds charging up
        ["Vortex"] = Colors("#8CF2FF", "#3FA9FF", "#6A5CFF", "#C04DFF", "#FFE45C"),
        // Rock glowing hotter and hotter
        ["Dreadbloon"] = Colors("#FFD27A", "#FF9A3C", "#FF5A1F", "#E8162B", "#FF006E"),
        // Phasing through the colours of its two halves
        ["Phayze"] = Colors("#FF7AF0", "#B85CFF", "#5C6BFF", "#1FD8FF", "#2BFFB0"),
        // Fire up to white heat
        ["Blastapopoulos"] = Colors("#FFE45C", "#FFA21C", "#FF5500", "#FF1744", "#B8F4FF"),
        // Gems of growing value
        ["Diamondback"] = Colors("#3DFFD8", "#2E8BFF", "#9A4DFF", "#FF3DAE", "#FFD21F")
    };

    /// <summary>
    /// For bosses the game adds later, so they still get a look
    /// </summary>
    private static readonly TierColor[] DefaultTheme = Colors("#FFE45C", "#FFA21C", "#FF5500", "#FF1744", "#D41CFF");

    private static TierColor[] Colors(params string[] hex) => Array.ConvertAll(hex, TierColor.FromHex);

    /// <summary>
    /// The bosses with a theme of their own
    /// </summary>
    public static IEnumerable<string> Bosses => Themes.Keys;

    /// <summary>
    /// How far above tier 5 a tier is: 0 for tiers 1-5, up to <see cref="Steps"/> for tier 10
    /// </summary>
    public static int Step(int tier) => Math.Clamp(tier - TierMath.VanillaTiers, 0, Steps);

    public static bool HasLook(int tier) => Step(tier) > 0;

    /// <summary>
    /// The colours of tiers 6 to 10 of a boss
    /// </summary>
    public static IReadOnlyList<TierColor> Theme(string boss) => Themes.TryGetValue(boss, out var theme) ? theme : DefaultTheme;

    /// <summary>
    /// The colour of a tier above 5, or white for tiers 1-5
    /// </summary>
    public static TierColor Color(string boss, int tier) => HasLook(tier) ? Theme(boss)[Step(tier) - 1] : new TierColor(1, 1, 1);

    /// <summary>
    /// How strongly the glow over the boss shows, before pulsing
    /// </summary>
    public static float GlowAlpha(int tier) => HasLook(tier) ? 0.12f + 0.08f * Step(tier) : 0;

    /// <summary>
    /// The size of the aura behind the boss compared with the boss
    /// </summary>
    public static float AuraSize(int tier) => HasLook(tier) ? 0.9f + 0.12f * Step(tier) : 0;

    public static float AuraAlpha(int tier) => HasLook(tier) ? 0.3f + 0.08f * Step(tier) : 0;

    /// <summary>
    /// How much bigger the boss is drawn than tier 5; only the look, not what towers can hit
    /// </summary>
    public static float Scale(int tier) => 1 + 0.03f * Step(tier);

    /// <summary>
    /// How far the glow and aura swing around their strength, as a share of it
    /// </summary>
    public static float PulseAmplitude(int tier) => 0.08f * Step(tier);

    /// <summary>
    /// Seconds per pulse: later tiers pulse faster
    /// </summary>
    public static float PulsePeriod(int tier) => 2.6f - 0.3f * Step(tier);

    /// <summary>
    /// The factor the glow and aura are multiplied with at <paramref name="seconds"/>, around 1
    /// </summary>
    public static float Pulse(int tier, double seconds) => HasLook(tier)
        ? 1 + PulseAmplitude(tier) * (float) Math.Sin(2 * Math.PI * seconds / PulsePeriod(tier))
        : 1;
}
