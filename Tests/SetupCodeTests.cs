using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            Boss = BossType.Lych, Elite = true, Tiers = 9, SpeedPerTier = 15, HealthPerTier = 45, SkullMode = SkullMode.Custom,
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

        // Codes from before 2.3 had Extra Tier Health; it becomes Health per Tier
        Assert(SetupCode.TryDecode(SetupCode.Pack("{\"o\":{\"Tiers\":7,\"ExtraTierHealth\":225},\"c\":{}}"), out var old, out _, out _),
            "An old code must still load");
        AssertEquals(old, new BossOptions {Tiers = 7, HealthPerTier = 50}, "Old code options");

        Assert(!SetupCode.TryDecode("CBC9:" + payload, out _, out _, out var tooNew), "A newer code must not decode");
        AssertEquals(tooNew, CodeProblem.TooNew, "Problem of a code from a newer version");
        yield break;
    }

    private static string Shorten(string? text) => text == null ? "null" : text.Length > 30 ? text[..30] + "..." : text;
}

/// <summary>
/// Hand made codes can't create other types, push values out of range, break the game or dress up the popup
/// </summary>
public class MaliciousCodeTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        // Type names anywhere are removed before the game reads the challenge
        var typed = SetupCode.Pack(
            "{\"o\":{\"$type\":\"System.IO.FileInfo, System.IO.FileSystem\"},\"c\":{" +
            "\"$type\":\"System.IO.FileInfo, System.IO.FileSystem\"," +
            "\"customMapModel\":{\"$type\":\"System.Diagnostics.Process, System\",\"StartInfo\":{\"FileName\":\"calc\"}}," +
            "\"bloonModifiers\":{\"$type\":\"Nope\",\"$ref\":\"1\",\"allCamo\":true}," +
            "\"towers\":{\"$items\":{\"0\":{\"$type\":\"Nope\",\"max\":2}}}}}");
        Assert(SetupCode.TryDecode(typed, out _, out var typedDcm, out var typedProblem),
            $"A code with type names must still load without them: {typedProblem}");
        Assert(typedDcm!.bloonModifiers.allCamo, "The rest of the code must still apply");
        AssertEquals(typedDcm.customMapModel, null, "Custom map model");
        AssertEquals(typedDcm.towers[0].max, 2, "Tower limit next to a type name");
        var json = Newtonsoft.Json.Linq.JToken.Parse("{\"$type\":\"a\",\"x\":[{\"$id\":\"1\",\"y\":{\"$values\":[]}}]}");
        DcmJson.RemoveMetadata(json);
        AssertEquals(json.ToString(Newtonsoft.Json.Formatting.None), "{\"x\":[{\"y\":{}}]}", "Metadata removed everywhere");

        // Values far outside what the editor allows are brought into range
        var extreme = SetupCode.Pack(
            "{\"o\":{\"Tiers\":9999,\"Boss\":99},\"c\":{" +
            "\"bloonModifiers\":{\"bossSpeedMultiplier\":1e30,\"speedMultiplier\":NaN,\"moabSpeedMultiplier\":-3," +
            "\"healthMultipliers\":{\"boss\":-5,\"moabs\":1e9}}," +
            "\"startRules\":{\"cash\":2147483647,\"lives\":0,\"round\":-77,\"endRound\":2000000000,\"revives\":5000}," +
            "\"maxTowers\":999999,\"abilityCooldownReductionMultiplier\":-1," +
            "\"map\":\"NoSuchMap\",\"difficulty\":\"Nightmare\",\"mode\":\"Hacked\"," +
            "\"name\":\"  Line\nbreak\u0000 and a very long name that keeps going and going and going on\"}}");
        Assert(SetupCode.TryDecode(extreme, out var options, out var dcm, out var problem),
            $"A code with extreme values must still load: {problem}");
        AssertEquals(options.Tiers, TierMath.MaxTiers, "Tiers");
        AssertEquals(options.Boss, new BossOptions().Boss, "Unknown boss number");
        Assert(!SetupCode.TryDecode(SetupCode.Pack("{\"o\":{\"Boss\":\"NotABoss\"},\"c\":{}}"), out _, out _, out _),
            "A boss this version doesn't know must not load");
        var defaults = DailyChallengeModel.CreateDefaultEditorModel();
        var modifiers = dcm!.bloonModifiers;
        AssertEquals(modifiers.bossSpeedMultiplier, ChallengeLimits.MaxBossSpeed, "Boss speed");
        AssertEquals(modifiers.speedMultiplier, ChallengeLimits.MinMultiplier, "NaN bloon speed");
        AssertEquals(modifiers.moabSpeedMultiplier, ChallengeLimits.MinMultiplier, "Negative MOAB speed");
        AssertEquals(modifiers.healthMultipliers.boss, ChallengeLimits.MinBossHealth, "Boss health");
        AssertEquals(modifiers.healthMultipliers.moabs, ChallengeLimits.MaxMultiplier, "MOAB health");
        AssertEquals(dcm.startRules.cash, ChallengeLimits.MaxCash, "Cash");
        AssertEquals(dcm.startRules.lives, 1, "Lives");
        AssertEquals(dcm.startRules.round, -1, "A negative start round means default");
        AssertEquals(dcm.startRules.endRound, TierMath.MaxRound + TierMath.RoundsBetweenTiers, "End round");
        AssertEquals(dcm.startRules.revives, ChallengeLimits.MaxRevives, "Revives");
        AssertEquals(dcm.maxTowers, ChallengeLimits.MaxTowerCount, "Max towers");
        AssertEquals(dcm.abilityCooldownReductionMultiplier, 0f, "Cooldown multiplier");
        AssertEquals(dcm.map, defaults.map, "Unknown map");
        AssertEquals(dcm.difficulty, defaults.difficulty, "Unknown difficulty");
        AssertEquals(dcm.mode, defaults.mode, "Unknown mode");
        Assert(dcm.name.Length <= ChallengeLimits.MaxNameLength && dcm.name.StartsWith("Linebreak and"),
            $"Name without control characters and cut to length: '{dcm.name}'");

        // What was brought into range is reported for the log, and normal setups report nothing
        AssertEquals(ChallengeLimits.Sanitize(CustomEditorDcm(), defaults).Count, 0, "Changes to a normal setup");
        var tooFast = CustomEditorDcm();
        tooFast.bloonModifiers.bossSpeedMultiplier = 1e30f;
        tooFast.map = "NoSuchMap";
        var changes = ChallengeLimits.Sanitize(tooFast, defaults);
        AssertEquals(string.Join("; ", changes), $"unknown map 'NoSuchMap' -> {defaults.map}; boss speed 1E+30 -> 10",
            "Reported changes");
        var tooMany = new BossOptions {Tiers = 50};
        AssertEquals(string.Join("; ", ChallengeLimits.OptionChanges(tooMany, tooMany.Sanitized())), "Tiers 50 -> 10",
            "Reported option changes");

        // Rich text in a shared name is shown as text, even if it tries to close the protection itself
        dcm.name = "</noparse><size=500>Official</size>";
        var summary = Menus.BossEditorSection.Summary(options, dcm);
        Assert(summary.Contains("<noparse></\u200Bnoparse><size=500>Official</size></noparse>"), $"Summary: {summary}");
        AssertEquals(Regex.Matches(summary, "</noparse>").Count, 3, $"Only the summary's own noparse tags: {summary}");

        // Codes that unpack to too much or nest too deeply are refused, not crashed on
        var bomb = SetupCode.Pack("{\"o\":{},\"c\":{\"name\":\"" + new string('A', SetupCode.MaxJsonLength) + "\"}}");
        Assert(bomb.Length < SetupCode.MaxLength, $"The test bomb must be a small code, was {bomb.Length}");
        Assert(!SetupCode.TryDecode(bomb, out _, out _, out var bombProblem), "A code unpacking to too much must not load");
        AssertEquals(bombProblem, CodeProblem.Invalid, "Problem of a code unpacking to too much");
        var deep = SetupCode.Pack("{\"o\":{},\"c\":{\"name\":" + new string('[', 5000) + new string(']', 5000) + "}}");
        Assert(!SetupCode.TryDecode(deep, out _, out _, out var deepProblem), "A deeply nested code must not load");
        AssertEquals(deepProblem, CodeProblem.Invalid, "Problem of a deeply nested code");
        yield break;
    }
}
