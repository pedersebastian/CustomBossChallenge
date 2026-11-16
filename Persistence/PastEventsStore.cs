using System;
using System.IO;

namespace CustomBossChallenge.Persistence;

/// <summary>
/// Where the events the Past Events list shows come from
/// </summary>
public enum PastEventsSource
{
    /// <summary>Not loaded yet, or nothing usable at all</summary>
    None,

    /// <summary>The copy built into the mod</summary>
    BuiltIn,

    /// <summary>The copy downloaded on an earlier start</summary>
    Cache,

    /// <summary>Downloaded from the website on this start</summary>
    Download
}

/// <summary>
/// The events of the Past Events list (#146): the copy built into the mod, the one cached on disk and the one
/// downloaded from the website, whichever knows the newest events (<see cref="PastEventsFormat.Newest"/>). A broken
/// cache, or one from a newer version of the mod, gives the built-in copy; a cache from a newer version is never
/// overwritten. Free of the game's types, so the unit tests check the fallbacks; it is used from the download's
/// thread, so everything that changes it is locked, and <see cref="Current"/> is only ever replaced as a whole.
/// </summary>
public sealed class PastEventsStore
{
    public string CachePath { get; }

    /// <summary>The ETag of the cached file, so a start that finds nothing new downloads nothing (304)</summary>
    public string ETagPath => Path.ChangeExtension(CachePath, ".etag");

    private readonly Func<Stream?> builtIn;
    private readonly Action<string> warn;
    private readonly object gate = new();

    /// <summary>Whether the cache could be read or was just written: only then does its ETag describe it</summary>
    private bool cacheReadable;

    /// <param name="builtIn">Opens the gzipped copy built into the mod, or gives null when there is none</param>
    /// <param name="warn">Says in the log what couldn't be read</param>
    public PastEventsStore(string cachePath, Func<Stream?> builtIn, Action<string> warn)
    {
        CachePath = cachePath;
        this.builtIn = builtIn;
        this.warn = warn;
    }

    /// <summary>The events shown, or null before <see cref="Load"/> or when nothing could be read</summary>
    public PastEventsFile? Current { get; private set; }

    public PastEventsSource Source { get; private set; }

    /// <summary>Counts up whenever <see cref="Current"/> changes, so the menus can see it from the main thread</summary>
    public int Version { get; private set; }

    /// <summary>Reads the built-in copy and the cache, and keeps whichever knows the newest events</summary>
    public void Load()
    {
        lock (gate)
        {
            var builtInFile = ReadBuiltIn();
            var cached = ReadCache();
            var newest = PastEventsFormat.Newest(cached, builtInFile);
            Set(newest, newest == null ? PastEventsSource.None
                : ReferenceEquals(newest, cached) ? PastEventsSource.Cache : PastEventsSource.BuiltIn);
        }
    }

    /// <summary>The ETag to send with the next download, or null to download the whole file</summary>
    public string? ETag()
    {
        lock (gate)
        {
            try
            {
                // Without the file it describes, or with one that can't be read, an ETag would make the website answer
                // "nothing new" and the broken cache would never be replaced
                return cacheReadable && File.Exists(CachePath) && File.Exists(ETagPath) && File.ReadAllText(ETagPath).Trim() is {Length: > 0} tag
                    ? tag
                    : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Takes a file downloaded from the website: caches it, with its ETag, and shows it if it knows newer events
    /// than what is shown. A file this version can't read is ignored. Returns false when it wasn't usable.
    /// </summary>
    public bool Accept(string text, string? etag)
    {
        var read = PastEventsFormat.Read(text);
        if (read.Value == null)
        {
            warn(read.Problem == FormatProblem.TooNew
                ? $"Ignoring the downloaded boss events: {read.Error}"
                : $"Ignoring the downloaded boss events, they can't be read: {read.Error}");
            return false;
        }
        foreach (var change in read.Changes) warn($"Downloaded boss events: {change}");

        lock (gate)
        {
            if (CacheIsTooNew()) warn($"Not caching the downloaded boss events: {CachePath} is from a newer version of the mod");
            else
            {
                try
                {
                    SafeFile.WriteAllText(CachePath, text);
                    cacheReadable = true;
                    if (etag != null) SafeFile.WriteAllText(ETagPath, etag);
                    else if (File.Exists(ETagPath)) File.Delete(ETagPath);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    warn($"Couldn't cache the downloaded boss events in {CachePath}: {e.Message}");
                }
            }

            var newest = PastEventsFormat.Newest(read.Value, Current);
            if (ReferenceEquals(newest, read.Value)) Set(read.Value, PastEventsSource.Download);
        }
        return true;
    }

    private void Set(PastEventsFile? file, PastEventsSource source)
    {
        Current = file;
        Source = source;
        Version++;
    }

    private PastEventsFile? ReadBuiltIn()
    {
        try
        {
            using var stream = builtIn();
            if (stream == null)
            {
                warn("The mod has no built-in copy of the boss events; only a downloaded one can be shown");
                return null;
            }
            var read = PastEventsFormat.Read(PastEventsFormat.Gunzip(stream));
            if (read.Value == null) warn($"The built-in boss events can't be read: {read.Error}");
            return read.Value;
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            warn($"The built-in boss events can't be read: {e.Message}");
            return null;
        }
    }

    private PastEventsFile? ReadCache()
    {
        try
        {
            cacheReadable = false;
            if (!File.Exists(CachePath)) return null;
            var read = PastEventsFormat.Read(File.ReadAllText(CachePath));
            cacheReadable = read.Value != null;
            if (read.Value == null)
            {
                warn(read.Problem == FormatProblem.TooNew
                    ? $"Keeping {CachePath} from a newer version of the mod, using the built-in boss events"
                    : $"Ignoring the cached boss events in {CachePath}: {read.Error}");
            }
            return read.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warn($"Ignoring the cached boss events in {CachePath}: {e.Message}");
            return null;
        }
    }

    private bool CacheIsTooNew()
    {
        try
        {
            return File.Exists(CachePath) && PastEventsFormat.Read(File.ReadAllText(CachePath)).Problem == FormatProblem.TooNew;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Can't be read to find out: don't write over it either
            return true;
        }
    }
}
