using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppNewtonsoft.Json;
using JObject = Newtonsoft.Json.Linq.JObject;
using JToken = Newtonsoft.Json.Linq.JToken;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// (De)serializes map saves with the game's own JSON library. Tower, bloon and projectile data are polymorphic, so
/// their type names are written too; the files are the mod's own and never shared.
/// <br/>
/// Reading them still only allows type names from the game and its system library (#108), so a hand-edited
/// Continue.json can't make the JSON library create anything else.
/// </summary>
public static class MapSaveJson
{
    private static readonly JsonSerializerSettings Settings = new() {TypeNameHandling = TypeNameHandling.Auto};

    /// <summary>
    /// The assemblies a map save's types come from: the game's own and the system library of its collections
    /// </summary>
    private static readonly string[] AllowedAssemblies = {"Assembly-CSharp", "mscorlib", "System.Private.CoreLib"};

    public static string Serialize(MapSaveDataModel save) => JsonConvert.SerializeObject(save, Settings);

    /// <exception cref="InvalidDataException">The JSON names a type from another assembly</exception>
    public static MapSaveDataModel? Deserialize(string json)
    {
        if (FirstUnknownType(JToken.Parse(json)) is { } type)
        {
            throw new InvalidDataException($"The map save names a type the game doesn't use: {type}");
        }

        return JsonConvert.DeserializeObject<MapSaveDataModel>(json, Settings);
    }

    /// <summary>
    /// The first "$type" in the JSON whose assembly isn't one of <see cref="AllowedAssemblies"/>, or null
    /// </summary>
    internal static string? FirstUnknownType(JToken token)
    {
        if (token is JObject obj && obj["$type"] is { } typeToken)
        {
            var type = (string?) typeToken ?? "";
            if (!IsAllowed(type)) return type;
        }

        return token.Children().Select(FirstUnknownType).FirstOrDefault(type => type != null);
    }

    /// <summary>
    /// Whether every assembly in a type name, generic arguments included, is one the game's map saves use. In
    /// "Type`1[[Arg, ArgAssembly, Version=…]], Assembly" the assemblies are the parts after a comma without an "=".
    /// </summary>
    internal static bool IsAllowed(string typeName)
    {
        var assemblies = AssemblyPart.Matches(typeName).Select(match => match.Groups[1].Value.Trim())
            .Where(part => !part.Contains('=')).ToList();
        return assemblies.Count > 0 && assemblies.All(assembly =>
            AllowedAssemblies.Contains(assembly) || assembly.StartsWith("Il2Cpp", StringComparison.Ordinal));
    }

    private static readonly Regex AssemblyPart = new(@",([^,\[\]]+)(?=,|\]|$)");
}
