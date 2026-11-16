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
    /// Sets up a fresh InGameData for a single player Boss Challenge with the given model. The game never saves it,
    /// normal Boss Challenges included: a test's game must not end up among the player's own unfinished games (a normal
    /// Bloonarius game of ContinueLifecycleTest was saved as BCBloonarius at round 11). Custom games are told apart by
    /// their name, so this doesn't make a normal one custom.
    /// </summary>
    public static InGameData SetupBossChallenge(DailyChallengeModel dcm, BossType boss, bool ranked = false,
        bool elite = false)
    {
        dcm.disableSaves = true;
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

    public static BossOptions CustomOptions => new()
    {
        Boss = BossType.Bloonarius,
        SkullMode = SkullMode.EvenlySpaced,
        SkullCount = 4
    };

    /// <summary>
    /// An editor model with custom boss and start rules, like one made in the Boss Creator
    /// </summary>
    public static DailyChallengeModel CustomEditorDcm()
    {
        var dcm = BaseDcm();
        dcm.bloonModifiers.bossSpeedMultiplier = 2;
        dcm.bloonModifiers.healthMultipliers.boss = 0.5f;
        dcm.startRules.cash = 5000;
        return dcm;
    }
}
