using System.Collections;
using System.IO;
using System.Linq;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The game's default rounds and what each bloon is worth (its RBE), for tools/make-gildebloon-rounds.py, which makes
/// Gildebloon's own Gold Rush rounds from them (#25). Started with <c>CBC_ROUND_DATA=&lt;repo&gt;/tools/data</c> it
/// writes defaultroundset.json (in the game's round set format, times in seconds) and bloonrbe.json; without it the
/// test only checks that the game has what the tool needs.
/// </summary>
public class RoundDataTest : CustomBossTest
{
    public const string EnvironmentVariable = "CBC_ROUND_DATA";

    protected override IEnumerator Run()
    {
        var set = GameData.Instance.RoundSetByName("DefaultRoundSet");
        AssertNotNull(set, "The game's default rounds");
        Assert(set!.rounds.Length >= 140, $"The default rounds have 140 rounds: {set.rounds.Length}");

        var rounds = new JArray();
        for (var i = 0; i < set.rounds.Length; i++)
        {
            var groups = new JArray();
            foreach (var group in set.rounds[i].groups)
            {
                groups.Add(new JObject
                {
                    ["bloon"] = group.bloon, ["count"] = group.count, ["start"] = Seconds(group.start),
                    ["duration"] = Seconds(group.end - group.start)
                });
            }
            rounds.Add(new JObject {["roundNumber"] = i + 1, ["addToRound"] = false, ["bloonGroups"] = groups});
        }

        var rbe = new JObject();
        foreach (var bloon in Game.instance.model.bloons.ToArray().Where(bloon => !bloon.isBoss).OrderBy(bloon => bloon.id))
        {
            rbe[bloon.id] = Rbe(bloon);
        }
        Assert(rbe["Ceramic"]?.Value<int>() > 0 && rbe["Moab"]?.Value<int>() > rbe["Ceramic"]!.Value<int>(), "Bloons have an RBE");

        var folder = System.Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "defaultroundset.json"), new JObject {["rounds"] = rounds}.ToString(Formatting.Indented) + "\n");
            File.WriteAllText(Path.Combine(folder, "bloonrbe.json"), rbe.ToString(Formatting.Indented) + "\n");
            Log($"wrote {rounds.Count} rounds and {rbe.Count} bloons to {folder}");
        }
        yield break;
    }

    private static double Seconds(float frames) => System.Math.Round(frames / 60.0, 3);

    /// <summary>What popping a bloon fully takes: its own health and all that comes out of it</summary>
    private static int Rbe(BloonModel bloon, int depth = 0)
    {
        if (depth > 12) return 0;
        var total = (int) bloon.maxHealth;
        var children = bloon.GetBehavior<Il2CppAssets.Scripts.Models.Bloons.Behaviors.SpawnChildrenModel>();
        if (children == null) return total;
        foreach (var child in children.children)
        {
            if (Game.instance.model.DoesBloonExist(child)) total += Rbe(Game.instance.model.GetBloon(child), depth + 1);
        }
        return total;
    }
}
