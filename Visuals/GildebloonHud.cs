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
/// Made with Mod Helper's components once, as a template that lasts the session, and copied into each match as plain
/// Unity objects that go with it: Mod Helper's panels and texts are never quite freed, and making a dozen each match
/// kept about 1.2 MB of the game's memory a match (2026-11-22, GildebloonCostTest).
/// </summary>
public static class GildebloonHud
{
    public const string Name = "GildebloonVault";

    private const float ChangeSeconds = 2.5f;

    /// <summary>How long coins take between the cash and Gildebloon, and how long "-$700" shows at the cash</summary>
    private const float FlySeconds = 0.8f;

    private const float CashPopSeconds = 1.8f;

    private static GameObject? panel;
    private static TMP_Text? amount;
    private static TMP_Text? change;

    /// <summary>The shield from a skull: a gold bar under the Vault that empties as it is hit, and how wide it is full</summary>
    private static GameObject? shieldBar;
    private static UnityEngine.UI.Image? shieldFill;
    private static float shieldWidth;

    /// <summary>How full the shield bar shows, 0 when hidden, for tests</summary>
    public static float ShieldShown => shieldBar != null && shieldBar.activeSelf && shieldFill != null
        ? shieldFill.rectTransform.sizeDelta.x / Math.Max(1, shieldWidth) : 0;
    private static float changeStart = -100;
    private static bool subscribed;

    /// <summary>The Vault panel while it is shown, for tests (hidden while a tower is selected)</summary>
    public static GameObject? Panel => panel != null ? panel : null;

