using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The "Boss" section the Boss Creator adds to the top of the Challenge Editor's settings, plus its Play button.
/// It only exists (or is only shown) while the editor is used as the Boss Creator.
/// </summary>
public sealed class BossEditorSection
{
    public const string SectionName = "CustomBossSection";
    public const string RowsName = "Rows";
    public const string PlayName = "CustomBossPlay";
    public static string Title => Texts.BossCreator.T();
    public const string SelectedMarker = "Selected";

    /// <summary>Game modes a boss game doesn't support</summary>
    public static readonly string[] UnsupportedModes = {"Deflation", "Apopalypse"};

    private const float Padding = 150;
    private const float Spacing = 96;

    private static BossEditorSection? current;

    /// <summary>
    /// The section of the most recent Boss Creator editor, or null once that editor is gone
    /// </summary>
    public static BossEditorSection? Current
    {
        get => current != null && current.Editor != null && current.Section != null ? current : null;
        private set => current = value;
    }

    public ChallengeEditor Editor { get; }
    public GameObject Section { get; }
    public GameObject Play { get; }

    public SliderRow BossSpeed { get; private set; } = null!;
    public SliderRow BossHealth { get; private set; } = null!;
    public SliderRow Tiers { get; private set; } = null!;
    public SliderRow SpeedPerTier { get; private set; } = null!;
    public SliderRow HealthPerTier { get; private set; } = null!;
    public TierStrip Strip { get; private set; } = null!;
    public SliderRow SkullCount { get; private set; } = null!;
    public SliderRow MaxParagons { get; private set; } = null!;

    /// <summary>
    /// Fill in Spawn Rounds evenly; they show the first two tiers of whatever Spawn Rounds holds
    /// </summary>
    public SliderRow FirstSpawnRound { get; private set; } = null!;
    public SliderRow RoundsBetween { get; private set; } = null!;
    public ModHelperCheckbox Elite { get; private set; } = null!;

    /// <summary>
    /// Boss Event Rounds (#80, #84): the rounds of the official boss events, moved with the tiers
    /// </summary>
    public ModHelperCheckbox BossEventRounds { get; private set; } = null!;

    /// <summary>
    /// In CHIMPS, the Boss Event Rounds tick's place: Off, or which CHIMPS boss events' rounds (#139). The entries are
    /// <see cref="ChimpsEntry"/>'s.
    /// </summary>
    public ModHelperDropdown ChimpsEventRounds { get; private set; } = null!;

    /// <summary>The i next to <see cref="ChimpsEventRounds"/>: what each CHIMPS round set tests (#157)</summary>
    public ModHelperButton ChimpsRoundsInfo { get; private set; } = null!;

    public GameObject BossEventRoundsRow { get; private set; } = null!;

    /// <summary>
    /// The Standard and Advanced tabs at the top of the section. In Advanced Mode the tiers have bosses of their own,
    /// picked on the Tier Preview's cards, and the Boss Type row is hidden.
    /// </summary>
    public ModeTabs Mode { get; private set; } = null!;
    public GameObject BossTypeRow { get; private set; } = null!;

    /// <summary>
    /// The bosses per tier from before Advanced Mode was turned off, so turning it back on brings them back
    /// </summary>
    private string? lastTierBosses;

    /// <summary>
    /// Boss Speed, Boss Health and the skulls from before Advanced Mode, for going back to Standard. In Advanced Mode
    /// every tier has its own instead, and their rows are hidden at 100 % and Vanilla.
    /// </summary>
    private TierBoss? standardValues;

    /// <summary>
    /// The editor's slider row, which the tier picker copies for a tier's speed and health
    /// </summary>
    public ChallengeEditorSliderPanel SliderTemplate { get; private set; } = null!;
    public ModHelperDropdown SkullMode { get; private set; } = null!;
    public ModHelperInputField CustomSkulls { get; private set; } = null!;
    public SkullStrip Skulls { get; private set; } = null!;

    public ModHelperInputField SpawnRounds { get; private set; } = null!;
    public ModHelperDropdown SavedList { get; private set; } = null!;
    public ModHelperButton SaveSetup { get; private set; } = null!;
    public ModHelperButton LoadSetup { get; private set; } = null!;
    public ModHelperButton DeleteSetup { get; private set; } = null!;

    /// <summary>
    /// The saved setup picked in the list, or null when an example is picked
    /// </summary>
    public string? SelectedSetup { get; private set; }

    /// <summary>
    /// The example picked in the list, or null when a saved setup is picked
    /// </summary>
    public ExampleSetup? SelectedExample { get; private set; }

    /// <summary>
    /// The names of the saved setups, in the list's order
    /// </summary>
    private List<string> savedNames = new();

    public ModHelperButton CopyCode { get; private set; } = null!;
    public ModHelperButton PasteCode { get; private set; } = null!;
    public Dictionary<BossType, ModHelperButton> BossButtons { get; } = new();

    private readonly Transform rows;
    private ModHelperButton resetRounds = null!;

    /// <summary>
    /// The buttons that put one option back to its default: the boss, the skulls and the custom skulls
    /// </summary>
    public ModHelperButton ResetBossButton { get; private set; } = null!;
    public ModHelperButton ResetSkullsButton { get; private set; } = null!;
    public ModHelperButton ResetCustomSkullsButton { get; private set; } = null!;

    /// <summary>
    /// On the section's title line: every boss option back to its default, after asking
    /// </summary>
    public ModHelperButton ResetAllButton { get; private set; } = null!;
    private readonly List<FocusStop> focusStops = new();
    private GameObject skullModeRow = null!;
    private GameObject customSkullsRow = null!;
    private RectTransform? everything;
    private float appliedHeight;
    private bool refreshing;

    /// <summary>
    /// Which rows were shown and how high they were when the section was last fitted, see <see cref="FitIfChanged"/>
    /// </summary>
    private string? fittedLayout;

    /// <summary>
    /// How often the section was fitted into the editor this session, for the tests (#184)
    /// </summary>
    internal static int Fits { get; private set; }

    private TMP_Text? titleText;
    private string? originalTitleKey;

