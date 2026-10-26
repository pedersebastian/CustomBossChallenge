using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Testing;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Main;
using UnityEngine;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Base for this mod's tests: logs each step so a hanging test is easy to spot in the log, starts every test from
/// the main menu, and makes sure exceptions thrown anywhere inside nested coroutines fail the test
/// </summary>
public abstract class CustomBossTest : ModTest
{
    public const float MenuTimeout = 60;

    public sealed override IEnumerator Test()
    {
        // CBC_TESTS=RetryTest,ContinueTest runs only those tests, the others pass right away
        var only = System.Environment.GetEnvironmentVariable("CBC_TESTS");
        if (!string.IsNullOrWhiteSpace(only) && !only.Split(',').Select(name => name.Trim()).Contains(GetType().Name))
        {
            yield break;
        }

        // Tests play custom games, which must never replace or delete the player's unfinished one. A failing test
        // leaves the temporary store in place, which is fine: every test gets its own.
        var store = CustomBossRetries.Store;
        TempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CustomBossChallengeTest-" + Guid.NewGuid().ToString("N"));
        CustomBossRetries.Store = new Persistence.ContinueStore(System.IO.Path.Combine(TempFolder, "Continue.json"));

        Log("starting");
        yield return RunSafely(ToMainMenu());
        yield return RunSafely(Run());
        Log("done");

        CustomBossRetries.Store = store;
        if (System.IO.Directory.Exists(TempFolder)) System.IO.Directory.Delete(TempFolder, true);
    }

    /// <summary>
    /// A folder of this test's own, deleted when it is done; <see cref="CustomBossRetries.Store"/> writes into it
    /// </summary>
    protected string TempFolder { get; private set; } = "";

    protected abstract IEnumerator Run();

    protected void Log(string message) => ModHelper.Msg<CustomBossChallenge>($"{Name}: {message}");

    /// <summary>
    /// Every tier in <paramref name="match"/> has the speed and health Speed and Health per Tier give it, worked out
    /// from the values the match's models had before they were scaled
    /// </summary>
    protected void AssertScaledTiers(Il2CppAssets.Scripts.Models.GameModel match, Config.BossOptions options)
    {
        for (var tier = 1; tier <= options.Tiers; tier++)
        {
            var model = match.GetBloon(options.BossId(tier));
            var source = Config.TierScaling.Original(match, options.BossId(Math.Min(tier, Config.TierMath.VanillaTiers)));
            var speed = (float) Config.TierMath.SpeedFactor(tier, options.SpeedPerTier);
            Assert(Math.Abs(model.speed - source.Speed * speed) < 0.0001f,
                $"{model.id} speed {model.speed}, expected {source.Speed * speed}");
            Assert(Math.Abs(model.speedFrames - source.SpeedFrames * speed) < 0.0001f, $"{model.id} speed frames");
            // The game applies Boss Health to the models after they are scaled, so compare health with tier 1's
            var expected = Config.TierMath.ScaleHealth(source.MaxHealth,
                Config.TierMath.ExtraTierFactor(tier) * Config.TierMath.HealthFactor(tier, options.HealthPerTier)) /
                (double) Config.TierScaling.Original(match, options.BossId(1)).MaxHealth;
            var actual = model.maxHealth / (double) match.GetBloon(options.BossId(1)).maxHealth;
            Assert(Math.Abs(actual / expected - 1) < 0.001, $"{model.id} health is x{actual} of tier 1, expected x{expected}");
        }

        // Compared with the game's own models, each tier is faster than tier 1 by exactly Speed per Tier
        var game = Il2CppAssets.Scripts.Unity.Game.instance.model;
        var tier1 = match.GetBloon(options.BossId(1)).speed / game.GetBloon(options.BossId(1)).speed;
        for (var tier = 2; tier <= Math.Min(options.Tiers, Config.TierMath.VanillaTiers); tier++)
        {
            var ratio = match.GetBloon(options.BossId(tier)).speed / game.GetBloon(options.BossId(tier)).speed / tier1;
            Assert(Math.Abs(ratio - Config.TierMath.SpeedFactor(tier, options.SpeedPerTier)) < 0.001,
                $"Tier {tier} is x{ratio} as fast as the game makes it, compared with tier 1");
        }
    }

