using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CustomBossChallenge.Config;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The website (web/) reads share codes without the game, so it needs what only the game knows: the default
/// challenge codes are made against, the bosses' health, speed and skulls, and the names of maps and towers. It also
/// gets the look of tiers above 5 (<see cref="TierLook"/>), so the website's effect matches the game's, and example codes and fixtures: codes with what the mod reads from them, which the website's tests must match.
/// <br/>
/// Started with the environment variable <c>CBC_WEB_DATA=&lt;repo&gt;/web/src/data</c>, this writes those files.
/// Otherwise it checks that the files in the repo still match the game and the mod, so a game update or a change to
/// the mod's code math is noticed.
/// </summary>
public class WebDataTest : CustomBossTest
{
    public const string EnvironmentVariable = "CBC_WEB_DATA";

    public static readonly string[] Files = {"game.json", "tierlook.json", "examples.json", "fixtures.json"};

    protected override IEnumerator Run()
    {
        var data = new Dictionary<string, JToken>
        {
            ["game.json"] = GameInfo(),
            ["tierlook.json"] = TierLookInfo(),
            ["examples.json"] = Examples(),
            ["fixtures.json"] = Fixtures()
        };

        foreach (var (file, json) in data)
        {
            Assert(!json.ToString().Contains(PlayerDefaultName), $"{file} contains the player's name");
        }

        var folder = System.Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
            foreach (var (file, json) in data)
            {
                File.WriteAllText(Path.Combine(folder, file), json.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
                Log($"wrote {Path.Combine(folder, file)}");
            }

            // The mod still has the old files built in; the next run after rebuilding checks the new ones
            yield break;
        }

        foreach (var (file, json) in data)
        {
            var stream = typeof(WebDataTest).Assembly.GetManifestResourceStream("CustomBossChallenge.web." + file);
            var committed = stream == null ? null : JToken.Parse(new StreamReader(stream).ReadToEnd());
            // Through text like the file, so the game's floats (0.8333f) compare equal to the numbers read back (0.8333)
            var current = JToken.Parse(json.ToString(Formatting.None));
            Assert(committed != null && JToken.DeepEquals(committed, current),
                $"web/src/data/{file} is out of date: run the tests with {EnvironmentVariable}=<repo>/web/src/data " +
                "and rebuild (see CONTRIBUTING.md)");
        }

        yield break;
    }

    /// <summary>
    /// What the website needs to know about the game
    /// </summary>
    private static JObject GameInfo()
    {
        var game = Game.instance.model;
        var bosses = new JObject();
        foreach (var boss in Enum.GetValues<BossType>())
        {
            var versions = new JObject();
            foreach (var elite in new[] {false, true})
            {
                var options = new BossOptions {Boss = boss, Elite = elite};
                versions[elite ? "elite" : "normal"] = new JArray(Enumerable.Range(1, TierMath.VanillaTiers).Select(tier =>
                {
                    var model = game.GetBloon(options.BossId(tier));
                    return new JObject
                    {
                        ["health"] = model.maxHealth,
                        ["speed"] = model.speed,
                        ["skulls"] = new JArray((SkullModifier.GetSkulls(model) ?? Array.Empty<float>()).Select(p => (object) p))
                    };
                }));
            }

            bosses[boss.ToString()] = versions;
        }

        var defaults = DailyChallengeModel.CreateDefaultEditorModel();
        var maps = new JObject();
        foreach (var map in Il2CppAssets.Scripts.Data.GameData.Instance.mapSet.Maps.items)
        {
            if (map != null && !string.IsNullOrEmpty(map.id)) maps[map.id] = map.id.T();
        }

        return new JObject
        {
            ["gameVersion"] = UnityEngine.Application.version,
            ["defaultChallenge"] = Neutral(SetupCode.ChallengeJson(defaults)),
            ["defaultOptions"] = SetupCode.OptionsJson(new BossOptions()),
            ["bosses"] = bosses,
            ["maps"] = maps,
            ["towers"] = new JObject(defaults.towers.ToArray().Select(tower => new JProperty(tower.tower, tower.tower.T()))),
            ["modes"] = new JObject(ChallengeLimits.Modes.Select(mode => new JProperty(mode, mode.T()))),
            ["difficulties"] = new JObject(ChallengeLimits.Difficulties.Select(d => new JProperty(d, d.T()))),
            ["unsupportedModes"] = new JArray(BossEditorSection.UnsupportedModes.Cast<object>()),
            ["limits"] = new JObject
            {
                ["maxCodeLength"] = SetupCode.MaxLength,
                ["maxJsonLength"] = SetupCode.MaxJsonLength,
                ["maxNameLength"] = ChallengeLimits.MaxNameLength,
                ["minMultiplier"] = ChallengeLimits.MinMultiplier,
                ["maxMultiplier"] = ChallengeLimits.MaxMultiplier,
                ["minBossSpeed"] = ChallengeLimits.MinBossSpeed,
                ["maxBossSpeed"] = ChallengeLimits.MaxBossSpeed,
                ["minBossHealth"] = ChallengeLimits.MinBossHealth,
                ["maxBossHealth"] = ChallengeLimits.MaxBossHealth,
                ["maxCash"] = ChallengeLimits.MaxCash,
                ["maxLives"] = ChallengeLimits.MaxLives,
                ["maxRevives"] = ChallengeLimits.MaxRevives,
                ["maxTowerCount"] = ChallengeLimits.MaxTowerCount
            }
        };
    }

