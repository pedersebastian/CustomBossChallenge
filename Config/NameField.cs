using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// What the Challenge Editor's name field keeps (#218). Only ASCII letters and digits and the punctuation below stay as
/// they are when a name is put into it: it drops others like '-', ':' or '(' and changes 'Å' to 'A', and a name from
/// another script loses its letters. The changed text then becomes the setup's name. RandomNamesFieldTest checks the
/// rule against the real field.
/// </summary>
public static class NameField
{
    /// <summary>Besides ASCII letters and digits, the only characters the field keeps</summary>
    public const string Punctuation = " !?;/#%',.~";

    /// <summary>How many characters the field holds</summary>
    public const int MaxLength = 40;

    public static bool Keeps(char c) => IsAsciiLetterOrDigit(c) || Punctuation.Contains(c);

    /// <summary>char.IsAsciiLetterOrDigit, which the game's .NET 6 doesn't have</summary>
    public static bool IsAsciiLetterOrDigit(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    /// <summary>Whether the field keeps this text as it is</summary>
    public static bool Fits(string text) => text.Length <= MaxLength && text.All(Keeps);

    /// <summary>
    /// The translated text when the field keeps it as it is, else the English one: "Maraton Lych" stays Norwegian,
    /// "Ingen hodeskaller, ingen nåde" becomes "No Skulls, No Mercy"
    /// </summary>
    public static string Translated(string translated, string english) => Fits(translated) ? translated : english;
}
