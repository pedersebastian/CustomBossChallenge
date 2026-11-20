using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppNinjaKiwi.Localization;
using Il2CppTMPro;
using UnityEngine;
using static CustomBossChallenge.Tests.LayoutChecks;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// In every language the game has, no text of the Boss section, the tier picker or Past Events is cut off, runs out of
/// its box or into the text next to it (#234). Fonts differ: Arabic, Chinese, Japanese, Korean, Russian and Thai have
/// taller lines than the Latin font, and German, Swedish and Polish have longer words.
/// </summary>
[FullSuiteOnly("every language the game has (about 2 min)")]
public class LanguageLayoutTest : CustomBossTest
{
    /// <summary>
    /// Five tiers in Advanced Mode, getting faster and stronger, so every card shows its ▲ marks and skulls
    /// </summary>
    private static readonly BossOptions Setup = new()
    {
        Boss = BossType.Lych, Elite = true, Tiers = 5, SpawnRounds = "40, 60, 80, 100, 120", SpeedPerTier = 10,
        HealthPerTier = 20, SkullMode = SkullMode.EvenlySpaced, SkullCount = 3,
        TierBosses = "LychElite,Vortex#4,Random,Phayze#60%;30%,BloonariusElite"
    };

    private readonly List<string> problems = new();
    private string language = "";

    protected override IEnumerator Run()
    {
        var original = LocalizationManager.Instance.ActiveLanguage;
        var folder = Path.Combine(Path.GetTempPath(), "CustomBossChallengeLanguages-" + Guid.NewGuid().ToString("N"));
        var (store, saved, options) = (BossMode.Store, BossMode.Saved, BossMode.Options);
        BossMode.Store = new LastSetupStore(Path.Combine(folder, "LastSetup.json"));
        BossMode.Saved = new SavedSetupsStore(Path.Combine(folder, "SavedSetups.json"));
        try
        {
            // CBC_LANGUAGES=German,Japanese checks only those, CBC_SCREENSHOTS=folder also saves what was checked
            var only = System.Environment.GetEnvironmentVariable("CBC_LANGUAGES")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var name in LocalizationTest.Languages.Where(name => only is not {Length: > 0} || only.Contains(name)))
            {
                yield return CheckLanguage(name);
            }
        }
        finally
        {
            LocalizationManager.Instance.UpdateLanguageAsync(original);
            BossMode.Store = store;
            BossMode.Saved = saved;
            BossMode.Options = options;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        yield return WaitFor(() => LocalizationManager.Instance.ActiveLanguage == original, "Going back to the player's language", 30);
        yield return ToMainMenu();
        AssertEquals(string.Join("\n", problems), "", "Texts that don't fit");
    }

    private IEnumerator CheckLanguage(string name)
    {
        language = name;
        yield return ToMainMenu();
        var target = Enum.Parse<Language>(name);
        var task = LocalizationManager.Instance.UpdateLanguageAsync(target);
        yield return WaitFor(() => task.IsCompleted, $"Switching to {name}", 30);
        yield return new WaitForSecondsRealtime(1);
        AssertEquals(LocalizationManager.Instance.ActiveLanguage, target, "Language");

        BossMode.Enter(Setup, BossMode.DefaultDcm(Setup));
        yield return WaitFor(() => BossEditorSection.Current?.Editor != null &&
                                   MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
            "Opening the Boss Creator", 30);
        yield return new WaitForSecondsRealtime(1);
        var section = BossEditorSection.Current!;
        Canvas.ForceUpdateCanvases();
        CheckTexts(section.Section.transform, "Boss Creator, Advanced");
        var card = section.Strip.Cards[1];
        Log($"{name}: tier 2's speed {Describe(InkRect(card.Speed.Text))} and health {Describe(InkRect(card.Health.Text))}, " +
            $"tier {Describe(InkRect(card.Tier.Text))} and round {Describe(InkRect(card.Round.Text))}");
        yield return Screenshot("creator");

        var picker = section.OpenPicker(4);
        if (picker == null) Fail($"{name}: the tier picker did not open");
        Canvas.ForceUpdateCanvases();
        yield return null;
        CheckTexts(picker!.Panel.transform, "tier picker");
        yield return Screenshot("picker");
        picker.Hide();

        section.SetAdvanced(false);
        Canvas.ForceUpdateCanvases();
        yield return null;
        CheckTexts(section.Section.transform, "Boss Creator, Standard");

        section.OpenPastEvents();
        yield return new WaitForSecondsRealtime(1);
        var browser = PastEventsBrowser.Current;
        if (browser == null) Fail($"{name}: Past Events did not open");
        CheckTexts(browser!.Panel.transform, "Past Events");
        yield return Screenshot("past-events");
        browser.Hide();

        Object.FindObjectOfType<CommonForegroundScreen>().backBtn.onClick.Invoke();
        yield return ToMainMenu();
    }

