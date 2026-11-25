using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CustomBossChallenge.Config;

/// <summary>
/// A made-up language for tests (#267): every English text about 40 % longer, with accents and in brackets, so
/// <c>Speed per Tier</c> becomes <c>[Śpééd pér Tíér ~~]</c>. A text that is too long for its box shows cut off, and
/// one that never went through the mod's localization stays plain English. <c>{0}</c> placeholders and rich text
/// tags stay as they are.
/// </summary>
public static class PseudoLocalization
{
    /// <summary>What every pseudo text starts and ends with, and what makes it longer</summary>
    public const char Open = '[', Close = ']', Pad = '~';

    /// <summary>How much longer a text gets, brackets and padding together</summary>
    public const double Growth = 0.4;

    /// <summary>
    /// Each plain letter's accented look-alike. Only letters of languages BTD6 has (Spanish, French, Polish, Turkish),
    /// so the game's Latin fonts have them; the accents above capitals also make lines taller.
    /// </summary>
    private static readonly Dictionary<char, char> Accents = new()
    {
        ['a'] = 'á', ['c'] = 'ç', ['e'] = 'é', ['g'] = 'ğ', ['i'] = 'í', ['l'] = 'ł', ['n'] = 'ñ', ['o'] = 'ó',
        ['s'] = 'ś', ['u'] = 'ú', ['z'] = 'ż',
        ['A'] = 'Á', ['C'] = 'Ç', ['E'] = 'É', ['G'] = 'Ğ', ['I'] = 'Í', ['L'] = 'Ł', ['N'] = 'Ñ', ['O'] = 'Ó',
        ['S'] = 'Ś', ['U'] = 'Ú', ['Z'] = 'Ż'
    };

    /// <summary>Every accented letter <see cref="Make"/> uses</summary>
    public static IReadOnlyCollection<char> AccentedLetters => Accents.Values;

    private static readonly Regex Word = new("[A-Za-z]{2,}", RegexOptions.CultureInvariant);

    /// <summary>
    /// The pseudo text for an English text: accented, in brackets and padded with <see cref="Pad"/> to about
    /// <see cref="Growth"/> more letters. An empty text stays empty.
    /// </summary>
    public static string Make(string english)
    {
        if (string.IsNullOrEmpty(english)) return english;
        var hidden = FormatPlaceholders.Hide(english, out var placeholders);
        var shown = RichText.Strip(hidden).Length;
        var accented = RichText.MapText(hidden, part =>
        {
            var letters = new StringBuilder(part.Length);
            foreach (var letter in part) letters.Append(Accents.TryGetValue(letter, out var accent) ? accent : letter);
            return letters.ToString();
        });
        // "[", " " and "]" are 3 of the extra letters, the rest is padding, at least one
        var pad = Math.Max(1, (int) Math.Ceiling(shown * Growth) - 3);
        return FormatPlaceholders.Show($"{Open}{accented} {new string(Pad, pad)}{Close}", placeholders);
    }

    /// <summary>
    /// A whole English text table in the pseudo language, same keys and order
    /// </summary>
    public static Dictionary<string, string> MakeAll(IEnumerable<KeyValuePair<string, string>> english) =>
        english.ToDictionary(pair => pair.Key, pair => Make(pair.Value));

    /// <summary>
    /// The English words in a shown text that are outside every pseudo text: words the mod shows without its
    /// localization, or the game's and the player's own words. A pseudo text cut off at its end counts up to where
    /// the text ends.
    /// </summary>
    public static List<string> PlainWords(string shown)
    {
        var outside = new StringBuilder(shown.Length);
        var depth = 0;
        foreach (var letter in RichText.Strip(shown))
        {
            if (letter == Open) depth++;
            else if (letter == Close && depth > 0) depth--;
            else if (depth == 0) outside.Append(letter);
        }

        return Word.Matches(outside.ToString()).Select(match => match.Value).ToList();
    }
}
