using System.Collections;
using BTD_Mod_Helper.Api.Components;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Unity.UI_New.Main;
using Il2CppAssets.Scripts.Unity.UI_New.Main.EventPanel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The main menu has exactly one Boss Creator button, below the event column, that only opens the Boss Creator
/// </summary>
public class MainMenuButtonTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var mainMenu = AssertNotNull(Object.FindObjectOfType<MainMenu>(), "No main menu");
        var menu = mainMenu.transform;

        MainMenuButton.Create(mainMenu);
        MainMenuButton.Create(mainMenu);
        mainMenu.ReOpen(null);
        yield return null;

        var count = 0;
        for (var i = 0; i < menu.childCount; i++)
        {
            if (menu.GetChild(i).name == MainMenuButton.Name) count++;
        }
        AssertEquals(count, 1, "Boss Creator buttons on the main menu");

        var button = menu.Find(MainMenuButton.Name);
        Assert(button.gameObject.activeSelf, "The button is hidden");

        var unityButton = AssertNotNull(button.GetComponentInChildren<Button>(true), "No Unity button");
        AssertEquals(unityButton.onClick.GetPersistentEventCount(), 0, "Copied listeners must be gone");

        // The button always shows the mod's logo, whichever boss was chosen last
        var options = Creator.BossMode.Options;
        Creator.BossMode.Options = options with {Boss = Il2CppAssets.Scripts.Data.Boss.BossType.Vortex};
        MainMenuButton.Create(mainMenu);
        Creator.BossMode.Options = options;
        yield return new WaitForSecondsRealtime(1);
        var sprite = AssertNotNull(unityButton.image.sprite, "The button has no icon");
        AssertEquals(sprite!.name, "CustomBossChallenge-" + MainMenuButton.LogoName, "Button icon");

        var label = AssertNotNull(button.GetComponentInChildren<ModHelperText>(true), "No label");
        AssertEquals(label.Text.text, MainMenuButton.Label, "Label");

        var panel = AssertNotNull(mainMenu.GetComponentInChildren<MainMenuEventPanel>(true), "No event column");
        var column = panel.GetComponent<RectTransform>();
        var columnCorners = new Il2CppStructArray<Vector3>(4);
        var buttonCorners = new Il2CppStructArray<Vector3>(4);
        column.GetWorldCorners(columnCorners);
        button.GetComponent<RectTransform>().GetWorldCorners(buttonCorners);
        Assert(columnCorners[1].y - columnCorners[0].y > 1 && buttonCorners[1].y - buttonCorners[0].y > 1,
            "Could not measure the column and button");
        Assert(buttonCorners[0].y > columnCorners[0].y - (columnCorners[1].y - columnCorners[0].y),
            $"The button (bottom {buttonCorners[0].y}) is far below the event column");
        Assert(buttonCorners[1].y <= columnCorners[1].y + 0.01f,
            $"The button (top {buttonCorners[1].y}) must start in the event column (top {columnCorners[1].y})");

        var icons = panel.iconContainer;
        for (var i = 0; i < icons.childCount; i++)
        {
            if (!icons.GetChild(i).gameObject.activeInHierarchy) continue;
            var iconCorners = new Il2CppStructArray<Vector3>(4);
            icons.GetChild(i).GetComponent<RectTransform>().GetWorldCorners(iconCorners);
            Assert(buttonCorners[1].y <= iconCorners[0].y + 0.01f, $"The button overlaps event button {i}");
        }
        Assert(buttonCorners[0].x >= columnCorners[0].x - 1 && buttonCorners[2].x <= columnCorners[2].x + 1,
            "The button must be lined up with the event column");
    }
}