    /// <summary>
    /// Every shown text under <paramref name="root"/>: whole (not cut off with "…"), in its box, and apart from the
    /// texts next to it
    /// </summary>
    private void CheckTexts(Transform root, string where)
    {
        var texts = root.GetComponentsInChildren<TMP_Text>(false)
            .Where(text => text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text)).ToList();
        var at = $"{language}, {where}";
        foreach (var text in texts)
        {
            text.ForceMeshUpdate();
            var path = NamePath(text.transform, root);
            var shown = text.GetParsedText() ?? text.text;
            var quoted = shown.Length > 40 ? shown[..40] + "..." : shown;
            // A long setup name may end in "…" in the Saved Setups list, like in the game's own lists, and so may a
            // past event's details, which name the event that replaced it
            var mayEnd = text.GetComponentInParent<TMP_Dropdown>()?.captionText?.Pointer == text.Pointer || text.name == "Details";
            Expect(mayEnd || !text.isTextTruncated, $"{at}: {path} '{quoted}' is cut off");
            Expect(!text.isTextOverflowing, $"{at}: {path} '{quoted}' runs out of its box");

            var letters = LettersRect(text);
            if (letters.width < 1) continue;
            var box = ScreenRect(text);
            var outside = letters.xMin < box.xMin - 4 || letters.xMax > box.xMax + 4 || letters.yMin < box.yMin - 6 || letters.yMax > box.yMax + 6;
            Expect(!outside, $"{at}: {path} '{quoted}' letters {Describe(letters)} are outside its box {Describe(box)}");

            // The letters without the room around them, which the check below compares, are about where the text's
            // bounds are (those leave out accents, like on É, and right-to-left text is measured a little wider)
            var ink = InkRect(text);
            Expect(ink.width >= 1 && Overlap(ink, letters) && ink.height > letters.height / 3 && ink.height < letters.height * 1.5f,
                $"{at}: {path} '{quoted}' letters {Describe(ink)} without the room around them aren't measured right, with it {Describe(letters)}");
        }

        // Texts side by side, like a tier card's lines, don't run into each other: their letters, as the room some fonts
        // keep around them would count lines that look well apart
        for (var i = 0; i < texts.Count; i++)
        {
            for (var j = i + 1; j < texts.Count; j++)
            {
                var (a, b) = (texts[i], texts[j]);
                if (a.transform.parent != b.transform.parent) continue;
                var (ra, rb) = (InkRect(a), InkRect(b));
                if (ra.width < 1 || rb.width < 1) continue;
                Expect(!Overlap(ra, rb),
                    $"{at}: {NamePath(a.transform, root)} '{a.GetParsedText()}' {Describe(ra)} runs into {NamePath(b.transform, root)} '{b.GetParsedText()}' {Describe(rb)}");
            }
        }
    }

    private IEnumerator Screenshot(string what)
    {
        var folder = System.Environment.GetEnvironmentVariable("CBC_SCREENSHOTS");
        if (!string.IsNullOrWhiteSpace(folder)) yield return ScreenshotTour.Capture(folder, $"languages-{language}-{what}");
    }

    private void Expect(bool condition, string problem)
    {
        if (condition) return;
        problems.Add(problem);
        Log(problem);
    }

    private static string NamePath(Transform transform, Transform root)
    {
        var names = new List<string>();
        for (var current = transform; current != null && current != root; current = current.parent) names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
