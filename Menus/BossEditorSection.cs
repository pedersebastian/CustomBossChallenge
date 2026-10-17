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
    public const string PlayName = "CustomBossPlay";
    public static string Title => Texts.BossCreator.T();
    private const string SelectedMarker = "Selected";

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
    public SliderRow ExtraTierHealth { get; private set; } = null!;
    public SliderRow SkullCount { get; private set; } = null!;
    public SliderRow MaxParagons { get; private set; } = null!;
    public ModHelperCheckbox Elite { get; private set; } = null!;
    public ModHelperCheckbox AllFortified { get; private set; } = null!;
    public ModHelperDropdown SkullMode { get; private set; } = null!;
    public ModHelperInputField CustomSkulls { get; private set; } = null!;
    public ModHelperInputField SpawnRounds { get; private set; } = null!;
    public ModHelperButton CopyCode { get; private set; } = null!;
    public ModHelperButton PasteCode { get; private set; } = null!;
    public Dictionary<BossType, ModHelperButton> BossButtons { get; } = new();

    private readonly Transform rows;
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

        var rowsObject = new GameObject("Rows", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
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

        var fortifiedRow = EditorRows.LabelRow(template, rows, "FortifiedRow", Texts.AllFortified.T(), VanillaSprites.FortifiedBloonsIcon);
        AllFortified = ModHelperCheckbox.Create(EditorRows.ControlInfo("AllFortified", 150, 150),
            false, VanillaSprites.BlueInsertPanelRound,
            new Action<bool>(value => UpdateDcm(dcm => dcm.bloonModifiers.allFortified = value)), VanillaSprites.TickGreenIcon, 20);
        AllFortified.transform.SetParent(fortifiedRow.transform, false);

        // The last position means no limit
        var paragons = ParagonMath.Available;
        MaxParagons = SliderRow.Clone(template, rows, "MaxParagonsRow", Texts.MaxParagons.T(), VanillaSprites.ParagonLimitIcon,
            0, paragons, 1, paragons, value => value >= paragons ? Texts.Unlimited.T() : $"{value:0}",
            value => UpdateDcm(dcm => dcm.maxParagons = ParagonMath.FromSlider((int) value, paragons)));

        Tiers = SliderRow.Clone(template, rows, "TiersRow", Texts.BossTiers.T(), VanillaSprites.BossTiersIconSmall,
            TierMath.MinTiers, TierMath.MaxTiers, 1, TierMath.VanillaTiers, value => $"{value:0}",
            value => UpdateOptions(o => o with {Tiers = (int) value}));
        ExtraTierHealth = SliderRow.Clone(template, rows, "ExtraTierHealthRow", Texts.ExtraTierHealth.T(),
            VanillaSprites.BossT5Icon, TierMath.MinExtraTierHealth, TierMath.MaxExtraTierHealth, 10, 150, value => Texts.PerTier.T((int) (value - 100)),
            value => UpdateOptions(o => o with {ExtraTierHealth = (int) value}));

        var roundsRow = EditorRows.LabelRow(template, rows, "SpawnRoundsRow", Texts.SpawnRounds.T(), VanillaSprites.BossT1Icon);
        SpawnRounds = ModHelperInputField.Create(EditorRows.ControlInfo("SpawnRounds", 1700, 150),
            TierMath.FormatRounds(TierMath.DefaultSpawnRounds(TierMath.VanillaTiers)), VanillaSprites.BlueInsertPanelRound,
            null, 70, TMP_InputField.CharacterValidation.None, TextAlignmentOptions.Center, "40, 60, 80, ...", 10);
        SpawnRounds.transform.SetParent(roundsRow.transform, false);
        SpawnRounds.InputField.onEndEdit.AddListener(new Action<string>(text =>
        {
            UpdateOptions(o => o with {SpawnRounds = text});
            ShowSpawnRounds();
        }));
        var resetRounds = ModHelperButton.Create(new Info("ResetRounds", -132, 0, 180, 180)
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
        CustomSkulls.InputField.onEndEdit.AddListener(new Action<string>(text =>
        {
            // Show what was understood; a list without any valid percentage keeps the previous skulls
            var skulls = SkullMath.NormalizeCustom(text, ShownSkulls(BossMode.Options));
            UpdateOptions(o => o with {CustomSkulls = skulls});
            CustomSkulls.SetText(skulls, false);
        }));

        // Sharing the setup as a text code, after all the boss options
        var shareRow = EditorRows.LabelRow(template, rows, "ShareCodeRow", Texts.ShareCode.T(), VanillaSprites.CopyClipboardBtn);
        var shareX = EditorRows.ControlX - EditorRows.ControlWidth / 2 + 300;
        CopyCode = ShareButton(shareRow, "CopyCode", shareX, Texts.CopyCode.T(), OnCopyCode);
        PasteCode = ShareButton(shareRow, "PasteCode", shareX + 640, Texts.PasteCode.T(), OnPasteCode);

        EditorRows.AddInfo(bossRow, Texts.BossType, Texts.BossTypeInfo);
        EditorRows.AddInfo(BossSpeed.Row, Texts.BossSpeed, Texts.BossSpeedInfo);
        EditorRows.AddInfo(BossHealth.Row, Texts.BossHealth, Texts.BossHealthInfo);
        EditorRows.AddInfo(fortifiedRow, Texts.AllFortified, Texts.AllFortifiedInfo);
        EditorRows.AddInfo(MaxParagons.Row, Texts.MaxParagons, Texts.MaxParagonsInfo);
        EditorRows.AddInfo(Tiers.Row, Texts.BossTiers, Texts.BossTiersInfo);
        EditorRows.AddInfo(ExtraTierHealth.Row, Texts.ExtraTierHealth, Texts.ExtraTierHealthInfo);
        EditorRows.AddInfo(roundsRow, Texts.SpawnRounds, Texts.SpawnRoundsInfo);
        EditorRows.AddInfo(skullModeRow, Texts.Skulls, Texts.SkullsInfo);
        EditorRows.AddInfo(SkullCount.Row, Texts.SkullCount, Texts.SkullCountInfo);
        EditorRows.AddInfo(customSkullsRow, Texts.SkullHealth, Texts.SkullHealthInfo);
        EditorRows.AddInfo(shareRow, Texts.ShareCode, Texts.ShareCodeInfo);
    }

    private static ModHelperButton ShareButton(GameObject row, string name, float x, string label, Action action)
    {
        var button = ModHelperButton.Create(new Info(name, x, 0, 560, 170)
        {
            Anchor = new Vector2(1, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.BlueBtnLong, action);
        button.transform.SetParent(row.transform, false);
        button.AddText(new Info("Label", InfoPreset.FillParent), label, 70);
        return button;
    }

    private void OnCopyCode()
    {
        Editor.ApplyUiSettingsToDCM();
        var code = SetupCode.Encode(BossMode.Options, Editor.dcm);
        Clipboard.Write(code);
        PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(Texts.CodeCopied.T(code.Length), null));
    }

    private void OnPasteCode()
    {
        if (!SetupCode.TryDecode(Clipboard.Read(), out var options, out var dcm, out var problem) || dcm == null)
        {
            var text = problem == CodeProblem.TooNew ? Texts.CodeTooNew : Texts.CodeInvalid;
            PopupScreen.instance.SafelyQueue(screen => screen.ShowOkPopup(text.T(), null));
            return;
        }

        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter, Texts.ShareCode.T(),
            $"{Texts.LoadCodeQuestion.T()}\n\n{Summary(options, dcm)}",
            new Action(() => BossMode.Replace(Editor, options, dcm)), Texts.Load.T(),
            null, Texts.Cancel.T(), Popup.TransitionAnim.Scale));
    }

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

    private GameObject BuildPlayButton()
    {
        var template = Editor.transform.Find("Play");
        GameObject play;
        if (template != null)
        {
            play = Object.Instantiate(template.gameObject, template.parent, false);
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
    }

    private void SetVisible(bool show)
    {
        Section.SetActive(show);
        Play.SetActive(show);
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
                BossSpeed.Show(Mathf.Round(dcm.bloonModifiers.bossSpeedMultiplier * 100));
                BossHealth.Show(Mathf.Round(dcm.bloonModifiers.healthMultipliers.boss * 100));
                AllFortified.SetChecked(dcm.bloonModifiers.allFortified, false);
                MaxParagons.Show(ParagonMath.ToSlider(dcm.maxParagons, (int) MaxParagons.Slider.maxValue));
            }

            Elite.SetChecked(options.Elite, false);
            Tiers.Show(options.Tiers);
            ExtraTierHealth.Show(options.ExtraTierHealth);
            SkullMode.Dropdown.SetValueWithoutNotify((int) options.SkullMode);
            SkullCount.Show(options.SkullCount);
            CustomSkulls.SetText(ShownSkulls(options), false);
            ShowSpawnRounds();
            FollowBossName(options.Boss);
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
        dcm.bloonModifiers.allFortified = AllFortified.CurrentValue;
        dcm.maxParagons = ParagonMath.FromSlider((int) MaxParagons.Value, (int) MaxParagons.Slider.maxValue);
    }

    private void UpdateDcm(Action<DailyChallengeModel> change)
    {
        if (refreshing || Editor.dcm == null) return;
        Editor.dcm.bloonModifiers ??= new BloonModifiers();
        Editor.dcm.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
        change(Editor.dcm);
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
        FollowBossName(boss);
        ShowBoss();
    }

    /// <summary>
    /// Follows the boss in the challenge name as long as the player hasn't named it themselves
    /// </summary>
    private void FollowBossName(BossType boss)
    {
        var name = Editor.chalName;
        if (name == null || !Enum.GetValues<BossType>().Any(other => name.text == BossMode.DefaultName(other))) return;

        name.text = BossMode.DefaultName(boss);
        if (Editor.dcm != null) Editor.dcm.name = name.text;
    }

    private void SetElite(bool elite)
    {
        if (refreshing) return;
        BossMode.Options = BossMode.Options with {Elite = elite};
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
    }

    private static string ShownSkulls(BossOptions options) =>
        SkullMath.NormalizeCustom(options.CustomSkulls, new BossOptions().CustomSkulls);

    private void ShowSpawnRounds() =>
        SpawnRounds.SetText(TierMath.FormatRounds(BossMode.Options.EffectiveSpawnRounds), false);

    /// <summary>
    /// Shows only the rows that apply, and makes room for the section in the editor's scroll area
    /// </summary>
    public void Relayout()
    {
        var options = BossMode.Options;
        ExtraTierHealth.Row.SetActive(options.Tiers > TierMath.VanillaTiers);
        SkullCount.Row.SetActive(options.SkullMode == Config.SkullMode.EvenlySpaced);
        customSkullsRow.SetActive(options.SkullMode == Config.SkullMode.Custom);
        ShowSpawnRounds();

        var visibleRows = Enumerable.Range(0, rows.childCount).Count(i => rows.GetChild(i).gameObject.activeSelf);
        var height = Section.activeSelf
            ? Padding + visibleRows * EditorRows.RowHeight + Math.Max(visibleRows - 1, 0) * Spacing + Padding / 2
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

        var missing = ExtraTiers.MissingVanillaTier(Il2CppAssets.Scripts.Unity.Game.instance.model, options);
        if (missing == null) return null;

        ModHelper.Warning<CustomBossChallenge>($"No bloon model {missing}");
        return Texts.BossUnavailable.T(missing);
    }

    private static string Percent(float value) => $"{value:0} %";

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
