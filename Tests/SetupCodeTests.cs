using System;
using System.Collections;
using System.Linq;
using System.Text;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Setup codes survive the round trip, stay short enough for a chat message and never break on bad input
/// </summary>
public class SetupCodeTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var defaultCode = SetupCode.Encode(new BossOptions(), DailyChallengeModel.CreateDefaultEditorModel());
        Log($"default code: {defaultCode}");
        Assert(defaultCode.StartsWith(SetupCode.Prefix), "Codes start with the prefix");
        Assert(defaultCode.Length < 60, $"The default setup must give a tiny code, got {defaultCode.Length} characters");
        Assert(defaultCode.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ':'),
            $"Codes must only use characters chat apps leave alone: {defaultCode}");

        var options = new BossOptions
        {
            Boss = BossType.Lych, Elite = true, Tiers = 9, ExtraTierHealth = 220, SkullMode = SkullMode.Custom,
            CustomSkulls = "90, 45", SpawnRounds = "30, 45"
        };
        var dcm = CustomEditorDcm();
        dcm.name = "Impossible Lych";
        dcm.map = "Logs";
        dcm.difficulty = "Hard";
        dcm.maxParagons = 2;
        dcm.bloonModifiers.allFortified = true;
        dcm.towers[dcm.towers.Count - 1].max = 3;
        var restricted = dcm.towers[dcm.towers.Count - 1].tower;

        var code = SetupCode.Encode(options, dcm);
        Log($"code ({code.Length} characters): {code}");
        Assert(code.Length < 400, $"Changing a few rules must give a short code, got {code.Length} characters: {SetupCode.Contents(code)}");

        Assert(SetupCode.TryDecode($"  {code}\n", out var decoded, out var decodedDcm, out var problem),
            $"The code did not decode: {problem}");
        AssertEquals(problem, CodeProblem.None, "Problem");
        AssertEquals(decoded, options, "Boss options");
        AssertNotNull(decodedDcm, "Challenge");
        AssertEquals(decodedDcm!.name, "Impossible Lych", "Name");
        AssertEquals(decodedDcm.map, "Logs", "Map");
        AssertEquals(decodedDcm.difficulty, "Hard", "Difficulty");
        AssertEquals(decodedDcm.startRules.cash, 5000, "Start cash");
        AssertEquals(decodedDcm.maxParagons, 2, "Max paragons");
        Assert(decodedDcm.bloonModifiers.allFortified, "All fortified");
        Assert(Approximately(decodedDcm.bloonModifiers.bossSpeedMultiplier, 2), "Boss speed");
        Assert(Approximately(decodedDcm.bloonModifiers.healthMultipliers.boss, 0.5f), "Boss health");
        var tower = decodedDcm.towers.ToArray().First(t => t.tower == restricted);
        AssertEquals(tower.max, 3, $"Tower limit of {restricted}");
        var again = SetupCode.Encode(decoded, decodedDcm);
        Assert(again == code, "Encoding a decoded setup must give the same code:\n" +
                              $"{SetupCode.Contents(code)}\n{SetupCode.Contents(again)}");

        // Values out of range are brought back into range, like a hand edited setup file
        Assert(SetupCode.TryDecode(SetupCode.Encode(new BossOptions {Tiers = 50, SkullCount = -4}, dcm),
            out var odd, out _, out _), "A code with odd values must still load");
        AssertEquals(odd.Tiers, TierMath.MaxTiers, "Tiers brought into range");
        AssertEquals(odd.SkullCount, 0, "Skull count brought into range");

        var payload = code[SetupCode.Prefix.Length..];
        foreach (var bad in new[]
                 {
                     null, "", "   ", "hello", payload, SetupCode.Prefix, SetupCode.Prefix + "!!!!",
                     SetupCode.Prefix + payload[..(payload.Length / 2)], SetupCode.Prefix + "aGVsbG8",
                     SetupCode.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"o\": 1}")),
                     SetupCode.Prefix + new string('A', SetupCode.MaxLength)
                 })
        {
            Assert(!SetupCode.TryDecode(bad, out var badOptions, out var badDcm, out var badProblem),
                $"'{Shorten(bad)}' must not decode");
            AssertEquals(badProblem, CodeProblem.Invalid, $"Problem of '{Shorten(bad)}'");
            AssertEquals(badOptions, new BossOptions(), $"Options of '{Shorten(bad)}'");
            AssertEquals(badDcm, null, $"Challenge of '{Shorten(bad)}'");
        }

        Assert(!SetupCode.TryDecode("CBC9:" + payload, out _, out _, out var tooNew), "A newer code must not decode");
        AssertEquals(tooNew, CodeProblem.TooNew, "Problem of a code from a newer version");
        yield break;
    }

    private static string Shorten(string? text) => text == null ? "null" : text.Length > 30 ? text[..30] + "..." : text;
}
