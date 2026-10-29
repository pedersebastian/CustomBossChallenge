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
    public ModHelperDropdown SkullMode { get; private set; } = null!;
    public ModHelperInputField CustomSkulls { get; private set; } = null!;
    public SkullStrip Skulls { get; private set; } = null!;
    public ModHelperInputField SpawnRounds { get; private set; } = null!;
    public ModHelperDropdown SavedList { get; private set; } = null!;
    public ModHelperButton SaveSetup { get; private set; } = null!;
    public ModHelperButton LoadSetup { get; private set; } = null!;
    public ModHelperButton DeleteSetup { get; private set; } = null!;

    /// <summary>
    /// The saved setup picked in the list, or null when there are none
    /// </summary>
    public string? SelectedSetup { get; private set; }

    /// <summary>
    /// The names shown in the saved setups list, in its order
    /// </summary>
    private List<string> savedNames = new();

    public ModHelperButton CopyCode { get; private set; } = null!;
    public ModHelperButton PasteCode { get; private set; } = null!;
    public Dictionary<BossType, ModHelperButton> BossButtons { get; } = new();

    private readonly Transform rows;
    private ModHelperButton resetRounds = null!;
    private readonly List<FocusStop> focusStops = new();
    private GameObject customSkullsRow = null!;
    private RectTransform? everything;
    private float appliedHeight;
    private bool refreshing;

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

        var topInfo = editor.transform.Find("TopInfo/Title");
        titleText = topInfo != null ? topInfo.GetComponent<TMP_Text>() : null;
        originalTitleKey = titleText?.TryCast<Il2Cpp.NK_TextMeshProUGUI>()?.localizeKey;
    }

    private void BuildRows(ChallengeEditorSliderPanel template)
    {
        // Boss picker and elite toggle
        var bossRow = EditorRows.LabelRow(template, rows, "BossRow", Texts.BossType.T(), VanillaSprites.BossChallenge);
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

        BossSpeed = SliderRow.Clone(template, rows, "BossSpeedRow", Texts.BossSpeed.T(), VanillaSprites.FasterBossIcon,
            10, 1000, 10, 100, Percent, value => UpdateDcm(dcm => dcm.bloonModifiers.bossSpeedMultiplier = value / 100f));
        BossHealth = SliderRow.Clone(template, rows, "BossHealthRow", Texts.BossHealth.T(), VanillaSprites.BossBoostIcon,
            10, 2000, 10, 100, Percent, value => UpdateDcm(dcm => dcm.bloonModifiers.healthMultipliers.boss = value / 100f));

        // The last position means no limit
        var paragons = ParagonMath.Available;
        MaxParagons = SliderRow.Clone(template, rows, "MaxParagonsRow", Texts.MaxParagons.T(), VanillaSprites.ParagonLimitIcon,
            0, paragons, 1, paragons, value => value >= paragons ? Texts.Unlimited.T() : $"{value:0}",
            value => UpdateDcm(dcm => dcm.maxParagons = ParagonMath.FromSlider((int) value, paragons)));

        Tiers = SliderRow.Clone(template, rows, "TiersRow", Texts.BossTiers.T(), VanillaSprites.BossTiersIconSmall,
            TierMath.MinTiers, TierMath.MaxTiers, 1, TierMath.VanillaTiers, value => $"{value:0}",
            value => UpdateOptions(o => o with {Tiers = (int) value}));
        SpeedPerTier = SliderRow.Clone(template, rows, "SpeedPerTierRow", Texts.SpeedPerTier.T(), VanillaSprites.FasterBossIcon,
            TierMath.MinSpeedPerTier, TierMath.MaxSpeedPerTier, TierMath.PerTierStep, 0, PerTier,
            value => UpdateOptions(o => o with {SpeedPerTier = (int) value}));
        HealthPerTier = SliderRow.Clone(template, rows, "HealthPerTierRow", Texts.HealthPerTier.T(), VanillaSprites.BossBoostIcon,
            TierMath.MinHealthPerTier, TierMath.MaxHealthPerTier, TierMath.PerTierStep, 0, PerTier,
            value => UpdateOptions(o => o with {HealthPerTier = (int) value}));
        var stripRow = EditorRows.LabelRow(template, rows, "TierStripRow", Texts.TierPreview.T(), VanillaSprites.BossT5Icon);
        Strip = new TierStrip(stripRow);

        FirstSpawnRound = SliderRow.Clone(template, rows, "FirstSpawnRoundRow", Texts.FirstBossRound.T(),
            VanillaSprites.BossT1Icon, 1, TierMath.MaxFirstSpawnRound, 1, TierMath.FirstSpawnRound, value => $"{value:0}",
            _ => SpreadSpawnRounds());
        RoundsBetween = SliderRow.Clone(template, rows, "RoundsBetweenRow", Texts.RoundsBetweenTiers.T(),
            VanillaSprites.BossTiersIconSmall, 1, TierMath.MaxRoundsBetweenTiers, 1, TierMath.RoundsBetweenTiers,
            value => $"{value:0}", _ => SpreadSpawnRounds());

        var roundsRow = EditorRows.LabelRow(template, rows, "SpawnRoundsRow", Texts.SpawnRounds.T(), VanillaSprites.BossT1Icon);
        SpawnRounds = ModHelperInputField.Create(EditorRows.ControlInfo("SpawnRounds", 1700, 150),
            TierMath.FormatRounds(TierMath.DefaultSpawnRounds(TierMath.VanillaTiers)), VanillaSprites.BlueInsertPanelRound,
            null, 70, TMP_InputField.CharacterValidation.None, TextAlignmentOptions.Center, "40, 60, 80, ...", 10);
        SpawnRounds.transform.SetParent(roundsRow.transform, false);
        FollowKeyboard(SpawnRounds);
        SpawnRounds.InputField.onEndEdit.AddListener(new Action<string>(text =>
        {
            UpdateOptions(o => o with {SpawnRounds = text});
            ShowSpawnRounds();
        }));
        resetRounds = ModHelperButton.Create(new Info("ResetRounds", -132, 0, 180, 180)
            {
                Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
            },
            VanillaSprites.RestartBtn, new Action(() =>
            {
                UpdateOptions(o => o with {SpawnRounds = null});
                ShowSpawnRounds();
            }));
        resetRounds.transform.SetParent(roundsRow.transform, false);

        var skullModeRow = EditorRows.LabelRow(template, rows, "SkullModeRow", Texts.Skulls.T(), VanillaSprites.CrossboneSkull);
        var modes = new Il2CppSystem.Collections.Generic.List<string>();
        modes.Add(Texts.SkullsVanilla.T());
        modes.Add(Texts.SkullsEvenlySpaced.T());
        modes.Add(Texts.SkullsCustom.T());
        SkullMode = ModHelperDropdown.Create(EditorRows.ControlInfo("SkullMode", 900, 150),
            modes, 500, new Action<int>(index =>
            {
                UpdateOptions(o => o with {SkullMode = (Config.SkullMode) index});
                Relayout();
            }), VanillaSprites.BlueInsertPanelRound, 70);
        SkullMode.transform.SetParent(skullModeRow.transform, false);

        SkullCount = SliderRow.Clone(template, rows, "SkullCountRow", Texts.SkullCount.T(), VanillaSprites.CrossboneSkull,
            0, SkullMath.MaxSkulls, 1, 3, value => $"{value:0}", value => UpdateOptions(o => o with {SkullCount = (int) value}));

        customSkullsRow = EditorRows.LabelRow(template, rows, "CustomSkullsRow", Texts.SkullHealth.T(), VanillaSprites.CrossboneSkull);
        CustomSkulls = ModHelperInputField.Create(EditorRows.ControlInfo("CustomSkulls", 1700, 150), "75, 50, 25",
            VanillaSprites.BlueInsertPanelRound, null, 70, TMP_InputField.CharacterValidation.None,
            TextAlignmentOptions.Center, "75, 50, 25", 10);
        CustomSkulls.transform.SetParent(customSkullsRow.transform, false);
        FollowKeyboard(CustomSkulls);
        CustomSkulls.InputField.onEndEdit.AddListener(new Action<string>(text =>
        {
            // Show what was understood; a list without any valid percentage keeps the previous skulls
            var skulls = SkullMath.NormalizeCustom(text, ShownSkulls(BossMode.Options));
            UpdateOptions(o => o with {CustomSkulls = skulls});
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
                if (index >= 0 && index < savedNames.Count) SelectedSetup = savedNames[index];
            }), VanillaSprites.BlueInsertPanelRound, 60);
        SavedList.transform.SetParent(savedRow.transform, false);
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
        EditorRows.AddInfo(skullModeRow, Texts.Skulls, Texts.SkullsInfo);
        EditorRows.AddInfo(SkullCount.Row, Texts.SkullCount, Texts.SkullCountInfo);
        EditorRows.AddInfo(customSkullsRow, Texts.SkullHealth, Texts.SkullHealthInfo);
        EditorRows.AddInfo(skullStripRow, Texts.SkullPreview, Texts.SkullPreviewInfo);
        EditorRows.AddInfo(savedRow, Texts.SavedSetups, Texts.SavedSetupsInfo);
        EditorRows.AddInfo(shareRow, Texts.ShareCode, Texts.ShareCodeInfo);
    }

    public const float SavedListWidth = 760;
    public const float SavedListHeight = 750;
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
    /// The text fields among <see cref="FocusStops"/>
    /// </summary>
    public IReadOnlyList<TMP_InputField> InputFields => FocusStops.Where(stop => stop.Field != null).Select(stop => stop.Field!).ToList();

    public Button PlayButton => Play.GetComponentInChildren<Button>(true);

    private void BuildFocusStops()
    {
        // Every boss is a stop of its own, picked with Enter or Space like a click
        foreach (var (boss, button) in BossButtons) focusStops.Add(FocusStop.ForButton($"Boss{boss}", button.Button));
        focusStops.Add(new FocusStop("Elite", () => Elite.gameObject) {Activate = () => Elite.Toggle.isOn = !Elite.Toggle.isOn});
        focusStops.Add(FocusStop.ForSlider("BossSpeed", BossSpeed));
        focusStops.Add(FocusStop.ForSlider("BossHealth", BossHealth));
        focusStops.Add(FocusStop.ForSlider("MaxParagons", MaxParagons));
        focusStops.Add(FocusStop.ForSlider("Tiers", Tiers));
        focusStops.Add(FocusStop.ForSlider("SpeedPerTier", SpeedPerTier));
        focusStops.Add(FocusStop.ForSlider("HealthPerTier", HealthPerTier));
        focusStops.Add(FocusStop.ForSlider("FirstSpawnRound", FirstSpawnRound));
        focusStops.Add(FocusStop.ForSlider("RoundsBetween", RoundsBetween));
        focusStops.Add(FocusStop.ForField("SpawnRounds", SpawnRounds.InputField));
        focusStops.Add(FocusStop.ForButton("ResetRounds", resetRounds.Button));
        focusStops.Add(FocusStop.ForDropdown("SkullMode", SkullMode.Dropdown));
        focusStops.Add(FocusStop.ForSlider("SkullCount", SkullCount));
        focusStops.Add(FocusStop.ForField("CustomSkulls", CustomSkulls.InputField));
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
        PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(Texts.CodeCopied.T(code.Length), null));
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

    /// <summary>
    /// Fills the saved setups list, keeping <see cref="SelectedSetup"/> picked when it is still there. Without saved
    /// setups the list says so, and Load and Delete can't be pressed.
    /// </summary>
    public void ShowSavedSetups()
    {
        var names = savedNames = BossMode.Saved.All.Select(entry => entry.Name).ToList();
        if (SelectedSetup == null || !names.Contains(SelectedSetup)) SelectedSetup = names.FirstOrDefault();

        var shown = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var name in names) shown.Add(PlainText(name));
        if (names.Count == 0) shown.Add(Texts.NoSavedSetups.T());

        var dropdown = SavedList.Dropdown;
        dropdown.ClearOptions();
        dropdown.AddOptions(shown);
        dropdown.SetValueWithoutNotify(SelectedSetup == null ? 0 : names.IndexOf(SelectedSetup));
        dropdown.RefreshShownValue();
        dropdown.interactable = names.Count > 0;

        // Mod Helper sizes the opened list for the options it was created with, so size it for the current ones
        var template = SavedList.TemplatePanel.RectTransform;
        var itemHeight = SavedList.RectTransform.sizeDelta.y;
        var listHeight = Math.Min(SavedListHeight, Math.Max(shown.Count, 1) * itemHeight);
        template.sizeDelta = new Vector2(template.sizeDelta.x, listHeight);
        template.anchoredPosition = new Vector2(template.anchoredPosition.x, -itemHeight / 2 - listHeight / 2);
        LoadSetup.Button.interactable = names.Count > 0;
        DeleteSetup.Button.interactable = names.Count > 0;
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
        $"'{dcm.name}', {options.Boss}{(options.Elite ? " Elite" : "")}, {options.Tiers} tiers, " +
        $"{dcm.map}, {dcm.difficulty}, {dcm.mode}";

    /// <summary>
    /// What a shared setup is, in a few words
    /// </summary>
    public static string Summary(BossOptions options, DailyChallengeModel dcm)
    {
        var boss = options.Elite ? $"{options.Boss} {Texts.Elite.T()}" : options.Boss.ToString();
        return $"<b>{PlainText(dcm.name)}</b>\n{boss}, {Texts.TierCount.T(options.Tiers)}\n" +
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
            Tiers.Show(options.Tiers);
            SpeedPerTier.Show(options.SpeedPerTier);
            HealthPerTier.Show(options.HealthPerTier);
            SkullMode.Dropdown.SetValueWithoutNotify((int) options.SkullMode);
            SkullCount.Show(options.SkullCount);
            CustomSkulls.SetText(ShownSkulls(options), false);
            ShowSpawnRounds();
            FollowBossName(options.Boss, options.Elite);
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

    private void UpdateDcm(Action<DailyChallengeModel> change)
    {
        if (refreshing || Editor.dcm == null) return;
        Editor.dcm.bloonModifiers ??= new BloonModifiers();
        Editor.dcm.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
        change(Editor.dcm);
        ShowStrip();
    }

    private void UpdateOptions(Func<BossOptions, BossOptions> change)
    {
        if (refreshing) return;
        BossMode.Options = change(BossMode.Options);
        if (!refreshing) Relayout();
    }

    private void SelectBoss(BossType boss)
    {
        BossMode.Options = BossMode.Options with {Boss = boss};
        FollowBossName(boss, BossMode.Options.Elite);
        ShowBoss();
    }

    /// <summary>
    /// Follows the boss and Elite in the challenge name as long as the player hasn't named it themselves
    /// </summary>
    private void FollowBossName(BossType boss, bool elite)
    {
        var name = Editor.chalName;
        if (name == null || !BossMode.IsDefaultName(name.text)) return;

        name.text = BossMode.DefaultName(boss, elite);
        if (Editor.dcm != null) Editor.dcm.name = name.text;
    }

    private void SetElite(bool elite)
    {
        if (refreshing) return;
        BossMode.Options = BossMode.Options with {Elite = elite};
        FollowBossName(BossMode.Options.Boss, elite);
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
    /// The strip grows when it shows the health cap line, and the rows below must move with it
    /// </summary>
    private void ShowStrip()
    {
        // The boss's own skulls follow the boss and Elite
        Skulls.Show(BossMode.Options);
        if (Strip.Show(BossMode.Options, Editor.dcm) && !refreshing) Relayout();
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
    private void SpreadSpawnRounds() =>
        UpdateOptions(o => o with {SpawnRounds = TierMath.EvenRounds((int) FirstSpawnRound.Value, (int) RoundsBetween.Value)});

    /// <summary>
    /// Shows only the rows that apply, and makes room for the section in the editor's scroll area
    /// </summary>
    public void Relayout()
    {
        var options = BossMode.Options;
        SkullCount.Row.SetActive(options.SkullMode == Config.SkullMode.EvenlySpaced);
        customSkullsRow.SetActive(options.SkullMode == Config.SkullMode.Custom);
        ShowSpawnRounds();
        Strip.Show(options, Editor.dcm);
        Skulls.Show(options);

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

    private static string Percent(float value) => $"{value:0} %";

    private static string PerTier(float value) => Texts.PerTier.T(value > 0 ? $"+{value:0}" : $"{value:0}");

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
