using System.Linq;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppNewtonsoft.Json;
using JObject = Newtonsoft.Json.Linq.JObject;
using JToken = Newtonsoft.Json.Linq.JToken;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// (De)serializes challenge models with the game's own JSON library, the same way the game saves its editor draft
/// </summary>
public static class DcmJson
{
    /// <summary>
    /// Never let JSON choose which types get created, whatever the game sets as its default
    /// </summary>
    private static readonly JsonSerializerSettings ReadSettings = new() {TypeNameHandling = TypeNameHandling.None};

    public static string Serialize(DailyChallengeModel dcm)
    {
        var copy = dcm.Clone();

        // Never used by Boss Creator, and polymorphic models don't round trip without type names
        copy.customMapModel = null;
        copy.simulationBehaviorsMod = null;
        return JsonConvert.SerializeObject(copy);
    }

    /// <summary>
    /// Reads a challenge from JSON that may come from someone else, so type names and references are removed first
    /// </summary>
    public static DailyChallengeModel? Deserialize(string json)
    {
        var token = JToken.Parse(json);
        RemoveMetadata(token);
        return JsonConvert.DeserializeObject<DailyChallengeModel>(token.ToString(Newtonsoft.Json.Formatting.None),
            ReadSettings);
    }

    /// <summary>
    /// Removes Json.NET's special properties ($type, $ref, $id, $values, ...) everywhere in the JSON
    /// </summary>
    internal static void RemoveMetadata(JToken token)
    {
        if (token is JObject obj)
        {
            foreach (var name in obj.Properties().Select(p => p.Name).Where(name => name.StartsWith('$')).ToList())
            {
                obj.Remove(name);
            }
        }

        foreach (var child in token.Children())
        {
            RemoveMetadata(child);
        }
    }
}
