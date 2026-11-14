using System.Globalization;

namespace CustomBossChallenge.Config;

/// <summary>
/// Numbers shown in the mod's own texts, written the same on every system culture (#172). The player's culture can
/// put U+00A0, U+202F or U+2019 between thousands and, with ICU, U+2212 before a negative number, which the game's
/// font may not have; the tier strip already writes its numbers like this.
/// </summary>
public static class ShownNumber
{
    /// <summary>A whole number, rounded, with a plain hyphen-minus: "-10", "250"</summary>
    public static string Whole(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    /// <summary>With a comma between thousands, like the game's own cash: "12,345"</summary>
    public static string Grouped(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A change per tier, with its sign: "+5", "-10", "0"</summary>
    public static string Signed(double value) => value > 0 ? "+" + Whole(value) : Whole(value);
}
