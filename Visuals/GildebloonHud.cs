using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Extensions;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.Races;
using Il2CppTMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// Gildebloon on the screen (#25):
/// <list type="bullet">
/// <item>its Vault under the game's boss panel: a gold coin with what it has stolen, and what went in last;</item>
/// <item>what it takes and pays back, at the player's own cash: "-$700" (or "+$840") under the cash at the top left,
/// and coins flying from the cash to Gildebloon (or back), so it is plain where the money went;</item>
/// <item>the Repossession at a skull: a big "Repossession!" with what it took under it, on the half of the screen
/// Gildebloon isn't on, so it doesn't cover it;</item>
/// <item>a selected seized tower's time left.</item>
/// </list>
/// </summary>
public static class GildebloonHud
{
    public const string Name = "GildebloonVault";

    private const float ChangeSeconds = 2.5f;

    /// <summary>How long coins take between the cash and Gildebloon, and how long "-$700" shows at the cash</summary>
    private const float FlySeconds = 0.8f;

    private const float CashPopSeconds = 1.8f;

    private static ModHelperPanel? panel;
    private static ModHelperText? amount;
    private static ModHelperText? change;
    private static float changeStart = -100;
    private static bool subscribed;

    /// <summary>The Vault panel while it is shown, for tests (hidden while a tower is selected)</summary>
    public static ModHelperPanel? Panel => panel != null && panel.gameObject != null ? panel : null;

    /// <summary>The amount it shows, for tests</summary>
    public static string? Shown => amount != null ? amount.Text.text : null;

    /// <summary>Called every frame</summary>
    public static void Update()
    {
        if (!subscribed)
        {
            subscribed = true;
            GildebloonBrain.CashTaken += OnCashTaken;
            GildebloonBrain.Repossessed += OnRepossessed;
        }

        // The Payout comes as the boss goes: its coins and "+$" play out after the Vault is put away
        UpdateFlights();

        if (!GildebloonBrain.Active)
        {
            if (Panel != null) Panel.gameObject.SetActive(false);
            if (seizedLine != null) seizedLine.gameObject.SetActive(false);
            if (banner != null) banner.gameObject.SetActive(false);
            return;
        }

        if (Panel == null)
        {
            var ui = Object.FindObjectOfType<BossUI>();
            if (ui == null) return;
            Build(ui);
        }

        if (BossDisplay() is { } boss) lastBoss = boss.position;

        // The tower menu covers the boss panel's right end, where the Vault is: the Vault makes way while it is open
        var selected = Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu.TowerSelectionMenu.instance?.selectedTower != null;
        Panel!.gameObject.SetActive(!selected);
        amount!.SetText($"${GildebloonBrain.Stolen:N0}");

        var age = Time.unscaledTime - changeStart;
        change!.gameObject.SetActive(age < ChangeSeconds);
        if (age < ChangeSeconds) change.Text.alpha = Math.Min(1, (ChangeSeconds - age) / 0.6f);

        UpdateSeizedLine();

        var shown = Time.unscaledTime - bannerStart;
        banner!.gameObject.SetActive(shown < BannerSeconds);
        if (shown < BannerSeconds)
        {
            banner.Text.alpha = Math.Min(1, (BannerSeconds - shown) / 0.5f);
            banner.transform.localScale = Vector3.one * (1 + 0.15f * Math.Max(0, 1 - shown * 4));
        }
    }

    /// <summary>The Repossession at a skull, shown big for a moment where Gildebloon isn't</summary>
    private static void OnRepossessed(GildebloonBrain.Repossession repossession)
    {
        if (Panel == null || banner == null) return;
        banner.SetText(BannerText(repossession));
        bannerStart = Time.unscaledTime;
        Repossessions++;

        // On the half of the screen Gildebloon isn't on
        var top = true;
        try
        {
            var display = repossession.Boss.GetUnityDisplayNode();
            if (display != null && Camera.main is { } camera)
                top = camera.WorldToScreenPoint(display.transform.position).y < Screen.height * 0.5f;
        }
        catch (Exception)
        {
            // The boss went with the match
        }

        var height = Canvas().rect.height;
        banner.RectTransform.anchoredPosition = new Vector2(0, height * (top ? 0.22f : -0.24f));
        BannerOnTop = top;
    }

    /// <summary>Whether the last banner went on the top half of the screen, for tests</summary>
    public static bool BannerOnTop { get; private set; }

