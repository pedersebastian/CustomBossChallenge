using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CustomBossChallenge.Config;

/// <summary>
/// The rich text tags in the mod's own texts, for languages written right to left (#244). The game shows Arabic by
/// reversing a text and laying it out right to left, which breaks every tag in it: <c>&lt;b&gt;</c> becomes
/// <c>&gt;b&lt;</c> and shows as letters.
/// </summary>
public static class RichText
{
    /// <summary>The tags TextMeshPro knows, with or without a value; anything else in &lt;&gt; is just text</summary>
    private static readonly Regex Tag = new(
        @"<\/?(?:b|i|u|s|color|size|noparse|rotate|alpha|mark|sup|sub|font|material|cspace|voffset|link|style|align|nobr|lowercase|uppercase|smallcaps|line-height|line-indent|indent|margin|mspace|pos|space|width|br|sprite|gradient)(?:=[^<>]*)?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly record struct Token(string Text, bool IsTag);

    /// <summary>Whether <paramref name="text"/> has a tag TextMeshPro would read</summary>
    public static bool HasTags(string? text) => !string.IsNullOrEmpty(text) && Tag.IsMatch(text);

    /// <summary>
    /// <paramref name="text"/> without its tags: what it shows, with the inside of &lt;noparse&gt; as it is
    /// </summary>
    public static string Strip(string? text) =>
        string.Concat(Tokens(text ?? "").Where(token => !token.IsTag).Select(token => token.Text));

    /// <summary>
    /// The text to give TextMeshPro, laying out right to left, so that it shows <paramref name="shown"/> from left to
    /// right with its tags working: the parts in reverse order, each part's letters reversed, and each pair of tags
    /// turned around so that it opens before what it wraps. Without tags it is <paramref name="shown"/> reversed, the
    /// same as the game makes of its own Arabic texts.
    /// </summary>
    public static string ForRightToLeft(string shown)
    {
        var tokens = Tokens(shown);
        var partner = Enumerable.Repeat(-1, tokens.Count).ToArray();
        var open = new Stack<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!tokens[i].IsTag) continue;
            if (!tokens[i].Text.StartsWith("</", StringComparison.Ordinal))
            {
                open.Push(i);
                continue;
            }

            var name = Name(tokens[i].Text);
            // A closing tag closes the latest open one of its kind; ones left open in between stay unpaired
            var match = open.FirstOrDefault(at => Name(tokens[at].Text) == name, -1);
            if (match < 0) continue;
            while (open.Pop() != match)
            {
            }

            partner[match] = i;
            partner[i] = match;
        }

        var result = new StringBuilder(shown.Length);
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (!token.IsTag) result.Append(Reverse(token.Text));
            else result.Append(partner[i] >= 0 ? tokens[partner[i]].Text : token.Text);
        }

        return result.ToString();
    }

    private static string Name(string tag)
    {
        var start = tag.StartsWith("</", StringComparison.Ordinal) ? 2 : 1;
        var end = tag.IndexOfAny(['=', '>'], start);
        return tag[start..end].ToLowerInvariant();
    }

    private static string Reverse(string text)
    {
        var letters = text.ToCharArray();
        Array.Reverse(letters);
        // A pair of UTF-16 halves stays in order
        for (var i = 0; i < letters.Length - 1; i++)
        {
            if (char.IsLowSurrogate(letters[i]) && char.IsHighSurrogate(letters[i + 1]))
            {
                (letters[i], letters[i + 1]) = (letters[i + 1], letters[i]);
                i++;
            }
        }

        return new string(letters);
    }

    /// <summary>Tags and the text between them; the inside of &lt;noparse&gt; is text, whatever it holds</summary>
    private static List<Token> Tokens(string text)
    {
        var tokens = new List<Token>();
        var at = 0;
        while (at < text.Length)
        {
            var tag = Tag.Match(text, at);
            if (!tag.Success)
            {
                tokens.Add(new Token(text[at..], false));
                break;
            }

            if (tag.Index > at) tokens.Add(new Token(text[at..tag.Index], false));
            tokens.Add(new Token(tag.Value, true));
            at = tag.Index + tag.Length;
            if (!tag.Value.Equals("<noparse>", StringComparison.OrdinalIgnoreCase)) continue;

            var end = text.IndexOf("</noparse>", at, StringComparison.OrdinalIgnoreCase);
            if (end < 0) end = text.Length;
            if (end > at) tokens.Add(new Token(text[at..end], false));
            at = end;
        }

        return tokens;
    }
}
