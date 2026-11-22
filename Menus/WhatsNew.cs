using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The What's new popup (#264): the first time the Boss Creator opens after the mod is updated, it shows the Added
/// points of the new version from CHANGELOG.md, which the mod carries from its build. Never on a fresh install, at most
/// once per version, and not at all with Show What's New off in Mod Settings.
/// </summary>
public static class WhatsNew
{
    /// <summary>The most points the popup lists; the website has them all</summary>
    public const int MaxPoints = 3;

    /// <summary>With this many points or fewer, the popup shows each with its first sentence; with more, only the titles</summary>
    public const int MaxWithText = 1;

    /// <summary>The longest first sentence shown with a title, and the longest point without a title</summary>
    public const int SentenceLength = 120, TitleLength = 70;

    /// <summary>Where the version last seen is kept; tests point this at a temporary file</summary>
    public static WhatsNewStore Store { get; set; } = WhatsNewStore.Default;

    /// <summary>Tests only: Show What's New without touching the player's Mod Settings</summary>
    internal static bool? EnabledOverride { get; set; }

    public static bool Enabled => EnabledOverride ?? Settings.ShowWhatsNew;

    /// <summary>
    /// Tests only: the Boss Creator opens without the popup. Every test but the popup's own has it, like the popup about
    /// newer files.
    /// </summary>
    internal static bool Suppressed { get; set; }

    /// <summary>
    /// Whether the mod's folder in UserData was there when the game started: the mod was used before, so a missing
    /// <see cref="WhatsNewStore"/> file means an update from a version before the popup, not a fresh install
    /// </summary>
    internal static bool UsedBefore { get; set; } = true;

    /// <summary>The version of this build</summary>
    public static string Current => ModHelperData.Version;

    private static IReadOnlyList<ChangelogEntry>? entries;

    /// <summary>CHANGELOG.md as the mod was built with it</summary>
    public static IReadOnlyList<ChangelogEntry> Entries => entries ??= Changelog.Parse(ReadChangelog());

    private static string ReadChangelog()
    {
        using var stream = typeof(WhatsNew).Assembly.GetManifestResourceStream("CustomBossChallenge.CHANGELOG.md");
        if (stream == null)
        {
            ModHelper.Warning<CustomBossChallenge>("The mod was built without CHANGELOG.md, so What's new has nothing to show");
            return "";
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// At the start of the game, before the mod writes anything: a fresh install remembers this version as seen, so
    /// its first Boss Creator shows no popup, nor any later one until the next update
    /// </summary>
    public static void OnGameStart(bool modFolderExists)
    {
        UsedBefore = modFolderExists;
        if (!modFolderExists && Store.Load() == null) Store.Save(Current);
    }

    /// <summary>
    /// When the Boss Creator opens: shows what's new once after an update, and remembers this version as seen
    /// </summary>
    /// <returns>Whether the popup was shown</returns>
    public static bool ShowIfNew()
    {
        if (Suppressed) return false;

        var recorded = Store.Load();
        var decision = WhatsNewRule.Decide(recorded, UsedBefore, Current);
        if (decision.Record != recorded) Store.Save(decision.Record);
        if (!decision.Show || !Enabled) return false;

        var points = Points(decision.Since);
        if (points.Count == 0) return false;

        Show(Body(points));
        ModHelper.Msg<CustomBossChallenge>($"What's new since {decision.Since?.ToString(3) ?? "an earlier version"}: " +
                                           $"{points.Count} point(s)");
        return true;
    }

    /// <summary>
    /// The game's popup with <paramref name="body"/>, OK and Don't Show Again
    /// </summary>
    internal static void Show(string body) =>
        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            Texts.WhatsNew.T(), body, null, Texts.Ok.T(), new Action(TurnOff), Texts.DontShowAgain.T(),
            Popup.TransitionAnim.Scale));

    /// <summary>
    /// The points shown to a player who last saw <paramref name="since"/> (null: only this version's), newest first
    /// </summary>
    public static List<(ChangelogEntry Entry, ChangelogPoint Point)> Points(Version? since) =>
        Changelog.PlayerPoints(Changelog.NewSince(Entries, since, Changelog.ReadVersion(Current)!)).ToList();

    /// <summary>
    /// The popup's text: a line saying which version this is, then each point's title in bold. With a single point it
    /// also gets its text's first sentence; with more than <see cref="MaxPoints"/> a last line says how many more
    /// the website has. More text would only fit the game's popup in letters too small to read.
    /// </summary>
    public static string Body(List<(ChangelogEntry Entry, ChangelogPoint Point)> points)
    {
        var text = new StringBuilder(Texts.WhatsNewIntro.Raw(Current)).Append('\n');
        var withText = points.Count <= MaxWithText;
        foreach (var (_, point) in points.Take(MaxPoints))
        {
            var title = point.Title.Length > 0 ? $"<b>{Changelog.ToRichText(point.Title)}</b>" : null;
            if (title != null && !withText)
            {
                text.Append($"\n• {title}");
                continue;
            }

            var sentence = Changelog.ToRichText(Changelog.FirstSentence(point.Text, withText ? SentenceLength : TitleLength));
            text.Append(title == null ? $"\n• {sentence}" : $"\n• {title}: {sentence}");
        }
        if (points.Count > MaxPoints)
        {
            text.Append($"\n<size=85%>{Texts.WhatsNewMore.Raw(points.Count - MaxPoints)}</size>");
        }

        // In Arabic the English points are shaped along with the rest, so the game's right to left layout keeps them
        // readable; the game would show tags in its popup as letters
        return LanguageText.RightToLeft
            ? LanguageText.Shape(RichText.Strip(text.ToString()))
            : text.ToString();
    }

    /// <summary>
    /// The popup's Don't Show Again: turns Show What's New off in Mod Settings
    /// </summary>
    internal static Action TurnOff { get; set; } = () =>
    {
        Settings.ShowWhatsNew.SetValueAndSave(false);
        ModHelper.Msg<CustomBossChallenge>("Show What's New turned off from its popup");
    };
}
