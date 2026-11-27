using System.Collections.Generic;
using CustomBossChallenge.Config;
using Il2Cpp;
using Il2CppArabicSupport;
using Il2CppNinjaKiwi.Localization;
using Il2CppTMPro;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The mod's texts the way the game shows its own in each language (#244): in the language's font, and in Arabic
/// with joined letters, right to left, and tags that still work.
/// </summary>
public static class LanguageText
{
    /// <summary>Whether the game's language is written right to left; of the languages BTD6 has, only Arabic</summary>
    public static bool RightToLeft => LocalizationManager.Instance?.ActiveLanguage == Language.Arabic;

    /// <summary>
    /// A translated text as the game keeps its own Arabic: letters joined and in the order they are shown, numbers,
    /// Latin words and <c>{0}</c> left to right. Mod Helper adds the mod's translations as they are written, so they
    /// would show backwards with loose letters. Other languages come back as they are.
    /// </summary>
    public static string Shape(string text)
    {
        if (!RightToLeft || string.IsNullOrEmpty(text)) return text;
        // The shaper mirrors brackets, which would turn "({0})" into a broken format: each {0} is one letter meanwhile.
        // Without the short vowel marks, like the shadda in "تصفّح": the game's Arabic font doesn't have them.
        var placeholders = FormatPlaceholders.Hide(text, out var hidden);
        return FormatPlaceholders.Show(ShownBrackets.FacingIn(ArabicFixer.Fix(placeholders, false, false)), hidden);
    }

    /// <summary>The mod's Arabic texts as written, by key, while the game's table holds them shaped</summary>
    private static readonly Dictionary<string, string> Written = new();

    /// <summary>What the game's table holds for each key in <see cref="Written"/>, to know a text shaped already</summary>
    private static readonly Dictionary<string, string> Shaped = new();

    private static bool listening;

    /// <summary>
    /// The mod's text for <paramref name="key"/> as it is written in the player's language, not yet shaped for
    /// Arabic, even while the game's table holds it shaped (<see cref="ShapeTable"/>)
    /// </summary>
    public static string WrittenText(string key) =>
        Written.TryGetValue(key, out var text) ? text : BTD_Mod_Helper.Extensions.StringExt.Localize(key);

    /// <summary>
    /// Shapes the mod's translations in the game's table each time the language changes, after Mod Helper has added
    /// them (#292). Once the main menu is up: Mod Helper starts listening on the title screen.
    /// </summary>
    public static void ListenForLanguage()
    {
        if (listening || LocalizationManager.Instance == null) return;
        listening = true;
        LocalizationManager.Instance.add_OnLanguageChanged(new System.Action(ShapeTable));
        ShapeTable();
    }

    /// <summary>
    /// In Arabic, puts the mod's texts into the game's table shaped (<see cref="Shape"/>), the way the game keeps its
    /// own: the game and Mod Helper look some of them up themselves, by key (a modifier's title, Mod Settings and
    /// their info), and would show them with loose letters. Mod Helper adds them as they are written.
    /// </summary>
    public static void ShapeTable()
    {
        var manager = LocalizationManager.Instance;
        if (manager == null) return;
        if (!RightToLeft)
        {
            // Another language has a table of its own, written as it shows
            Written.Clear();
            Shaped.Clear();
            return;
        }

        var table = manager.textTable;
        var keys = new List<string>();
        foreach (var entry in table)
        {
            if (entry.Key.StartsWith(Texts.KeyPrefix, System.StringComparison.Ordinal)) keys.Add(entry.Key);
        }
        foreach (var key in keys)
        {
            var value = table[key];
            // Shaped already: the language was set again without a new table
            if (Shaped.TryGetValue(key, out var shaped) && shaped == value) continue;
            Written[key] = value;
            Shaped[key] = table[key] = Shape(value);
        }
        // Texts on screen were filled in from the table as it was written
        manager.UpdateAllTextObjectsForLanguageAsync();
    }

    /// <summary>
    /// A text for a text the game sets itself, like a popup's or a dropdown list's: in Arabic without tags, which the
    /// game would show as letters. Elsewhere as it is.
    /// </summary>
    public static string ForGameText(string text) => RightToLeft ? RichText.Strip(text) : text;

    /// <summary>
    /// Shows <paramref name="value"/>, which the game's text has just been given, with its tags working in Arabic.
    /// The game reverses a text that starts with an Arabic letter and lays it out right to left, tags and all, so the
    /// text is given again past the game's own text, in the reversed form with whole tags.
    /// </summary>
    public static void FixTags(TMP_Text text, string value)
    {
        if (!RightToLeft || !text.isRightToLeftText || !RichText.HasTags(value)) return;
        text.SetText(RichText.ForRightToLeft(value));
        text.isRightToLeftText = true;
    }

    /// <summary>
    /// Gives a text the font of the game's language. The game swaps fonts only for texts it translates itself, so a
    /// text the mod sets keeps the Latin title font, which shows boxes in Arabic, Japanese and other languages.
    /// </summary>
    public static void UseLanguageFont(TMP_Text text)
    {
        if (text.TryCast<NK_TextMeshProUGUI>() == null) return;
        LocalizationManager.Instance?.UpdateTextObjectFontAsync(text);
    }
}