    /// <summary>
    /// "Repossession!", and under it, smaller, a line for each thing it took: "-$2,400", "1 tower frozen for 3 s",
    /// "2 Banana Farms locked for 12 s"
    /// </summary>
    public static string BannerText(GildebloonBrain.Repossession repossession) =>
        Texts.GildebloonRepossessionTitle.T() + "\n<size=60%>" + string.Join("\n", BannerLines(repossession)) + "</size>";

    /// <summary>The banner's lines under its title</summary>
    public static List<string> BannerLines(GildebloonBrain.Repossession repossession)
    {
        var lines = new List<string>();
        if (repossession.Cash > 0) lines.Add($"-${repossession.Cash:N0}");
        if (repossession.Frozen == 1) lines.Add(Texts.GildebloonTowerFrozen.T(GildebloonBrain.FreezeSeconds));
        else if (repossession.Frozen > 1) lines.Add(Texts.GildebloonTowersFrozen.T(repossession.Frozen, GildebloonBrain.FreezeSeconds));
        if (repossession.Farms == 1) lines.Add(Texts.GildebloonFarmLocked.T(GildebloonBrain.SeizeSeconds));
        else if (repossession.Farms > 1) lines.Add(Texts.GildebloonFarmsLocked.T(repossession.Farms, GildebloonBrain.SeizeSeconds));
        return lines;
    }

    /// <summary>The line under the Vault while a seized tower is selected, for tests</summary>
    public static string? SeizedLine => seizedLine != null && seizedLine.gameObject.activeSelf ? seizedLine.Text.text : null;

    private static ModHelperText? seizedLine;

    /// <summary>A seized tower selected: how long until it is let go</summary>
    private static void UpdateSeizedLine()
    {
        if (seizedLine == null) return;
        var selected = Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu.TowerSelectionMenu.instance?.selectedTower?.tower;
        var seized = selected == null ? null
            : GildebloonBrain.SeizedNow().Where(entry => entry.Tower != null && entry.Tower.Pointer == selected.Pointer)
                .Select(entry => (float?) entry.SecondsLeft).FirstOrDefault();
        seizedLine.gameObject.SetActive(seized != null);
        if (seized is { } left) seizedLine.SetText(Texts.GildebloonSeizedFor.T(Math.Ceiling(left)));
    }

    /// <summary>How many Repossessions the HUD showed, for tests</summary>
    public static int Repossessions { get; private set; }

    /// <summary>The Repossession banner while it shows, for tests</summary>
    public static string? Banner => banner != null && banner.gameObject.activeSelf ? banner.Text.text : null;

    private const float BannerSeconds = 3f;
    private static ModHelperText? banner;
    private static float bannerStart = -100;

    /// <summary>It took cash (a Toll, a skull, a Tax Collector) or paid it back (negative)</summary>
    private static void OnCashTaken(double cash)
    {
        if (change != null)
        {
            // what went into the Vault, or came out of it
            change.SetText(cash >= 0 ? $"+${cash:N0}" : $"-${-cash:N0}");
            change.Text.color = cash >= 0 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.45f);
            changeStart = Time.unscaledTime;
        }

