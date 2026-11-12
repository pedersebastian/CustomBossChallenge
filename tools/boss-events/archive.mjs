// The boss events archiver (#144): saves every new Boss Bloon Event from Ninja Kiwi's Open Data API to
// data/boss-events/<id>.json, and builds boss-events.json from all files for GitHub Pages. Runs every day in
// .github/workflows/boss-events.yml, and locally too:
//
//   node tools/boss-events/archive.mjs [--dry-run]     save the new events
//   node tools/boss-events/archive.mjs --bundle <path>  write boss-events.json from the files (offline)
//
// The API only keeps the last few events, so whatever it doesn't save in time is lost. A file is never changed or
// deleted, and only written from responses with success: true: an event whose metadata fails is tried again on the
// next run, not saved half.
import { appendFile, mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { formatVersion } from './convert.mjs';
import { formatRecord } from './format.mjs';
import { replaces } from './records.mjs';
import { fetchText } from './sources.mjs';

export const apiUrl = 'https://data.ninjakiwi.com/btd6/bosses';
export const metadataUrl = (id, level) => `${apiUrl}/${id}/metadata/${level}`;
export const bundleFormatVersion = 1;
export const bundleLicense = 'Rules and events (c) Ninja Kiwi, from Ninja Kiwi\'s Open Data. Events with '
    + '"license": "CC BY-NC-SA 4.0" use content from Blooncyclopedia (https://www.bloonswiki.com/) and are shared under '
    + 'CC BY-NC-SA 4.0 (https://creativecommons.org/licenses/by-nc-sa/4.0/). Events with "license": "CC BY-SA 3.0" use '
    + 'content from the Bloons wiki on Fandom (https://bloons.fandom.com/) and are shared under CC BY-SA 3.0 '
    + '(https://creativecommons.org/licenses/by-sa/3.0/). The pages they come from are in sourceUrls. See '
    + 'data/boss-events/README.md in https://github.com/pedersebastian/CustomBossChallenge.';

const levels = ['standard', 'elite'];
// Ids become file names
const safeId = /^[A-Za-z0-9_-]+$/;

/** The body of an API response, or null unless it is JSON with success: true (an HTML error page, an error, empty). */
export function parseApi(text) {
    try {
        const json = JSON.parse(text);
        return json?.success === true && json.body != null ? json.body : null;
    } catch {
        return null;
    }
}

/** The list entries that aren't in the archive yet, and why any others were skipped. */
export function newEntries(list, existing) {
    const entries = [];
    const skipped = [];
    for (const entry of list) {
        if (!entry || typeof entry.id !== 'string' || !safeId.test(entry.id) || typeof entry.name !== 'string'
            || !Number.isFinite(entry.start) || !Number.isFinite(entry.end)) {
            skipped.push(`${JSON.stringify(entry?.id ?? entry)}: not a valid event in the list`);
            continue;
        }
        const old = existing.get(entry.id);
        // A file the archive has is never changed: replaces() is false for every source against an existing file
        if (old && !replaces({ source: 'api' }, old)) continue;
        entries.push(entry);
    }
    return { entries, skipped };
}

/**
 * The records of the new events.
 * @param entries   the list entries from newEntries()
 * @param metadata  Map(id -> { standard, elite }): the parsed bodies, null where the response was bad
 */
export function newRecords(entries, metadata) {
    const records = [];
    const skipped = [];
    for (const entry of entries) {
        const rules = metadata.get(entry.id);
        const bad = levels.filter(level => !rules?.[level] || typeof rules[level] !== 'object');
        if (bad.length) {
            skipped.push(`${entry.name} (${entry.id}): no ${bad.join(' or ')} metadata yet, tried again next run`);
            continue;
        }
        records.push(record(entry, rules));
    }
    return { records, skipped };
}

// The list entry's fields that stay the same while the event runs: not the scores, nor the links
function record(entry, rules) {
    const result = { formatVersion };
    for (const field of ['id', 'name', 'bossType', 'start', 'end', 'normalScoringType', 'eliteScoringType'])
        if (entry[field] !== undefined && entry[field] !== null) result[field] = entry[field];
    result.source = 'api';
    result.sourceUrls = levels.map(level => metadataUrl(entry.id, level));
    result.license = null;
    result.lossy = result.normalScoringType && result.eliteScoringType ? [] : ['scoringType'];
    result.standard = rules.standard;
    result.elite = rules.elite;
    return result;
}

/** boss-events.json: every event, oldest first. */
export function buildBundle(records, generatedAt) {
    return {
        formatVersion: bundleFormatVersion,
        generated: new Date(generatedAt).toISOString(),
        license: bundleLicense,
        events: [...records].sort((a, b) => a.start - b.start || a.id.localeCompare(b.id)),
    };
}

/** Every file in the archive, as Map(id -> record). */
export async function readArchive(dir) {
    const records = new Map();
    for (const name of (await readdir(dir)).sort()) {
        if (!name.endsWith('.json')) continue;
        records.set(name.slice(0, -5), JSON.parse(await readFile(join(dir, name), 'utf8')));
    }
    return records;
}

async function fetchApi(url) {
    try {
        return parseApi(await fetchText(url, 'application/json'));
    } catch (error) {
        console.log(`${url}: ${error.message}`);
        return null;
    }
}

async function main(args) {
    const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
    const dir = join(root, 'data', 'boss-events');
    const bundleArg = args.indexOf('--bundle');
    if (bundleArg >= 0) {
        const path = args[bundleArg + 1];
        if (!path) throw new Error('--bundle needs the path to write boss-events.json to');
        const archive = await readArchive(dir);
        await mkdir(dirname(resolve(path)), { recursive: true });
        await writeFile(path, JSON.stringify(buildBundle(archive.values(), Date.now())) + '\n');
        console.log(`${path}: ${archive.size} events`);
        return;
    }

    const dryRun = args.includes('--dry-run');
    // Without the list there is nothing to do: fail, so the run fails and sends its e-mail
    const list = parseApi(await fetchText(apiUrl, 'application/json'));
    if (!Array.isArray(list)) throw new Error(`${apiUrl} didn't answer a list with success: true`);

    const archive = await readArchive(dir);
    const { entries, skipped } = newEntries(list, archive);
    const metadata = new Map();
    for (const entry of entries) {
        const [standard, elite] = await Promise.all(levels.map(level => fetchApi(metadataUrl(entry.id, level))));
        metadata.set(entry.id, { standard, elite });
    }
    const result = newRecords(entries, metadata);
    skipped.push(...result.skipped);

    for (const record of result.records)
        // 'wx': never overwrite a file, also not one written since the archive was read
        if (!dryRun) await writeFile(join(dir, `${record.id}.json`), formatRecord(record), { flag: 'wx' });

    for (const line of skipped) console.log(line);
    const names = result.records.map(record => record.name).join(', ');
    console.log(`${list.length} events in the API: ${result.records.length} new${names ? ` (${names})` : ''}`
        + `${dryRun ? ', not written (dry run)' : ''}`);
    if (process.env.GITHUB_OUTPUT && !dryRun) await appendFile(process.env.GITHUB_OUTPUT, `names=${names}\n`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        await main(process.argv.slice(2));
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
