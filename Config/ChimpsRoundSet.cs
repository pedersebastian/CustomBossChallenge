using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CustomBossChallenge.Config;

/// <summary>
/// The rounds of the CHIMPS boss events (#139), <see cref="BossOptions.ChimpsEventRoundSet"/>. NK sends this set with
/// the events, so the game itself doesn't have it; the mod brings a copy in RoundSets/chimpsbloonarius.json, taken
/// from Blooncyclopedia's dump of the game's set (JSON:Bloons_TD_6/Rounds/chimpsbloonarius) in the game's own format.
/// </summary>
public static class ChimpsRoundSet
{
    /// <summary>The name the mod's copy is built into the mod under</summary>
    public const string ResourceName = "CustomBossChallenge.chimpsbloonarius.json";

    /// <summary>A group of bloons as the game's round sets have it, times in seconds</summary>
    public sealed record Group(string Bloon, int Count, float Start, float Duration)
    {
        /// <summary>When the last bloon of the group comes, like the game's own BloonGroup.End</summary>
        public float End => Start + Duration;
    }

    /// <summary>A round of the set: its bloons replace the round's own (the set never adds to a round)</summary>
    public sealed record Round(int Number, bool AddToRound, IReadOnlyList<Group> Groups);

    private static IReadOnlyList<Round>? rounds;

    /// <summary>Every round of the mod's copy, in order, read once</summary>
    public static IReadOnlyList<Round> Rounds
    {
        get
        {
            if (rounds != null) return rounds;
            using var stream = typeof(ChimpsRoundSet).Assembly.GetManifestResourceStream(ResourceName) ??
                               throw new FileNotFoundException($"The mod has no {ResourceName}");
            using var reader = new StreamReader(stream);
            return rounds = Parse(reader.ReadToEnd());
        }
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