    /// <summary>
    /// Plays rounds until the game is lost, then waits for the defeat screen
    /// </summary>
    protected IEnumerator LoseAndOpenDefeatScreen(Action<Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossDefeatScreen> found)
    {
        var bridge = InGame.instance.bridge;
        bridge.SetFastForward(true);
        var start = Time.realtimeSinceStartup;
        while (!bridge.Simulation.GameOver)
        {
            if (Time.realtimeSinceStartup - start > BossNotDefeatedTest.TimeoutSeconds) Fail("The game didn't end");
            if (!bridge.AreRoundsActive()) bridge.StartRound();
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Log($"lost on round {bridge.GetCurrentRound() + 1}");

        Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossDefeatScreen? screen;
        start = Time.realtimeSinceStartup;
        while ((screen = UnityEngine.Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossDefeatScreen>()) == null)
        {
            if (Time.realtimeSinceStartup - start > 30) Fail("The defeat screen did not open");
            yield return new WaitForSecondsRealtime(0.25f);
        }
        yield return new WaitForSecondsRealtime(1);
        found(screen);
    }

    /// <summary>
    /// Waits for the game to go on after a retry from the defeat screen
    /// </summary>
    protected IEnumerator WaitForResume()
    {
        var start = Time.realtimeSinceStartup;
        while (InGame.instance?.bridge?.Simulation == null || InGame.instance.bridge.Simulation.GameOver ||
               UnityEngine.Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossDefeatScreen>() != null)
        {
            if (Time.realtimeSinceStartup - start > 30)
            {
                var screen = UnityEngine.Object.FindObjectOfType<Il2CppAssets.Scripts.Unity.UI_New.GameOver.BossDefeatScreen>();
                Fail($"The game did not resume after the retry: game over {InGame.instance?.bridge?.Simulation?.GameOver}, " +
                     $"defeat screen {(screen == null ? "closed" : $"open, active {screen.gameObject.activeInHierarchy}")}");
            }
            yield return new WaitForSecondsRealtime(0.25f);
        }
        yield return new WaitForSecondsRealtime(1);
    }

    /// <summary>
    /// Steps through a coroutine and all managed coroutines it yields, turning any exception into a test failure.
    /// Without this, exceptions in nested coroutines are only logged and the test still counts as succeeded.
    /// </summary>
    private IEnumerator RunSafely(IEnumerator coroutine)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(coroutine);

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            bool moved;
            Exception? error = null;
            try
            {
                moved = current.MoveNext();
            }
            catch (Exception e)
            {
                moved = false;
                error = e;
            }

            if (error != null)
            {
                Fail(error.GetType().Name == "AssertException" ? error.Message : error.ToString());
            }

            if (!moved)
            {
                stack.Pop();
            }
            else if (current.Current is IEnumerator nested)
            {
                stack.Push(nested);
            }
            else
            {
                yield return current.Current;
            }
        }
    }

    private static bool OnMainMenu =>
        InGame.instance == null && MenuManager.instance != null &&
        MenuManager.instance.GetCurrentMenu()?.Is<MainMenu>() == true;

    protected IEnumerator WaitForPopup(Action<Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup> found)
    {
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitFor(() => (popup = Il2CppAssets.Scripts.Unity.UI_New.Popups.PopupScreen.instance.GetFirstActivePopup()) != null,
            "Showing a popup");
        found(popup!);
    }

    protected static IEnumerator ClosePopup(Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup popup, bool ok)
    {
        if (ok) popup.OKClicked();
        else popup.CancelClicked();
        yield return new WaitForSecondsRealtime(1);
    }

    protected IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 20)
    {
        var start = Time.realtimeSinceStartup;
        while (!condition())
        {
            if (Time.realtimeSinceStartup - start > seconds) Fail($"{what} took more than {seconds}s");
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    /// <summary>
    /// Goes to the main menu. Mod Helper's EnsureOnMainMenuWithNoPopUps can wait forever when it is called again
    /// right after reaching the main menu, so it is only used when actually needed.
    /// </summary>
    protected IEnumerator ToMainMenu()
    {
        yield return GetToMainMenu();

        // Menus ignore requests to open while another menu is still opening or closing
        var start = Time.realtimeSinceStartup;
        while (MenuManager.instance != null &&
               (MenuManager.instance.IsClosingOrOpeningMenu || MenuManager.instance.IsTransitioning) &&
               Time.realtimeSinceStartup - start < 10)
        {
            yield return new WaitForSecondsRealtime(0.25f);
        }
    }

    private IEnumerator GetToMainMenu()
    {
        if (OnMainMenu) yield break;

        yield return new WaitForSecondsRealtime(1);
        if (OnMainMenu) yield break;

        // Outside of a game, backing out of menus is more reliable than Mod Helper's helper
        for (var i = 0; i < 5 && InGame.instance == null && MenuManager.instance != null &&
                        MenuManager.instance.GetCurrentMenu() != null && !OnMainMenu; i++)
        {
            MenuManager.instance.Back();
            yield return new WaitForSecondsRealtime(1.5f);
        }
        if (OnMainMenu) yield break;

        yield return Timeout(EnsureOnMainMenuWithNoPopUps(), MenuTimeout);
        Log("on main menu");
    }
}