    /// <summary>The amount it shows, for tests</summary>
    public static string? Shown => amount != null ? amount.text : null;

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
            if (Panel != null) Panel.SetActive(false);
            if (seizedLine != null) seizedLine.gameObject.SetActive(false);
            if (banner != null) banner.gameObject.SetActive(false);
            return;
        }

        if (Panel == null)
        {
            var ui = Object.FindObjectOfType<BossUI>();
            if (ui == null || !Build(ui)) return;
        }

        if (BossDisplay() is { } boss) lastBoss = boss.position;

        // The tower menu covers the boss panel's right end, where the Vault is: the Vault makes way while it is open
        var selected = Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu.TowerSelectionMenu.instance?.selectedTower != null;
        Panel!.SetActive(!selected);
        var stolen = $"${GildebloonBrain.Stolen:N0}";
        if (amount!.text != stolen) amount.text = stolen;
        var shield = GildebloonBrain.ShieldLeft;
        shieldBar!.SetActive(shield > 0 && GildebloonBrain.ShieldSecondsLeft > 0);
        if (shield > 0)
        {
            var size = shieldFill!.rectTransform.sizeDelta;
            shieldFill.rectTransform.sizeDelta = new Vector2(shieldWidth * shield, size.y);
            // it blinks in its last two seconds
            var left = GildebloonBrain.ShieldSecondsLeft;
            shieldFill.color = left < 2 && Mathf.Repeat(Time.unscaledTime * 4, 1) < 0.5f
                ? new Color(1f, 0.97f, 0.75f) : new Color(1f, 0.78f, 0.1f);
        }

        var age = Time.unscaledTime - changeStart;
        change!.gameObject.SetActive(age < ChangeSeconds);
        if (age < ChangeSeconds) change.alpha = Math.Min(1, (ChangeSeconds - age) / 0.6f);

        UpdateSeizedLine();

        var shown = Time.unscaledTime - bannerStart;
        banner!.gameObject.SetActive(shown < BannerSeconds);
        if (shown < BannerSeconds)
        {
            banner.alpha = Math.Min(1, (BannerSeconds - shown) / 0.5f);
            banner.transform.localScale = Vector3.one * (1 + 0.15f * Math.Max(0, 1 - shown * 4));
        }
    }

    /// <summary>The Repossession at a skull, shown big for a moment where Gildebloon isn't</summary>
    private static void OnRepossessed(GildebloonBrain.Repossession repossession)
    {
        if (Panel == null || banner == null) return;
        banner.text = BannerText(repossession);
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
        banner.rectTransform.anchoredPosition = new Vector2(0, height * (top ? 0.22f : -0.24f));
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
    public static string? SeizedLine => seizedLine != null && seizedLine.gameObject.activeSelf ? seizedLine.text : null;

    private static TMP_Text? seizedLine;

    /// <summary>A seized tower selected: how long until it is let go</summary>
    private static void UpdateSeizedLine()
    {
        if (seizedLine == null) return;
        var selected = Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu.TowerSelectionMenu.instance?.selectedTower?.tower;
        var seized = selected == null ? null
            : GildebloonBrain.SeizedNow().Where(entry => entry.Tower != null && entry.Tower.Pointer == selected.Pointer)
                .Select(entry => (float?) entry.SecondsLeft).FirstOrDefault();
        seizedLine.gameObject.SetActive(seized != null);
        if (seized is { } left) seizedLine.text = Texts.GildebloonSeizedFor.T(Math.Ceiling(left));
    }

    /// <summary>How many Repossessions the HUD showed, for tests</summary>
    public static int Repossessions { get; private set; }

    /// <summary>The Repossession banner while it shows, for tests</summary>
    public static string? Banner => banner != null && banner.gameObject.activeSelf ? banner.text : null;

    private const float BannerSeconds = 3f;
    private static TMP_Text? banner;
    private static float bannerStart = -100;

    /// <summary>It took cash (a Toll, a skull, a Tax Collector) or paid it back (negative)</summary>
    private static void OnCashTaken(double cash)
    {
        if (change != null)
        {
            // what went into the Vault, or came out of it
            change.text = cash >= 0 ? $"+${cash:N0}" : $"-${-cash:N0}";
            change.color = cash >= 0 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.45f);
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

    /// <summary>
    /// Whether the Vault is on this match's canvas. Cash can be taken before it is (a Tax Collector leaking right after
    /// the match loads, while the template's pictures still load), and the canvas of an earlier match is destroyed.
    /// </summary>
    private static bool OnMatchCanvas => panel != null && canvasRect != null;

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
        if (popTemplate == null || !OnMatchCanvas || CashText() is not { } cash || CashPoint() is not { } at) return;
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
        if (coinTemplate == null || !OnMatchCanvas || CashPoint() == null) return;
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
        if (!OnMatchCanvas)
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

    // ---- The template, made once a session ----

    /// <summary>
    /// The HUD made with Mod Helper's components, kept for the session (not drawn), with those components taken off
    /// once its pictures have loaded; each match gets copies. Made again only if the canvas's height changed (another
    /// screen size), which its sizes come from.
    /// </summary>
    private static GameObject? template;
    private static GameObject? panelTemplate, bannerTemplate, seizedTemplate, coinTemplate, popTemplate;
    private static float templateHeight;
    private static bool templateReady;

    /// <summary>How many times the template was made this session, for tests</summary>
    public static int TemplatesMade { get; private set; }

    /// <returns>Whether the template is there to copy: its pictures load over a few frames</returns>
    private static bool Template(float canvasHeight)
    {
        if (template != null && Math.Abs(templateHeight - canvasHeight) > 1)
        {
            Object.Destroy(template);
            template = null;
        }

        if (template == null)
        {
            MakeTemplate(canvasHeight);
            return false;
        }
        if (templateReady) return true;

        // ready once every picture has loaded; then Mod Helper's components come off, and copies are plain Unity
        if (template.GetComponentsInChildren<UnityEngine.UI.Image>(true).Any(image => image.sprite == null)) return false;
        foreach (var component in template.GetComponentsInChildren<ModHelperComponent>(true)) Object.DestroyImmediate(component);
        templateReady = true;
        return true;
    }

    private static void MakeTemplate(float canvasHeight)
    {
        // Mod Helper leaves an empty text asset behind for every component (#235)
        using var leftovers = new ModHelperLeftovers();
        templateReady = false;
        templateHeight = canvasHeight;
        TemplatesMade++;
        template = new GameObject("GildebloonHudTemplate");
        template.AddComponent<RectTransform>();
        template.SetActive(false);
        Object.DontDestroyOnLoad(template);
        var root = template.transform;

        var height = canvasHeight * 0.045f;
        var width = height * 4.2f;
        var vault = ModHelperPanel.Create(new Info(Name, canvasHeight * 0.42f, -canvasHeight * 0.12f, width, height)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
        }, VanillaSprites.MainBGPanelBlue);
        vault.transform.SetParent(root, false);
        var group = vault.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        vault.AddImage(new Info("Coin", -width / 2 + height * 0.6f, 0, height * 0.85f, height * 0.85f), VanillaSprites.GoldenBloonIcon);
        var amountText = vault.AddText(new Info("Amount", height * 0.45f, 0, width - height * 1.3f, height * 0.8f), "", height * 0.5f);
        amountText.Text.color = new Color(1f, 0.85f, 0.3f);
        amountText.Text.textWrappingMode = TextWrappingModes.NoWrap;
        var changeText = vault.AddText(new Info("Change", width * 0.5f + height * 1.2f, 0, height * 2.6f, height * 0.8f), "", height * 0.45f);
        changeText.Text.textWrappingMode = TextWrappingModes.NoWrap;
        changeText.gameObject.SetActive(false);

        // The shield bar, under the Vault and as wide, drawn with the panel (so it hides with it)
        shieldWidth = width - height * 0.3f;
        var bar = vault.AddImage(new Info("Shield", 0, -height * 0.9f, width, height * 0.55f), VanillaSprites.BarfillContainer);
        var fill = bar.AddImage(new Info("Fill", -shieldWidth / 2, 0, shieldWidth, height * 0.36f)
        {
            Anchor = new Vector2(0.5f, 0.5f), Pivot = new Vector2(0, 0.5f)
        }, VanillaSprites.Barfillgreen);
        // the game's fill is green; a plain white one takes the gold as it is
        var white = Texture2D.whiteTexture;
        fill.Image.sprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f));
        fill.Image.color = new Color(1f, 0.78f, 0.1f);
        bar.gameObject.SetActive(false);
        panelTemplate = vault.gameObject;

        // The Repossession banner: a big heading and what it took under it, placed when it shows
        var bannerText = ModHelperText.Create(new Info("Repossession", 0, 0, canvasHeight * 1.4f, height * 5.5f)
        {
            Anchor = new Vector2(0.5f, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
        }, "", height * 1.1f);
        bannerText.transform.SetParent(root, false);
        bannerText.Text.color = new Color(1f, 0.82f, 0.25f);
        bannerText.Text.textWrappingMode = TextWrappingModes.NoWrap;
        bannerText.Text.lineSpacing = 12;
        bannerText.Text.outlineWidth = 0.25f;
        bannerText.Text.outlineColor = new Color32(70, 40, 0, 255);
        bannerText.Text.raycastTarget = false;
        bannerText.gameObject.SetActive(false);
        bannerTemplate = bannerText.gameObject;

        // A seized tower's time left, in the middle under the boss panel, where the tower menu doesn't cover it
        var seized = ModHelperText.Create(new Info("GildebloonSeized", 0, -canvasHeight * 0.2f, canvasHeight * 0.8f, height)
        {
            Anchor = new Vector2(0.5f, 1), Pivot = new Vector2(0.5f, 0.5f)
        }, "", height * 0.6f);
        seized.transform.SetParent(root, false);
        seized.Text.color = new Color(1f, 0.82f, 0.25f);
        seized.Text.textWrappingMode = TextWrappingModes.NoWrap;
        seized.Text.outlineWidth = 0.2f;
        seized.Text.outlineColor = new Color32(70, 40, 0, 255);
        seized.gameObject.SetActive(false);
        seizedTemplate = seized.gameObject;

        // What the flying coins and the "-$700" at the cash are copied from
        var coin = ModHelperImage.Create(new Info("GildebloonFlyingCoin", height * 0.7f), VanillaSprites.CoinIcon);
        coin.transform.SetParent(root, false);
        coin.Image.raycastTarget = false;
        coinTemplate = coin.gameObject;

        var pop = ModHelperText.Create(new Info("GildebloonCashPop", height * 4, height * 0.9f), "", height * 0.6f);
        pop.transform.SetParent(root, false);
        pop.Text.textWrappingMode = TextWrappingModes.NoWrap;
        pop.Text.outlineWidth = 0.25f;
        pop.Text.outlineColor = new Color32(40, 20, 0, 255);
        pop.Text.raycastTarget = false;
        popTemplate = pop.gameObject;
    }

    /// <returns>Whether the HUD is on the match's canvas now; not until the template is ready</returns>
    private static bool Build(BossUI ui)
    {
        var canvas = ui.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
        if (!Template(canvas.rect.height)) return false;
        canvasRect = canvas;

        panel = Object.Instantiate(panelTemplate!, canvas, false);
        panel.name = Name;
        panel.SetActive(true);
        // Right after the boss panel among the canvas's children, so the tower menus and popups drawn after it cover
        // the Vault like they cover the boss panel, instead of the Vault lying on top of them
        var above = ui.transform;
        while (above.parent != null && above.parent != canvas.transform) above = above.parent;
        if (above.parent == canvas.transform) panel.transform.SetSiblingIndex(above.GetSiblingIndex() + 1);
        amount = panel.transform.Find("Amount").GetComponent<TMP_Text>();
        change = panel.transform.Find("Change").GetComponent<TMP_Text>();
        shieldBar = panel.transform.Find("Shield").gameObject;
        shieldFill = shieldBar.transform.Find("Fill").GetComponent<UnityEngine.UI.Image>();

        var bannerCopy = Object.Instantiate(bannerTemplate!, canvas, false);
        bannerCopy.name = "Repossession";
        bannerCopy.transform.SetSiblingIndex(panel.transform.GetSiblingIndex() + 1);
        banner = bannerCopy.GetComponent<TMP_Text>();

        var seizedCopy = Object.Instantiate(seizedTemplate!, canvas, false);
        seizedCopy.name = "GildebloonSeized";
        seizedCopy.transform.SetSiblingIndex(panel.transform.GetSiblingIndex() + 1);
        seizedLine = seizedCopy.GetComponent<TMP_Text>();
        return true;
    }
}
