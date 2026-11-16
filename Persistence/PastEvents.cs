using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTD_Mod_Helper;
using MelonLoader.Utils;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// The events of the Past Events list (#146), and the download that keeps them up to date without a new release.
/// On the first main menu, a background task reads the copy built into the mod and the cached one, then asks the
/// website for a newer file (with the cached file's ETag, so an unchanged file isn't downloaded again). Nothing waits
/// for it: the menus show what is there, and <see cref="Update"/> tells them on the main thread when it changes.
/// Offline, after an error or with the setting off, the cached or built-in copy is used.
/// </summary>
public static class PastEvents
{
    /// <summary>How long the download may take; the website usually answers in well under a second</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static PastEventsStore Store { get; set; } = new(
        Path.Combine(MelonEnvironment.UserDataDirectory, "CustomBossChallenge", "boss-events.json"),
        () => typeof(PastEvents).Assembly.GetManifestResourceStream(PastEventsFormat.ResourceName),
        Warn);

    /// <summary>
    /// Whether the download is allowed: off with the setting, and with Mod Helper's --modhelper.offline, which the
    /// headless tests use. Tests can set it.
    /// </summary>
    public static Func<bool> DownloadAllowed { get; set; } = () =>
        Settings.DownloadPastEvents &&
        !Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--modhelper.offline", StringComparison.OrdinalIgnoreCase));

    /// <summary>What the background task is doing, for the Past Events row</summary>
    public static bool Loading => task is {IsCompleted: false} && !loaded;

    /// <summary>Whether the background task is still asking the website</summary>
    public static bool Checking => task is {IsCompleted: false} && loaded;

    /// <summary>Whether the website was asked and answered on this start: new events, or none (304)</summary>
    public static bool Checked { get; private set; }

    /// <summary>Called on the main thread whenever the events or the state of the download change</summary>
    public static event Action? Changed;

    private static Task? task;
    private static volatile bool loaded;
    private static volatile bool answered;
    private static int shownVersion = -1;
    private static bool shownChecking;
    private static readonly ConcurrentQueue<string> Warnings = new();

    private static readonly Lazy<HttpClient> Client = new(() =>
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        }) {Timeout = Timeout};
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{ModHelperData.Name}/{ModHelperData.Version}");
        return client;
    });

    /// <summary>
    /// Starts reading the events and, when allowed, the download, once per start of the game. Never blocks.
    /// </summary>
    public static void Start()
    {
        if (task != null) return;
        var download = DownloadAllowed();
        var store = Store;
        task = Task.Run(() => Run(store, download));
    }

    /// <summary>
    /// Reads the events right away on this thread if they haven't been yet, for the tests and for a menu opened before
    /// the background task got to it
    /// </summary>
    public static PastEventsFile? Current
    {
        get
        {
            if (task == null && Store.Current == null) Store.Load();
            return Store.Current;
        }
    }

    /// <summary>
    /// Makes the events be read again from <see cref="Store"/>, without downloading: for tests that swap the store
    /// </summary>
    public static void Reload()
    {
        Store.Load();
        Update();
    }

    private static async Task Run(PastEventsStore store, bool download)
    {
        try
        {
            store.Load();
            loaded = true;
            if (!download) return;

            using var request = new HttpRequestMessage(HttpMethod.Get, PastEventsFormat.Url);
            if (store.ETag() is { } etag && EntityTagHeaderValueParse(etag) is { } tag) request.Headers.IfNoneMatch.Add(tag);
            using var cancel = new CancellationTokenSource(Timeout);
            using var response = await Client.Value.SendAsync(request, cancel.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                answered = true;
                return;
            }
            if (!response.IsSuccessStatusCode)
            {
                Warn($"Couldn't download the newest boss events from {PastEventsFormat.Url}: {(int) response.StatusCode} {response.ReasonPhrase}");
                return;
            }

            var text = await response.Content.ReadAsStringAsync(cancel.Token).ConfigureAwait(false);
            if (store.Accept(text, response.Headers.ETag?.ToString())) answered = true;
        }
        catch (Exception e)
        {
            // Offline, a timeout, a proxy: the list keeps the copy it has
            Warn($"Couldn't download the newest boss events from {PastEventsFormat.Url}: {e.GetBaseException().Message}");
        }
        finally
        {
            loaded = true;
        }
    }

    private static System.Net.Http.Headers.EntityTagHeaderValue? EntityTagHeaderValueParse(string etag) =>
        System.Net.Http.Headers.EntityTagHeaderValue.TryParse(etag, out var tag) ? tag : null;

    /// <summary>
    /// Runs every frame on the main thread: logs what the background task found, and tells the menus when the events
    /// or the download's state changed
    /// </summary>
    public static void Update()
    {
        while (Warnings.TryDequeue(out var warning)) ModHelper.Warning<CustomBossChallenge>(warning);

        var checking = Checking || Loading;
        Checked = answered;
        if (Store.Version == shownVersion && checking == shownChecking) return;
        shownVersion = Store.Version;
        shownChecking = checking;
        Changed?.Invoke();
    }

    private static void Warn(string message) => Warnings.Enqueue(message);
}
