using System;
using System.Globalization;

namespace CustomBossChallenge.Config;

/// <summary>
/// Dates of the Past Events list (#146) written the way the player's language writes them: "17 Oct 2026",
/// "17. okt. 2026", "2026年10月17日". The month names and the order come from the translations (DateMonths,
/// DateFormat), not from .NET's cultures, which the game's runtime may not have.
/// </summary>
public static class PastEventDates
{
    public const string EnglishMonths = "Jan,Feb,Mar,Apr,May,Jun,Jul,Aug,Sep,Oct,Nov,Dec";

    /// <summary>{0} is the day, {1} the month's name and {2} the year</summary>
    public const string EnglishFormat = "{0} {1} {2}";

    /// <summary>
    /// The date with <paramref name="format"/> and the twelve comma separated <paramref name="months"/>. A
    /// translation that isn't usable gives the English date instead of an error.
    /// </summary>
    public static string Format(DateTime date, string format, string months)
    {
        var names = months.Split(',');
        if (names.Length != 12 || Array.Exists(names, name => name.Trim().Length == 0))
        {
            names = EnglishMonths.Split(',');
        }
        try
        {
            return string.Format(CultureInfo.InvariantCulture, format, date.Day, names[date.Month - 1].Trim(), date.Year);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.InvariantCulture, EnglishFormat, date.Day, EnglishMonths.Split(',')[date.Month - 1],
                date.Year);
        }
    }

    /// <summary>The English date, for the website and where no language is known</summary>
    public static string English(DateTime date) => Format(date, EnglishFormat, EnglishMonths);
}
