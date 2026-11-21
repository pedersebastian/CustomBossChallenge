using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CustomBossChallenge.Config;

/// <summary>
/// The <c>{0}</c>s of a format text, kept whole while something works on its letters (#244): the Arabic shaper
/// reverses a text and mirrors its brackets, which would break them
/// </summary>
public static class FormatPlaceholders
{
    /// <summary>The private-use letters that stand in for the placeholders, one each</summary>
    private const char First = '', Last = '';

    private static readonly Regex Placeholder = new(@"\{\d+(?:,-?\d+)?(?::[^{}]*)?\}", RegexOptions.CultureInvariant);

    /// <summary>
    /// <paramref name="text"/> with each placeholder as one private-use letter; <paramref name="hidden"/> has them in
    /// order. A text that already has such letters is left as it is.
    /// </summary>
    public static string Hide(string text, out List<string> hidden)
    {
        var found = new List<string>();
        hidden = found;
        if (text.Any(letter => letter is >= First and <= Last)) return text;
        return Placeholder.Replace(text, match =>
        {
            if (First + found.Count > Last) return match.Value;
            found.Add(match.Value);
            return ((char) (First + found.Count - 1)).ToString();
        });
    }

    /// <summary>The placeholders <see cref="Hide"/> took out, back where their letters are now</summary>
    public static string Show(string text, List<string> hidden)
    {
        for (var i = 0; i < hidden.Count; i++) text = text.Replace(((char) (First + i)).ToString(), hidden[i]);
        return text;
    }
}
