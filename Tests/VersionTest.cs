using System.Collections;
using UnityEngine;

namespace CustomBossChallenge.Tests;

/// <summary>
/// WorksOnVersion says which BTD6 version the tests last passed on, so it must be the game the tests run on. After a
/// game update this fails until it is checked and updated.
/// </summary>
public class VersionTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var game = Application.version;
        Log($"BTD6 {game}, WorksOnVersion {ModHelperData.WorksOnVersion}");
        Assert(!string.IsNullOrWhiteSpace(game), "The game's version is unknown");
        AssertEquals(ModHelperData.WorksOnVersion, game, "WorksOnVersion in ModHelperData.cs must match the game");
        yield break;
    }
}