    /// <summary>
    /// The colours of tiers 6 to 10 for every boss in the game, and how strong and fast their glow pulses, from
    /// <see cref="TierLook"/>
    /// </summary>
    private static JObject TierLookInfo()
    {
        static JArray Theme(string boss) => new(TierLook.Theme(boss).Select(color => (object) color.Hex));
        static double Round(float value) => Math.Round(value, 6);

        return new JObject
        {
            ["themes"] = new JObject(Enum.GetValues<BossType>().Select(boss => new JProperty(boss.ToString(), Theme(boss.ToString())))),
            // A name no boss has, for bosses the game adds later
            ["defaultTheme"] = Theme(""),
            ["tiers"] = new JArray(Enumerable.Range(TierMath.VanillaTiers + 1, TierLook.Steps).Select(tier => new JObject
            {
                ["tier"] = tier,
                ["glowAlpha"] = Round(TierLook.GlowAlpha(tier)),
                ["auraSize"] = Round(TierLook.AuraSize(tier)),
                ["auraAlpha"] = Round(TierLook.AuraAlpha(tier)),
                ["scale"] = Round(TierLook.Scale(tier)),
                ["pulseAmplitude"] = Round(TierLook.PulseAmplitude(tier)),
                ["pulsePeriod"] = Round(TierLook.PulsePeriod(tier))
            }))
        };
    }

    /// <summary>
    /// The game names a new challenge after the player ("name's Challenge"). A code without a name of its own gets the
    /// name of whoever opens it, so the website shows none, and the player's name never ends up in the repo. Every new
    /// challenge also gets a random seed, which codes always carry, so the default's seed is left out.
    /// </summary>
    private static JObject Neutral(JObject challenge)
    {
        challenge["name"] = "";
        challenge["seed"] = 0;
        return challenge;
    }

    private static string PlayerDefaultName => DailyChallengeModel.CreateDefaultEditorModel().name;

    /// <summary>
    /// The mod's example setups (<see cref="ExampleSetups"/>), in English, as codes the website lets players copy
    /// </summary>
    private static JArray Examples() => new(ExampleSetups.All.Select(example =>
    {
        var (options, dcm) = ExampleSetups.Build(example, english: true);
        return new JObject
        {
            ["name"] = example.EnglishName, ["description"] = example.EnglishDescription,
            ["code"] = SetupCode.Encode(options, dcm)
        };
    }));

