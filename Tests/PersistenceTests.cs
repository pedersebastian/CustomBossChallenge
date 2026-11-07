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
                Boss = BossType.Diamondback, Elite = true, SkullMode = SkullMode.Custom, CustomSkulls = "90, 10",
                Tiers = 7, SpeedPerTier = 10, HealthPerTier = 25, SpawnRounds = "30, 50, 70"
            };
            var dcm = CustomEditorDcm();
            dcm.map = "Logs";
            dcm.mode = "HalfCash";
            dcm.difficulty = "Hard";
            dcm.bloonModifiers.allFortified = true;
            dcm.disableSelling = true;
            dcm.maxParagons = 2;
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
            AssertEquals(loaded.maxParagons, 2, "Max paragons");

            // A hand edited file is held to the same limits as a shared code
            var extreme = dcm.Clone();
            extreme.bloonModifiers.bossSpeedMultiplier = 1e30f;
            extreme.startRules.endRound = int.MaxValue;
            store.Save(options, extreme);
            Assert(store.TryLoad(out _, out var limited), "A file with extreme values must still load");
            AssertEquals(limited!.bloonModifiers.bossSpeedMultiplier, ChallengeLimits.MaxBossSpeed, "Boss speed from a file");
            AssertEquals(limited.startRules.endRound, TierMath.MaxRound + TierMath.RoundsBetweenTiers, "End round from a file");

            // The polymorphic models are never read from a file (#108)
            var withModels = Newtonsoft.Json.Linq.JObject.Parse(DcmJson.Serialize(dcm));
            withModels["simulationBehaviorsMod"] = new Newtonsoft.Json.Linq.JObject();
            withModels["customMapModel"] = new Newtonsoft.Json.Linq.JObject();
            var modelsDcm = DcmJson.Deserialize(withModels.ToString());
            AssertEquals(modelsDcm?.simulationBehaviorsMod, null, "Simulation behaviours from a file");
            AssertEquals(modelsDcm?.customMapModel, null, "Custom map from a file");

            // Setups saved before Max Paragons existed have no limit
            var oldChallenge = System.Text.RegularExpressions.Regex.Replace(DcmJson.Serialize(dcm), "\"maxParagons\":-?[0-9]+,?", "");
            Assert(!oldChallenge.Contains("maxParagons"), "The old challenge still has max paragons");
            AssertEquals(AssertNotNull(DcmJson.Deserialize(oldChallenge), "Old challenge")!.maxParagons, ParagonMath.NoLimit,
                "Max paragons of an old setup");

            // A hand edited file: values out of range are brought back into range, unknown fields ignored
            var challenge = Newtonsoft.Json.JsonConvert.SerializeObject(DcmJson.Serialize(dcm));
            File.WriteAllText(store.FilePath,
                $"{{\"Version\": 2, \"Future\": true, \"Challenge\": {challenge}, \"Options\": {{\"Boss\": 99, " +
                "\"SkullMode\": 42, \"SkullCount\": 500, \"CustomSkulls\": null, \"Tiers\": 50, \"SpeedPerTier\": 999, \"ExtraTierHealth\": -3}}");
            Assert(store.TryLoad(out var odd, out var oddDcm), "A file with odd values must still load");
            AssertNotNull(oddDcm, "Challenge of a file with odd values");
            AssertEquals(odd, new BossOptions
            {
                SkullCount = SkullMath.MaxSkulls, Tiers = TierMath.MaxTiers, HealthPerTier = TierMath.MinHealthPerTier,
                SpeedPerTier = TierMath.MaxSpeedPerTier
            }, "Odd values brought into range");

            // Setups from before 2.3 stored Extra Tier Health for tiers above 5; it becomes Health per Tier
            foreach (var (old, perTier) in new[] {(150, 0), (225, 10), (175, 5), (100, -5)})
            {
                File.WriteAllText(store.FilePath,
                    $"{{\"Version\": 2, \"Challenge\": {challenge}, \"Options\": {{\"Tiers\": 7, \"ExtraTierHealth\": {old}}}}}");
                Assert(store.TryLoad(out var migrated, out _), $"A setup with Extra Tier Health {old} must load");
                AssertEquals(migrated, new BossOptions {Tiers = 7, HealthPerTier = perTier}, $"Extra Tier Health {old}");
            }

            // A file from a newer version of the mod is neither loaded nor overwritten (#63)
            var future = $"{{\"Version\": 99, \"Challenge\": {challenge}, \"Options\": {{\"Boss\": \"Lych\"}}}}";
            File.WriteAllText(store.FilePath, future);
            Assert(store.IsTooNew, "A version 99 file is too new");
            Assert(!store.TryLoad(out var newer, out var newerDcm), "A file from a newer version must not load");
            AssertEquals(newer, new BossOptions(), "Defaults for a file from a newer version");
            AssertEquals(newerDcm, null, "No challenge for a file from a newer version");
            store.Save(options, dcm);
            AssertEquals(File.ReadAllText(store.FilePath), future, "A file from a newer version must not be overwritten");

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

/// <summary>
/// A last setup or saved setups from a newer version of the mod are kept but not used, and the Boss Creator says so
/// once per session (#110)
/// </summary>
public class NewerFilesNoticeTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        // The test base gives every test empty stores of its own
        Creator.BossMode.ToldAboutNewerFiles = false;
        Assert(!Creator.BossMode.TellAboutNewerFiles(), "Nothing to tell without files from a newer version");

        Directory.CreateDirectory(Path.GetDirectoryName(Creator.BossMode.Saved.FilePath)!);
        const string newer = "{\"Version\": 99, \"Setups\": []}";
        File.WriteAllText(Creator.BossMode.Saved.FilePath, newer);
        Assert(Creator.BossMode.TellAboutNewerFiles(), "Saved setups from a newer version are told about");
        Il2CppAssets.Scripts.Unity.UI_New.Popups.Popup? popup = null;
        yield return WaitForPopup(found => popup = found);
        Assert(popup!.body.text.Contains(Menus.Texts.T(Menus.Texts.NewerSetupFiles)), $"Popup: {popup.body.text}");
        yield return ClosePopup(popup, true);
        Assert(!Creator.BossMode.TellAboutNewerFiles(), "Told only once per session");
        AssertEquals(File.ReadAllText(Creator.BossMode.Saved.FilePath), newer, "The newer file is kept as it was");

        // A last setup from a newer version is told about too
        File.Delete(Creator.BossMode.Saved.FilePath);
        File.WriteAllText(Creator.BossMode.Store.FilePath, "{\"Version\": 99}");
        Creator.BossMode.ToldAboutNewerFiles = false;
        Assert(Creator.BossMode.TellAboutNewerFiles(), "A last setup from a newer version is told about");
        yield return WaitForPopup(found => popup = found);
        yield return ClosePopup(popup!, true);
    }
}
