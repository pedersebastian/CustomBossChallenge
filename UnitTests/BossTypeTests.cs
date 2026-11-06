using System;
using System.IO;
using System.Linq;
using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// Shared/BossType.cs stands in for the game's BossType, so it must have the game's bosses, in the game's order. The
/// in-game WebDataTest writes them to web/src/data/game.json.
/// </summary>
public class BossTypeTests
{
    [Fact]
    public void The_stand_in_has_the_games_bosses()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "web", "src", "data", "game.json"))) folder = folder.Parent;
        if (folder == null) throw new FileNotFoundException("web/src/data/game.json not found above " + AppContext.BaseDirectory);

        var game = JObject.Parse(File.ReadAllText(Path.Combine(folder.FullName, "web", "src", "data", "game.json")));
        var bosses = ((JObject) game["bosses"]!).Properties().Select(property => property.Name);
        Assert.Equal(bosses, Enum.GetNames<BossType>());
    }
}
