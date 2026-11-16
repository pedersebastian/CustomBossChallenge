using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// What asking the website for newer boss events gave
/// </summary>
public enum PastEventsDownloadResult
{
    /// <summary>A file this version reads, now cached</summary>
    Downloaded,

    /// <summary>The cached file is the newest (304)</summary>
    NotModified,

    /// <summary>The website answered with a file this version can't use; the store said why</summary>
    Rejected,

    /// <summary>No answer: offline, a timeout, an error status</summary>
    Failed
}

/// <summary>
/// The download of the Past Events list (#146), without the game, so the unit tests run it against a fake website:
/// one GET of boss-events.json, with the cached file's ETag, that gives up after <c>timeout</c>
/// </summary>
public static class PastEventsDownload
{
    /// <summary>
    /// Whether the mod may download: the setting is on, and Mod Helper doesn't run offline (--modhelper.offline, which
    /// the headless tests use)
    /// </summary>
    public static bool Allowed(bool setting, IEnumerable<string> commandLine) =>
        setting && !commandLine.Any(arg => arg.StartsWith("--modhelper.offline", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Asks <paramref name="url"/> for a newer file and gives it to <paramref name="store"/>. Never throws; a failure
    /// comes with what went wrong, for the log.
    /// </summary>
    public static async Task<(PastEventsDownloadResult Result, string Problem)> Run(HttpMessageInvoker client,
        PastEventsStore store, string url, TimeSpan timeout)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (store.ETag() is { } etag && EntityTagHeaderValue.TryParse(etag, out var tag)) request.Headers.IfNoneMatch.Add(tag);
            using var cancel = new CancellationTokenSource(timeout);
            using var response = await client.SendAsync(request, cancel.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified) return (PastEventsDownloadResult.NotModified, "");
            if (!response.IsSuccessStatusCode)
            {
                return (PastEventsDownloadResult.Failed, $"{(int) response.StatusCode} {response.ReasonPhrase}");
            }

            var text = await response.Content.ReadAsStringAsync(cancel.Token).ConfigureAwait(false);
            return store.Accept(text, response.Headers.ETag?.ToString())
                ? (PastEventsDownloadResult.Downloaded, "")
                : (PastEventsDownloadResult.Rejected, "");
        }
        catch (OperationCanceledException)
        {
            return (PastEventsDownloadResult.Failed, $"no answer within {timeout.TotalSeconds:0} s");
        }
        catch (Exception e)
        {
            // Offline, a proxy, a broken connection: the list keeps the copy it has
            return (PastEventsDownloadResult.Failed, e.GetBaseException().Message);
        }
    }
}
