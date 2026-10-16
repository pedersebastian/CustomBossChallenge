using System;
using System.Collections;
using System.Collections.Generic;
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
        Log("starting");
        yield return RunSafely(ToMainMenu());
        yield return RunSafely(Run());
        Log("done");
    }

    protected abstract IEnumerator Run();

    protected void Log(string message) => ModHelper.Msg<CustomBossChallenge>($"{Name}: {message}");

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

    /// <summary>
    /// Goes to the main menu. Mod Helper's EnsureOnMainMenuWithNoPopUps can wait forever when it is called again
    /// right after reaching the main menu, so it is only used when actually needed.
    /// </summary>
    protected IEnumerator ToMainMenu()
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
