using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppNewtonsoft.Json;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// (De)serializes challenge models with the game's own JSON library, the same way the game saves its editor draft
/// </summary>
public static class DcmJson
{
    public static string Serialize(DailyChallengeModel dcm)
    {
        var copy = dcm.Clone();

        // Never used by Boss Creator, and polymorphic models don't round trip without type names
        copy.customMapModel = null;
        copy.simulationBehaviorsMod = null;
        return JsonConvert.SerializeObject(copy);
    }

    public static DailyChallengeModel? Deserialize(string json) =>
        JsonConvert.DeserializeObject<DailyChallengeModel>(json);
}
