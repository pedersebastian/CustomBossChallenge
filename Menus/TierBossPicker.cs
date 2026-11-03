using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The window that opens on a tier's card in the Tier Preview in Advanced Mode: every boss and Random to pick from, the
/// tier's own Boss Speed, Boss Health and skulls with their preview, and Elite for that tier. A click outside it or Esc
/// closes it. The keyboard moves through its controls with the arrows (or Tab), steps a slider or the skull mode with
/// left and right, puts it back to its default with Delete, types in the skull list after Enter, and presses buttons
/// with Enter or Space.
/// </summary>
public sealed class TierBossPicker
{
    public const string Name = "CustomBossTierPicker";
    public const float Width = 2140;
    private const float SliderWidth = 2000;

    /// <summary>
    /// From the top of the window: the middle of the boss buttons and of the first row, the room a row takes, and the
    /// room from the last row to Elite and from Elite to the bottom
    /// </summary>
    private const float ChoicesTop = 370;
    private const float RowsTop = 660;
    private const float RowStep = 220;
    private const float EliteGap = 130;
    private const float BottomGap = 120;
    private const float ChoiceSize = 200;
    private const float ChoiceGap = 30;

    /// <summary>
    /// The picker on the screen, or null
    /// </summary>
    public static TierBossPicker? Current => current != null && current.Root != null ? current : null;

    private static TierBossPicker? current;

    public int Tier { get; }
    public GameObject Root { get; }
    public ModHelperPanel Panel { get; }
    public ModHelperText Title { get; }

    /// <summary>
    /// A button per boss and one for Random, by <see cref="TierBossMath.Choices"/>
    /// </summary>
    public Dictionary<string, ModHelperButton> Choices { get; } = new();

    public ModHelperCheckbox Elite { get; }
    public ModHelperButton Close { get; }

    /// <summary>
    /// The tier's own speed and health in percent, like Boss Speed and Boss Health in Standard
    /// </summary>
    public SliderRow Speed { get; }
    public SliderRow Health { get; }

    /// <summary>
    /// The tier's own skulls, like the Skulls rows in Standard: the mode, the count for Evenly Spaced or the list for
    /// Custom, and the preview of what the tier gets
    /// </summary>
    public ModHelperDropdown SkullMode { get; }
    public SliderRow SkullCount { get; }
    public ModHelperInputField CustomSkulls { get; }
    public SkullStrip Skulls { get; }
    public ModHelperButton ResetSkulls { get; }
    public ModHelperButton ResetCustomSkulls { get; }

    /// <summary>
    /// The window's height, which follows the skull rows that show
    /// </summary>
    public float Height => Panel.RectTransform.sizeDelta.y;

    /// <summary>
    /// What the keyboard moves through, in order: the bosses, Random, speed, health, the skull rows, Elite, Close;
    /// hidden ones are skipped
    /// </summary>
    public IReadOnlyList<GameObject> Stops { get; }

    /// <summary>
    /// The stop with the keyboard frame, or -1
    /// </summary>
    public int Focused { get; private set; } = -1;

    /// <summary>
    /// Whether the keyboard goes back to the tier's card when the picker closes, because it was opened from there
    /// </summary>
    public bool ReturnFocus { get; set; }

    private readonly BossEditorSection section;
    private readonly GameObject skullModeRow;
    private readonly GameObject customSkullsRow;
    private readonly GameObject eliteLabel;
    private bool refreshing;

    private TierBossPicker(BossEditorSection section, int tier)
    {
        this.section = section;
        Tier = tier;

        // Covers the whole editor, so a click anywhere else closes the picker instead of reaching the editor
        var shade = ModHelperPanel.Create(new Info(Name, InfoPreset.FillParent));
        Root = shade.gameObject;
        Root.transform.SetParent(section.Editor.transform, false);
        Root.transform.SetAsLastSibling();
        var dim = Root.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.6f);
        var closeOutside = Root.AddComponent<Button>();
        closeOutside.transition = Selectable.Transition.None;
        closeOutside.onClick.AddListener(new Action(Hide));

