using Il2CppAssets.Scripts.Models.Profile;
using Il2CppNewtonsoft.Json;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// (De)serializes map saves with the game's own JSON library. Tower, bloon and projectile data are polymorphic, so
/// their type names are written too; the files are the mod's own and never shared.
/// </summary>
public static class MapSaveJson
{
    private static readonly JsonSerializerSettings Settings = new() {TypeNameHandling = TypeNameHandling.Auto};

    public static string Serialize(MapSaveDataModel save) => JsonConvert.SerializeObject(save, Settings);

    public static MapSaveDataModel? Deserialize(string json) =>
        JsonConvert.DeserializeObject<MapSaveDataModel>(json, Settings);
}
