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
