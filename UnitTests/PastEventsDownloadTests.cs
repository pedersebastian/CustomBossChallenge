using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CustomBossChallenge.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// The Past Events download (#146) against a fake website: a new file is cached with its ETag, the next start sends
/// the ETag and a 304 changes nothing, and errors, a timeout or a file this version can't read leave the list as it
/// was. Whether it may download at all follows the setting and Mod Helper's --modhelper.offline.
/// </summary>
public class PastEventsDownloadTests : IDisposable
{
    private const string Url = PastEventsFormat.Url;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly string folder = Path.Combine(Path.GetTempPath(), "CbcPastEventsDownload-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
    }

    /// <summary>A website that answers every request with <paramref name="answer"/>, and remembers the requests</summary>
    private sealed class FakeWebsite(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return answer(request, cancellationToken);
        }
    }

    private static FakeWebsite Answering(HttpStatusCode status, string body = "", string? etag = null) =>
        new((_, _) =>
        {
            var response = new HttpResponseMessage(status) {Content = new StringContent(body, Encoding.UTF8, "application/json")};
            if (etag != null) response.Headers.ETag = EntityTagHeaderValue.Parse(etag);
            return Task.FromResult(response);
        });

    /// <summary>A bundle with one event that started on <paramref name="start"/></summary>
    private static string Bundle(long start, string generated = "2026-11-12T06:00:00.000Z", int formatVersion = 1) =>
        new JObject
        {
            ["formatVersion"] = formatVersion, ["generated"] = generated, ["license"] = "test",
            ["events"] = new JArray(new JObject
            {
                ["formatVersion"] = 1, ["id"] = $"Lych{start}", ["name"] = "Lych1", ["displayName"] = "Lych 1",
                ["bossType"] = "lych", ["start"] = start, ["end"] = start + 1, ["lossy"] = new JArray(),
                ["standard"] = new JObject {["map"] = "Tutorial"}, ["elite"] = new JObject {["map"] = "Tutorial"}
            })
        }.ToString(Formatting.None);

    private static MemoryStream Gzip(string text)
    {
        var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.Optimal, true)) gzip.Write(Encoding.UTF8.GetBytes(text));
        stream.Position = 0;
        return stream;
    }

    /// <summary>A store whose built-in copy knows one event from 2025</summary>
    private PastEventsStore Store(List<string>? warnings = null)
    {
        var store = new PastEventsStore(Path.Combine(folder, "boss-events.json"), () => Gzip(Bundle(1_750_000_000_000)),
            message => warnings?.Add(message));
        store.Load();
        return store;
    }

    private static Task<(PastEventsDownloadResult Result, string Problem)> Download(FakeWebsite website, PastEventsStore store) =>
        PastEventsDownload.Run(new HttpClient(website), store, Url, Timeout);

    [Fact]
    public async Task A_newer_file_is_shown_and_cached_with_its_ETag()
    {
        var store = Store();
        var website = Answering(HttpStatusCode.OK, Bundle(1_780_000_000_000), "\"v2\"");
        var (result, _) = await Download(website, store);

        Assert.Equal(PastEventsDownloadResult.Downloaded, result);
        Assert.Equal(PastEventsSource.Download, store.Source);
        Assert.Equal(1_780_000_000_000, store.Current!.NewestStart);
        Assert.Equal("\"v2\"", store.ETag());
        Assert.True(File.Exists(store.CachePath));

        // The first start had no ETag to send; it asked for the website's file
        var request = Assert.Single(website.Requests);
        Assert.Equal(Url, request.RequestUri!.ToString());
        Assert.Empty(request.Headers.IfNoneMatch);
    }

    [Fact]
    public async Task The_next_start_sends_the_ETag_and_an_unchanged_file_changes_nothing()
    {
        var store = Store();
        await Download(Answering(HttpStatusCode.OK, Bundle(1_780_000_000_000), "W/\"v2\""), store);

        // A new start of the game: the cache is read, and the website says it has nothing newer
        var next = Store();
        Assert.Equal(PastEventsSource.Cache, next.Source);
        var version = next.Version;
        var website = Answering(HttpStatusCode.NotModified);
        var (result, _) = await Download(website, next);

        Assert.Equal(PastEventsDownloadResult.NotModified, result);
        Assert.Equal("W/\"v2\"", Assert.Single(Assert.Single(website.Requests).Headers.IfNoneMatch).ToString());
        Assert.Equal(version, next.Version);
        Assert.Equal(PastEventsSource.Cache, next.Source);
    }

    [Fact]
    public async Task An_older_file_from_the_website_doesnt_hide_the_built_in_events()
    {
        var store = Store();
        var (result, _) = await Download(Answering(HttpStatusCode.OK, Bundle(1_700_000_000_000)), store);
        Assert.Equal(PastEventsDownloadResult.Downloaded, result);
        Assert.Equal(PastEventsSource.BuiltIn, store.Source);
        Assert.Equal(1_750_000_000_000, store.Current!.NewestStart);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "404")]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    [InlineData(HttpStatusCode.Forbidden, "403")]
    public async Task An_error_answer_leaves_the_list_as_it_was(HttpStatusCode status, string problem)
    {
        var store = Store();
        var shown = store.Current;
        var (result, why) = await Download(Answering(status, "<html>error</html>"), store);

        Assert.Equal(PastEventsDownloadResult.Failed, result);
        Assert.Contains(problem, why);
        Assert.Same(shown, store.Current);
        Assert.False(File.Exists(store.CachePath));
    }

    [Fact]
    public async Task A_file_this_version_cant_use_leaves_the_list_as_it_was()
    {
        var warnings = new List<string>();
        var store = Store(warnings);
        var shown = store.Current;

        Assert.Equal(PastEventsDownloadResult.Rejected,
            (await Download(Answering(HttpStatusCode.OK, "<!DOCTYPE html><p>Not found</p>"), store)).Result);
        Assert.Equal(PastEventsDownloadResult.Rejected,
            (await Download(Answering(HttpStatusCode.OK, Bundle(1_780_000_000_000, formatVersion: 2)), store)).Result);
        Assert.Same(shown, store.Current);
        Assert.False(File.Exists(store.CachePath));
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public async Task Offline_the_list_keeps_its_copy()
    {
        var store = Store();
        var shown = store.Current;
        var website = new FakeWebsite((_, _) => throw new HttpRequestException("No such host is known"));
        var (result, why) = await Download(website, store);

        Assert.Equal(PastEventsDownloadResult.Failed, result);
        Assert.Contains("No such host", why);
        Assert.Same(shown, store.Current);
    }

    [Fact]
    public async Task A_website_that_doesnt_answer_is_given_up_on()
    {
        var store = Store();
        var website = new FakeWebsite(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var started = DateTime.UtcNow;
        var (result, why) = await PastEventsDownload.Run(new HttpClient(website), store, Url, TimeSpan.FromMilliseconds(200));

        Assert.Equal(PastEventsDownloadResult.Failed, result);
        Assert.Contains("no answer", why);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10), "The download waited for the website");
        Assert.Equal(PastEventsSource.BuiltIn, store.Source);
    }

    [Theory]
    [InlineData(true, new string[0], true)]
    [InlineData(false, new string[0], false)]
    [InlineData(true, new[] {"BloonsTD6.exe", "--modhelper.offline"}, false)]
    [InlineData(true, new[] {"BloonsTD6.exe", "--MODHELPER.OFFLINE"}, false)]
    [InlineData(true, new[] {"BloonsTD6.exe", "--modhelper.noaudio"}, true)]
    [InlineData(false, new[] {"--modhelper.offline"}, false)]
    public void The_download_follows_the_setting_and_offline(bool setting, string[] commandLine, bool allowed) =>
        Assert.Equal(allowed, PastEventsDownload.Allowed(setting, commandLine));
}
