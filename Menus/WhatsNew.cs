using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BTD_Mod_Helper;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The What's new popup (#264): the first time the Boss Creator opens after the mod is updated, it shows the Added
/// points of the new version from CHANGELOG.md, which the mod carries from its build, whole, in a list that scrolls.
/// Never on a fresh install, at most once per version, and not at all with Show What's New off in Mod Settings.
/// </summary>
public static class WhatsNew
{
    /// <summary>
    /// The size of the popup's text: like the Boss Creator's info popups, which players read already. The game would
    /// shrink a long text until it fits; this one scrolls instead.
    /// </summary>
    public const float FontSize = 66;

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

        // Only the newest release's points, also when the player skipped versions: the website has the others
        var points = Points();
        if (points.Count == 0) return false;

        Show(Body(points));
        ModHelper.Msg<CustomBossChallenge>($"What's new in {Current}, last seen " +
                                           $"{decision.Since?.ToString(3) ?? "an earlier version"}: {points.Count} point(s)");
        return true;
    }

    /// <summary>
    /// The points of this version, the newest release, that players are shown
    /// </summary>
    public static List<(ChangelogEntry Entry, ChangelogPoint Point)> Points() =>
        Changelog.PlayerPoints(Changelog.NewSince(Entries, null, Changelog.ReadVersion(Current)!)).ToList();

    /// <summary>
    /// The popup's text: a line saying which version this is, then every point, its title in bold and its whole text
    /// </summary>
    public static string Body(List<(ChangelogEntry Entry, ChangelogPoint Point)> points)
    {
        var text = new StringBuilder(Texts.WhatsNewIntro.Raw(Current));
        foreach (var (_, point) in points)
        {
            var whole = Changelog.ToRichText(point.Text);
            text.Append(point.Title.Length > 0
                ? $"\n\n• <b>{Changelog.ToRichText(point.Title)}</b>: {whole}"
                : $"\n\n• {whole}");
        }

        // In Arabic the English points are shaped along with the rest, so the game's right to left layout keeps them
        // readable; the game would show tags in its popup as letters
        return LanguageText.RightToLeft
            ? LanguageText.Shape(RichText.Strip(text.ToString()))
            : text.ToString();
    }

    /// <summary>
    /// The game's popup with <paramref name="body"/>, OK and Don't Show Again, its text in a list that scrolls
    /// </summary>
    internal static void Show(string body) =>
        PopupScreen.instance.SafelyQueue(screen =>
        {
            var before = Popups().Select(popup => popup.Pointer).ToHashSet();
            screen.ShowPopup(PopupScreen.Placement.menuCenter, Texts.WhatsNew.T(), body,
                new Action(Restore), Texts.Ok.T(),
                new Action(() =>
                {
                    Restore();
                    TurnOff();
                }), Texts.DontShowAgain.T(), Popup.TransitionAnim.Scale);
            MelonLoader.MelonCoroutines.Start(ScrollWhenShown(before));
        });

    /// <summary>The name of the list that scrolls, next to the popup's own text, which it stands in for</summary>
    public const string ScrollName = "CbcWhatsNewScroll";

    /// <summary>The scroll bar's width and its gap to the text, in the popup's units</summary>
    private const float BarWidth = 28, BarGap = 24;

    /// <summary>The popup being shown with the list, to give back its own text when it closes</summary>
    private static Popup? scrolled;

    private static Popup[] Popups() =>
        PopupScreen.instance == null ? [] : PopupScreen.instance.GetComponentsInChildren<Popup>().ToArray();

    /// <summary>The list in <paramref name="popup"/>, or null when it has none</summary>
    public static ScrollRect? ScrollOf(Popup popup)
    {
        if (popup.body == null) return null;
        var list = popup.body.transform.parent.Find(ScrollName);
        return list == null ? null : list.GetComponent<ScrollRect>();
    }

    /// <summary>
    /// Waits for the popup <see cref="Show"/> opened, the one that wasn't there before, puts its text in a list that
    /// scrolls, and keeps the list where the text is while the popup opens and lays itself out
    /// </summary>
    private static System.Collections.IEnumerator ScrollWhenShown(HashSet<IntPtr> before)
    {
        var start = Time.realtimeSinceStartup;
        Popup? popup = null;
        while (popup == null && Time.realtimeSinceStartup - start < 5)
        {
            popup = Popups().FirstOrDefault(found => !before.Contains(found.Pointer) && found.body != null);
            if (popup == null) yield return null;
        }
        if (popup == null)
        {
            ModHelper.Warning<CustomBossChallenge>("The What's new popup wasn't found to make its text scroll");
            yield break;
        }

        var list = AddScroll(popup);
        var text = popup.body.rectTransform;
        while (list != null && text != null)
        {
            Follow(list, text);
            yield return null;
        }
    }

    /// <summary>The list where the popup's own text is</summary>
    private static void Follow(RectTransform list, RectTransform text)
    {
        list.pivot = text.pivot;
        list.position = text.position;
        list.sizeDelta = text.rect.size;
    }

    /// <summary>
    /// A copy of the popup's text, at <see cref="FontSize"/> and as tall as it needs, in a masked view that scrolls
    /// with the mouse wheel, by dragging or with the bar on its right. The popup's own text is hidden meanwhile.
    /// </summary>
    private static RectTransform AddScroll(Popup popup)
    {
        var body = popup.body;
        var parent = body.transform.parent;
        var old = parent.Find(ScrollName);
        if (old != null) UnityEngine.Object.Destroy(old.gameObject);
        scrolled = popup;

        var root = Child(ScrollName, parent);
        root.SetSiblingIndex(body.transform.GetSiblingIndex() + 1);
        root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        Follow(root, body.rectTransform);

        var viewport = Child("Viewport", root);
        Stretch(viewport, BarWidth + BarGap);
        // Clear, but there for the mouse wheel and dragging
        viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0);
        viewport.gameObject.AddComponent<RectMask2D>();

        // The game has set the text already: right to left in Arabic, in the language's font
        var content = UnityEngine.Object.Instantiate(body.gameObject, viewport, false);
        content.name = "Text";
        var text = content.GetComponent<TMP_Text>();
        text.enabled = true;
        text.enableAutoSizing = false;
        text.fontSize = FontSize;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var bar = Child("Scrollbar", root);
        bar.anchorMin = new Vector2(1, 0);
        bar.anchorMax = Vector2.one;
        bar.pivot = new Vector2(1, 0.5f);
        bar.sizeDelta = new Vector2(BarWidth, 0);
        bar.anchoredPosition = Vector2.zero;
        Rounded(bar.gameObject.AddComponent<Image>(), popup, new Color(0, 0, 0, 0.25f));
        var area = Child("Sliding Area", bar);
        Stretch(area, 0);
        var handle = Child("Handle", area);
        Stretch(handle, 0);
        var handleImage = handle.gameObject.AddComponent<Image>();
        Rounded(handleImage, popup, Color.white);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = FontSize * 2;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalNormalizedPosition = 1;

        body.enabled = false;
        return root;
    }

    private static RectTransform Child(string name, Transform parent)
    {
        var child = new GameObject(name);
        var rect = child.AddComponent<RectTransform>();
        child.transform.SetParent(parent, false);
        return rect;
    }

    /// <summary>Fills its parent, but <paramref name="right"/> from its right edge</summary>
    private static void Stretch(RectTransform rect, float right)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-right, 0);
    }

    /// <summary>A bar with round ends</summary>
    private static void Rounded(Image image, Popup popup, Color color)
    {
        image.sprite = EditorRows.RoundedFill;
        image.type = Image.Type.Sliced;
        // The sprite's corners as round as half the bar is wide
        var unitScale = (popup.GetComponentInParent<Canvas>()?.referencePixelsPerUnit ?? 100) / 100f;
        image.pixelsPerUnitMultiplier = unitScale * EditorRows.ListFrameRadius / (BarWidth / 2);
        image.color = color;
    }

    /// <summary>
    /// The game uses its popups again for other texts, so the list goes and the popup's own text comes back
    /// </summary>
    private static void Restore()
    {
        var popup = scrolled;
        scrolled = null;
        if (popup == null || popup.body == null) return;
        popup.body.enabled = true;
        var list = popup.body.transform.parent.Find(ScrollName);
        if (list != null) UnityEngine.Object.Destroy(list.gameObject);
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
