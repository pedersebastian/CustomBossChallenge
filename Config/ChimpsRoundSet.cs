using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CustomBossChallenge.Config;

/// <summary>
/// The rounds of the CHIMPS boss events (#139), <see cref="BossOptions.ChimpsEventRoundSet"/>. NK sends these sets
/// with the events, so the game itself doesn't have them; the mod brings copies in RoundSets/, in the game's own
/// format:
/// <list type="bullet">
/// <item>chimpsbloonarius.json from Blooncyclopedia's dump of the set (JSON:Bloons_TD_6/Rounds/chimpsbloonarius)</item>
/// <item>chimpsboss.json from BTD6 API Explorer's dump (github.com/HalfHydra/BTD6-API-Explorer,
/// data/roundsets/chimpsboss.json), with each group's end turned back into the game's duration</item>
/// </list>
/// </summary>
public static class ChimpsRoundSet
{
    /// <summary>The name the mod's copy of <paramref name="set"/> is built into the mod under</summary>
    public static string ResourceName(string set) => $"CustomBossChallenge.{set}.json";

    /// <summary>A group of bloons as the game's round sets have it, times in seconds</summary>
    public sealed record Group(string Bloon, int Count, float Start, float Duration)
    {
        /// <summary>When the last bloon of the group comes, like the game's own BloonGroup.End</summary>
        public float End => Start + Duration;
    }

    /// <summary>A round of the set: its bloons replace the round's own (the set never adds to a round)</summary>
    public sealed record Round(int Number, bool AddToRound, IReadOnlyList<Group> Groups);

    private static readonly Dictionary<string, IReadOnlyList<Round>> Sets = new();

    /// <summary>Every round of the mod's copy of <paramref name="set"/>, in order, read once</summary>
    public static IReadOnlyList<Round> Rounds(string set)
    {
        if (Sets.TryGetValue(set, out var rounds)) return rounds;
        var resource = ResourceName(set);
        using var stream = typeof(ChimpsRoundSet).Assembly.GetManifestResourceStream(resource) ??
                           throw new FileNotFoundException($"The mod has no {resource}");
        using var reader = new StreamReader(stream);
        return Sets[set] = Parse(reader.ReadToEnd());
    }

    /// <summary>The rounds of a round set in the game's JSON format, in order of their numbers</summary>
    public static IReadOnlyList<Round> Parse(string json)
    {
        var file = JsonConvert.DeserializeObject<FileJson>(json) ?? throw new InvalidDataException("No round set");
        return (file.rounds ?? [])
            .Select(round => new Round(round.roundNumber, round.addToRound,
                (round.bloonGroups ?? []).Select(g => new Group(g.bloon ?? "", g.count, g.start, g.duration)).ToList()))
            .OrderBy(round => round.Number)
            .ToList();
    }

    // The file's own names, as the game writes them
#pragma warning disable CS0649, IDE1006
    private sealed class FileJson
    {
        public RoundJson[]? rounds;
    }

    private sealed class RoundJson
    {
        public int roundNumber;
        public bool addToRound;
        public GroupJson[]? bloonGroups;
    }

    private sealed class GroupJson
    {
        public string? bloon;
        public int count;
        public float start;
        public float duration;
    }
#pragma warning restore CS0649, IDE1006
}
