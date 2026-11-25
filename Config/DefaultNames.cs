using System.Collections.Generic;
using System.Linq;

namespace CustomBossChallenge.Config;

/// <summary>
/// The names a new setup gets in one language (#279): {0} is the boss, e.g. "Custom {0}" and "Custom Elite {0}"
/// </summary>
public sealed record DefaultNameTexts(string Name, string EliteName, string MixedName)
{
    public static readonly DefaultNameTexts English = new("Custom {0}", "Custom Elite {0}", "Custom Boss Gauntlet");
}

/// <summary>
/// The name the Boss Creator gives a new setup, in the player's language where the Challenge Editor's name field keeps
/// it, else in English, like the example setups' names (#218)
/// </summary>
public static class DefaultNames
{
    public static string For(DefaultNameTexts texts, string boss, bool elite = false)
    {
        var english = string.Format(elite ? DefaultNameTexts.English.EliteName : DefaultNameTexts.English.Name, boss);
        var translated = Format(elite ? texts.EliteName : texts.Name, boss);
        return translated == null ? english : NameField.Translated(translated, english);
    }

    public static string Mixed(DefaultNameTexts texts) =>
        NameField.Translated(texts.MixedName, DefaultNameTexts.English.MixedName);

    /// <summary>
    /// Whether the name is one the Boss Creator gives in any of these languages, so a setup made in one language still
    /// counts as unnamed, and follows the boss, after the player switches to another
    /// </summary>
    public static bool IsDefault(string? name, IEnumerable<DefaultNameTexts> languages, IEnumerable<string> bosses)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var bossList = bosses.ToList();
        return languages.Append(DefaultNameTexts.English).Any(texts =>
            name == Mixed(texts) || bossList.Any(boss => name == For(texts, boss) || name == For(texts, boss, true)));
    }

    /// <summary>The name with the boss in it, or null when a translation's placeholder is broken</summary>
    private static string? Format(string text, string boss)
    {
        try
        {
            return string.Format(text, boss);
        }
        catch (System.FormatException)
        {
            return null;
        }
    }
}
