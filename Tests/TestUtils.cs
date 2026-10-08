using System;
using System.Linq;
using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;

namespace CustomBossChallenge.Tests;

internal static class TestUtils
{
    public const string Map = "Tutorial";

    /// <summary>
    /// A plain Boss Challenge model like the one the Boss Challenge screen creates
    /// </summary>
    public static DailyChallengeModel BaseDcm()
    {
        var dcm = DailyChallengeModel.CreateDefaultEditorModel();
        dcm.map = Map;
        dcm.mode = "Standard";
        dcm.difficulty = "Medium";
        dcm.name = "Test Boss Challenge";
        return dcm;
    }

    /// <summary>
    /// Sets up a fresh InGameData for a single player Boss Challenge with the given model
    /// </summary>
    public static InGameData SetupBossChallenge(DailyChallengeModel dcm, BossType boss, bool ranked = false,
        bool elite = false)
    {
        InGameData.CreateNewInstance();
        var data = InGameData.Editable;
        data.selectedMap = dcm.map;
        data.selectedMode = dcm.mode;
        data.selectedDifficulty = dcm.difficulty;
        data.SetupBossChallenge("", boss, elite, ranked, dcm, LeaderboardScoringType.GameTime);
        return data;
    }

    public static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;

    public static bool SameValues(float[]? a, float[]? b) =>
        a != null && b != null && a.Length == b.Length && a.Zip(b).All(pair => Approximately(pair.First, pair.Second));

    public static string Show(float[]? values) => values == null ? "null" : $"[{string.Join(", ", values)}]";

    public static BossChallengeConfig CustomConfig => new()
    {
        BossSpeed = 200,
        BossHealth = 50,
        BloonSpeed = 150,
        MoabSpeed = 125,
        CeramicHealth = 300,
        MoabHealth = 250,
        RegrowRate = 75,
        SkullMode = SkullMode.EvenlySpaced,
        SkullCount = 4,
        StartCash = 5000,
        StartLives = 50,
        NoSelling = true,
        AllCamo = true
    };
}
