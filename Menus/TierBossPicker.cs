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
/// tier's own Boss Speed and Boss Health, and Elite for that tier. A click outside it or Esc closes it. The keyboard
/// moves through its controls with the arrows (or Tab), steps a slider with left and right, puts it back to 100 % with
/// Delete, and presses buttons with Enter or Space.
/// </summary>
public sealed class TierBossPicker
{
    public const string Name = "CustomBossTierPicker";
    public const float Width = 2140;
    public const float Height = 1240;
    private const float SliderWidth = 2000;
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
    /// What the keyboard moves through, in order: the bosses, Random, speed, health, Elite, Close
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

        Panel = shade.AddPanel(new Info("Panel", 0, 0, Width, Height), VanillaSprites.MainBGPanelBlue);
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
            var button = Panel.AddButton(new Info($"Choice{choice}", x, 250, ChoiceSize, ChoiceSize), VanillaSprites.RandomIcon,
                new Action(() => Pick(name)));
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

        // The editor's own slider rows, shrunk to fit the window; a run of changes is one step for Undo
        Speed = Slider("Speed", -40, Texts.BossSpeed.T(), VanillaSprites.FasterBossIcon, TierBossMath.MinSpeed,
            TierBossMath.MaxSpeed, value => SetValues(Shown() with {Speed = (int) value}, "Speed"));
        Health = Slider("Health", -260, Texts.BossHealth.T(), VanillaSprites.BossBoostIcon, TierBossMath.MinHealth,
            TierBossMath.MaxHealth, value => SetValues(Shown() with {Health = (int) value}, "Health"));

        var bottom = -Height / 2 + 120;
        Elite = Panel.AddCheckbox(new Info("Elite", -110, bottom, 140, 140), false, VanillaSprites.BlueInsertPanelRound,
            new Action<bool>(SetElite), VanillaSprites.TickGreenIcon, 20);
        Panel.AddText(new Info("EliteLabel", 110, bottom, 280, 140), Texts.Elite.T(), 70, TextAlignmentOptions.Left);

        Stops = Choices.Values.Select(button => button.gameObject)
            .Concat(new[] {Speed.Row, Health.Row, Elite.gameObject, Close.gameObject}).ToList();
        Show();
    }

    private SliderRow Slider(string name, float y, string label, string icon, int min, int max, Action<float> changed)
    {
        var row = SliderRow.Clone(section.SliderTemplate, Panel.transform, name, label, icon, min, max,
            TierBossMath.ValueStep, TierBossMath.Normal, value => $"{value:0} %", changed);
        // As wide and high as the editor's own rows, so the slider and its arrows sit the same, then shrunk as a whole
        var size = section.BossSpeed.Row.GetComponent<RectTransform>().rect.size;
        var rect = row.Row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        var scale = Mathf.Min(1, SliderWidth / Mathf.Max(size.x, 1));
        rect.localScale = new Vector3(scale, scale, 1);
        rect.anchoredPosition = new Vector2(0, y);
        return row;
    }

    private void SetValues(TierBoss tier, string control)
    {
        if (refreshing) return;
        section.SetTierBoss(Tier, tier, $"Tier{Tier}{control}");
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
        refreshing = false;
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
        switch (key)
        {
            case ShortcutKey.Left or ShortcutKey.Right when slider != null:
                slider.Step(key == ShortcutKey.Right ? 1 : -1);
                break;
            case ShortcutKey.Delete or ShortcutKey.Backspace when slider != null:
                slider.Reset();
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
        Focused < 0 ? null : Stops[Focused] == Speed.Row ? Speed : Stops[Focused] == Health.Row ? Health : null;

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
            Focused = ((Focused + step) % Stops.Count + Stops.Count) % Stops.Count;
        }

        FocusFrame.Show(Stops[Focused]);
    }

    private void Press(GameObject stop)
    {
        if (stop == Elite.gameObject) Elite.Toggle.isOn = !Elite.Toggle.isOn;
        else stop.GetComponent<Button>()?.onClick.Invoke();
    }
}