        Panel = shade.AddPanel(new Info("Panel", 0, 0, Width, RowsTop), VanillaSprites.MainBGPanelBlue);
        // The panel takes clicks itself, so clicking between its buttons doesn't close it
        Panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        Title = Panel.AddText(new Info("Title", 0, -40, Width - 300, 110)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 1)
        }, Texts.TierBossTitle.T(tier), 80);
        Close = Panel.AddButton(new Info("Close", -40, -40, 130, 130)
        {
            Anchor = new Vector2(1, 1), Pivot = new Vector2(1, 1)
        }, VanillaSprites.CloseBtn, new Action(Hide));

        var x = -(TierBossMath.Choices.Length - 1) * (ChoiceSize + ChoiceGap) / 2;
        foreach (var choice in TierBossMath.Choices)
        {
            var name = choice;
            var button = Panel.AddButton(new Info($"Choice{choice}", x, -ChoicesTop, ChoiceSize, ChoiceSize)
            {
                Anchor = new Vector2(0.5f, 1)
            }, VanillaSprites.RandomIcon, new Action(() => Pick(name)));
            var marker = button.AddImage(new Info(BossEditorSection.SelectedMarker, 0, 0, 110, 110)
            {
                Anchor = new Vector2(1, 0), Pivot = new Vector2(0.75f, 0.25f)
            }, VanillaSprites.TickGreenIcon);
            marker.Image.raycastTarget = false;
            // Long names like Blastapopoulos shrink to their own button's width instead of running into the next one
            var label = button.AddText(new Info("Label", 0, -ChoiceSize / 2 - 40, ChoiceSize + ChoiceGap - 16, 60),
                choice == TierBossMath.Random ? Texts.RandomBoss.T() : choice, 38);
            label.Text.enableAutoSizing = true;
            label.Text.textWrappingMode = TextWrappingModes.NoWrap;
            label.Text.fontSizeMin = 12;
            label.Text.fontSizeMax = 38;
            label.Text.raycastTarget = false;
            Choices[choice] = button;
            x += ChoiceSize + ChoiceGap;
        }

        // The editor's own rows, shrunk to fit the window; a run of changes is one step for Undo
        var template = section.SliderTemplate;
        Speed = Fit(SliderRow.Clone(template, Panel.transform, "Speed", Texts.BossSpeed.T(), VanillaSprites.FasterBossIcon,
            TierBossMath.MinSpeed, TierBossMath.MaxSpeed, TierBossMath.ValueStep, TierBossMath.Normal, value => $"{value:0} %",
            value => SetValues(Shown() with {Speed = (int) value}, "Speed")));
        Health = Fit(SliderRow.Clone(template, Panel.transform, "Health", Texts.BossHealth.T(), VanillaSprites.BossBoostIcon,
            TierBossMath.MinHealth, TierBossMath.MaxHealth, TierBossMath.ValueStep, TierBossMath.Normal, value => $"{value:0} %",
            value => SetValues(Shown() with {Health = (int) value}, "Health")));

        skullModeRow = Fit(EditorRows.LabelRow(template, Panel.transform, "SkullModeRow", Texts.Skulls.T(), VanillaSprites.CrossboneSkull));
        var modes = new Il2CppSystem.Collections.Generic.List<string>();
        modes.Add(Texts.SkullsVanilla.T());
        modes.Add(Texts.SkullsEvenlySpaced.T());
        modes.Add(Texts.SkullsCustom.T());
        SkullMode = ModHelperDropdown.Create(EditorRows.ControlInfo("SkullMode", 900, 150), modes, 500,
            new Action<int>(index => SetValues(Shown() with {Skulls = (Config.SkullMode) index}, "SkullMode")),
            VanillaSprites.BlueInsertPanelRound, 70);
        SkullMode.transform.SetParent(skullModeRow.transform, false);
        ResetSkulls = EditorRows.ResetButton(skullModeRow, "ResetSkulls", () => SetValues(Shown() with
        {
            Skulls = Config.SkullMode.Vanilla, SkullCount = SkullMath.DefaultCount, CustomSkulls = SkullMath.DefaultCustom
        }, "ResetSkulls"));

        SkullCount = Fit(SliderRow.Clone(template, Panel.transform, "SkullCountRow", Texts.SkullCount.T(),
            VanillaSprites.CrossboneSkull, 0, SkullMath.MaxSkulls, 1, SkullMath.DefaultCount, value => $"{value:0}",
            value => SetValues(Shown() with {SkullCount = (int) value}, "SkullCount")));

        customSkullsRow = Fit(EditorRows.LabelRow(template, Panel.transform, "CustomSkullsRow", Texts.SkullHealth.T(),
            VanillaSprites.CrossboneSkull));
        CustomSkulls = ModHelperInputField.Create(EditorRows.ControlInfo("CustomSkulls", 1700, 150), SkullMath.DefaultCustom,
            VanillaSprites.BlueInsertPanelRound, null, 70, TMP_InputField.CharacterValidation.None,
            TextAlignmentOptions.Center, SkullMath.DefaultCustom, 10);
        CustomSkulls.transform.SetParent(customSkullsRow.transform, false);
        CustomSkulls.InputField.onEndEdit.AddListener(new Action<string>(text =>
        {
            // Show what was understood; a list without any valid percentage keeps the previous skulls
            var shown = Shown();
            SetValues(shown with {CustomSkulls = SkullMath.NormalizeCustom(text, shown.CustomSkulls)}, "CustomSkulls");
            Show();
        }));
        ResetCustomSkulls = EditorRows.ResetButton(customSkullsRow, "ResetCustomSkulls",
            () => SetValues(Shown() with {CustomSkulls = SkullMath.DefaultCustom}, "ResetCustomSkulls"));

        Skulls = new SkullStrip(Fit(EditorRows.LabelRow(template, Panel.transform, "SkullStripRow", Texts.SkullPreview.T(),
            VanillaSprites.CrossboneSkull)));

        Elite = Panel.AddCheckbox(new Info("Elite", -110, 0, 140, 140), false, VanillaSprites.BlueInsertPanelRound,
            new Action<bool>(SetElite), VanillaSprites.TickGreenIcon, 20);
        eliteLabel = Panel.AddText(new Info("EliteLabel", 110, 0, 280, 140), Texts.Elite.T(), 70, TextAlignmentOptions.Left).gameObject;

        Stops = Choices.Values.Select(button => button.gameObject)
            .Concat(new[]
            {
                Speed.Row, Health.Row, SkullMode.gameObject, ResetSkulls.gameObject, SkullCount.Row, CustomSkulls.gameObject,
                ResetCustomSkulls.gameObject, Elite.gameObject, Close.gameObject
            }).ToList();
        Show();
    }

    private SliderRow Fit(SliderRow row)
    {
        Fit(row.Row);
        return row;
    }

    /// <summary>
    /// Makes one of the editor's rows as wide as there, so its controls sit the same, then shrinks it as a whole to fit
    /// the window; <see cref="Layout"/> places it
    /// </summary>
    private GameObject Fit(GameObject row)
    {
        var size = section.BossSpeed.Row.GetComponent<RectTransform>().rect.size;
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size.x, Mathf.Max(rect.sizeDelta.y, size.y));
        var scale = Mathf.Min(1, SliderWidth / Mathf.Max(size.x, 1));
        rect.localScale = new Vector3(scale, scale, 1);
        return row;
    }

    /// <summary>
    /// Shows the skull rows the tier's skull mode uses, stacks the rows from the top, and makes the window as high as
    /// they need
    /// </summary>
    private void Layout(TierBoss shown)
    {
        SkullCount.Row.SetActive(shown.Skulls == Config.SkullMode.EvenlySpaced);
        customSkullsRow.SetActive(shown.Skulls == Config.SkullMode.Custom);

        var rowHeight = section.BossSpeed.Row.GetComponent<RectTransform>().rect.height;
        var middle = RowsTop - RowStep;
        var previousRoom = RowStep;
        foreach (var row in new[] {Speed.Row, Health.Row, skullModeRow, SkullCount.Row, customSkullsRow, Skulls.Row})
        {
            if (!row.activeSelf) continue;
            var rect = row.GetComponent<RectTransform>();

            // The skull bar is higher than a slider row; the rest of a row's room is for the value above it
            var room = RowStep + (rect.sizeDelta.y - rowHeight) * rect.localScale.y;
            middle += (previousRoom + room) / 2;
            rect.anchoredPosition = new Vector2(0, -middle);
            previousRoom = room;
        }

        var elite = middle + previousRoom / 2 + EliteGap;
        foreach (var control in new[] {Elite.gameObject, eliteLabel})
        {
            var rect = control.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -elite);
        }

        Panel.RectTransform.sizeDelta = new Vector2(Width, elite + BottomGap);
    }

    private void SetValues(TierBoss tier, string control)
    {
        if (refreshing) return;
        section.SetTierBoss(Tier, tier, $"Tier{Tier}{control}");
        Show();
    }

    /// <summary>
    /// Opens the picker for a tier, closing any other one
    /// </summary>
    public static TierBossPicker Open(BossEditorSection section, int tier)
    {
        Current?.Hide();
        current = new TierBossPicker(section, tier);
        return current;
    }

    /// <summary>
    /// What the tier is now, with the Boss Type filled in for a tier that follows it
    /// </summary>
    private TierBoss Shown()
    {
        var options = Creator.BossMode.Options;
        var entry = options.EntryOf(Tier);
        return entry.FollowsSetup ? entry with {Boss = options.Boss.ToString(), Elite = options.Elite} : entry;
    }

    /// <summary>
    /// Shows the tier's boss with a tick, and every portrait as Elite or not
    /// </summary>
    public void Show()
    {
        var shown = Shown();
        foreach (var (choice, button) in Choices)
        {
            button.Image.SetSprite(choice == TierBossMath.Random
                ? VanillaSprites.RandomIcon
                : BossEditorSection.Portrait(Enum.Parse<BossType>(choice), shown.Elite));
            button.transform.Find(BossEditorSection.SelectedMarker)?.gameObject.SetActive(choice == shown.Boss);
        }

        refreshing = true;
        Elite.SetChecked(shown.Elite, false);
        Speed.Show(shown.Speed);
        Health.Show(shown.Health);
        SkullMode.Dropdown.SetValueWithoutNotify((int) shown.Skulls);
        SkullCount.Show(shown.SkullCount);
        if (!CustomSkulls.InputField.isFocused) CustomSkulls.SetText(shown.CustomSkulls, false);
        refreshing = false;

        Skulls.Show(Creator.BossMode.Options, Tier);
        Layout(shown);
    }

    /// <summary>
    /// Gives the tier this boss (or Random) and closes the picker
    /// </summary>
    public void Pick(string choice)
    {
        section.SetTierBoss(Tier, Shown() with {Boss = choice, Elite = Elite.Toggle.isOn});
        Hide();
    }

    private void SetElite(bool elite)
    {
        if (refreshing) return;
        var shown = Shown();
        section.SetTierBoss(Tier, shown with {Elite = elite});
        Show();
    }

    public void Hide()
    {
        if (Root != null) Object.Destroy(Root);
        if (current == this) current = null;
        FocusFrame.Hide();
        section.OnPickerClosed(Tier, ReturnFocus);
    }

    /// <summary>
    /// A key press while the picker is open; the Boss Creator's own keys wait until it is closed
    /// </summary>
    public void Handle(ShortcutKey key, bool shift = false)
    {
        var slider = FocusedSlider();
        var onMode = Focused >= 0 && Stops[Focused] == SkullMode.gameObject;
        switch (key)
        {
            case ShortcutKey.Left or ShortcutKey.Right when slider != null:
                slider.Step(key == ShortcutKey.Right ? 1 : -1);
                break;
            case ShortcutKey.Delete or ShortcutKey.Backspace when slider != null:
                slider.Reset();
                break;
            case ShortcutKey.Left or ShortcutKey.Right when onMode:
                var dropdown = SkullMode.Dropdown;
                dropdown.value = Math.Clamp(dropdown.value + (key == ShortcutKey.Right ? 1 : -1), 0, dropdown.options.Count - 1);
                break;
            case ShortcutKey.Delete or ShortcutKey.Backspace when onMode:
                ResetSkulls.Button.onClick.Invoke();
                break;
            case ShortcutKey.Left or ShortcutKey.Up:
                Move(-1);
                break;
            case ShortcutKey.Right or ShortcutKey.Down:
                Move(1);
                break;
            case ShortcutKey.Tab:
                Move(shift ? -1 : 1);
                break;
            case ShortcutKey.Enter or ShortcutKey.Space when Focused >= 0:
                Press(Stops[Focused]);
                break;
        }
    }

    private SliderRow? FocusedSlider() =>
        Focused < 0 ? null : new[] {Speed, Health, SkullCount}.FirstOrDefault(slider => slider.Row == Stops[Focused]);

    private void Move(int step)
    {
        // The first press starts on the tier's own boss
        if (Focused < 0)
        {
            var shown = Array.IndexOf(TierBossMath.Choices, Shown().Boss);
            Focused = Math.Max(shown, 0);
        }
        else
        {
            // Rows the skull mode hides are skipped
            do Focused = ((Focused + step) % Stops.Count + Stops.Count) % Stops.Count;
            while (!Stops[Focused].activeInHierarchy);
        }

        FocusFrame.Show(Stops[Focused]);
    }

    private void Press(GameObject stop)
    {
        if (stop == Elite.gameObject) Elite.Toggle.isOn = !Elite.Toggle.isOn;
        else if (stop == SkullMode.gameObject) SkullMode.Dropdown.Show();
        else if (stop == CustomSkulls.gameObject)
        {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(stop);
            CustomSkulls.InputField.ActivateInputField();
        }
        else stop.GetComponent<Button>()?.onClick.Invoke();
    }
}
