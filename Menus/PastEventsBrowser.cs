using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The Past Events window (#146), opened from its row in the Boss Creator: every official boss event, newest first,
/// with a search field and filters for the boss, the year and the mode. Each event opens as normal or Elite with its
/// own rules, after asking, as one Undo step. Like the tier boss picker, it covers the editor, and a click outside it
/// or Esc closes it; the arrows or Tab move through its controls and Enter presses one.
/// <br/>
/// The list is virtual: only the rows that fit in the window (and one more on each side) exist, and they show other
/// events as it scrolls. A row per event, built all at once, took seconds to open (#146).
/// </summary>
public sealed class PastEventsBrowser
{
    public const string Name = "CustomBossPastEvents";
    public const float Width = 2900;
    public const float Height = 1800;
    private const float Margin = 60;
    private const float FilterTop = 230;
    private const float FilterHeight = 130;
    private const float ListTop = 340;
    private const float ListHeight = Height - ListTop - Margin;
    private const float ListPadding = 20;
    public const float RowHeight = 190;
    private const float RowGap = 16;
    private const float RowStep = RowHeight + RowGap;
    private const float PortraitSize = 160;
    public const float ChoiceWidth = 330;
    private const float ChoiceHeight = 130;
    private const float DisabledAlpha = 0.45f;

    /// <summary>The window on the screen, or null</summary>
    public static PastEventsBrowser? Current => current != null && current.Root != null ? current : null;

    private static PastEventsBrowser? current;

    /// <summary>What the list showed when it was last closed, so it opens on the same events again</summary>
    private static PastEventQuery lastQuery = new();

    public GameObject Root { get; }
    public ModHelperPanel Panel { get; }
    public ModHelperInputField Search { get; }
    public ModHelperDropdown BossFilter { get; }
    public ModHelperDropdown YearFilter { get; }
    public ModHelperDropdown ModeFilter { get; }
    public ModHelperText Count { get; }
    public ModHelperText Empty { get; }
    public ModHelperScrollPanel List { get; }
    public ModHelperButton Close { get; }

    /// <summary>Every event, newest first</summary>
    public IReadOnlyList<PastEvent> All => all;

    /// <summary>The events the query picks, in the list's order</summary>
    public IReadOnlyList<PastEvent> Shown => shown;

    /// <summary>The rows that exist, each showing one of <see cref="Shown"/> or hidden</summary>
    public IReadOnlyList<EventRow> Rows => rows;

    /// <summary>
    /// One of the list's rows, showing the event at <see cref="Index"/> in <see cref="Shown"/>, or -1 while hidden
    /// </summary>
    public sealed class EventRow
    {
        public GameObject Row { get; init; } = null!;
        public RectTransform Rect { get; init; } = null!;
        public ModHelperImage Portrait { get; init; } = null!;
        public ModHelperText Title { get; init; } = null!;
        public ModHelperText Details { get; init; } = null!;
        public ModHelperText NoRules { get; init; } = null!;
        public ModHelperButton Normal { get; init; } = null!;
        public ModHelperButton Elite { get; init; } = null!;
        public int Index { get; set; } = -1;
        public PastEvent? Event { get; set; }
    }

    public PastEventQuery Query { get; private set; }

    /// <summary>The events the file had when the list was filled, to see when the download brings others</summary>
    private PastEventsFile? shownFile;

    private readonly BossEditorSection section;
    private List<PastEvent> all = new();
    private List<PastEvent> shown = new();
    private readonly List<EventRow> rows = new();

    /// <summary>What a search looks in, and the rows' texts, worked out once per event</summary>
    private readonly Dictionary<string, string> searchTexts = new();
    private readonly Dictionary<string, (string Title, string Details)> rowTexts = new();

    private readonly List<string?> bossChoices = new();
    private readonly List<int?> yearChoices = new();
    private readonly List<string?> modeChoices = new();
    private bool refreshing;

    /// <summary>
    /// Whether the window is built: the search field reports its first text while it is made, before the list exists
    /// </summary>
    private bool built;

    /// <summary>The stop with the keyboard frame, or -1; see <see cref="Stops"/></summary>
    public int Focused { get; private set; } = -1;

    /// <summary>The window's listeners, let go when it closes (#235)</summary>
    private readonly Listeners listeners = new();

    private PastEventsBrowser(BossEditorSection section)
    {
        // Mod Helper leaves an empty text asset behind for every component (#235)
        using var leftovers = new ModHelperLeftovers();
        using var building = listeners.Build();
        this.section = section;
        Query = lastQuery;

        // Covers the whole editor, so a click anywhere else closes the window instead of reaching the editor
        var shade = ModHelperPanel.Create(new Info(Name, InfoPreset.FillParent));
        Root = shade.gameObject;
        Root.transform.SetParent(section.Editor.transform, false);
        Root.transform.SetAsLastSibling();
        Root.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);
        var closeOutside = Root.AddComponent<Button>();
        closeOutside.transition = Selectable.Transition.None;
        closeOutside.onClick.AddListener(Listeners.Wrap(Hide));

        Panel = shade.AddPanel(new Info("Panel", 0, 0, Width, Height), VanillaSprites.MainBGPanelBlue);
        Panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        var title = Panel.AddText(new Info("Title", 0, -40, Width - 400, 110)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1)
        }, Texts.PastEventsTitle.T(), 80);
        // Fonts with tall lines, like Japanese, would otherwise not fit the title's height and show nothing (#234)
        BossEditorSection.ShrinkToFit(title.Text, 80, 48);
        Close = Panel.AddButton(new Info("Close", -40, -40, 130, 130)
        {
            Anchor = new Vector2(1, 1), Pivot = new Vector2(1, 1)
        }, VanillaSprites.CloseBtn, Listeners.Wrap(Hide));

        // The filter line: search, boss, year, mode, and how many events are shown
        var x = -Width / 2 + Margin;
        Info FilterInfo(string name, float width)
        {
            var info = new Info(name, x + width / 2, -FilterTop, width, FilterHeight) {Anchor = new Vector2(0.5f, 1)};
            x += width + 30;
            return info;
        }

        Search = ModHelperInputField.Create(FilterInfo("Search", 900), Query.Text, VanillaSprites.BlueInsertPanelRound,
            Listeners.Wrap<string>(text =>
            {
                if (!refreshing && built) SetQuery(Query with {Text = text});
            }), 60, TMP_InputField.CharacterValidation.None, TextAlignmentOptions.Left, Texts.PastEventsSearch.T(), 60);
        Search.transform.SetParent(Panel.transform, false);
        // The hint stays on one line in every language, a little smaller where it's long or its font tall (#234)
        if (Search.InputField.placeholder?.TryCast<TMP_Text>() is { } hint) BossEditorSection.ShrinkToFit(hint, 60, 34);
        Search.InputField.onValidateInput = new Func<string, int, char, char>((_, _, added) => added == '\t' ? '\0' : added);
        Search.InputField.onSubmit.AddListener(Listeners.Wrap<string>(_ => FocusFirstEvent()));

        BossFilter = Filter(FilterInfo("BossFilter", 470), index => SetQuery(Query with {Boss = bossChoices[index]}));
        YearFilter = Filter(FilterInfo("YearFilter", 360), index => SetQuery(Query with {Year = yearChoices[index]}));
        ModeFilter = Filter(FilterInfo("ModeFilter", 560), index => SetQuery(Query with {Mode = modeChoices[index]}));
        Count = Panel.AddText(new Info("Count", -Margin, -FilterTop, Width / 2 - Margin - x, FilterHeight)
        {
            Anchor = new Vector2(1, 1), Pivot = new Vector2(1, 0.5f)
        }, "", 52, TextAlignmentOptions.Right);
        Count.Text.enableAutoSizing = true;
        Count.Text.fontSizeMax = 52;
        Count.Text.fontSizeMin = 30;

        List = ModHelperScrollPanel.Create(new Info("List", 0, -ListTop, Width - 2 * Margin, ListHeight)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1)
        }, RectTransform.Axis.Vertical, VanillaSprites.BlueInsertPanelRound, RowGap, (int) ListPadding);
        List.transform.SetParent(Panel.transform, false);
        List.ScrollRect.scrollSensitivity = 60;
        // Clipped by its rectangle, not by Mod Helper's stencil mask: the search field's own mask is drawn just before
        // it, and with both on the stencil the rows scrolled past the top showed through behind the search field
        var viewport = List.ScrollRect.viewport != null ? List.ScrollRect.viewport.gameObject : List.gameObject;
        if (List.Mask != null) List.Mask.enabled = false;
        foreach (var mask in viewport.GetComponents<Mask>()) mask.enabled = false;
        if (viewport.GetComponent<RectMask2D>() == null) viewport.AddComponent<RectMask2D>();
        // The rows are placed by hand, not by Mod Helper's layout, and the content is as high as every event's row
        foreach (var layout in List.ScrollContent.GetComponents<LayoutGroup>()) layout.enabled = false;
        if (List.ContentSizeFitter != null) List.ContentSizeFitter.enabled = false;
        var content = List.ScrollContent.RectTransform;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        List.ScrollRect.onValueChanged.AddListener(Listeners.Wrap<Vector2>(_ => ShowRows()));

        // Enough rows for a full window and one more above and below while it scrolls
        for (var i = 0; i < Mathf.CeilToInt(ListHeight / RowStep) + 2; i++) rows.Add(BuildRow(i));

        Empty = Panel.AddText(new Info("Empty", 0, -ListTop - 200, Width - 400, 200)
        {
            Anchor = new Vector2(0.5f, 1)
        }, "", 60);
        Empty.Text.raycastTarget = false;

        built = true;
        FitScreen();
        Refresh();
        PastEvents.Changed += OnEventsChanged;
    }

    /// <summary>Opens the window on the Boss Creator, closing one that is open</summary>
    public static PastEventsBrowser Open(BossEditorSection section)
    {
        // current, not Current: a window whose root is already gone must still stop following PastEvents.Changed
        current?.Hide();
        current = new PastEventsBrowser(section);
        return current;
    }

    /// <summary>Closes the window on this editor (Back, or the editor turning into the normal Challenge Editor)</summary>
    public static void CloseOn(ChallengeEditor editor)
    {
        if (Current is { } browser && browser.section.Editor != null && browser.section.Editor.Pointer == editor.Pointer)
        {
            browser.Hide();
        }

        var leftover = editor.transform.Find(Name);
        if (leftover != null) Object.Destroy(leftover.gameObject);
    }

    public void Hide()
    {
        PastEvents.Changed -= OnEventsChanged;
        lastQuery = Query;
        if (Root != null) Object.Destroy(Root);
        if (current == this) current = null;
        FocusFrame.Hide();
        listeners.Release();
    }

    /// <summary>The filter whose list is open, which then has the arrows, Enter and Esc to itself</summary>
    public TMP_Dropdown? OpenList =>
        new[] {BossFilter, YearFilter, ModeFilter}.Select(filter => filter.Dropdown)
            .FirstOrDefault(dropdown => dropdown != null && dropdown.IsExpanded);

    /// <summary>A narrow screen (4:3) gets the whole window, a little smaller</summary>
    private void FitScreen()
    {
        var area = Root.GetComponent<RectTransform>().rect;
        if (area.width <= 0 || area.height <= 0) return;
        var scale = Mathf.Min(1, (area.width - 80) / Width, (area.height - 80) / Height);
        Panel.RectTransform.localScale = new Vector3(scale, scale, 1);
    }

    private ModHelperDropdown Filter(Info info, Action<int> picked)
    {
        var filter = ModHelperDropdown.Create(info, new Il2CppSystem.Collections.Generic.List<string>(), 900,
            Listeners.Wrap<int>(index =>
            {
                if (!refreshing) picked(index);
            }), VanillaSprites.BlueInsertPanelRound, 56);
        filter.transform.SetParent(Panel.transform, false);
        EditorRows.FrameList(filter);
        foreach (var text in new[] {filter.Dropdown.captionText, filter.Dropdown.itemText})
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = 34;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            // Already translated: the game's localizer would take a boss's or a map's name for a key
            EditorRows.KeepAsIs(text);
        }
        return filter;
    }

    /// <summary>
    /// PastEvents.Changed also comes when the download only starts or ends (a 304, or a failure): then the same events
    /// stay where they are, and only the count and the empty text are updated
    /// </summary>
    private void OnEventsChanged()
    {
        if (Root == null) return;
        if (ReferenceEquals(PastEvents.Current, shownFile)) ShowCount();
        else Refresh();
    }

    /// <summary>
    /// Takes the events again and fills the filters' choices when the events changed, and shows what the query picks
    /// </summary>
    public void Refresh()
    {
        var file = PastEvents.Current;
        if (!ReferenceEquals(file, shownFile))
        {
            shownFile = file;
            all = file == null ? new List<PastEvent>() : PastEventsFormat.NewestFirst(file.Events).ToList();
            searchTexts.Clear();
            rowTexts.Clear();
            ClearFocus();
            BuildFilters(file);
        }

        ApplyQuery();
    }

    /// <summary>One of the rows the list reuses; <see cref="Bind"/> gives it an event</summary>
    private EventRow BuildRow(int number)
    {
        var width = Width - 2 * Margin - 2 * ListPadding;
        var row = ModHelperPanel.Create(new Info($"Row{number}", 0, 0, width, RowHeight)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1)
        }, VanillaSprites.MainBGPanelBlue);
        row.transform.SetParent(List.ScrollContent.transform, false);

        var portrait = row.AddImage(new Info("Portrait", 30 + PortraitSize / 2, 0, PortraitSize, PortraitSize)
        {
            Anchor = new Vector2(0, 0.5f)
        }, VanillaSprites.BossChallenge);
        portrait.Image.raycastTarget = false;

        var textLeft = 30 + PortraitSize + 40;
        var textWidth = width - textLeft - 2 * ChoiceWidth - 3 * 30;
        var title = row.AddText(new Info("Name", textLeft + textWidth / 2, 40, textWidth, 90)
        {
            Anchor = new Vector2(0, 0.5f)
        }, "", 62, TextAlignmentOptions.Left);
        FitLine(title, 62, 40);
        var details = row.AddText(new Info("Details", textLeft + textWidth / 2, -42, textWidth, 80)
        {
            Anchor = new Vector2(0, 0.5f)
        }, "", 46, TextAlignmentOptions.Left);
        FitLine(details, 46, 30);

        var noRules = row.AddText(new Info("NoRules", -30 - ChoiceWidth - 15, 0, 2 * ChoiceWidth + 30, ChoiceHeight)
        {
            Anchor = new Vector2(1, 0.5f)
        }, "", 44);
        noRules.Text.enableAutoSizing = true;
        noRules.Text.fontSizeMax = 44;
        noRules.Text.fontSizeMin = 28;
        noRules.Text.raycastTarget = false;
        ShowAsIs(noRules.Text, Texts.PastEventNoRules.T());

        EventRow? eventRow = null;
        var normal = Choice(row, "Normal", -30 - ChoiceWidth * 1.5f - 30, Texts.PastEventNormal.T(),
            () => PickRow(eventRow!, false));
        var elite = Choice(row, "Elite", -30 - ChoiceWidth / 2,
            $"<color={BossEditorSection.ExampleColor}>{Texts.Elite.T()}</color>", () => PickRow(eventRow!, true));
        eventRow = new EventRow
        {
            Row = row.gameObject, Rect = row.gameObject.GetComponent<RectTransform>(), Portrait = portrait, Title = title, Details = details, NoRules = noRules,
            Normal = normal, Elite = elite
        };
        row.gameObject.SetActive(false);
        return eventRow;
    }

    private void PickRow(EventRow row, bool elite)
    {
        if (row.Event != null) Pick(row.Event, elite);
    }

    private static ModHelperButton Choice(ModHelperPanel row, string name, float x, string label, Action pressed)
    {
        var button = row.AddButton(new Info(name, x, 0, ChoiceWidth, ChoiceHeight)
        {
            Anchor = new Vector2(1, 0.5f)
        }, name == "Elite" ? VanillaSprites.BlueBtnLong : VanillaSprites.GreenBtnLong, Listeners.Wrap(pressed));
        var text = button.AddText(new Info("Label", InfoPreset.FillParent), "", 60);
        ShowAsIs(text.Text, label);
        text.Text.enableAutoSizing = true;
        text.Text.fontSizeMax = 60;
        text.Text.fontSizeMin = 32;
        text.Text.raycastTarget = false;
        return button;
    }

    private static void FitLine(ModHelperText text, float max, float min)
    {
        text.Text.enableAutoSizing = true;
        text.Text.fontSizeMax = max;
        text.Text.fontSizeMin = min;
        text.Text.textWrappingMode = TextWrappingModes.NoWrap;
        text.Text.overflowMode = TextOverflowModes.Ellipsis;
        text.Text.raycastTarget = false;
        ShowAsIs(text.Text, "");
    }

    /// <summary>Makes a row show the event at <paramref name="index"/> in <see cref="Shown"/></summary>
    private void Bind(EventRow row, int index)
    {
        var pastEvent = shown[index];
        row.Rect.anchoredPosition = new Vector2(0, -(ListPadding + index * RowStep));
        if (!row.Row.activeSelf) row.Row.SetActive(true);
        if (row.Index == index && ReferenceEquals(row.Event, pastEvent)) return;
        row.Index = index;
        row.Event = pastEvent;

        var boss = PastEventRules.Boss(pastEvent.BossKey);
        row.Portrait.Image.SetSprite(boss is { } known ? BossEditorSection.Portrait(known, false) : VanillaSprites.BossChallenge);
        var (title, details) = TextsOf(pastEvent);
        ShowAsIs(row.Title.Text, title);
        ShowAsIs(row.Details.Text, details);

        row.Normal.gameObject.SetActive(pastEvent.Standard != null);
        row.Elite.gameObject.SetActive(pastEvent.Elite != null);
        row.NoRules.gameObject.SetActive(!pastEvent.HasRules);
        // An event without rules is shown faded
        var alpha = pastEvent.HasRules ? 1 : DisabledAlpha;
        foreach (var graphic in new Graphic[] {row.Portrait.Image, row.Title.Text, row.Details.Text, row.NoRules.Text})
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }

    private (string Title, string Details) TextsOf(PastEvent pastEvent)
    {
        if (rowTexts.TryGetValue(pastEvent.Id, out var texts)) return texts;
        texts = ($"<b>{BossEditorSection.PlainText(pastEvent.DisplayName)}</b>  <color=#C8D8FF>{Dates(pastEvent)}</color>",
            Details(pastEvent));
        rowTexts[pastEvent.Id] = texts;
        return texts;
    }

    /// <summary>
    /// Gives the rows the events that are in view, after scrolling or when the events shown changed
    /// </summary>
    private void ShowRows()
    {
        var top = List.ScrollContent.RectTransform.anchoredPosition.y;
        var first = Mathf.Max(0, Mathf.FloorToInt((top - ListPadding) / RowStep));
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var index = first + i;
            if (index < shown.Count) Bind(row, index);
            else
            {
                if (row.Row.activeSelf) row.Row.SetActive(false);
                row.Index = -1;
                row.Event = null;
            }
        }

        // The keyboard frame stays on its event while the rows move; it hides when that event scrolls out of view
        if (Focused >= 0 && Focused < Stops.Count && Stops[Focused].EventIndex >= 0)
        {
            if (StopObject(Stops[Focused]) is { } target) FocusFrame.Show(target);
            else FocusFrame.Hide();
        }
    }

    /// <summary>"17 Jan 2025 - 22 Jan 2025" in the player's language, with "~" in front when the archive only knows the dates roughly</summary>
    public static string Dates(PastEvent pastEvent)
    {
        // Written whole, the way the player's language writes a date ("17. okt. 2026", "2026年10月17日"): a shortened
        // range like "17-22 Oct 2026" only works for some languages
        var start = Texts.Date(pastEvent.StartDate);
        var end = Texts.Date(pastEvent.EndDate);
        var dates = start == end ? start : $"{start} - {end}";
        return pastEvent.DatesGuessed ? "~" + dates : dates;
    }

    /// <summary>
    /// Shows text that is already translated, with its rich text tags. Set through the game's localizer, the text is
    /// taken for a key and spaced out, which breaks a colour like #C8D8FF into "#C8 D8 F F" and shows the tag: the
    /// localizer is turned off first (<see cref="EditorRows.SetText"/>), and only then is the text set.
    /// </summary>
    public static void ShowAsIs(TMP_Text text, string value)
    {
        text.richText = true;
        EditorRows.SetText(text, value);
    }

    /// <summary>
    /// The map, mode and difficulty of the normal event, the Elite one's where they differ, how both were ranked, and
    /// which event replaced it
    /// </summary>
    private string Details(PastEvent pastEvent)
    {
        var parts = new List<string>();
        var rules = pastEvent.Standard ?? pastEvent.Elite;
        if (rules != null)
        {
            var map = ShownName((string?) rules["map"]);
            var eliteMap = pastEvent.Elite != null && pastEvent.Standard != null &&
                           (string?) pastEvent.Elite["map"] != (string?) rules["map"]
                ? ShownName((string?) pastEvent.Elite["map"])
                : null;
            parts.Add(eliteMap == null ? map : $"{map} / {Texts.Elite.T()}: {eliteMap}");
            var modes = PastEventFilter.Modes(pastEvent).Select(ShownName).ToList();
            parts.Add(string.Join(" / ", modes));
            parts.Add(ShownName((string?) rules["difficulty"]));
        }

        var normalScoring = pastEvent.NormalScoring;
        var eliteScoring = pastEvent.EliteScoring;
        if (normalScoring != null || eliteScoring != null)
        {
            parts.Add(Texts.PastEventRanked.T(Scoring(normalScoring), Scoring(eliteScoring)));
        }

        var line = BossEditorSection.PlainText(string.Join("  |  ", parts.Where(part => part.Length > 0)));
        var replacedBy = pastEvent.ReplacedBy.Select(DisplayNameOf).ToList();
        if (replacedBy.Count > 0)
        {
            line += $"  <color=#FFB0A0>{BossEditorSection.PlainText(Texts.PastEventReplacedBy.T(string.Join(", ", replacedBy)))}</color>";
        }
        return line;
    }

    private string DisplayNameOf(string id) =>
        all.FirstOrDefault(e => e.Id == id)?.DisplayName ?? id;

    /// <summary>How an event was ranked, in the player's language; "?" when not known</summary>
    public static string Scoring(string? scoring) => scoring switch
    {
        "GameTime" => Texts.ScoringTime.T(),
        "LeastCash" => Texts.ScoringLeastCash.T(),
        "LeastTiers" => Texts.ScoringLeastTiers.T(),
        _ => "?"
    };

    /// <summary>The name the player sees of a map, mode or difficulty id, from the game's own texts</summary>
    public static string ShownName(string? id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var shownName = id.T();
        return string.IsNullOrEmpty(shownName) ? id : shownName;
    }

    private void BuildFilters(PastEventsFile? file)
    {
        var events = file?.Events ?? [];
        refreshing = true;
        try
        {
            bossChoices.Clear();
            var bosses = new Il2CppSystem.Collections.Generic.List<string>();
            bossChoices.Add(null);
            bosses.Add(Texts.AnyBoss.T());
            foreach (var boss in Enum.GetValues<BossType>())
            {
                var key = boss.ToString().ToLowerInvariant();
                if (!events.Any(e => e.BossKey == key)) continue;
                bossChoices.Add(key);
                bosses.Add(boss.ToString());
            }
            SetChoices(BossFilter, bosses, bossChoices.IndexOf(Query.Boss));

            yearChoices.Clear();
            var years = new Il2CppSystem.Collections.Generic.List<string>();
            yearChoices.Add(null);
            years.Add(Texts.AnyYear.T());
            foreach (var year in PastEventFilter.Years(events))
            {
                yearChoices.Add(year);
                years.Add(year.ToString(CultureInfo.InvariantCulture));
            }
            SetChoices(YearFilter, years, yearChoices.IndexOf(Query.Year));

            modeChoices.Clear();
            var modes = new Il2CppSystem.Collections.Generic.List<string>();
            modeChoices.Add(null);
            modes.Add(Texts.AnyMode.T());
            foreach (var mode in PastEventFilter.AllModes(events))
            {
                modeChoices.Add(mode);
                modes.Add(ShownName(mode));
            }
            SetChoices(ModeFilter, modes, modeChoices.IndexOf(Query.Mode));

            // A filter whose choice the new events don't have any more shows every event again
            Query = Query with
            {
                Boss = bossChoices.Contains(Query.Boss) ? Query.Boss : null,
                Year = yearChoices.Contains(Query.Year) ? Query.Year : null,
                Mode = modeChoices.Contains(Query.Mode) ? Query.Mode : null
            };
        }
        finally
        {
            refreshing = false;
        }
    }

    private static void SetChoices(ModHelperDropdown filter, Il2CppSystem.Collections.Generic.List<string> choices, int index)
    {
        var dropdown = filter.Dropdown;
        dropdown.ClearOptions();
        dropdown.AddOptions(choices);
        dropdown.SetValueWithoutNotify(Math.Max(index, 0));
        dropdown.RefreshShownValue();

        // Mod Helper sizes the opened list for the options it was created with
        var template = filter.TemplatePanel.RectTransform;
        var itemHeight = filter.RectTransform.sizeDelta.y;
        var listHeight = Math.Min(900, Math.Max(choices.Count, 1) * itemHeight);
        template.sizeDelta = new Vector2(template.sizeDelta.x, listHeight);
        template.anchoredPosition = new Vector2(template.anchoredPosition.x, -itemHeight / 2 - listHeight / 2);
    }

    /// <summary>Shows the events of another query, as the search field and the filters change it</summary>
    public void SetQuery(PastEventQuery query)
    {
        Query = query;
        lastQuery = query;
        ApplyQuery();
    }

    private string SearchTextOf(PastEvent pastEvent)
    {
        if (!searchTexts.TryGetValue(pastEvent.Id, out var text))
        {
            searchTexts[pastEvent.Id] = text = PastEventFilter.SearchText(pastEvent, ShownName);
        }
        return text;
    }

    private void ApplyQuery()
    {
        shown = all.Where(e => PastEventFilter.Matches(e, Query, () => SearchTextOf(e))).ToList();
        stopList = null;

        // A new list starts at its top, as high as its rows
        var content = List.ScrollContent.RectTransform;
        content.sizeDelta = new Vector2(0, shown.Count == 0 ? 0 : 2 * ListPadding + shown.Count * RowStep - RowGap);
        List.ScrollRect.StopMovement();
        content.anchoredPosition = Vector2.zero;
        foreach (var row in rows) row.Index = -1;
        ClearFocus();
        ShowRows();
        ShowCount();
    }

    /// <summary>The count and, when no event is shown, why</summary>
    private void ShowCount()
    {
        ShowAsIs(Count.Text, Texts.PastEventsCount.T(shown.Count, all.Count));
        ShowAsIs(Empty.Text, all.Count == 0 ? EmptyText() : shown.Count == 0 ? Texts.PastEventsNoMatch.T() : "");
        Empty.gameObject.SetActive(shown.Count == 0);
    }

    private static string EmptyText() =>
        PastEvents.Loading ? Texts.PastEventsLoading.T() : Texts.PastEventsNone.T();

    /// <summary>
    /// The row showing an event's Normal or Elite button, scrolled into view first; null for an event the query
    /// doesn't show, or without those rules. For the keyboard and the tests.
    /// </summary>
    public ModHelperButton? ButtonFor(PastEvent pastEvent, bool elite)
    {
        var index = shown.IndexOf(pastEvent);
        if (index < 0 || pastEvent.Rules(elite) == null) return null;
        ScrollTo(index);
        var row = rows.FirstOrDefault(row => row.Index == index);
        return row == null ? null : elite ? row.Elite : row.Normal;
    }

    /// <summary>
    /// Asks before replacing the setup with the event's, and says which of its rules the Boss Creator leaves out. An
    /// event that can't be opened says why.
    /// </summary>
    public void Pick(PastEvent pastEvent, bool elite)
    {
        if (!PastEventSetups.TryBuild(pastEvent, elite, out var setup, out var dcm) || dcm == null)
        {
            var why = setup.Problem switch
            {
                PastEventProblem.MissingMap => Texts.PastEventMissingMap.T(BossEditorSection.PlainText(setup.Map ?? "?")),
                PastEventProblem.UnknownBoss => Texts.PastEventUnknownBoss.T(),
                PastEventProblem.NoRules => Texts.PastEventCantOpen.T(),
                // The rules were read, but the challenge couldn't be made of them
                _ => Texts.PastEventOpenFailed.T()
            };
            ModHelper.Msg<CustomBossChallenge>($"Could not open the past event {pastEvent.Id}{(elite ? " (Elite)" : "")}: {setup.Problem}");
            PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(why, null));
            return;
        }

        var options = setup.Options;
        var text = $"{Texts.LoadPastEventQuestion.T()}\n\n{BossEditorSection.Summary(options, dcm)}";
        if (pastEvent.Scoring(elite) is "LeastCash" or "LeastTiers")
        {
            text += $"\n\n<size=85%>{Texts.PastEventScoring.T(Scoring(pastEvent.Scoring(elite)))}</size>";
        }
        if (setup.Unsupported.Count > 0)
        {
            text += $"\n\n<size=85%><color=#FFB0A0>{Texts.PastEventUnsupported.T(BossEditorSection.PlainText(string.Join(", ", setup.Unsupported)))}</color></size>";
        }

        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            Texts.PastEvents.T(), text,
            listeners.Of(() =>
            {
                BossMode.Replace(section.Editor, options, dcm);
                ModHelper.Msg<CustomBossChallenge>($"Opened the past event {pastEvent.Id}{(elite ? " (Elite)" : "")}: " +
                                                   $"{BossEditorSection.Describe(options, dcm)}" +
                                                   (setup.Unsupported.Count > 0 ? $"; left out {string.Join(", ", setup.Unsupported)}" : ""));
                Hide();
            }), Texts.Load.T(),
            null, Texts.Cancel.T(), Popup.TransitionAnim.Scale));
    }

    /// <summary>
    /// A place the keyboard can be: one of the window's own controls, or the Normal or Elite button of the event at
    /// <see cref="EventIndex"/> in <see cref="Shown"/>, which only has a row while it is in view
    /// </summary>
    public readonly record struct Stop(GameObject? Control, int EventIndex, bool Elite);

    /// <summary>
    /// What the keyboard moves through, in order: the search field, the filters, the buttons of every event shown, and
    /// Close
    /// </summary>
    public IReadOnlyList<Stop> Stops => stopList ??= BuildStops();

    /// <summary>The stops of the events shown, made again only when those change (<see cref="ApplyQuery"/>)</summary>
    private List<Stop>? stopList;

    private List<Stop> BuildStops()
    {
        var stops = new List<Stop>
        {
            new(Search.gameObject, -1, false), new(BossFilter.gameObject, -1, false),
            new(YearFilter.gameObject, -1, false), new(ModeFilter.gameObject, -1, false)
        };
        for (var i = 0; i < shown.Count; i++)
        {
            if (shown[i].Standard != null) stops.Add(new Stop(null, i, false));
            if (shown[i].Elite != null) stops.Add(new Stop(null, i, true));
        }
        stops.Add(new Stop(Close.gameObject, -1, false));
        return stops;
    }

    /// <summary>The control a stop is on the screen, or null for an event that is out of view</summary>
    private GameObject? StopObject(Stop stop)
    {
        if (stop.Control != null) return stop.Control;
        var row = rows.FirstOrDefault(row => row.Index == stop.EventIndex && row.Row.activeSelf);
        return row == null ? null : (stop.Elite ? row.Elite : row.Normal).gameObject;
    }

    /// <summary>A key press while the window is open; the Boss Creator's own keys wait until it is closed</summary>
    public void Handle(ShortcutKey key, bool shift = false)
    {
        switch (key)
        {
            case ShortcutKey.Up or ShortcutKey.Left:
                Move(-1);
                break;
            case ShortcutKey.Down or ShortcutKey.Right:
                Move(1);
                break;
            case ShortcutKey.Tab:
                Move(shift ? -1 : 1);
                break;
            case ShortcutKey.Enter or ShortcutKey.Space when Focused >= 0 && Focused < Stops.Count:
                Press(Stops[Focused]);
                break;
        }
    }

    private void Move(int step)
    {
        var stops = Stops;
        Focused = Focused < 0 ? (step > 0 ? 0 : stops.Count - 1) : ((Focused + step) % stops.Count + stops.Count) % stops.Count;
        FocusOn(stops[Focused]);
    }

    private void FocusOn(Stop stop)
    {
        if (stop.EventIndex >= 0) ScrollTo(stop.EventIndex);
        if (StopObject(stop) is { } target) FocusFrame.Show(target);
        else FocusFrame.Hide();
    }

    private void ClearFocus()
    {
        Focused = -1;
        FocusFrame.Hide();
    }

    /// <summary>Enter in the search field goes to the first event shown</summary>
    private void FocusFirstEvent()
    {
        Search.InputField.DeactivateInputField();
        var stops = Stops.ToList();
        var first = stops.FindIndex(stop => stop.EventIndex >= 0);
        if (first < 0) return;
        Focused = first;
        FocusOn(stops[first]);
    }

    private void Press(Stop stop)
    {
        if (stop.EventIndex >= 0)
        {
            Pick(shown[stop.EventIndex], stop.Elite);
            return;
        }

        var control = stop.Control!;
        if (control == Search.gameObject)
        {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(control);
            Search.InputField.ActivateInputField();
        }
        else if (control.GetComponent<TMP_Dropdown>() is { } dropdown) dropdown.Show();
        else control.GetComponent<Button>()?.onClick.Invoke();
    }

    /// <summary>Scrolls the list so the event at <paramref name="index"/> in <see cref="Shown"/> is in view</summary>
    private void ScrollTo(int index)
    {
        var content = List.ScrollContent.RectTransform;
        var top = content.anchoredPosition.y;
        var rowTop = ListPadding + index * RowStep;
        var rowBottom = rowTop + RowHeight;
        var target = rowTop - ListPadding < top ? rowTop - ListPadding
            : rowBottom + ListPadding > top + ListHeight ? rowBottom + ListPadding - ListHeight
            : top;
        target = Mathf.Clamp(target, 0, Mathf.Max(0, content.sizeDelta.y - ListHeight));
        if (Mathf.Approximately(target, top)) return;
        List.ScrollRect.StopMovement();
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, target);
        ShowRows();
    }
}
