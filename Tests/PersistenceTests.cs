using System;
using System.Collections;
using System.IO;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The last Boss Creator setup survives a save and load, and broken or missing files fall back to defaults
/// </summary>
public class PersistenceTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CustomBossChallengeTest-" + Guid.NewGuid().ToString("N"));
        var store = new LastSetupStore(Path.Combine(directory, "Nested", "LastSetup.json"));
        try
        {
            Assert(!store.TryLoad(out var defaults, out var none), "A missing file must not load");
            AssertEquals(defaults, new BossOptions(), "Defaults for a missing file");
            AssertEquals(none, null, "No challenge for a missing file");

            var options = new BossOptions
            {
                Boss = BossType.Diamondback, Elite = true, SkullMode = SkullMode.Custom, CustomSkulls = "90,10",
                Tiers = 7, ExtraTierHealth = 175, SpawnRounds = "30, 50, 70"
            };
            var dcm = CustomEditorDcm();
            dcm.map = "Logs";
            dcm.mode = "HalfCash";
            dcm.difficulty = "Hard";
            dcm.bloonModifiers.allFortified = true;
            dcm.disableSelling = true;
            store.Save(options, dcm);
            Assert(File.Exists(store.FilePath), "The setup file was not written");
            Assert(!File.Exists(store.FilePath + ".tmp"), "The temporary file was left behind");

            Assert(store.TryLoad(out var loadedOptions, out var loaded), "The saved setup did not load");
            AssertEquals(loadedOptions, options, "Loaded options");
            AssertNotNull(loaded, "Loaded challenge");
            AssertEquals(loaded!.map, "Logs", "Map");
            AssertEquals(loaded.mode, "HalfCash", "Mode");
            AssertEquals(loaded.difficulty, "Hard", "Difficulty");
            AssertEquals(loaded.name, dcm.name, "Name");
            AssertEquals(loaded.startRules.cash, 5000, "Start cash");
            Assert(Approximately(loaded.bloonModifiers.bossSpeedMultiplier, 2), "Boss speed");
            Assert(Approximately(loaded.bloonModifiers.healthMultipliers.boss, 0.5f), "Boss health");
            Assert(loaded.bloonModifiers.allFortified, "All fortified");
            Assert(loaded.disableSelling, "No selling");

            // A hand edited or future file: values out of range are brought back into range, unknown fields ignored
            var challenge = Newtonsoft.Json.JsonConvert.SerializeObject(DcmJson.Serialize(dcm));
            File.WriteAllText(store.FilePath,
                $"{{\"Version\": 99, \"Future\": true, \"Challenge\": {challenge}, \"Options\": {{\"Boss\": 99, " +
                "\"SkullMode\": 42, \"SkullCount\": 500, \"CustomSkulls\": null, \"Tiers\": 50, \"ExtraTierHealth\": -3}}");
            Assert(store.TryLoad(out var odd, out var oddDcm), "A file with odd values must still load");
            AssertNotNull(oddDcm, "Challenge of a file with odd values");
            AssertEquals(odd, new BossOptions
            {
                SkullCount = SkullMath.MaxSkulls, Tiers = TierMath.MaxTiers, ExtraTierHealth = TierMath.MinExtraTierHealth
            }, "Odd values brought into range");

            File.WriteAllText(store.FilePath, "{\"Version\": 2, \"Options\": {\"Boss\": \"Lych\"}}");
            Assert(!store.TryLoad(out var incomplete, out var incompleteDcm), "A file without a challenge must not load");
            AssertEquals(incomplete, new BossOptions(), "Defaults for an incomplete file");
            AssertEquals(incompleteDcm, null, "No challenge for an incomplete file");

            File.WriteAllText(store.FilePath, "{ this is not json");
            Assert(!store.TryLoad(out var broken, out var brokenDcm), "A broken file must not load");
            AssertEquals(broken, new BossOptions(), "Defaults for a broken file");
            AssertEquals(brokenDcm, null, "No challenge for a broken file");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        yield break;
    }
}