        CashPop(cash >= 0 ? $"-${cash:N0}" : $"+${-cash:N0}", cash >= 0 ? new Color(1f, 0.35f, 0.3f) : new Color(0.45f, 1f, 0.4f));
        // a few coins for a small sum, more for a big one; the Payout is a shower
        var coins = cash < 0 ? 14 : Math.Clamp(3 + (int) Math.Log10(Math.Max(1, cash)), 3, 8);
        Fly(coins, cash < 0);
        CashShown++;
    }

    /// <summary>How many thefts and payouts were shown at the cash, for tests</summary>
    public static int CashShown { get; private set; }

    // ---- Coins between the cash and Gildebloon ----

    private sealed class Flight
    {
        public RectTransform Item = null!;
        public float Start, Seconds;
        public bool ToCash, Pop;
        public Vector2 Sway;
    }

    private static readonly List<Flight> Flights = new();
    private static GameObject? coinTemplate, popTemplate;
    private static TMP_Text? cashText;
    private static Vector3? lastBoss;

    /// <summary>The coins and "-$700"s on the screen now, for tests</summary>
    public static int Flying => Flights.Count;

    private static Transform? BossDisplay()
    {
        try
        {
            var boss = InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager?.CurrentBoss;
            return boss != null && Gildebloon.IsGildebloon(boss.bloonModel?.id) ? boss.GetUnityDisplayNode()?.transform : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The game's cash at the top left: the text under the lives and cash that shows a dollar amount</summary>
    private static TMP_Text? CashText()
    {
        if (cashText != null && cashText.gameObject != null && cashText.isActiveAndEnabled) return cashText;
        var hud = GameObject.Find(RankedHud.LeftHudPath);
        cashText = hud == null ? null
            : hud.GetComponentsInChildren<TMP_Text>().FirstOrDefault(text => text.isActiveAndEnabled && (text.text ?? "").StartsWith("$"));
        return cashText;
    }

    /// <summary>The game's canvas the Vault is on (a C# cast of its Transform fails under Il2Cpp)</summary>
    private static RectTransform? canvasRect;

    private static RectTransform Canvas() => canvasRect!;

    /// <summary>Where on the canvas a point on the map is</summary>
    private static Vector3? OnCanvas(Vector3 world)
    {
        if (panel == null || Camera.main is not { } camera) return null;
        var canvas = Canvas();
        var screen = camera.WorldToScreenPoint(world);
        var ui = canvas.GetComponentInParent<Canvas>().rootCanvas;
        var uiCamera = ui.renderMode == RenderMode.ScreenSpaceOverlay ? null : ui.worldCamera;
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas, screen, uiCamera, out var point) ? point : null;
    }

    /// <summary>The cash's middle on the canvas</summary>
    private static Vector3? CashPoint() => CashText() is { } text ? text.rectTransform.TransformPoint(text.rectTransform.rect.center) : null;

    private static void CashPop(string text, Color color)
    {
        if (popTemplate == null || CashText() is not { } cash || CashPoint() is not { } at) return;
        var made = Object.Instantiate(popTemplate, Canvas(), false);
        made.SetActive(true);
        var item = made.GetComponent<RectTransform>();
        item.position = at;
        var label = made.GetComponent<TMP_Text>();
        label.text = text;
        label.color = color;
        // just under the cash, as long as it is
        var height = cash.rectTransform.rect.height;
        item.anchoredPosition += new Vector2(0, -height * 0.9f);
        Flights.Add(new Flight {Item = item, Start = Time.unscaledTime, Seconds = CashPopSeconds, Pop = true});
    }

    private static void Fly(int coins, bool toCash)
    {
        if (coinTemplate == null || CashPoint() == null) return;
        for (var i = 0; i < coins; i++)
        {
            var made = Object.Instantiate(coinTemplate, Canvas(), false);
            made.SetActive(false);
            var sway = UnityEngine.Random.insideUnitCircle * Canvas().rect.height * 0.08f;
            Flights.Add(new Flight
            {
                Item = made.GetComponent<RectTransform>(), Start = Time.unscaledTime + i * (toCash ? 0.05f : 0.07f), Seconds = FlySeconds,
                ToCash = toCash, Sway = sway
            });
        }
    }

    private static void UpdateFlights()
    {
        if (Flights.Count == 0) return;
        if (panel == null || canvasRect == null)
        {
            Clear();
            return;
        }

        if (BossDisplay() is { } boss) lastBoss = boss.position;
        var bossPoint = lastBoss is { } world ? OnCanvas(world) : null;
        var cashPoint = CashPoint();
        for (var i = Flights.Count - 1; i >= 0; i--)
        {
            var flight = Flights[i];
            var age = Time.unscaledTime - flight.Start;
            if (flight.Item == null || age > flight.Seconds || (!flight.Pop && (bossPoint == null || cashPoint == null)))
            {
                if (flight.Item != null) Object.Destroy(flight.Item.gameObject);
                Flights.RemoveAt(i);
                continue;
            }
            if (age < 0) continue;

            var t = age / flight.Seconds;
            if (flight.Pop)
            {
                // drifts down a little and fades
                var label = flight.Item.GetComponent<TMP_Text>();
                label.alpha = Math.Min(1, (1 - t) * 3);
                flight.Item.anchoredPosition += new Vector2(0, -Time.unscaledDeltaTime * Canvas().rect.height * 0.02f);
                flight.Item.localScale = Vector3.one * (1 + 0.25f * Math.Max(0, 1 - age * 6));
                continue;
            }

            if (!flight.Item.gameObject.activeSelf) flight.Item.gameObject.SetActive(true);
            var from = flight.ToCash ? bossPoint!.Value : cashPoint!.Value;
            var to = flight.ToCash ? cashPoint!.Value : bossPoint!.Value;
            // eased, on a curve out to one side
            var eased = t * t * (3 - 2 * t);
            var bend = Mathf.Sin(t * Mathf.PI);
            flight.Item.position = Vector3.Lerp(from, to, eased);
            flight.Item.anchoredPosition += flight.Sway * bend;
            flight.Item.localScale = Vector3.one * (0.7f + 0.5f * bend);
            flight.Item.localRotation = Quaternion.Euler(0, 0, age * 540);
        }
    }

    private static void Clear()
    {
        foreach (var flight in Flights)
        {
            if (flight.Item != null) Object.Destroy(flight.Item.gameObject);
        }
        Flights.Clear();
    }

    private static void Build(BossUI ui)
    {
        // Mod Helper leaves an empty text asset behind for every component (#235)
        using var leftovers = new ModHelperLeftovers();
        var canvas = ui.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
        canvasRect = canvas;
        var height = canvas.rect.height * 0.045f;
        var width = height * 4.2f;
        panel = ModHelperPanel.Create(new Info(Name, canvas.rect.height * 0.42f, -canvas.rect.height * 0.12f, width, height)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.MainBGPanelBlue);
        panel.transform.SetParent(canvas, false);

        // Right after the boss panel among the canvas's children, so the tower menus and popups drawn after it cover
        // the Vault like they cover the boss panel, instead of the Vault lying on top of them
        var above = ui.transform;
        while (above.parent != null && above.parent != canvas.transform) above = above.parent;
        if (above.parent == canvas.transform) panel.transform.SetSiblingIndex(above.GetSiblingIndex() + 1);
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        panel.AddImage(new Info("Coin", -width / 2 + height * 0.6f, 0, height * 0.85f, height * 0.85f), VanillaSprites.GoldenBloonIcon);
        amount = panel.AddText(new Info("Amount", height * 0.45f, 0, width - height * 1.3f, height * 0.8f), "", height * 0.5f);
        amount.Text.color = new Color(1f, 0.85f, 0.3f);
        amount.Text.textWrappingMode = TextWrappingModes.NoWrap;
        change = panel.AddText(new Info("Change", width * 0.5f + height * 1.2f, 0, height * 2.6f, height * 0.8f), "", height * 0.45f);
        change.Text.textWrappingMode = TextWrappingModes.NoWrap;
        change.gameObject.SetActive(false);

        // The Repossession banner: a big heading and what it took under it, placed when it shows
        banner = ModHelperText.Create(new Info("Repossession", 0, 0, canvas.rect.height * 1.4f, height * 5.5f)
        {
            Anchor = new Vector2(0.5f, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, "", height * 1.1f);
        banner.transform.SetParent(canvas, false);
        banner.transform.SetSiblingIndex(panel.transform.GetSiblingIndex() + 1);
        banner.Text.color = new Color(1f, 0.82f, 0.25f);
        banner.Text.textWrappingMode = TextWrappingModes.NoWrap;
        banner.Text.lineSpacing = 12;
        banner.Text.outlineWidth = 0.25f;
        banner.Text.outlineColor = new Color32(70, 40, 0, 255);
        banner.Text.raycastTarget = false;
        banner.gameObject.SetActive(false);

        // A seized tower's time left, in the middle under the boss panel, where the tower menu doesn't cover it
        seizedLine = ModHelperText.Create(new Info("GildebloonSeized", 0, -canvas.rect.height * 0.2f, canvas.rect.height * 0.8f, height)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
        }, "", height * 0.6f);
        seizedLine.transform.SetParent(canvas, false);
        seizedLine.transform.SetSiblingIndex(panel.transform.GetSiblingIndex() + 1);
        seizedLine.Text.color = new Color(1f, 0.82f, 0.25f);
        seizedLine.Text.textWrappingMode = TextWrappingModes.NoWrap;
        seizedLine.Text.outlineWidth = 0.2f;
        seizedLine.Text.outlineColor = new Color32(70, 40, 0, 255);
        seizedLine.gameObject.SetActive(false);

        // What the flying coins and the "-$700" at the cash are copied from: copies make no more Mod Helper leftovers
        if (coinTemplate == null)
        {
            var coin = ModHelperImage.Create(new Info("GildebloonFlyingCoin", height * 0.7f), VanillaSprites.CoinIcon);
            coin.transform.SetParent(canvas, false);
            coin.Image.raycastTarget = false;
            coin.gameObject.SetActive(false);
            coinTemplate = coin.gameObject;
        }

        if (popTemplate == null)
        {
            var pop = ModHelperText.Create(new Info("GildebloonCashPop", height * 4, height * 0.9f), "", height * 0.6f);
            pop.transform.SetParent(canvas, false);
            pop.Text.textWrappingMode = TextWrappingModes.NoWrap;
            pop.Text.outlineWidth = 0.25f;
            pop.Text.outlineColor = new Color32(40, 20, 0, 255);
            pop.Text.raycastTarget = false;
            pop.gameObject.SetActive(false);
            popTemplate = pop.gameObject;
        }
    }
}
