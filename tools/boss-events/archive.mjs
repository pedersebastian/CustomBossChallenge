// The boss events archiver (#144): saves every new Boss Bloon Event from Ninja Kiwi's Open Data API to
// data/boss-events/<id>.json, and builds boss-events.json from all files for GitHub Pages. Runs every day in
// .github/workflows/boss-events.yml, and locally too:
//
//   node tools/boss-events/archive.mjs [--dry-run]     save the new events, update the changed ones
//   node tools/boss-events/archive.mjs --bundle <path>  write boss-events.json from the files (offline)
//
// The API only keeps the last few events, so whatever it doesn't save in time is lost. An event is listed (and saved)
// days before it starts, and Ninja Kiwi sometimes changes its rules or dates before it ends (#178), so the metadata of
// a saved event is fetched again every run until its end has passed, and the file is written again when anything but
// the play counts changed. After that a file is never changed or deleted. Files are only written from responses with
// success: true: an event whose metadata fails is tried again on the next run, not saved half.
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

const validEntry = entry => !!entry && typeof entry.id === 'string' && safeId.test(entry.id)
    && typeof entry.name === 'string' && Number.isFinite(entry.start) && Number.isFinite(entry.end);

/** The list entries that aren't in the archive yet, and why any others were skipped. */
export function newEntries(list, existing) {
    const entries = [];
    const skipped = [];
    for (const entry of list) {
        if (!validEntry(entry)) {
            skipped.push(`${JSON.stringify(entry?.id ?? entry)}: not a valid event in the list`);
            continue;
        }
        const old = existing.get(entry.id);
        // A file the archive has is only changed through openEntries(): replaces() is false for every source against
        // an existing file
        if (old && !replaces({ source: 'api' }, old)) continue;
        entries.push(entry);
    }
    return { entries, skipped };
}

/**
 * The list entries of events the archiver saved (source "api") that haven't ended yet, by the file's end or the
 * list's: their metadata is fetched again, as Ninja Kiwi may still change them.
 */
export function openEntries(list, existing, now) {
    return list.filter(entry => {
        const old = validEntry(entry) && existing.get(entry.id);
        return old?.source === 'api' && now < Math.max(old.end, entry.end);
    });
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

/**
 * The records of the open events that changed since they were saved, and why any were left as they are.
 * @param entries   the list entries from openEntries()
 * @param metadata  as for newRecords()
 * @param existing  Map(id -> record): the archive
 */
export function changedRecords(entries, metadata, existing) {
    const { records, skipped } = newRecords(entries, metadata);
    return {
        records: records.filter(record => !sameEvent(record, existing.get(record.id))),
        skipped: skipped.map(line => line.replace(/tried again next run$/, 'kept the saved file, checked again next run')),
    };
}

// The metadata's play counts go up every day while the event runs: that isn't a change of the event
const statFields = ['plays', 'wins', 'restarts', 'losses', 'upvotes', 'playsUnique', 'winsUnique', 'lossesUnique'];
const withoutStats = rules =>
    Object.fromEntries(Object.entries(rules ?? {}).filter(([key]) => !statFields.includes(key)));

/** Whether two records of an event have the same fields and rules, apart from the play counts. */
export function sameEvent(a, b) {
    const normal = record =>
        JSON.stringify({ ...record, standard: withoutStats(record.standard), elite: withoutStats(record.elite) });
    return normal(a) === normal(b);
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
    const open = openEntries(list, archive, Date.now());
    const metadata = new Map();
    for (const entry of [...entries, ...open]) {
        const [standard, elite] = await Promise.all(levels.map(level => fetchApi(metadataUrl(entry.id, level))));
        metadata.set(entry.id, { standard, elite });
    }
    const result = newRecords(entries, metadata);
    skipped.push(...result.skipped);

    const changed = changedRecords(open, metadata, archive);
    skipped.push(...changed.skipped);

    if (!dryRun) {
        for (const record of result.records)
            // 'wx': never overwrite a file, also not one written since the archive was read
            await writeFile(join(dir, `${record.id}.json`), formatRecord(record), { flag: 'wx' });
        for (const record of changed.records) await writeFile(join(dir, `${record.id}.json`), formatRecord(record));
    }

    for (const line of skipped) console.log(line);
    const nameList = records => records.length ? ` (${records.map(record => record.name).join(', ')})` : '';
    console.log(`${list.length} events in the API: ${result.records.length} new${nameList(result.records)}, `
        + `${open.length} checked again, ${changed.records.length} changed${nameList(changed.records)}`
        + `${dryRun ? ', not written (dry run)' : ''}`);
    // The workflow commits when names isn't empty, and puts it in the commit message
    const names = [...result.records.map(record => record.name),
        ...changed.records.map(record => `${record.name} (updated)`)].join(', ');
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
