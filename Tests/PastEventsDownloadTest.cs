using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CustomBossChallenge.Creator;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The Past Events download in the game (#146), against a fake website: the headless run counts as offline and
/// downloads nothing, a download that isn't allowed never asks the website, and a newer file reaches the Boss
/// Creator's row on the main thread
/// </summary>
public class PastEventsDownloadTest : CustomBossTest
{
    private sealed class FakeWebsite(Func<string> body) : HttpMessageHandler
    {
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            var response = new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent(body(), Encoding.UTF8)};
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"test\"");
            return Task.FromResult(response);
        }
    }

    protected override IEnumerator Run()
    {
        var store = PastEvents.Store;
        var client = PastEvents.Client;
        var allowed = PastEvents.DownloadAllowed;
        try
        {
            // The headless tests run with --modhelper.offline, which must keep the mod from going online
            Assert(!PastEvents.DefaultDownloadAllowed(), "The mod downloads although Mod Helper runs offline");

            var builtIn = PastEventsFormat.Gunzip(
                typeof(PastEvents).Assembly.GetManifestResourceStream(PastEventsFormat.ResourceName)!);
            var website = new FakeWebsite(() => WithNewEvent(builtIn));
            PastEvents.Client = () => new HttpClient(website);

            // Not allowed: the built-in copy, and the website is never asked
            yield return Download(false);
            AssertEquals(website.Requests, 0, "Requests while the download is off");
            AssertEquals(PastEvents.LastResult, null, "Download result while it is off");
            AssertEquals(PastEvents.Store.Source, PastEventsSource.BuiltIn, "Where the events come from while it is off");
            var builtInCount = PastEvents.Current!.Events.Count;

            // Allowed: the newer file is shown, and the menus hear of it on the main thread
            var mainThread = Thread.CurrentThread.ManagedThreadId;
            var changedOnMain = 0;
            var changedElsewhere = 0;
            void Changed()
            {
                if (Thread.CurrentThread.ManagedThreadId == mainThread) changedOnMain++;
                else changedElsewhere++;
            }
            PastEvents.Changed += Changed;
            try
            {
                yield return Download(true);
                yield return WaitFor(() => changedOnMain > 0 && !PastEvents.Checking, "The download reaching the menus");
            }
            finally
            {
                PastEvents.Changed -= Changed;
            }
            AssertEquals(website.Requests, 1, "Requests while the download is on");
            AssertEquals(PastEvents.LastResult, PastEventsDownloadResult.Downloaded, "Download result");
            AssertEquals(PastEvents.Store.Source, PastEventsSource.Download, "Where the events come from");
            AssertEquals(PastEvents.Current!.Events.Count, builtInCount + 1, "Events after the download");
            AssertEquals(changedElsewhere, 0, "The menus were told off the main thread");
            Assert(File.Exists(PastEvents.Store.CachePath), "The download is cached");

            // The Boss Creator's row shows the new count and that the list is up to date
            BossMode.Enter();
            yield return WaitFor(() => Object.FindObjectOfType<ChallengeEditor>() != null &&
                                       MenuManager.instance.GetCurrentMenu()?.GetIl2CppType().Name == nameof(ChallengeEditor),
                "Opening the Challenge Editor");
            yield return new WaitForSecondsRealtime(1);
            var section = BossEditorSection.Current!;
            var label = section.BrowseEvents.GetComponentInChildren<Il2CppTMPro.TMP_Text>().text;
            Assert(label.Contains((builtInCount + 1).ToString()), $"The row's count after the download: {label}");
            var upToDate = Texts.PastEventsUpToDate.T("").Trim().TrimEnd(',', ' ');
            Assert(section.PastEventsStatus.Text.text.Contains(upToDate),
                $"The row says the list is up to date: {section.PastEventsStatus.Text.text}");
            MenuManager.instance.Back();
            yield return ToMainMenu();
        }
        finally
        {
            PastEvents.Store = store;
            PastEvents.Client = client;
            PastEvents.DownloadAllowed = allowed;
            PastEvents.Restart();
            PastEvents.Reload();
        }
    }

    /// <summary>Starts this start's download again with a fresh cache, and waits for it</summary>
    private IEnumerator Download(bool allowed)
    {
        PastEvents.Restart();
        PastEvents.Store = new PastEventsStore(Path.Combine(TempFolder, allowed ? "on" : "off", "boss-events.json"),
            () => typeof(PastEvents).Assembly.GetManifestResourceStream(PastEventsFormat.ResourceName),
            message => Log(message));
        PastEvents.DownloadAllowed = () => allowed;
        PastEvents.Start();
        yield return WaitFor(() => PastEvents.Running is {IsCompleted: true}, "The download", 30);
        PastEvents.Update();
    }

    /// <summary>The built-in file with one more event, a week after its newest, built later</summary>
    private static string WithNewEvent(string builtIn)
    {
        var bundle = JObject.Parse(builtIn);
        var events = (JArray) bundle["events"]!;
        var newest = (JObject) events.OrderBy(e => (long) e["start"]!).Last().DeepClone();
        var week = (long) TimeSpan.FromDays(7).TotalMilliseconds;
        newest["id"] = "DownloadTest";
        newest["name"] = newest["displayName"] = "Download Test";
        newest["start"] = (long) newest["start"]! + week;
        newest["end"] = (long) newest["end"]! + week;
        events.Add(newest);
        bundle["generated"] = DateTime.UtcNow.ToString("O");
        return bundle.ToString(Formatting.None);
    }
}