    /// <summary>
    /// Codes with what the mod reads from them, for the website's tests
    /// </summary>
    private JObject Fixtures()
    {
        var readme = new StreamReader(typeof(WebDataTest).Assembly.GetManifestResourceStream("CustomBossChallenge.README.md")!)
            .ReadToEnd();
        var codes = Regex.Matches(readme, @"CBC\d:[A-Za-z0-9_-]+").Select(match => match.Value)
            .Concat(Examples().Select(example => (string) example["code"]!))
            .Concat(new[]
            {
                // Values out of range, an unknown map and the setting older codes had
                SetupCode.Pack("{\"o\":{\"Tiers\":50,\"SkullCount\":-4,\"SpeedPerTier\":900,\"CustomSkulls\":\"5, 5, 120, x\"," +
                               "\"SkullMode\":\"Custom\"},\"c\":{\"map\":\"NoSuchMap\",\"name\":\" \\u0007 \"}}"),
                SetupCode.Pack("{\"o\":{\"Tiers\":7,\"ExtraTierHealth\":225},\"c\":{}}"),
                SetupCode.Pack("{\"o\":{\"SpawnRounds\":\"995, 996, 1500\",\"Tiers\":6},\"c\":{}}")
            })
            .Distinct()
            .ToList();

        var valid = new JArray();
        foreach (var code in codes)
        {
            Assert(SetupCode.TryDecode(code, out var options, out var dcm, out var problem, "WebDataTest"),
                $"Fixture code doesn't load: {problem}");
            valid.Add(new JObject
            {
                ["code"] = code,
                ["options"] = SetupCode.OptionsJson(options),
                ["challenge"] = Challenge(dcm!),
                ["spawnRounds"] = new JArray(options.EffectiveSpawnRounds.Cast<object>()),
                ["endRound"] = TierMath.EndRound(options.EffectiveSpawnRounds),
                ["tiers"] = new JArray(TierStrip.Values(options, dcm, Game.instance.model).Select(card => new JObject
                {
                    ["round"] = card.Round,
                    ["speed"] = Math.Round(card.Speed, 6),
                    ["healthFactor"] = Math.Round(card.HealthFactor, 6),
                    ["health"] = card.Shown,
                    ["capped"] = card.Capped
                })),
                ["skulls"] = new JArray(SkullStrip.Skulls(options).Percents.Select(SkullStrip.Format).Cast<object>()),
                ["playProblem"] = BossEditorSection.PlayProblem(dcm!, options) is null ? null : "yes"
            });
        }

        var invalid = new JArray();
        foreach (var (code, why) in new (string, string)[]
                 {
                     ("", "empty"), ("hello", "not a code"), ("CBC9:abc", "too new"), ("CBC1:", "no data"),
                     ("CBC1:!!!!", "not base64"), ("CBC1:aGVsbG8", "not deflate"), (codes[0][..(codes[0].Length / 2)], "cut short"),
                     ("CBC1:" + new string('A', SetupCode.MaxLength), "too long"),
                     (SetupCode.Pack("[1, 2]"), "not an object"), (SetupCode.Pack("{\"o\": 1}"), "options aren't an object")
                 })
        {
            var ok = SetupCode.TryDecode(code, out _, out _, out var problem, "WebDataTest");
            invalid.Add(new JObject {["code"] = code, ["why"] = why, ["loads"] = ok, ["problem"] = problem.ToString()});
        }

        return new JObject {["valid"] = valid, ["invalid"] = invalid};
    }

    /// <summary>
    /// The challenge values the website shows, named like the challenge's own JSON
    /// </summary>
    private static JObject Challenge(DailyChallengeModel dcm)
    {
        var defaults = DailyChallengeModel.CreateDefaultEditorModel().towers.ToArray()
            .ToDictionary(tower => tower.tower, tower => tower.max);
        var modifiers = dcm.bloonModifiers;
        var health = modifiers.healthMultipliers;
        return new JObject
        {
            ["name"] = dcm.name == PlayerDefaultName ? "" : dcm.name,
            ["map"] = dcm.map,
            ["difficulty"] = dcm.difficulty,
            ["mode"] = dcm.mode,
            ["cash"] = dcm.startRules.cash,
            ["lives"] = dcm.startRules.lives,
            ["maxLives"] = dcm.startRules.maxLives,
            ["round"] = dcm.startRules.round,
            ["bossSpeed"] = Math.Round(modifiers.bossSpeedMultiplier, 6),
            ["bossHealth"] = Math.Round(health.boss, 6),
            ["bloonSpeed"] = Math.Round(modifiers.speedMultiplier, 6),
            ["moabSpeed"] = Math.Round(modifiers.moabSpeedMultiplier, 6),
            ["bloonHealth"] = Math.Round(health.bloons, 6),
            ["moabHealth"] = Math.Round(health.moabs, 6),
            ["allCamo"] = modifiers.allCamo,
            ["allRegen"] = modifiers.allRegen,
            ["allFortified"] = modifiers.allFortified,
            ["maxParagons"] = dcm.maxParagons,
            ["maxTowers"] = dcm.maxTowers,
            ["disableMK"] = dcm.disableMK,
            ["disableSelling"] = dcm.disableSelling,
            ["disablePowers"] = dcm.disablePowers,
            ["towers"] = new JObject(dcm.towers.ToArray()
                .Where(tower => !defaults.TryGetValue(tower.tower, out var max) || max != tower.max)
                .Select(tower => new JProperty(tower.tower, tower.max)))
        };
    }
}