    private BossEditorSection(ChallengeEditor editor)
    {
        Editor = editor;
        var sliders = editor.sliderSettings;
        var area = sliders.transform.parent;

        Section = new GameObject(SectionName, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        Section.transform.SetParent(area, false);
        Section.transform.SetSiblingIndex(0);
        var sectionRect = Section.GetComponent<RectTransform>();
        var sliderRect = sliders.GetComponent<RectTransform>();
        sectionRect.anchorMin = sliderRect.anchorMin;
        sectionRect.anchorMax = sliderRect.anchorMax;
        sectionRect.pivot = sliderRect.pivot;
        sectionRect.sizeDelta = new Vector2(sliderRect.sizeDelta.x, 0);

        var background = sliders.transform.Find("SliderBg");
        if (background != null)
        {
            var copy = Object.Instantiate(background.gameObject, Section.transform, false).GetComponent<RectTransform>();
            copy.anchorMin = Vector2.zero;
            copy.anchorMax = Vector2.one;
            copy.offsetMin = copy.offsetMax = Vector2.zero;
        }

        // Section title in the same style as the editor's "Modifiers included" title
        var titleTemplate = area.Find("ModifierSettingsDisplay/Title");
        if (titleTemplate != null)
        {
            var heading = Object.Instantiate(titleTemplate.gameObject, Section.transform, false);
            heading.name = "Heading";
            var headingRect = heading.GetComponent<RectTransform>();
            headingRect.anchorMin = headingRect.anchorMax = new Vector2(0.5f, 1);
            headingRect.anchoredPosition = new Vector2(titleTemplate.GetComponent<RectTransform>().anchoredPosition.x, -36.5f);
            EditorRows.SetText(heading.GetComponent<TMP_Text>(), Texts.Boss.T());
        }

        // Inside the section's top edge, on the title's line
        ResetAllButton = ModHelperButton.Create(new Info("ResetBossOptions", -60, -50, 740, 104)
        {
            Anchor = new Vector2(1, 1), Pivot = new Vector2(1, 0.5f)
        }, VanillaSprites.BlueBtnLong, new Action(OnResetAll));
        ResetAllButton.transform.SetParent(Section.transform, false);
        ResetAllButton.AddText(new Info("Label", InfoPreset.FillParent), Texts.ResetBossOptions.T(), 52);

        var rowsObject = new GameObject(RowsName, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        rowsObject.transform.SetParent(Section.transform, false);
        var rowsRect = rowsObject.GetComponent<RectTransform>();
        rowsRect.anchorMin = new Vector2(0.5f, 1);
        rowsRect.anchorMax = new Vector2(0.5f, 1);
        rowsRect.pivot = new Vector2(0.5f, 1);
        rowsRect.anchoredPosition = new Vector2(0, -Padding);
        rowsRect.sizeDelta = new Vector2(sliders.moabSpeedPanel.GetComponent<RectTransform>().sizeDelta.x, 0);
        var layout = rowsObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = Spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        rows = rowsObject.transform;

        BuildRows(sliders.moabSpeedPanel);
        Play = BuildPlayButton();
        BuildFocusStops();
        WatchEditorRules();

        var topInfo = editor.transform.Find("TopInfo/Title");
        titleText = topInfo != null ? topInfo.GetComponent<TMP_Text>() : null;
        originalTitleKey = titleText?.TryCast<Il2Cpp.NK_TextMeshProUGUI>()?.localizeKey;
    }

    private void BuildRows(ChallengeEditorSliderPanel template)
    {
        SliderTemplate = template;
        // Boss picker and elite toggle
        var bossRow = EditorRows.LabelRow(template, rows, "BossRow", Texts.BossType.T(), VanillaSprites.BossChallenge);
        BossTypeRow = bossRow;
        var x = EditorRows.ControlX - EditorRows.ControlWidth / 2 + 100;
        foreach (var boss in Enum.GetValues<BossType>())
        {
            var type = boss;
            var button = ModHelperButton.Create(new Info($"Boss{boss}", x, 0, 190, 190)
            {
                Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
            }, Portrait(boss, false), new Action(() => SelectBoss(type)));
            button.transform.SetParent(bossRow.transform, false);
            BossButtons[boss] = button;

            // The button animates its own color and scale, so the selection is shown with a tick instead
            var marker = ModHelperImage.Create(new Info(SelectedMarker, 0, 0, 110, 110)
            {
                Anchor = new Vector2(1, 0), Pivot = new Vector2(0.75f, 0.25f)
            }, VanillaSprites.TickGreenIcon);
            marker.transform.SetParent(button.transform, false);
            marker.Image.raycastTarget = false;
            x += 215;
        }

        Elite = ModHelperCheckbox.Create(new Info("Elite", x + 30, 0, 150, 150)
        {
            Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, false, VanillaSprites.BlueInsertPanelRound, new Action<bool>(SetElite), VanillaSprites.TickGreenIcon, 20);
        Elite.transform.SetParent(bossRow.transform, false);
        var eliteLabel = ModHelperText.Create(new Info("EliteLabel", x + 230, 0, 260, 150)
        {
            Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, Texts.Elite.T(), 80, TextAlignmentOptions.Left);
        eliteLabel.transform.SetParent(bossRow.transform, false);
        ResetBossButton = EditorRows.ResetButton(bossRow, "ResetBoss", ResetBoss);

        // Above the boss: Standard, one boss for every tier, or Advanced, a boss per tier chosen on the Tier Preview's
        // cards
        var modeRow = ModeTabs.EmptyRow(template, rows, "ModeRow");
        modeRow.transform.SetSiblingIndex(bossRow.transform.GetSiblingIndex());
        Mode = new ModeTabs(modeRow, SetAdvanced);
        EditorRows.AddInfo(modeRow, Texts.DifferentBosses, Texts.DifferentBossesInfo);

        BossSpeed = SliderRow.Clone(template, rows, "BossSpeedRow", Texts.BossSpeed.T(), VanillaSprites.FasterBossIcon,
            10, 1000, 10, 100, Percent, value => UpdateDcm("BossSpeed", dcm => dcm.bloonModifiers.bossSpeedMultiplier = value / 100f));
        BossHealth = SliderRow.Clone(template, rows, "BossHealthRow", Texts.BossHealth.T(), VanillaSprites.BossBoostIcon,
            10, 2000, 10, 100, Percent, value => UpdateDcm("BossHealth", dcm => dcm.bloonModifiers.healthMultipliers.boss = value / 100f));

        // The last position means no limit
        var paragons = ParagonMath.Available;
        MaxParagons = SliderRow.Clone(template, rows, "MaxParagonsRow", Texts.MaxParagons.T(), VanillaSprites.ParagonLimitIcon,
            0, paragons, 1, paragons, value => value >= paragons ? Texts.Unlimited.T() : ShownNumber.Whole(value),
            value => UpdateDcm("MaxParagons", dcm => dcm.maxParagons = ParagonMath.FromSlider((int) value, paragons)));

        Tiers = SliderRow.Clone(template, rows, "TiersRow", Texts.BossTiers.T(), VanillaSprites.BossTiersIconSmall,
            TierMath.MinTiers, TierMath.MaxTiers, 1, TierMath.VanillaTiers, value => ShownNumber.Whole(value),
            value => UpdateOptions("Tiers", o => o with {Tiers = (int) value}));
        SpeedPerTier = SliderRow.Clone(template, rows, "SpeedPerTierRow", Texts.SpeedPerTier.T(), VanillaSprites.FasterBossIcon,
            TierMath.MinSpeedPerTier, TierMath.MaxSpeedPerTier, TierMath.PerTierStep, 0, PerTier,
            value => UpdateOptions("SpeedPerTier", o => o with {SpeedPerTier = (int) value}));
        HealthPerTier = SliderRow.Clone(template, rows, "HealthPerTierRow", Texts.HealthPerTier.T(), VanillaSprites.BossBoostIcon,
            TierMath.MinHealthPerTier, TierMath.MaxHealthPerTier, TierMath.PerTierStep, 0, PerTier,
            value => UpdateOptions("HealthPerTier", o => o with {HealthPerTier = (int) value}));
        var stripRow = EditorRows.LabelRow(template, rows, "TierStripRow", Texts.TierPreview.T(), VanillaSprites.BossT5Icon);
        Strip = new TierStrip(stripRow) {CardClicked = tier => OpenPicker(tier)};

        FirstSpawnRound = SliderRow.Clone(template, rows, "FirstSpawnRoundRow", Texts.FirstBossRound.T(),
            VanillaSprites.BossT1Icon, 1, TierMath.MaxFirstSpawnRound, 1, TierMath.FirstSpawnRound, value => ShownNumber.Whole(value),
            _ => SpreadSpawnRounds("FirstSpawnRound"));
        RoundsBetween = SliderRow.Clone(template, rows, "RoundsBetweenRow", Texts.RoundsBetweenTiers.T(),
            VanillaSprites.BossTiersIconSmall, 1, TierMath.MaxRoundsBetweenTiers, 1, TierMath.RoundsBetweenTiers,
            value => ShownNumber.Whole(value), _ => SpreadSpawnRounds("RoundsBetween"));

        var roundsRow = EditorRows.LabelRow(template, rows, "SpawnRoundsRow", Texts.SpawnRounds.T(), VanillaSprites.BossT1Icon);
        SpawnRounds = ModHelperInputField.Create(EditorRows.ControlInfo("SpawnRounds", 1700, 150),
            TierMath.FormatRounds(TierMath.DefaultSpawnRounds(TierMath.VanillaTiers)), VanillaSprites.BlueInsertPanelRound,
            null, 70, TMP_InputField.CharacterValidation.None, TextAlignmentOptions.Center, "40, 60, 80, ...", 10);
        SpawnRounds.transform.SetParent(roundsRow.transform, false);
        FollowKeyboard(SpawnRounds);
        SpawnRounds.InputField.onEndEdit.AddListener(new Action<string>(text =>
        {
            // Leaving the field unchanged keeps the rounds as they are, following the defaults if they did
            if (text == TierMath.FormatRounds(BossMode.Options.EffectiveSpawnRounds)) return;
            UpdateOptions("SpawnRounds", o => o with {SpawnRounds = text});
            ShowSpawnRounds();
        }));
        resetRounds = EditorRows.ResetButton(roundsRow, "ResetRounds", () =>
        {
            UpdateOptions(null, o => o with {SpawnRounds = null});
            ShowSpawnRounds();
        });

        // Right under the Spawn Rounds, since the event rounds move with them
        BossEventRoundsRow = EditorRows.LabelRow(template, rows, "BossEventRoundsRow", Texts.BossEventRounds.T(),
            VanillaSprites.BossChallenge);
        BossEventRounds = ModHelperCheckbox.Create(EditorRows.ControlInfo("BossEventRounds", 150, 150),
            false, VanillaSprites.BlueInsertPanelRound,
            new Action<bool>(on => UpdateOptions("BossEventRounds", o => o with {BossEventRounds = on})),
            VanillaSprites.TickGreenIcon, 20);
        BossEventRounds.transform.SetParent(BossEventRoundsRow.transform, false);
        var chimpsEntries = new Il2CppSystem.Collections.Generic.List<string>();
        chimpsEntries.Add(Texts.EventRoundsOff.T());
        chimpsEntries.Add(Texts.ChimpsRoundsNewest.T());
        chimpsEntries.Add(Texts.ChimpsRoundsEarlier.T());
        chimpsEntries.Add(Texts.ChimpsRoundsOwn.T());
        // The opened list has room for every entry, so none of them is cut off or needs scrolling
        ChimpsEventRounds = ModHelperDropdown.Create(EditorRows.ControlInfo("ChimpsEventRounds", ChimpsDropdownWidth, ChimpsItemHeight),
            chimpsEntries, chimpsEntries.Count * ChimpsItemHeight, new Action<int>(index => UpdateOptions("ChimpsEventRounds", o => o with
            {
                BossEventRounds = index > 0,
                ChimpsRounds = index switch
                {
                    1 => Config.ChimpsRounds.Newest,
                    2 => Config.ChimpsRounds.Earlier,
                    3 => Config.ChimpsRounds.Own,
                    _ => o.ChimpsRounds
                }
            })), VanillaSprites.BlueInsertPanelRound, 60);
        ChimpsEventRounds.transform.SetParent(BossEventRoundsRow.transform, false);
        EditorRows.FrameList(ChimpsEventRounds);
        // The entries are longer than the skull modes: they shrink to fit instead of running past the arrow
        foreach (var text in new[] {ChimpsEventRounds.Dropdown.captionText, ChimpsEventRounds.Dropdown.itemText})
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = SavedListMinFontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            // Already translated: the game's localizer would take "CHIMPS" for a key and space out its letters
            var nkText = text.TryCast<Il2Cpp.NK_TextMeshProUGUI>();
            if (nkText != null)
            {
                nkText.AutoLocalize = false;
                nkText.localizeKey = "";
            }
        }
        for (var i = 0; i < chimpsEntries.Count; i++) ChimpsEventRounds.Dropdown.options[i].text = chimpsEntries[i];
        ChimpsEventRounds.Dropdown.RefreshShownValue();
        ChimpsEventRounds.gameObject.SetActive(false);
        // The two sets differ in what they test, not in how hard they are, so the i right of the dropdown tells how (#157)
        ChimpsRoundsInfo = EditorRows.AddChoiceInfo(BossEventRoundsRow, "ChimpsRoundsInfo",
            EditorRows.ControlRight(ChimpsDropdownWidth) + EditorRows.ChoiceInfoSize / 2 + 30,
            Texts.BossEventRounds, Texts.ChimpsRoundsInfo);
        ChimpsRoundsInfo.gameObject.SetActive(false);

        skullModeRow = EditorRows.LabelRow(template, rows, "SkullModeRow", Texts.Skulls.T(), VanillaSprites.CrossboneSkull);
        var modes = new Il2CppSystem.Collections.Generic.List<string>();
        modes.Add(Texts.SkullsVanilla.T());
        modes.Add(Texts.SkullsEvenlySpaced.T());
        modes.Add(Texts.SkullsCustom.T());
        SkullMode = ModHelperDropdown.Create(EditorRows.ControlInfo("SkullMode", 900, 150),
            modes, 500, new Action<int>(index =>
            {
                UpdateOptions("SkullMode", o => o with {SkullMode = (Config.SkullMode) index});
            }), VanillaSprites.BlueInsertPanelRound, 70);
        SkullMode.transform.SetParent(skullModeRow.transform, false);
        EditorRows.FrameList(SkullMode);
        ResetSkullsButton = EditorRows.ResetButton(skullModeRow, "ResetSkulls", ResetSkulls);

        SkullCount = SliderRow.Clone(template, rows, "SkullCountRow", Texts.SkullCount.T(), VanillaSprites.CrossboneSkull,
            0, SkullMath.MaxSkulls, 1, 3, value => ShownNumber.Whole(value), value => UpdateOptions("SkullCount", o => o with {SkullCount = (int) value}));

        customSkullsRow = EditorRows.LabelRow(template, rows, "CustomSkullsRow", Texts.SkullHealth.T(), VanillaSprites.CrossboneSkull);
        CustomSkulls = ModHelperInputField.Create(EditorRows.ControlInfo("CustomSkulls", 1700, 150), "75, 50, 25",
            VanillaSprites.BlueInsertPanelRound, null, 70, TMP_InputField.CharacterValidation.None,
            TextAlignmentOptions.Center, "75, 50, 25", 10);
        CustomSkulls.transform.SetParent(customSkullsRow.transform, false);
        ResetCustomSkullsButton = EditorRows.ResetButton(customSkullsRow, "ResetCustomSkulls", ResetCustomSkulls);
        FollowKeyboard(CustomSkulls);
        CustomSkulls.InputField.onEndEdit.AddListener(new Action<string>(text =>
        {
            // Show what was understood; a list without any valid percentage keeps the previous skulls
            var skulls = SkullMath.NormalizeCustom(text, ShownSkulls(BossMode.Options));
            UpdateOptions("CustomSkulls", o => o with {CustomSkulls = skulls});
            CustomSkulls.SetText(skulls, false);
        }));

        // Below whichever skull rows show, so the bar sits right under the setting it follows
        var skullStripRow = EditorRows.LabelRow(template, rows, "SkullStripRow", Texts.SkullPreview.T(), VanillaSprites.CrossboneSkull);
        Skulls = new SkullStrip(skullStripRow);

        // Saved setups and sharing the setup as a text code, after all the boss options
        var savedRow = EditorRows.LabelRow(template, rows, "SavedSetupsRow", Texts.SavedSetups.T(), VanillaSprites.FavouriteHeartIcon);
        SavedList = ModHelperDropdown.Create(EditorRows.ControlInfo("SavedList", SavedListWidth, 150),
            new Il2CppSystem.Collections.Generic.List<string>(), SavedListHeight, new Action<int>(index =>
            {
                if (index >= 0 && index < savedNames.Count + Examples.Count) Select(index);
            }), VanillaSprites.BlueInsertPanelRound, 60);
        SavedList.transform.SetParent(savedRow.transform, false);
        EditorRows.FrameList(SavedList);
        // Long names, and "Example:" in front of the examples, shrink a little and then end in "…" instead of
        // running past the list
        foreach (var text in new[] {SavedList.Dropdown.captionText, SavedList.Dropdown.itemText})
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = SavedListMinFontSize;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        var savedX = EditorRows.ControlX - EditorRows.ControlWidth / 2 + SavedListWidth + SetupButtonGap + SetupButtonWidth / 2;
        SaveSetup = ShareButton(savedRow, "SaveSetup", savedX, Texts.SaveSetup.T(), OnSaveSetup, SetupButtonWidth);
        LoadSetup = ShareButton(savedRow, "LoadSetup", savedX + SetupButtonWidth + SetupButtonGap, Texts.Load.T(),
            OnLoadSetup, SetupButtonWidth);
        DeleteSetup = ShareButton(savedRow, "DeleteSetup", savedX + 2 * (SetupButtonWidth + SetupButtonGap),
            Texts.DeleteSetup.T(), OnDeleteSetup, SetupButtonWidth);
        ShowSavedSetups();

        var shareRow = EditorRows.LabelRow(template, rows, "ShareCodeRow", Texts.ShareCode.T(), VanillaSprites.CopyClipboardBtn);
        var shareX = EditorRows.ControlX - EditorRows.ControlWidth / 2 + 300;
        CopyCode = ShareButton(shareRow, "CopyCode", shareX, Texts.CopyCode.T(), OnCopyCode);
        PasteCode = ShareButton(shareRow, "PasteCode", shareX + 640, Texts.PasteCode.T(), OnPasteCode);

        EditorRows.AddInfo(bossRow, Texts.BossType, Texts.BossTypeInfo);
        EditorRows.AddInfo(BossSpeed.Row, Texts.BossSpeed, Texts.BossSpeedInfo);
        EditorRows.AddInfo(BossHealth.Row, Texts.BossHealth, Texts.BossHealthInfo);
        EditorRows.AddInfo(MaxParagons.Row, Texts.MaxParagons, Texts.MaxParagonsInfo);
        EditorRows.AddInfo(Tiers.Row, Texts.BossTiers, Texts.BossTiersInfo);
        EditorRows.AddInfo(SpeedPerTier.Row, Texts.SpeedPerTier, Texts.SpeedPerTierInfo);
        EditorRows.AddInfo(HealthPerTier.Row, Texts.HealthPerTier, Texts.HealthPerTierInfo);
        EditorRows.AddInfo(stripRow, Texts.TierPreview, Texts.TierPreviewInfo);
        EditorRows.AddInfo(FirstSpawnRound.Row, Texts.FirstBossRound, Texts.FirstBossRoundInfo);
        EditorRows.AddInfo(RoundsBetween.Row, Texts.RoundsBetweenTiers, Texts.RoundsBetweenTiersInfo);
        EditorRows.AddInfo(roundsRow, Texts.SpawnRounds, Texts.SpawnRoundsInfo);
        EditorRows.AddInfo(BossEventRoundsRow, Texts.BossEventRounds, Texts.BossEventRoundsInfo);
        EditorRows.AddInfo(skullModeRow, Texts.Skulls, Texts.SkullsInfo);
        EditorRows.AddInfo(SkullCount.Row, Texts.SkullCount, Texts.SkullCountInfo);
        EditorRows.AddInfo(customSkullsRow, Texts.SkullHealth, Texts.SkullHealthInfo);
        EditorRows.AddInfo(skullStripRow, Texts.SkullPreview, Texts.SkullPreviewInfo);
        EditorRows.AddInfo(savedRow, Texts.SavedSetups, Texts.SavedSetupsInfo);
        EditorRows.AddInfo(shareRow, Texts.ShareCode, Texts.ShareCodeInfo);

        // The Tier Preview sits right under the boss, where in Advanced Mode its cards pick the bosses, and Max Paragons,
        // a rule of the game rather than of the boss, after the skulls
        stripRow.transform.SetSiblingIndex(bossRow.transform.GetSiblingIndex() + 1);
        MaxParagons.Row.transform.SetSiblingIndex(skullStripRow.transform.GetSiblingIndex());
    }

    /// <summary>The CHIMPS dropdown's width, leaving room for its i on the right (#157)</summary>
    public const float ChimpsDropdownWidth = 1180;

    /// <summary>The height of the CHIMPS dropdown and of each entry in its opened list</summary>
    public const float ChimpsItemHeight = 150;

    public const float SavedListWidth = 760;
    public const float SavedListHeight = 750;
    public const float SavedListMinFontSize = 42;
    public const float SetupButtonWidth = 380;
    public const float SetupButtonGap = 30;

    private static ModHelperButton ShareButton(GameObject row, string name, float x, string label, Action action,
        float width = 560)
    {
        var button = ModHelperButton.Create(new Info(name, x, 0, width, 170)
        {
            Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.BlueBtnLong, action);
        button.transform.SetParent(row.transform, false);
        button.AddText(new Info("Label", InfoPreset.FillParent), label, 70);
        return button;
    }

    /// <summary>
    /// The controls the keyboard moves through with Tab, in the order they are on the screen, without the hidden and
    /// disabled ones
    /// </summary>
    public IReadOnlyList<FocusStop> FocusStops => focusStops.Where(stop => stop.Available).ToList();

    /// <summary>
    /// The available stop of <paramref name="field"/>, or null; without making a list, as the keyboard looks it up
    /// while a field is being typed in (#183)
    /// </summary>
    public FocusStop? StopOf(TMP_InputField? field)
    {
        if (field == null) return null;
        foreach (var stop in focusStops)
        {
            if (stop.Field != null && stop.Field.Pointer == field.Pointer && stop.Available) return stop;
        }

        return null;
    }

    /// <summary>
    /// The text fields among <see cref="FocusStops"/>
    /// </summary>
    public IReadOnlyList<TMP_InputField> InputFields => FocusStops.Where(stop => stop.Field != null).Select(stop => stop.Field!).ToList();

    public Button PlayButton => Play.GetComponentInChildren<Button>(true);

    private void BuildFocusStops()
    {
        focusStops.Add(FocusStop.ForButton("ResetBossOptions", ResetAllButton.Button));

        // Left and right pick a tab, Enter or Space switch to the other one, Delete goes back to Standard
        focusStops.Add(new FocusStop("Mode", () => Mode.Row)
        {
            Step = direction => SetAdvanced(direction > 0),
            Activate = () => SetAdvanced(!BossMode.Options.Mixed),
            Reset = () => SetAdvanced(false)
        });

        // Every boss is a stop of its own, picked with Enter or Space like a click; Delete puts the boss back
        foreach (var (boss, button) in BossButtons)
        {
            var stop = FocusStop.ForButton($"Boss{boss}", button.Button);
            stop.Reset = ResetBoss;
            focusStops.Add(stop);
        }

        focusStops.Add(new FocusStop("Elite", () => Elite.gameObject)
        {
            Activate = () => Elite.Toggle.isOn = !Elite.Toggle.isOn, Reset = ResetBoss
        });
        focusStops.Add(FocusStop.ForButton("ResetBoss", ResetBossButton.Button));

        // In Advanced Mode, each tier's card: the arrows step through the bosses, Enter opens the picker and Delete puts
        // the tier back to the boss it started with
        for (var i = 0; i < Strip.Cards.Count; i++)
        {
            var tier = i + 1;
            var card = Strip.Cards[i];
            focusStops.Add(new FocusStop(TierStopName(tier), () => card.Panel.gameObject)
            {
                Step = direction => StepTierBoss(tier, direction),
                Activate = () => OpenPicker(tier),
                Reset = () => SetTierBoss(tier, TierBoss.Follows),
                Enabled = () => BossMode.Options.Mixed
            });
        }

        focusStops.Add(FocusStop.ForSlider("BossSpeed", BossSpeed));
        focusStops.Add(FocusStop.ForSlider("BossHealth", BossHealth));
        focusStops.Add(FocusStop.ForSlider("Tiers", Tiers));
        focusStops.Add(FocusStop.ForSlider("SpeedPerTier", SpeedPerTier));
        focusStops.Add(FocusStop.ForSlider("HealthPerTier", HealthPerTier));
        focusStops.Add(FocusStop.ForSlider("FirstSpawnRound", FirstSpawnRound));
        focusStops.Add(FocusStop.ForSlider("RoundsBetween", RoundsBetween));
        focusStops.Add(FocusStop.ForField("SpawnRounds", SpawnRounds.InputField));
        focusStops.Add(FocusStop.ForButton("ResetRounds", resetRounds.Button));
        // One stop for the tick or, in CHIMPS, the dropdown in its place
        focusStops.Add(new FocusStop("BossEventRounds",
            () => ChimpsEventRounds.gameObject.activeSelf ? ChimpsEventRounds.gameObject : BossEventRounds.gameObject)
        {
            Activate = () =>
            {
                if (ChimpsEventRounds.gameObject.activeSelf) ChimpsEventRounds.Dropdown.Show();
                else BossEventRounds.Toggle.isOn = !BossEventRounds.Toggle.isOn;
            },
            Step = direction =>
            {
                if (!ChimpsEventRounds.gameObject.activeSelf) return;
                var dropdown = ChimpsEventRounds.Dropdown;
                dropdown.value = Math.Clamp(dropdown.value + direction, 0, dropdown.options.Count - 1);
            },
            Reset = () =>
            {
                if (ChimpsEventRounds.gameObject.activeSelf) ChimpsEventRounds.Dropdown.value = 0;
                else BossEventRounds.Toggle.isOn = false;
            }
        });
        focusStops.Add(FocusStop.ForButton("ChimpsRoundsInfo", ChimpsRoundsInfo.Button));
        var skullMode = FocusStop.ForDropdown("SkullMode", SkullMode.Dropdown);
        skullMode.Reset = ResetSkulls;
        focusStops.Add(skullMode);
        focusStops.Add(FocusStop.ForButton("ResetSkulls", ResetSkullsButton.Button));
        focusStops.Add(FocusStop.ForSlider("SkullCount", SkullCount));
        focusStops.Add(FocusStop.ForField("CustomSkulls", CustomSkulls.InputField));
        focusStops.Add(FocusStop.ForButton("ResetCustomSkulls", ResetCustomSkullsButton.Button));
        focusStops.Add(FocusStop.ForSlider("MaxParagons", MaxParagons));
        focusStops.Add(FocusStop.ForDropdown("SavedList", SavedList.Dropdown));
        focusStops.Add(FocusStop.ForButton("SaveSetup", SaveSetup.Button));
        focusStops.Add(FocusStop.ForButton("LoadSetup", LoadSetup.Button));
        focusStops.Add(FocusStop.ForButton("DeleteSetup", DeleteSetup.Button));
        focusStops.Add(FocusStop.ForButton("CopyCode", CopyCode.Button));
        focusStops.Add(FocusStop.ForButton("PasteCode", PasteCode.Button));
        focusStops.Add(FocusStop.ForButton("Play", PlayButton));
    }

    /// <summary>
    /// Tab moves to the next field instead of typing a tab, and Enter applies the value and moves on as well
    /// </summary>
    private void FollowKeyboard(ModHelperInputField field)
    {
        field.InputField.onValidateInput = new Func<string, int, char, char>((_, _, added) => added == '\t' ? '\0' : added);
        field.InputField.onSubmit.AddListener(new Action<string>(_ => BossCreatorKeys.Submitted(this, field.InputField)));
        // onEndEdit applies the value; Esc restores the text from before the edit first, so it is applied unchanged
        field.InputField.restoreOriginalTextOnEscape = true;
    }

    private void OnCopyCode()
    {
        Editor.ApplyUiSettingsToDCM();
        var code = SetupCode.Encode(BossMode.Options, Editor.dcm);
        Clipboard.Write(code);
        ModHelper.Msg<CustomBossChallenge>(
            $"Copied a share code ({code.Length} characters): {Describe(BossMode.Options, Editor.dcm)}");
        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter, Texts.ShareCode.T(),
            Texts.CodeCopied.T(code.Length), null, Texts.Ok.T(), new Action(CopyLink), Texts.CopyLink.T(),
            Popup.TransitionAnim.Scale));
    }

    /// <summary>
    /// Replaces the copied code with a link that opens it on the website
    /// </summary>
    internal void CopyLink()
    {
        var link = CodeFormat.Link(SetupCode.Encode(BossMode.Options, Editor.dcm));
        Clipboard.Write(link);
        ModHelper.Msg<CustomBossChallenge>($"Copied a link to the setup ({link.Length} characters)");
        PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(Texts.LinkCopied.T(), null));
    }

    private void OnPasteCode()
    {
        if (!SetupCode.TryDecode(Clipboard.Read(), out var options, out var dcm, out var problem) || dcm == null)
        {
            var text = problem == CodeProblem.TooNew ? Texts.CodeTooNew : Texts.CodeInvalid;
            ModHelper.Msg<CustomBossChallenge>(problem == CodeProblem.TooNew
                ? "Pasted a share code from a newer version of the mod"
                : "Pasted text is not a share code");
            PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(text.T(), null));
            return;
        }

        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter, Texts.ShareCode.T(),
            $"{Texts.LoadCodeQuestion.T()}\n\n{Summary(options, dcm)}",
            new Action(() =>
            {
                BossMode.Replace(Editor, options, dcm);
                ModHelper.Msg<CustomBossChallenge>($"Loaded a share code: {Describe(options, dcm)}");
            }), Texts.Load.T(),
            null, Texts.Cancel.T(), Popup.TransitionAnim.Scale));
    }

    private static IReadOnlyList<ExampleSetup> Examples => Creator.ExampleSetups.All;

    /// <summary>
    /// Golden "Example:" in front of an example's name, so it can't be taken for one of the player's own setups
    /// </summary>
    public static string ExampleLine(ExampleSetup example)
    {
        // The label's text in gold, wherever the language puts the name in it
        var parts = Texts.ExampleLabel.T("\u0001").Split('\u0001');
        string Gold(string text) => text.Length == 0 ? "" : $"<color={ExampleColor}>{PlainText(text)}</color>";
        return Gold(parts[0]) + PlainText(example.Name) + Gold(parts.Length > 1 ? parts[1] : "");
    }

    public const string ExampleColor = "#FFD24A";

    /// <summary>
    /// Fills the list with the saved setups, then the examples, keeping the picked one when it is still there.
    /// Without saved setups the first example is picked. Examples can be loaded but not deleted.
    /// </summary>
    public void ShowSavedSetups()
    {
        var names = savedNames = BossMode.Saved.All.Select(entry => entry.Name).ToList();
        var index = SelectedSetup != null && names.Contains(SelectedSetup) ? names.IndexOf(SelectedSetup)
            : SelectedExample != null && SelectedSetup == null ? names.Count + Examples.ToList().IndexOf(SelectedExample)
            : 0;

        var shown = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var name in names) shown.Add(PlainText(name));
        foreach (var example in Examples) shown.Add(ExampleLine(example));

        var dropdown = SavedList.Dropdown;
        dropdown.ClearOptions();
        dropdown.AddOptions(shown);
        dropdown.SetValueWithoutNotify(index);
        dropdown.RefreshShownValue();
        Select(index);

        // Mod Helper sizes the opened list for the options it was created with, so size it for the current ones
        var template = SavedList.TemplatePanel.RectTransform;
        var itemHeight = SavedList.RectTransform.sizeDelta.y;
        var listHeight = Math.Min(SavedListHeight, Math.Max(shown.Count, 1) * itemHeight);
        template.sizeDelta = new Vector2(template.sizeDelta.x, listHeight);
        template.anchoredPosition = new Vector2(template.anchoredPosition.x, -itemHeight / 2 - listHeight / 2);
    }

    /// <summary>
    /// Picks the saved setup or example at this place in the list
    /// </summary>
    private void Select(int index)
    {
        SelectedSetup = index < savedNames.Count ? savedNames[index] : null;
        SelectedExample = SelectedSetup == null ? Examples[index - savedNames.Count] : null;
        DeleteSetup.Button.interactable = SelectedSetup != null;
    }

    private void OnSaveSetup()
    {
        Editor.ApplyUiSettingsToDCM();
        var options = BossMode.Options;
        var dcm = Editor.dcm;
        var name = SavedSetupsStore.NameOf(options, dcm);
        if (!savedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            SaveAs(name, options, dcm);
            return;
        }

        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            Texts.SavedSetups.T(), Texts.OverwriteSetupQuestion.T($"<b>{PlainText(name)}</b>"),
            new Action(() => SaveAs(name, options, dcm)), Texts.SaveSetup.T(),
            null, Texts.Cancel.T(), Popup.TransitionAnim.Scale));
    }

    private void SaveAs(string name, BossOptions options, DailyChallengeModel dcm)
    {
        SavedSetupsStore.SaveResult result;
        try
        {
            result = BossMode.Saved.Save(options, dcm);
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Could not save the setup '{name}': {e.Message}");
            ShowFileError();
            return;
        }

        if (result == SavedSetupsStore.SaveResult.TooNew)
        {
            PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(Texts.SetupsTooNew.T(), null));
            return;
        }

        if (result == SavedSetupsStore.SaveResult.Full)
        {
            PopupScreen.instance.SafelyQueue(screen =>
                screen.ShowOkPopup(Texts.TooManySetups.T(SavedSetupsStore.MaxSetups), null));
            return;
        }

        ModHelper.Msg<CustomBossChallenge>($"{(result == SavedSetupsStore.SaveResult.Replaced ? "Replaced" : "Saved")} the saved setup {Describe(options, dcm)}");
        SelectedSetup = name;
        ShowSavedSetups();
        PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(Texts.SetupSaved.T($"<b>{PlainText(name)}</b>"), null));
    }

    /// <summary>
    /// Tells the player the saved setups file couldn't be written, instead of only the log knowing
    /// </summary>
    private static void ShowFileError() =>
        PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(Texts.SetupFileError.T(), null));

    private void OnLoadSetup()
    {
        if (SelectedExample != null)
        {
            LoadExample(SelectedExample);
            return;
        }

        var name = SelectedSetup;
        if (name == null) return;

        if (!BossMode.Saved.TryLoad(name, out var options, out var dcm, out var problem) || dcm == null)
        {
            ModHelper.Msg<CustomBossChallenge>($"Could not load the saved setup '{name}': {problem}");
            var text = problem == CodeProblem.TooNew ? Texts.CodeTooNew : Texts.SetupUnreadable;
            PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(text.T(), null));
            return;
        }

        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter, Texts.SavedSetups.T(),
            $"{Texts.LoadSetupQuestion.T()}\n\n{Summary(options, dcm)}",
            new Action(() =>
            {
                BossMode.Replace(Editor, options, dcm);
                ModHelper.Msg<CustomBossChallenge>($"Loaded a saved setup: {Describe(options, dcm)}");
            }), Texts.Load.T(),
            null, Texts.Cancel.T(), Popup.TransitionAnim.Scale));
    }

    private void LoadExample(ExampleSetup example)
    {
        var (options, dcm) = Creator.ExampleSetups.Build(example);
        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            Texts.ExampleSetups.T(),
            $"{Texts.LoadExampleQuestion.T()}\n\n<i>{PlainText(example.Description)}</i>\n\n{Summary(options, dcm)}",
            new Action(() =>
            {
                BossMode.Replace(Editor, options, dcm);
                ModHelper.Msg<CustomBossChallenge>($"Loaded an example setup: {Describe(options, dcm)}");
            }), Texts.Load.T(),
            null, Texts.Cancel.T(), Popup.TransitionAnim.Scale));
    }

    private void OnDeleteSetup()
    {
        var name = SelectedSetup;
        if (name == null) return;

        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            Texts.SavedSetups.T(), Texts.DeleteSetupQuestion.T($"<b>{PlainText(name)}</b>"),
            new Action(() =>
            {
                try
                {
                    BossMode.Saved.Delete(name);
                    ModHelper.Msg<CustomBossChallenge>($"Deleted the saved setup '{name}'");
                }
                catch (Exception e)
                {
                    ModHelper.Warning<CustomBossChallenge>($"Could not delete the setup '{name}': {e.Message}");
                    ShowFileError();
                }

                ShowSavedSetups();
            }), Texts.DeleteSetup.T(),
            null, Texts.Cancel.T(), Popup.TransitionAnim.Scale));
    }

    /// <summary>
    /// A setup in one line for the log, e.g. "'Custom Vortex', Vortex Elite, 7 tiers, Logs, Hard, Standard"
    /// </summary>
    public static string Describe(BossOptions options, DailyChallengeModel dcm) =>
        $"'{dcm.name}', {options.Boss}{(options.Elite ? " Elite" : "")}, {options.Tiers} tiers" +
        (options.Mixed ? $" ({options.TierBosses})" : "") +
        (options.UsesBossEventRounds ? $", boss event rounds {options.BaseEventRoundSetIn(dcm.mode)}" : "") +
        $", {dcm.map}, {dcm.difficulty}, {dcm.mode}";

    /// <summary>
    /// What a shared setup is, in a few words
    /// </summary>
    public static string Summary(BossOptions options, DailyChallengeModel dcm)
    {
        var boss = options.Mixed ? Texts.DifferentBosses.T()
            : options.Elite ? $"{options.Boss} {Texts.Elite.T()}" : options.Boss.ToString();
        var rounds = !options.UsesBossEventRounds ? ""
            : dcm.mode == BossOptions.ChimpsMode ? $", {Texts.BossEventRounds.T()} ({ChimpsEntry(options).T()})"
            : $", {Texts.BossEventRounds.T()}";
        return $"<b>{PlainText(dcm.name)}</b>\n{boss}, {Texts.TierCount.T(options.Tiers)}{rounds}\n" +
               $"{PlainText(dcm.map.T())}, {PlainText(dcm.difficulty.T())}";
    }

    /// <summary>
    /// Shows text from a shared code as it is: rich text tags in it are not applied, so a name can't resize the
    /// popup or pretend to be something else
    /// </summary>
    public static string PlainText(string? text) =>
        $"<noparse>{(text ?? "").Replace("</noparse>", "</​noparse>", StringComparison.OrdinalIgnoreCase)}</noparse>";

    public const float PlayWidth = 500;
    public const float PlayGap = 16;

    /// <summary>
    /// The game's Play spot overlaps the Reset button, and further left is the title banner: a slightly narrower Play
    /// fits between the two
    /// </summary>
    private void PlaceBesideReset(RectTransform play)
    {
        var reset = Editor.transform.Find("Reset")?.GetComponent<RectTransform>();
        if (reset == null || play == null) return;

        var button = play.GetComponentInChildren<Button>(true).GetComponent<RectTransform>();
        if (button != play)
        {
            // The copied button sits at the right edge of its holder
            button.sizeDelta = new Vector2(PlayWidth, button.sizeDelta.y);
            button.anchoredPosition = new Vector2(-PlayWidth / 2, button.anchoredPosition.y);
        }
        else
        {
            play.sizeDelta = new Vector2(PlayWidth, play.sizeDelta.y);
        }

        var right = reset.anchoredPosition.x - reset.sizeDelta.x / 2 - PlayGap;
        play.anchoredPosition = new Vector2(right - (button != play ? play.sizeDelta.x : PlayWidth) / 2,
            play.anchoredPosition.y);
    }

    private GameObject BuildPlayButton()
    {
        var template = Editor.transform.Find("Play");
        GameObject play;
        if (template != null)
        {
            play = Object.Instantiate(template.gameObject, template.parent, false);
            // Next to the game's own Play, not last: the editor's panels (modifiers, towers, maps) and their dark
            // backgrounds come later and must cover it, like they cover Reset
            play.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);
            var button = play.GetComponentInChildren<Button>(true);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(new Action(OnPlay));
            EditorRows.SetText(play.GetComponentInChildren<TMP_Text>(true), Texts.Play.T());
        }
        else
        {
            var button = ModHelperButton.Create(new Info(PlayName, 1051, -47, 590, 212)
            {
                Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1)
            }, VanillaSprites.GreenBtnLong, new Action(OnPlay));
            button.transform.SetParent(Editor.transform, false);
            button.AddText(new Info("Label", InfoPreset.FillParent), Texts.Play.T(), 90);
            play = button.gameObject;
        }

        play.name = PlayName;
        PlaceBesideReset(play.GetComponent<RectTransform>());
        play.SetActive(false);
        return play;
    }

    /// <summary>
    /// Builds the section for this editor if needed, and shows or hides it
    /// </summary>
    public static BossEditorSection? Show(ChallengeEditor editor, bool show)
    {
        if (Current != null && Current.BelongsTo(editor))
        {
            Current.SetVisible(show);
            return Current;
        }

        if (!show) return null;

        Current = new BossEditorSection(editor);
        Current.SetVisible(true);
        return Current;
    }

    private bool BelongsTo(ChallengeEditor editor) =>
        Editor != null && Section != null && Editor.Pointer == editor.Pointer;

    /// <summary>
    /// Whether this editor is (still) set up as the Boss Creator. An editor stays on the menu stack after Play, so
    /// this is checked per editor instead of with the Boss Creator session.
    /// </summary>
    public static bool IsShowing(ChallengeEditor editor)
    {
        var section = editor.sliderSettings?.transform.parent?.Find(SectionName);
        return section != null && section.gameObject.activeSelf;
    }


    /// <summary>
    /// Whether the editor is working on the Boss Creator setup. The editor can stay on the menu stack after Play and
    /// be reopened, so this goes by its model rather than by the Boss Creator session.
    /// </summary>
    public static bool IsBossEditor(ChallengeEditor editor) =>
        BossMode.Dcm != null && editor.dcm != null && editor.dcm.Pointer == BossMode.Dcm.Pointer;

    /// <summary>
    /// Turns an editor back into the plain Challenge Editor
    /// </summary>
    public static void Hide(ChallengeEditor editor)
    {
        if (Current != null && Current.BelongsTo(editor))
        {
            Current.SetVisible(false);
            return;
        }

        editor.sliderSettings?.transform.parent?.Find(SectionName)?.gameObject.SetActive(false);
        editor.transform.Find(PlayName)?.gameObject.SetActive(false);
        ShowEndRound(editor, true);
        FortifiedModifier.Remove(editor);
    }

    /// <summary>
    /// The end round of a boss game always follows the last tier (see <see cref="ConfigApplier.Prepare"/>), so the
    /// editor's own End Round field would only mislead
    /// </summary>
    private static void ShowEndRound(ChallengeEditor editor, bool show)
    {
        var input = editor.endRound;
        if (input == null) return;
        input.gameObject.SetActive(show);
        input.transform.parent?.Find("EndRoundLbl")?.gameObject.SetActive(show);
    }

    private void SetVisible(bool show)
    {
        Section.SetActive(show);
        Play.SetActive(show);
        ShowEndRound(Editor, !show);
        if (titleText != null)
        {
            var nkText = titleText.TryCast<Il2Cpp.NK_TextMeshProUGUI>();
            if (show)
            {
                EditorRows.SetText(titleText, Title);
            }
            else if (nkText != null && !string.IsNullOrEmpty(originalTitleKey))
            {
                nkText.localizeKey = originalTitleKey;
                nkText.AutoLocalize = true;
                nkText.SetText(originalTitleKey);
            }
        }

        if (show) FortifiedModifier.Add(Editor, Editor.dcm);
        else FortifiedModifier.Remove(Editor);

        if (show) Load(Editor.dcm, BossMode.Options);
        Relayout();
    }

    /// <summary>
    /// Shows the given setup in the section
    /// </summary>
    public void Load(DailyChallengeModel? dcm, BossOptions options)
    {
        refreshing = true;
        try
        {
            if (dcm != null)
            {
                // A model without bloon modifiers has the game's defaults
                BossSpeed.Show(Mathf.Round((dcm.bloonModifiers?.bossSpeedMultiplier ?? 1) * 100));
                BossHealth.Show(Mathf.Round((dcm.bloonModifiers?.healthMultipliers?.boss ?? 1) * 100));
                MaxParagons.Show(ParagonMath.ToSlider(dcm.maxParagons, (int) MaxParagons.Slider.maxValue));
            }

            Elite.SetChecked(options.Elite, false);
            BossEventRounds.SetChecked(options.BossEventRounds, false);
            ShowEventRounds(dcm?.mode ?? Editor.dcm?.mode, options);
            Mode.Show(options.Mixed);
            Tiers.Show(options.Tiers);
            SpeedPerTier.Show(options.SpeedPerTier);
            HealthPerTier.Show(options.HealthPerTier);
            SkullMode.Dropdown.SetValueWithoutNotify((int) options.SkullMode);
            SkullCount.Show(options.SkullCount);
            CustomSkulls.SetText(ShownSkulls(options), false);
            ShowSpawnRounds();
            FollowBossName(options);
            ShowBoss();
        }
        finally
        {
            refreshing = false;
        }

        Relayout();
    }

    /// <summary>
    /// Writes the boss values the vanilla editor doesn't know about into its model
    /// </summary>
    public void Save(DailyChallengeModel dcm)
    {
        dcm.bloonModifiers ??= new BloonModifiers();
        dcm.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
        dcm.bloonModifiers.bossSpeedMultiplier = BossSpeed.Value / 100f;
        dcm.bloonModifiers.healthMultipliers.boss = BossHealth.Value / 100f;
        dcm.maxParagons = ParagonMath.FromSlider((int) MaxParagons.Value, (int) MaxParagons.Slider.maxValue);
    }

    /// <param name="control">The control that changes, for Undo (<see cref="BossHistory.RecordChange"/>)</param>
    private void UpdateDcm(string control, Action<DailyChallengeModel> change)
    {
        if (refreshing || Editor.dcm == null) return;
        BossHistory.RecordChange(Editor, control);
        Editor.dcm.bloonModifiers ??= new BloonModifiers();
        Editor.dcm.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
        change(Editor.dcm);
        ShowStrip();
    }

    /// <param name="control">The control that changes, for Undo, or null for a change that is a step of its own</param>
    private void UpdateOptions(string? control, Func<BossOptions, BossOptions> change)
    {
        if (refreshing) return;
        var changed = change(BossMode.Options);
        if (changed == BossMode.Options) return;
        BossHistory.RecordChange(Editor, control);
        BossMode.Options = changed;
        if (refreshing) return;
        ShowRowContent();
        FitIfChanged();
    }

    private void SelectBoss(BossType boss)
    {
        if (boss != BossMode.Options.Boss) BossHistory.RecordChange(Editor, null);
        BossMode.Options = BossMode.Options with {Boss = boss};
        FollowBossName(BossMode.Options);
        ShowBoss();
    }

    /// <summary>
    /// Follows the boss and Elite in the challenge name as long as the player hasn't named it themselves. An example's
    /// name stays while the boss is still the example's own, and a name Random made up while it still describes the
    /// setup; otherwise a made up name is made up again.
    /// </summary>
    private void FollowBossName(BossOptions options)
    {
        var name = Editor.chalName;
        if (name == null) return;
        var example = ExampleSetups.Named(name.text);
        var traits = BossMode.NameTraits(options, Editor.dcm);
        if (example != null
                ? example.Options.Boss == options.Boss && example.Options.Elite == options.Elite &&
                  example.Options.TierBosses == options.TierBosses
                : !BossMode.IsDefaultName(name.text) || RandomNames.Fits(name.text, traits))
        {
            return;
        }

        name.text = RandomNames.IsGenerated(name.text)
            ? RandomNames.For(traits, BossRandomizer.Random) ?? BossMode.DefaultName(options)
            : BossMode.DefaultName(options);
        if (Editor.dcm != null) Editor.dcm.name = name.text;
    }

    /// <summary>
    /// The boss and Elite back to a new setup's: Bloonarius, not Elite, the same for every tier
    /// </summary>
    public void ResetBoss()
    {
        var defaults = new BossOptions();
        var standard = standardValues ?? TierBoss.Follows;
        lastTierBosses = null;
        standardValues = null;
        if (!BossMode.Options.Mixed)
        {
            ResetOptions(o => o with {Boss = defaults.Boss, Elite = defaults.Elite});
            return;
        }

        // Standard's speed, health and skulls come back with it, in the same step for Undo
        BossHistory.RecordChange(Editor, null);
        SetBossValues(standard);
        BossMode.Options = BossMode.Options with {Boss = defaults.Boss, Elite = defaults.Elite, TierBosses = null};
        Load(Editor.dcm, BossMode.Options);
    }

    /// <summary>
    /// The Advanced tab: every tier starts with the Boss Type, which is then hidden, so each tier has a boss of its own
    /// to change. The Standard tab: one boss for every tier again, with the choices kept for going back to Advanced.
    /// </summary>
    public void SetAdvanced(bool on)
    {
        Mode.Show(BossMode.Options.Mixed);
        if (refreshing || on == BossMode.Options.Mixed) return;
        var options = BossMode.Options;
        var modifiers = Editor.dcm?.bloonModifiers;
        var current = TierBoss.Follows with
        {
            Speed = (int) Math.Round((modifiers?.bossSpeedMultiplier ?? 1) * 100),
            Health = (int) Math.Round((modifiers?.healthMultipliers?.boss ?? 1) * 100),
            Skulls = options.SkullMode, SkullCount = options.SkullCount, CustomSkulls = options.CustomSkulls
        };
        var standard = on ? current : standardValues ?? TierBoss.Follows;

        // Bosses per tier are remembered for coming back to Advanced only when they were changed; tiers that all still
        // have the boss, speed, health and skulls they started with follow what is picked in Standard instead
        if (!on)
        {
            lastTierBosses = options.TierBosses == StartingTierBosses(options, standard) ? null : options.TierBosses;
        }

        BossHistory.RecordChange(Editor, null);
        BossMode.Options = options with {TierBosses = on ? lastTierBosses ?? StartingTierBosses(options, standard) : null};

        // Advanced gives every tier its own speed, health and skulls, starting from Standard's, which then stay at 100 %
        // and Vanilla until Standard brings them back
        standardValues = on ? current : null;
        SetBossValues(on ? TierBoss.Follows : standard);
        Load(Editor.dcm, BossMode.Options);
    }

    /// <summary>
    /// What every tier starts with in Advanced Mode: the Boss Type, Elite, Boss Speed, Boss Health and skulls picked in
    /// Standard
    /// </summary>
    private static string StartingTierBosses(BossOptions options, TierBoss values) =>
        TierBossMath.Resolve(null, options.Boss.ToString(), options.Elite, TierMath.MaxTiers, _ => 0, values);

    /// <summary>
    /// Boss Speed and Boss Health in percent, in the challenge and on their sliders, and the skulls
    /// </summary>
    private void SetBossValues(TierBoss values)
    {
        BossMode.Options = BossMode.Options with
        {
            SkullMode = values.Skulls, SkullCount = values.SkullCount, CustomSkulls = values.CustomSkulls
        };
        if (Editor.dcm == null) return;
        Editor.dcm.bloonModifiers ??= new BloonModifiers();
        Editor.dcm.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
        Editor.dcm.bloonModifiers.bossSpeedMultiplier = values.Speed / 100f;
        Editor.dcm.bloonModifiers.healthMultipliers.boss = values.Health / 100f;
        BossSpeed.Show(values.Speed);
        BossHealth.Show(values.Health);
    }

    public static string TierStopName(int tier) => $"{TierStrip.CardName}{tier}";

    /// <summary>
    /// Gives a tier its own boss, or makes it follow the Boss Type with <see cref="TierBoss.Follows"/>; one step for
    /// Undo
    /// </summary>
    /// <param name="control">The control that changes, so a slider's run of changes is one step for Undo, or null</param>
    public void SetTierBoss(int tier, TierBoss boss, string? control = null)
    {
        if (!BossMode.Options.Mixed) return;
        var changed = BossMode.Options with {TierBosses = TierBossMath.With(BossMode.Options.TierBosses, tier, boss)};
        if (changed == BossMode.Options) return;
        BossHistory.RecordChange(Editor, control);
        BossMode.Options = changed;
        ShowBoss();
    }

    /// <summary>
    /// The next or previous boss (or Random) for a tier, from the keyboard; a run of presses is one step for Undo
    /// </summary>
    private void StepTierBoss(int tier, int direction)
    {
        var options = BossMode.Options;
        if (!options.Mixed) return;
        var next = TierBossMath.Step(options.EntryOf(tier), options.Boss.ToString(), options.Elite, direction);
        BossHistory.RecordChange(Editor, TierStopName(tier));
        BossMode.Options = options with {TierBosses = TierBossMath.With(options.TierBosses, tier, next)};
        ShowBoss();
    }

    /// <summary>
    /// Opens the boss picker for a tier's card
    /// </summary>
    public TierBossPicker? OpenPicker(int tier)
    {
        if (!BossMode.Options.Mixed || tier < 1 || tier > BossMode.Options.Tiers) return null;
        var fromKeyboard = BossCreatorKeys.Current?.Name == TierStopName(tier);
        BossCreatorKeys.ClearFocus();
        var picker = TierBossPicker.Open(this, tier);
        picker.ReturnFocus = fromKeyboard;
        return picker;
    }

    /// <summary>
    /// Shows the tier's new boss, and gives the keyboard back to its card if the picker was opened from there
    /// </summary>
    internal void OnPickerClosed(int tier, bool returnFocus)
    {
        ShowBoss();
        if (returnFocus) BossCreatorKeys.FocusOn(this, TierStopName(tier));
    }

    /// <summary>
    /// The skulls back to the boss's own, with the evenly spaced count and the custom list at their defaults too
    /// </summary>
    public void ResetSkulls()
    {
        var defaults = new BossOptions();
        ResetOptions(o => o with
        {
            SkullMode = defaults.SkullMode, SkullCount = defaults.SkullCount, CustomSkulls = defaults.CustomSkulls
        });
    }

    public void ResetCustomSkulls() => ResetOptions(o => o with {CustomSkulls = new BossOptions().CustomSkulls});

    /// <summary>
    /// A reset of some options: one step for Undo, shown right away
    /// </summary>
    private void ResetOptions(Func<BossOptions, BossOptions> change)
    {
        var changed = change(BossMode.Options);
        if (changed == BossMode.Options) return;
        BossHistory.RecordChange(Editor, null);
        BossMode.Options = changed;
        Load(Editor.dcm, changed);
    }

    /// <summary>
    /// Asks, then puts every boss option back to its default; the Challenge Editor's rules stay
    /// </summary>
    private void OnResetAll() =>
        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            Texts.ResetBossOptions.T(), Texts.ResetBossOptionsQuestion.T(), new Action(ResetAll), Texts.Reset.T(),
            null, Texts.Cancel.T(), Popup.TransitionAnim.Scale));

    /// <summary>
    /// Every boss option back to its default, without asking; one step for Undo
    /// </summary>
    public void ResetAll()
    {
        if (Editor.dcm == null) return;
        BossHistory.RecordSetup(Editor);
        ForgetAdvanced();
        ResetBossValues(Editor);
        Load(Editor.dcm, BossMode.Options);
        BossMode.Save();
        ModHelper.Msg<CustomBossChallenge>("Reset the boss options");
    }

    /// <summary>
    /// Forgets the bosses per tier and the Boss Speed and Boss Health remembered for switching between Advanced and
    /// Standard, so after a reset Advanced starts over from what Standard has
    /// </summary>
    public void ForgetAdvanced()
    {
        lastTierBosses = null;
        standardValues = null;
    }

    /// <summary>
    /// The boss values in the model and the boss options back to a new setup's, as the editor's own Reset does them.
    /// The challenge name is left to the caller.
    /// </summary>
    public static void ResetBossValues(ChallengeEditor editor)
    {
        var dcm = editor.dcm;
        dcm.bloonModifiers ??= new BloonModifiers();
        dcm.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
        dcm.bloonModifiers.bossSpeedMultiplier = 1;
        dcm.bloonModifiers.healthMultipliers.boss = 1;
        dcm.bloonModifiers.allFortified = false;
        dcm.maxParagons = ParagonMath.NoLimit;
        var settings = FortifiedModifier.Settings(editor);
        if (settings != null) FortifiedModifier.Show(settings, dcm);
        BossMode.Options = new BossOptions();
    }

    private void SetElite(bool elite)
    {
        if (refreshing) return;
        if (elite != BossMode.Options.Elite) BossHistory.RecordChange(Editor, null);
        BossMode.Options = BossMode.Options with {Elite = elite};
        FollowBossName(BossMode.Options);
        ShowBoss();
    }

    private void ShowBoss()
    {
        foreach (var (boss, button) in BossButtons)
        {
            var selected = boss == BossMode.Options.Boss;
            button.Image.SetSprite(Portrait(boss, BossMode.Options.Elite));
            button.transform.Find(SelectedMarker)?.gameObject.SetActive(selected);
        }

        ShowStrip();
    }

    /// <summary>
    /// The editor's own sliders for bloon and MOAB speed and health, by the name of their panel, with the model value
    /// each sets (the slider shows percent). The game only copies them into its model on Play.
    /// </summary>
    private static readonly (string Panel, Action<DailyChallengeModel, float> Set)[] RuleSliders =
    {
        ("BloonSpeedSlider", (dcm, value) => dcm.bloonModifiers.speedMultiplier = value),
        ("MoabSpeedSlider", (dcm, value) => dcm.bloonModifiers.moabSpeedMultiplier = value),
        ("CeramicHealthSlider", (dcm, value) => dcm.bloonModifiers.healthMultipliers.bloons = value),
        ("MoabHealthSlider", (dcm, value) => dcm.bloonModifiers.healthMultipliers.moabs = value)
    };

    /// <summary>
    /// The warnings under the Tier Preview also look at the editor's own rules (issue #70), so they follow them: the
    /// number fields write the model when they are left, the sliders are copied into it while they move, like the
    /// game does on Play, and the modifiers come in through <see cref="RulesChanged"/> when their popup saves them
    /// </summary>
    private void WatchEditorRules()
    {
        foreach (var field in new TMP_InputField?[] {Editor.round, Editor.lives, Editor.cash, Editor.maxTowers})
        {
            // Added after the editor's own listeners, so the model already has the new value
            field?.onEndEdit.AddListener(new Action<string>(_ => RulesChanged()));
        }

        foreach (var (panel, set) in RuleSliders)
        {
            var slider = RuleSlider(Editor, panel);
            slider?.onValueChanged.AddListener(new Action<float>(value =>
            {
                if (!BossMode.Active || refreshing || Editor.dcm == null) return;
                Editor.dcm.bloonModifiers ??= new BloonModifiers();
                Editor.dcm.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
                set(Editor.dcm, value / 100f);
                RulesChanged();
            }));
        }

        Editor.gameModeDropdown?.onValueChanged.AddListener(new Action<int>(_ => RulesChanged()));
    }

    /// <summary>
    /// The slider of the editor's own panel <paramref name="panel"/>, like "CeramicHealthSlider", or null
    /// </summary>
    public static Slider? RuleSlider(ChallengeEditor editor, string panel) =>
        editor.sliderSettings.GetComponentsInChildren<Slider>(true).FirstOrDefault(slider => slider.transform.parent?.name == panel);

    /// <summary>
    /// The editor's own rules changed: the warnings under the Tier Preview are checked again
    /// </summary>
    internal void RulesChanged()
    {
        if (!BossMode.Active || refreshing || Editor.dcm == null || !Section.activeInHierarchy) return;
        ShowEventRounds(SelectedMode());
        ShowStrip();
    }

    /// <summary>
    /// The game mode picked in the editor's mode dropdown, which the editor's model may only get after this hears it
    /// </summary>
    private string? SelectedMode()
    {
        var dropdown = Editor.gameModeDropdown;
        var modes = Editor.gameModes;
        return dropdown != null && modes != null && dropdown.value >= 0 && dropdown.value < modes.Count
            ? modes[dropdown.value]
            : Editor.dcm?.mode;
    }

    /// <summary>
    /// The Boss Event Rounds tick, or in CHIMPS the dropdown in its place with its entry for the options (#139)
    /// </summary>
    internal void ShowEventRounds(string? mode, BossOptions? shown = null)
    {
        var chimps = mode == BossOptions.ChimpsMode;
        BossEventRounds.gameObject.SetActive(!chimps);
        ChimpsEventRounds.gameObject.SetActive(chimps);
        ChimpsRoundsInfo.gameObject.SetActive(chimps);
        var options = shown ?? BossMode.Options;
        BossEventRounds.SetChecked(options.BossEventRounds, false);
        ChimpsEventRounds.Dropdown.SetValueWithoutNotify(ChimpsIndex(options));
    }

    /// <summary>The CHIMPS dropdown's entry for <paramref name="options"/>: Off, Newest, Earlier or the boss's own</summary>
    public static int ChimpsIndex(BossOptions options) => !options.BossEventRounds ? 0 : options.ChimpsRounds switch
    {
        Config.ChimpsRounds.Earlier => 2,
        Config.ChimpsRounds.Own => 3,
        _ => 1
    };

    /// <summary>The text of the CHIMPS dropdown's entry for <paramref name="options"/></summary>
    public static string ChimpsEntry(BossOptions options) => ChimpsIndex(options) switch
    {
        0 => Texts.EventRoundsOff,
        1 => Texts.ChimpsRoundsNewest,
        2 => Texts.ChimpsRoundsEarlier,
        _ => Texts.ChimpsRoundsOwn
    };

    /// <summary>
    /// The strip grows with its warning lines, and the rows below must move with it
    /// </summary>
    private void ShowStrip()
    {
        // The boss's own skulls follow the boss and Elite
        Skulls.Show(BossMode.Options);
        Strip.Show(BossMode.Options, Editor.dcm);
        if (!refreshing) FitIfChanged();
    }

    private static string ShownSkulls(BossOptions options) =>
        SkullMath.NormalizeCustom(options.CustomSkulls, new BossOptions().CustomSkulls);

    private void ShowSpawnRounds()
    {
        var rounds = BossMode.Options.EffectiveSpawnRounds;
        SpawnRounds.SetText(TierMath.FormatRounds(rounds), false);
        var (first, gap) = TierMath.FirstAndGap(rounds, (int) RoundsBetween.Value);
        FirstSpawnRound.Show(first);
        RoundsBetween.Show(gap);
    }

    /// <summary>
    /// Replaces Spawn Rounds with evenly spaced rounds from the two sliders
    /// </summary>
    private void SpreadSpawnRounds(string control) =>
        UpdateOptions(control, o => o with {SpawnRounds = TierMath.EvenRounds((int) FirstSpawnRound.Value, (int) RoundsBetween.Value)});

    /// <summary>
    /// Shows only the rows that apply, and makes room for the section in the editor's scroll area. Always lays the
    /// section out again; a change to one option uses <see cref="FitIfChanged"/> instead, which only does when a row
    /// was shown, hidden or changed height (#184).
    /// </summary>
    public void Relayout()
    {
        ShowRowContent();
        FitSection();
    }

    /// <summary>
    /// Shows the rows that apply and what they show
    /// </summary>
    private void ShowRowContent()
    {
        var options = BossMode.Options;
        BossTypeRow.SetActive(!options.Mixed);

        // In Advanced Mode every tier has its own speed, health and skulls, set in its picker. Boss Speed and Boss
        // Health still apply on top, so when a code or an older setup has them, their rows stay, for the player to
        // see and reset; they are hidden again once both are back at 100 % (#112)
        var modifiers = Editor.dcm?.bloonModifiers;
        var bossSpeedSet = !Mathf.Approximately(modifiers?.bossSpeedMultiplier ?? 1, 1);
        var bossHealthSet = !Mathf.Approximately(modifiers?.healthMultipliers?.boss ?? 1, 1);
        BossSpeed.Row.SetActive(!options.Mixed || bossSpeedSet);
        BossHealth.Row.SetActive(!options.Mixed || bossHealthSet);
        skullModeRow.SetActive(!options.Mixed);
        SkullCount.Row.SetActive(!options.Mixed && options.SkullMode == Config.SkullMode.EvenlySpaced);
        customSkullsRow.SetActive(!options.Mixed && options.SkullMode == Config.SkullMode.Custom);
        Skulls.Row.SetActive(!options.Mixed);
        ShowSpawnRounds();
        Strip.Show(options, Editor.dcm);
        Skulls.Show(options);
    }

    /// <summary>
    /// Which rows are shown and how high they are, as text, to see whether the section must be fitted again
    /// </summary>
    private string RowLayout()
    {
        var layout = new System.Text.StringBuilder(Section.activeSelf ? "1" : "0");
        for (var i = 0; i < rows.childCount; i++)
        {
            var row = rows.GetChild(i);
            layout.Append(row.gameObject.activeSelf ? '|' : '-');
            if (row.gameObject.activeSelf) layout.Append(row.GetComponent<RectTransform>().sizeDelta.y.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        }

        return layout.ToString();
    }

    /// <summary>
    /// Fits the section again only when a row was shown, hidden or changed height since it was last fitted. Laying out
    /// the editor's whole settings area is slow, and dragging a slider changes an option many times a second (#184).
    /// </summary>
    private void FitIfChanged()
    {
        if (RowLayout() != fittedLayout) FitSection();
    }

    /// <summary>
    /// Makes the section as high as its shown rows, and room for it in the editor's scroll area
    /// </summary>
    private void FitSection()
    {
        Fits++;
        fittedLayout = RowLayout();

        // Rows are as high as the slider rows they copy, except the tier strip and the skull bar
        var visible = Enumerable.Range(0, rows.childCount).Select(rows.GetChild).Where(row => row.gameObject.activeSelf).ToList();
        var rowHeights = visible.Sum(row => row.GetComponent<RectTransform>().sizeDelta.y);
        var height = Section.activeSelf
            ? Padding + rowHeights + Math.Max(visible.Count - 1, 0) * Spacing + Padding / 2
            : 0;

        var sectionRect = Section.GetComponent<RectTransform>();
        sectionRect.sizeDelta = new Vector2(sectionRect.sizeDelta.x, height);
        rows.GetComponent<RectTransform>().sizeDelta = new Vector2(rows.GetComponent<RectTransform>().sizeDelta.x,
            height - Padding * 1.5f);

        ResizeScrollContent(height);
        LayoutRebuilder.ForceRebuildLayoutImmediate(Section.transform.parent.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(rows.GetComponent<RectTransform>());
    }

    /// <summary>
    /// The editor's content has a fixed height with its parts anchored to the middle, so growing it means moving
    /// those parts up by half the growth to keep them in place
    /// </summary>
    private void ResizeScrollContent(float height)
    {
        everything ??= Section.transform.parent.parent.GetComponent<RectTransform>();
        var delta = height - appliedHeight;
        if (Math.Abs(delta) < 0.01f || everything == null) return;

        everything.sizeDelta += new Vector2(0, delta);
        for (var i = 0; i < everything.childCount; i++)
        {
            var child = everything.GetChild(i).GetComponent<RectTransform>();
            if (child != null && Math.Abs(child.anchorMin.y - 0.5f) < 0.01f && Math.Abs(child.anchorMax.y - 0.5f) < 0.01f)
            {
                child.anchoredPosition += new Vector2(0, delta / 2);
            }
        }

        appliedHeight = height;
    }

    private void OnPlay()
    {
        Editor.ValidateInputFields();
        Editor.ApplyUiSettingsToDCM();

        var dcm = Editor.dcm;
        var problem = PlayProblem(dcm, BossMode.Options);
        if (problem != null)
        {
            PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(problem, null));
            return;
        }

        BossGameLauncher.Launch(dcm, BossMode.Options);
    }

    /// <summary>
    /// Why the setup can't be played, or null if it can
    /// </summary>
    public static string? PlayProblem(DailyChallengeModel dcm, BossOptions options)
    {
        if (UnsupportedModes.Contains(dcm.mode)) return Texts.UnsupportedMode.T(dcm.mode);

        // Starting on or after a tier's round skips it, the same rule the Random button keeps to
        var start = dcm.startRules?.round ?? -1;
        var firstSpawn = options.EffectiveSpawnRounds[0];
        if (start >= firstSpawn) return Texts.StartRoundAfterBoss.T(start, firstSpawn);

        var missing = ExtraTiers.MissingVanillaTier(Il2CppAssets.Scripts.Unity.Game.instance.model, options);
        if (missing == null) return null;

        ModHelper.Warning<CustomBossChallenge>($"No bloon model {missing}");
        return Texts.BossUnavailable.T(missing);
    }

    private static string Percent(float value) => ShownNumber.Whole(value) + " %";

    private static string PerTier(float value) => Texts.PerTier.T(ShownNumber.Signed(value));

    /// <summary>
    /// Behind a Random tier's die when the tier is Elite, since the die has no Elite look of its own like the bosses'
    /// portraits have
    /// </summary>
    public const string EliteGlowName = "EliteGlow";

    private static readonly Color EliteGlowColor = new(1f, 0.32f, 0.08f, 0.9f);
    private const float EliteGlowSize = 1.3f;

    /// <summary>
    /// Shows or hides the Elite glow behind <paramref name="icon"/>. The glow is the icon's sibling just before it, so
    /// it is drawn behind it, and follows the icon's place and size.
    /// </summary>
    public static void ShowEliteGlow(Component icon, bool on)
    {
        var parent = icon.transform.parent;
        var name = $"{EliteGlowName}{icon.name}";
        var glow = parent.Find(name);
        if (glow == null)
        {
            if (!on) return;
            var image = ModHelperImage.Create(new Info(name), VanillaSprites.GlowSkillCircle);
            image.Image.color = EliteGlowColor;
            image.Image.raycastTarget = false;
            image.transform.SetParent(parent, false);
            glow = image.transform;
        }

        var source = icon.GetComponent<RectTransform>();
        var rect = glow.GetComponent<RectTransform>();
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta * EliteGlowSize;
        glow.SetSiblingIndex(Math.Max(icon.transform.GetSiblingIndex() - (glow.GetSiblingIndex() < icon.transform.GetSiblingIndex() ? 1 : 0), 0));
        glow.gameObject.SetActive(on);
    }

    public static string Portrait(BossType boss, bool elite) => (boss, elite) switch
    {
        (BossType.Bloonarius, false) => VanillaSprites.BloonariusPortrait,
        (BossType.Bloonarius, true) => VanillaSprites.BloonariusPortraitElite,
        (BossType.Lych, false) => VanillaSprites.LychPortrait,
        (BossType.Lych, true) => VanillaSprites.LychPortraitElite,
        (BossType.Vortex, false) => VanillaSprites.VortexPortrait,
        (BossType.Vortex, true) => VanillaSprites.VortexPortraitElite,
        (BossType.Dreadbloon, false) => VanillaSprites.DreadbloonPortrait,
        (BossType.Dreadbloon, true) => VanillaSprites.DreadbloonPortraitElite,
        (BossType.Phayze, false) => VanillaSprites.PhayzePortrait,
        (BossType.Phayze, true) => VanillaSprites.PhayzePortraitElite,
        (BossType.Blastapopoulos, false) => VanillaSprites.BlastapopoulosPortrait,
        (BossType.Blastapopoulos, true) => VanillaSprites.BlastapopoulosPortraitElite,
        (BossType.Diamondback, false) => VanillaSprites.DiamondbackPortrait,
        (BossType.Diamondback, true) => VanillaSprites.DiamondbackPortraitElite,
        _ => VanillaSprites.BossChallenge
    };
}
