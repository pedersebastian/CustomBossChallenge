using System;
using System.Collections;
using System.IO;
using System.Linq;
using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Data.Boss;
using static CustomBossChallenge.Tests.TestUtils;

namespace CustomBossChallenge.Tests;

/// <summary>
/// Saved setups keep their options and rules by name, replace a setup with the same name, stay sorted and limited,
/// and damaged files never lose the player's setups or crash the mod
/// </summary>
public class SavedSetupsStoreTest : CustomBossTest
{
    protected override IEnumerator Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CustomBossChallengeTest-" + Guid.NewGuid().ToString("N"));
        var store = new SavedSetupsStore(Path.Combine(directory, "Nested", "SavedSetups.json"));
        try
        {
            AssertEquals(store.All.Count, 0, "No setups without a file");
            Assert(!store.TryLoad("Nothing", out _, out _, out var missing), "A missing setup must not load");
            AssertEquals(missing, CodeProblem.Invalid, "Problem of a missing setup");

            // Saved under the challenge's name, with every option and rule
            var lych = new BossOptions {Boss = BossType.Lych, Elite = true, Tiers = 7, SpeedPerTier = 20, SkullMode = SkullMode.EvenlySpaced};
            var lychDcm = Setup("my lych run", "Logs");
            AssertEquals(store.Save(lych, lychDcm), SavedSetupsStore.SaveResult.Saved, "First save");
            Assert(File.Exists(store.FilePath), "The file was not written");
            Assert(!File.Exists(store.FilePath + ".tmp"), "The temporary file was left behind");
            Assert(store.TryLoad("my lych run", out var loaded, out var loadedDcm, out _), "The saved setup did not load");
            AssertEquals(loaded, lych, "Loaded options");
            AssertEquals(loadedDcm!.map, "Logs", "Loaded map");
            AssertEquals(loadedDcm.maxParagons, 2, "Loaded max paragons");
            Assert(Approximately(loadedDcm.bloonModifiers.bossSpeedMultiplier, 2), "Loaded boss speed");

            // Sorted by name, whatever the case
            store.Save(new BossOptions {Boss = BossType.Vortex}, Setup("Alpha", "Tutorial"));
            store.Save(new BossOptions(), Setup("zebra", "Tutorial"));
            AssertEquals(string.Join(", ", store.All.Select(entry => entry.Name)), "Alpha, my lych run, zebra", "Sorted names");

            // The same name in another case replaces the setup and takes the new spelling
            var other = new BossOptions {Boss = BossType.Phayze, Tiers = 3};
            AssertEquals(store.Save(other, Setup("My Lych Run", "Cubism")), SavedSetupsStore.SaveResult.Replaced, "Saving again");
            AssertEquals(store.All.Count, 3, "Replacing must not add a setup");
            Assert(store.Contains("MY LYCH RUN"), "Names are found whatever the case");
            Assert(store.TryLoad("my lych run", out var replaced, out _, out _) && replaced == other, "The replaced setup");
            AssertEquals(store.All[1].Name, "My Lych Run", "The new spelling of the name");

            // A new instance reads the same file, like after restarting the game
            AssertEquals(new SavedSetupsStore(store.FilePath).All.Count, 3, "Setups after a restart");

            Assert(store.Delete("alpha"), "Deleting a setup");
            Assert(!store.Delete("alpha"), "Deleting it twice");
            AssertEquals(string.Join(", ", store.All.Select(entry => entry.Name)), "My Lych Run, zebra", "After deleting");

            // Names are cleaned like the names in shared codes, and rich text is kept as plain text
            store.Save(new BossOptions(), Setup("Line\nbreak <b>bold</b> " + new string('x', 100), "Tutorial"));
            var cleaned = store.All.First(entry => entry.Name.StartsWith("Line"));
            Assert(cleaned.Name.Length <= ChallengeLimits.MaxNameLength && !cleaned.Name.Contains('\n'), $"Cleaned name: {cleaned.Name}");

            // No more than the limit
            for (var i = store.All.Count; i < SavedSetupsStore.MaxSetups; i++) store.Save(new BossOptions(), Setup($"Setup {i:00}", "Tutorial"));
            AssertEquals(store.All.Count, SavedSetupsStore.MaxSetups, "Full");
            AssertEquals(store.Save(new BossOptions(), Setup("One too many", "Tutorial")), SavedSetupsStore.SaveResult.Full, "Saving when full");
            Assert(!store.Contains("One too many"), "A setup over the limit must not be saved");
            AssertEquals(store.Save(other, Setup("zebra", "Tutorial")), SavedSetupsStore.SaveResult.Replaced, "Replacing when full");

            // Damaged entries are skipped, the rest still loads; values out of range come back like in a share code
            File.WriteAllText(store.FilePath,
                "{\"Version\":1,\"Setups\":[{\"Name\":\"Good\",\"Code\":\"" + SetupCode.Encode(lych, lychDcm) + "\"}," +
                "{\"Name\":\"\",\"Code\":\"CBC1:x\"},{\"Name\":\"good\",\"Code\":\"CBC1:x\"},{\"Name\":\"  Good \\n\",\"Code\":\"CBC1:x\"}," +
                "{\"Name\":\"Broken\",\"Code\":\"not a code\"}," +
                "{\"Name\":\"Future\",\"Code\":\"CBC7:abc\"},{\"Name\":\"Wild\",\"Code\":\"" +
                SetupCode.Pack("{\"o\":{\"Tiers\":9999},\"c\":{}}") + "\"},null]}");
            AssertEquals(string.Join(", ", store.All.Select(entry => entry.Name)), "Broken, Future, Good, Wild", "Readable entries");
            Assert(store.TryLoad("Good", out var good, out _, out _) && good == lych, "A good entry next to damaged ones");
            Assert(!store.TryLoad("Broken", out _, out _, out var broken), "A broken code must not load");
            AssertEquals(broken, CodeProblem.Invalid, "Problem of a broken code");
            Assert(!store.TryLoad("Future", out _, out _, out var future), "A code from a newer version must not load");
            AssertEquals(future, CodeProblem.TooNew, "Problem of a newer code");
            Assert(store.TryLoad("Wild", out var wild, out _, out _), "A code with values out of range loads");
            AssertEquals(wild.Tiers, TierMath.MaxTiers, "Tiers brought into range");

            // A hand edited file with more than the limit keeps them all: only new setups are refused
            var code = SetupCode.Encode(lych, lychDcm);
            var many = Enumerable.Range(0, SavedSetupsStore.MaxSetups + 5)
                .Select(i => "{\"Name\":\"Many " + i.ToString("00") + "\",\"Code\":\"" + code + "\"}");
            File.WriteAllText(store.FilePath, "{\"Version\":1,\"Setups\":[" + string.Join(",", many) + "]}");
            AssertEquals(store.All.Count, SavedSetupsStore.MaxSetups + 5, "Setups over the limit are shown");
            AssertEquals(store.Save(lych, Setup("Yet another", "Tutorial")), SavedSetupsStore.SaveResult.Full, "Saving over the limit");
            Assert(store.Delete("Many 00"), "Deleting over the limit");
            AssertEquals(store.All.Count, SavedSetupsStore.MaxSetups + 4, "Writing the file must not drop setups over the limit");

            // An unreadable file is kept aside, so saving a new setup can't throw the old ones away for good, and a
            // later, different breakage doesn't replace that copy
            var folder = Path.GetDirectoryName(store.FilePath)!;
            string[] Backups() => Directory.GetFiles(folder, "*.broken");
            const string unreadable = "{ this is not json";
            File.WriteAllText(store.FilePath, unreadable);
            File.SetLastWriteTimeUtc(store.FilePath, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            AssertEquals(store.All.Count, 0, "An unreadable file gives no setups");
            AssertEquals(store.All.Count, 0, "Reading it again");
            AssertEquals(Backups().Length, 1, "One copy of the unreadable file");
            AssertEquals(File.ReadAllText(Backups()[0]), unreadable, "The unreadable file is kept");
            AssertEquals(store.Save(lych, lychDcm), SavedSetupsStore.SaveResult.Saved, "Saving over an unreadable file");
            AssertEquals(store.All.Count, 1, "A new file after an unreadable one");

            File.WriteAllText(store.FilePath, "[ broken again");
            File.SetLastWriteTimeUtc(store.FilePath, new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc));
            AssertEquals(store.All.Count, 0, "A second unreadable file");
            AssertEquals(Backups().Length, 2, "The first copy must survive a second breakage");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        yield break;
    }

    private static Il2CppAssets.Scripts.Models.ServerEvents.DailyChallengeModel Setup(string name, string map)
    {
        var dcm = CustomEditorDcm();
        dcm.name = name;
        dcm.map = map;
        dcm.maxParagons = 2;
        return dcm;
    }
}
