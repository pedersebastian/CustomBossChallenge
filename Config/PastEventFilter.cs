using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CustomBossChallenge.Persistence;

namespace CustomBossChallenge.Config;

/// <summary>
/// What the Past Events list shows (#146): the text searched for, and a boss, year and mode, each null for any
/// </summary>
/// <param name="Boss">The API's lowercase boss name, like "lych"</param>
/// <param name="Mode">The game's mode id, like "Clicks"</param>
public sealed record PastEventQuery(string Text = "", string? Boss = null, int? Year = null, string? Mode = null);

/// <summary>
/// The search and filters of the Past Events list, without the game: the list gives it the names the player sees
/// (maps, modes, difficulties), so a search finds "Monkey Meadow" as well as its id "Tutorial".
/// </summary>
public static class PastEventFilter
{
    /// <summary>
    /// Whether the event is shown. Every word of the text must be found in its name, boss, year, map, mode or
    /// difficulty (normal or Elite), in any case; <paramref name="shownName"/> gives the name the player sees of a
    /// map, mode or difficulty id.
    /// </summary>
    public static bool Matches(PastEvent pastEvent, PastEventQuery query, Func<string, string> shownName) =>
        Matches(pastEvent, query, () => SearchText(pastEvent, shownName));

    /// <summary>
    /// The same, with the event's <see cref="SearchText"/> worked out before: the list keeps it for every event, so
    /// typing doesn't look up the names of every map and mode again on each key
    /// </summary>
    public static bool Matches(PastEvent pastEvent, PastEventQuery query, Func<string> searchText)
    {
        if (query.Boss != null && !string.Equals(pastEvent.BossKey, query.Boss, StringComparison.OrdinalIgnoreCase)) return false;
        if (query.Year != null && pastEvent.StartDate.Year != query.Year) return false;
        if (query.Mode != null && !Modes(pastEvent).Contains(query.Mode)) return false;

        var words = Words(query.Text);
        if (words.Length == 0) return true;
        var text = searchText();
        return words.All(word => text.Contains(word, StringComparison.Ordinal));
    }

    /// <summary>The modes of the normal and the Elite event</summary>
    public static IEnumerable<string> Modes(PastEvent pastEvent) =>
        new[] {pastEvent.Standard, pastEvent.Elite}
            .Select(rules => (string?) rules?["mode"]).OfType<string>().Distinct();

    /// <summary>The years events started in, newest first, for the Year filter</summary>
    public static IReadOnlyList<int> Years(IEnumerable<PastEvent> events) =>
        events.Select(e => e.StartDate.Year).Distinct().OrderByDescending(year => year).ToList();

    /// <summary>The modes events were played in, the most played first, for the Mode filter</summary>
    public static IReadOnlyList<string> AllModes(IEnumerable<PastEvent> events) =>
        events.SelectMany(Modes).GroupBy(mode => mode)
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key).ToList();

    private static string[] Words(string text) =>
        text.ToLowerInvariant().Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// What a search looks in, lowercase: the event's names, boss and year, and its maps, modes and difficulties as
    /// ids and as <paramref name="shownName"/> gives them
    /// </summary>
    public static string SearchText(PastEvent pastEvent, Func<string, string> shownName)
    {
        var parts = new List<string>
        {
            pastEvent.Name, pastEvent.DisplayName, pastEvent.BossKey,
            pastEvent.StartDate.Year.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var rules in new[] {pastEvent.Standard, pastEvent.Elite})
        {
            if (rules == null) continue;
            foreach (var field in new[] {"map", "mode", "difficulty"})
            {
                if ((string?) rules[field] is not { } id) continue;
                parts.Add(id);
                parts.Add(shownName(id));
            }
        }

        return string.Join(" ", parts).ToLowerInvariant();
    }
}
