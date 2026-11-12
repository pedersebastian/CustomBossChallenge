// Downloads the backfill's sources into a local cache (tools/boss-events/.cache/, not committed), so the conversion can
// be run again without going online. Delete the cache, or pass --refresh to backfill.mjs, to download again.
import { mkdir, readFile, writeFile, access } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const cacheDir = join(dirname(fileURLToPath(import.meta.url)), '.cache');

// Ninja Kiwi's API answers an HTML error page without a User-Agent naming the client
export const userAgent = 'CustomBossChallenge-boss-events (+https://github.com/pedersebastian/CustomBossChallenge)';

// The years Blooncyclopedia has an event history page for. The wiki stopped after Vortex42 (27 Dec 2025).
export const wikiYears = [2021, 2022, 2023, 2024, 2025];
export const wikiPage = year => `Event_history:Boss_Bloon_Event/${year}`;
export const wikiPageUrl = year => `https://www.bloonswiki.com/${wikiPage(year)}`;
const wikiRawUrl = year => `https://www.bloonswiki.com/index.php?title=${wikiPage(year)}&action=raw`;

export const jxuRepo = 'jxu/btd6-events';
export const jxuFileUrl = (commit, path) => `https://github.com/${jxuRepo}/blob/${commit}/${path}`;
const jxuRawUrl = (commit, path) => `https://raw.githubusercontent.com/${jxuRepo}/${commit}/${path}`;

async function exists(path) {
    try {
        await access(path);
        return true;
    } catch {
        return false;
    }
}

/** The text at the URL; retries server errors twice, throws on any other error. */
export async function fetchText(url, accept) {
    for (let attempt = 1; ; attempt++) {
        const response = await fetch(url, { headers: { 'User-Agent': userAgent, Accept: accept } });
        if (response.ok) return response.text();
        if (attempt >= 3 || response.status < 500) throw new Error(`${response.status} ${response.statusText} for ${url}`);
        await new Promise(resolve => setTimeout(resolve, 2000 * attempt));
    }
}

// Reads a cached file, or downloads it first
async function cached(name, url, accept, refresh) {
    const path = join(cacheDir, name);
    if (!refresh && await exists(path)) return readFile(path, 'utf8');
    const text = await fetchText(url, accept);
    await mkdir(dirname(path), { recursive: true });
    await writeFile(path, text);
    return text;
}

// Runs the tasks a few at a time, to be gentle on the servers
async function inBatches(items, size, task) {
    const results = [];
    for (let i = 0; i < items.length; i += size)
        results.push(...await Promise.all(items.slice(i, i + size).map(task)));
    return results;
}

/** The wikitext of every year's page, as { year, text }. */
export async function loadWiki({ refresh = false } = {}) {
    return inBatches(wikiYears, 2, async year => ({
        year,
        text: await cached(`bloonswiki/${year}.txt`, wikiRawUrl(year), 'text/plain', refresh),
    }));
}

/**
 * The files of jxu/btd6-events at one commit: the weekly snapshots of the API's event list, every event's metadata
 * and the boss data Ninja Kiwi gave the archive's author. Reads the commit's file list from GitHub's API, then each
 * file from raw.githubusercontent.com.
 */
export async function loadJxu({ refresh = false, commit } = {}) {
    if (!commit) {
        const head = JSON.parse(await fetchText(`https://api.github.com/repos/${jxuRepo}/commits/HEAD`, 'application/json'));
        commit = head.sha;
    }
    const tree = JSON.parse(await cached(`jxu/${commit}/tree.json`,
        `https://api.github.com/repos/${jxuRepo}/git/trees/${commit}?recursive=1`, 'application/json', refresh));
    if (tree.truncated) throw new Error('GitHub cut the file list of jxu/btd6-events short');
    const paths = tree.tree.filter(entry => entry.type === 'blob').map(entry => entry.path)
        .filter(path => /^bosses\/bosses\d{8}\.json$/.test(path)
            || /^bosses\/[^/]+\/metadata\/(standard|elite)\.json$/.test(path)
            || /^BossDataDecrypted\/[^/]+\.json$/.test(path));
    const files = await inBatches(paths, 8, async path => {
        const text = await cached(`jxu/${commit}/${path}`, jxuRawUrl(commit, path), 'application/json', refresh);
        const json = parseOrNull(text);
        if (!path.includes('/metadata/') || json?.success) return [path, { commit, json }];
        // The archive downloads an event again every week while the API still lists it, so a bad day can overwrite
        // good metadata with an empty file (Lych56 in May 2026). Then the newest good version in the history is used.
        return [path, await olderGoodVersion(path, commit, refresh) ?? { commit, json }];
    });
    return { commit, files: new Map(files) };
}

function parseOrNull(text) {
    try {
        return JSON.parse(text);
    } catch {
        return null;
    }
}

async function olderGoodVersion(path, newest, refresh) {
    const commits = JSON.parse(await cached(`jxu/${newest}/history/${path}`,
        `https://api.github.com/repos/${jxuRepo}/commits?sha=${newest}&path=${path}`, 'application/json', refresh));
    for (const { sha } of commits) {
        if (sha === newest) continue;
        const json = parseOrNull(await cached(`jxu/${sha}/${path}`, jxuRawUrl(sha, path), 'application/json', refresh));
        if (json?.success) return { commit: sha, json };
    }
    return null;
}
