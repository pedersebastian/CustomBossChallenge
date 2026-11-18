using System;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Writes enums as their names, like <see cref="StringEnumConverter"/>, and bosses by <see cref="BossList.Name"/>, so
/// the mod's own Gildebloon is "Gildebloon" in files and codes instead of a number the game doesn't know (#25).
/// Reading takes the names of every boss the mod knows, and numbers as before.
/// </summary>
public sealed class BossNameConverter : StringEnumConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is BossType boss && BossList.IsModBoss(boss)) writer.WriteValue(BossList.Name(boss));
        else base.WriteJson(writer, value, serializer);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var type = Nullable.GetUnderlyingType(objectType) ?? objectType;
        if (type == typeof(BossType) && reader.TokenType == JsonToken.String && BossList.TryParse((string?) reader.Value, out var boss) &&
            BossList.IsModBoss(boss))
        {
            return boss;
        }

        // Gildebloon only goes by its name: its number is no boss, as it was before it existed
        var read = base.ReadJson(reader, objectType, existingValue, serializer);
        return read is BossType number && BossList.IsModBoss(number) && reader.TokenType != JsonToken.String
            ? (BossType) (-1)
            : read;
    }
}
