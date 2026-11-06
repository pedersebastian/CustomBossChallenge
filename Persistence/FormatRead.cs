using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Why a file of the mod couldn't be read
/// </summary>
public enum FormatProblem
{
    None,

    /// <summary>The file comes from a newer version of the mod. It must be left alone: not used, overwritten or moved.</summary>
    TooNew,

    /// <summary>The file can't be read</summary>
    Invalid
}

/// <summary>
/// What reading one of the mod's files gave: the file, or why there is none
/// </summary>
/// <param name="Changes">What was brought into range while reading, for the log</param>
public sealed record FormatRead<T>(FormatProblem Problem, T? Value, string Error, IReadOnlyList<string> Changes)
    where T : class
{
    public static FormatRead<T> Read(T value, IReadOnlyList<string> changes) => new(FormatProblem.None, value, "", changes);
    public static FormatRead<T> TooNew(int version) => new(FormatProblem.TooNew, null, $"Version {version} is from a newer version of the mod", []);
    public static FormatRead<T> Invalid(string error) => new(FormatProblem.Invalid, null, error, []);
}

/// <summary>
/// The JSON settings of the mod's files. The formats (ContinueFormat, LastSetupFormat, SavedSetupsFormat) turn
/// their files into JSON text and back without the game, so the unit tests can check files from every released version
/// (UnitTests/Fixtures/saves/). The stores around them find the files and handle the game's types.
/// </summary>
internal static class FormatJson
{
    /// <summary>Boss options are written with their enums as names</summary>
    public static readonly JsonSerializerSettings WithEnumNames = new()
    {
        Formatting = Formatting.Indented,
        Converters = {new StringEnumConverter()}
    };

    public static readonly JsonSerializerSettings Plain = new() {Formatting = Formatting.Indented};
}
