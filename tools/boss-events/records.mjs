// Picks the best source for every event and builds the archive's records. Pure: takes the downloaded sources and
// returns the records plus a report of what was skipped or guessed, so it can be tested on saved samples.
import { templatesNamed } from './wikitext.mjs';
import { dcmToApi, formatVersion, makeNames, wikiEvent, wikiRulesToApi } from './convert.mjs';

const wikiLicense = 'CC BY-NC-SA 4.0';
const fandomLicense = 'CC BY-SA 3.0';
const day = 24 * 60 * 60 * 1000;

/**
 * @param wiki  [{ year, text }]: the wikitext of each year's event history page
 * @param jxu   { commit, files: Map(path -> { commit, json }) }: files of jxu/btd6-events
 * @param game  web/src/data/game.json
 * @param urls  { jxuFileUrl(commit, path), wikiPageUrl(year) }
 */
export function buildRecords({ wiki, jxu, game, urls }) {
    const names = makeNames(game);
    const report = [];
    const records = new Map();

    // Every event in the API's lists the archive saved, the newest copy of each
    const listed = new Map();
    for (const [path, { json }] of [...jxu.files].sort(([a], [b]) => a.localeCompare(b)))
        if (/^bosses\/bosses\d{8}\.json$/.test(path)) for (const entry of json?.body ?? []) listed.set(entry.id, entry);

    const wikiEvents = wiki.flatMap(({ year, text }) =>
        templatesNamed(text, 'BTD6 boss event rules').map(({ params }) => ({ year, params })));
    const wikiById = new Map(wikiEvents.map(event => [event.params.id, event]));

    // 1. The API's metadata, as the archive saved it. The archive also has the metadata of some events from before its
    //    first saved list (Nov 2024); those get their list entry from the wiki.
    const ids = new Set(listed.keys());
    for (const path of jxu.files.keys()) {
        const match = /^bosses\/([^/]+)\/metadata\//.exec(path);
        if (match) ids.add(match[1]);
    }
    for (const id of ids) {
        const standard = jxu.files.get(`bosses/${id}/metadata/standard.json`);
        const elite = jxu.files.get(`bosses/${id}/metadata/elite.json`);
        const wikiSource = wikiById.get(id);
        const entry = listed.get(id) ?? (wikiSource && wikiEvent(wikiSource.params));
        if (!entry) {
            report.push(`${id}: metadata in jxu/btd6-events but no date in any list or on the wiki: left out`);
            continue;
        }
        if (!standard?.json?.success || !elite?.json?.success) {
            // Ninja Kiwi sometimes puts an event up again under a new id; then the other id has the rules
            const other = [...listed.values(), ...wikiEvents.map(event => event.params)]
                .find(event => event.name === entry.name && event.id !== id);
            if (!wikiSource) report.push(`${entry.name} (${id}): no metadata in jxu/btd6-events`
                + (other ? `, kept ${other.id} instead` : ''));
            continue;
        }
        const wikiEntry = wikiSource ? wikiEvent(wikiSource.params) : null;
        const { scoring, lossy } = scoringOf(entry, wikiEntry);
        const sourceUrls = ['standard', 'elite'].map(level => urls.jxuFileUrl(
            (level === 'standard' ? standard : elite).commit, `bosses/${id}/metadata/${level}.json`));
        // Dates or scoring from the wiki bring its licence along
        const usesWiki = !listed.has(id) || (wikiEntry && !(entry.normalScoringType && entry.eliteScoringType));
        if (usesWiki) sourceUrls.push(urls.wikiPageUrl(wikiSource.year));
        const record = envelope({ ...pick(entry, ['id', 'name', 'bossType', 'start', 'end']), ...scoring }, 'jxu-api',
            sourceUrls, usesWiki ? wikiLicense : null);
        record.lossy = lossy;
        record.standard = standard.json.body;
        record.elite = elite.json.body;
        records.set(id, record);
    }

    // 2. and 3. The wiki's events, with Ninja Kiwi's own challenge models where the archive has them
    const dcmByName = new Map();
    for (const [path, file] of jxu.files) {
        const match = /^BossDataDecrypted\/(.+)\.json$/.exec(path);
        if (match && file.json) dcmByName.set(match[1], { path, ...file });
    }
    const usedDcm = new Set();
    for (const { year, params } of wikiEvents) {
        if (records.has(params.id)) continue;
        const problems = [];
        const standard = wikiRulesToApi(params['normal rules'] ?? '', params.name, names, problems);
        const elite = wikiRulesToApi(params['elite rules'] ?? '', params.name, names, problems);
        report.push(...problems);
        const entry = wikiEvent(params);
        const lossy = [];
        if (!entry.normalScoringType) lossy.push('scoringType');
        const dcm = dcmByName.get(params.name);
        const wikiUrl = urls.wikiPageUrl(year);
        let record;
        if (dcm?.json.normalDcm && dcm.json.eliteDcm) {
            usedDcm.add(params.name);
            record = envelope(entry, 'nk-dcm', [urls.jxuFileUrl(dcm.commit, dcm.path), wikiUrl], wikiLicense);
            record.lossy = lossy;
            record.standard = dcmToApi(dcm.json.normalDcm, params.name, standard);
            record.elite = dcmToApi(dcm.json.eliteDcm, params.name, elite);
        } else {
            record = envelope(entry, 'bloonswiki', [wikiUrl], wikiLicense);
            record.lossy = lossy;
            record.standard = standard;
            record.elite = elite;
        }
        if (record.standard?.maxParagons === undefined) record.lossy.push('maxParagons');
        if (records.has(record.id)) report.push(`${record.name}: id ${record.id} twice on the wiki, kept the first`);
        else records.set(record.id, record);
    }

    // Ninja Kiwi's models of events the wiki doesn't list. Dated by knownEvents, or else only when the wiki's timeline
    // has exactly one free week between the boss's previous and next event; the rest are left out.
    const timeline = [...records.values()].sort((a, b) => a.start - b.start);
    for (const [name, dcm] of dcmByName) {
        if (usedDcm.has(name)) continue;
        const known = knownEvents[name];
        const week = known ? { start: known.start, end: known.end } : freeWeek(name, timeline);
        if (!week || !dcm.json.normalDcm || !dcm.json.eliteDcm) {
            report.push(`${name}: in Ninja Kiwi's boss data but not on the wiki, and no free week to date it: left out`);
            continue;
        }
        const scoring = known?.scoringType
            ? { normalScoringType: known.scoringType, eliteScoringType: known.scoringType } : {};
        const entry = { id: known?.id ?? name, name, bossType: dcm.json.bossType ?? bossTypeOf(name), ...week, ...scoring };
        const sourceUrls = [urls.jxuFileUrl(dcm.commit, dcm.path), ...(known ? [known.sourceUrl] : [])];
        const record = envelope(entry, 'nk-dcm', sourceUrls, known ? fandomLicense : null);
        record.lossy = [];
        if (!known?.id) record.lossy.push('id');
        if (!known || known.approximate) record.lossy.push('dates');
        if (!known?.scoringType) record.lossy.push('scoringType');
        record.lossy.push('maxParagons');
        record.standard = dcmToApi(dcm.json.normalDcm, name, null);
        record.elite = dcmToApi(dcm.json.eliteDcm, name, null);
        records.set(record.id, record);
        report.push(`${name}: not on the wiki, dated ${known ? 'by knownEvents' : 'by the free week'} `
            + new Date(week.start).toISOString().slice(0, 10));
    }

    return { records: [...records.values()].sort((a, b) => a.start - b.start || a.id.localeCompare(b.id)), report };
}

// Events in Ninja Kiwi's boss data that Blooncyclopedia doesn't list, dated by hand (#152). The old Bloons wiki on Fandom
// (CC BY-SA 3.0) lists them on its pages per game version. Its times say NZST; the weekly start, "8:00pm NZST", is
// 08:00 UTC like in the API, and the other times are counted from it. An event's id ends with the time it was made,
// in base 36 milliseconds.
const fandomPage = (version, oldid) =>
    `https://bloons.fandom.com/wiki/Boss_Bloon_Event_(BTD6)/List_of_Boss_Events/Version_${version}.0?oldid=${oldid}`;
export const knownEvents = {
    // Week 87a: pulled after about four hours and replaced by Vortex17EX (week 87b). Its id was made on 16 Mar 2023.
    Vortex17: {
        id: 'Vortex17_lfbrv7rb', start: Date.UTC(2023, 2, 18, 8), end: Date.UTC(2023, 2, 18, 12, 8),
        sourceUrl: fandomPage(35, 458743),
    },
    // Week 62: the week the free week gave. The id isn't known.
    Lych23: { start: Date.UTC(2022, 8, 24, 8), end: Date.UTC(2022, 8, 29, 3), sourceUrl: fandomPage(32, 458740) },
    // Week 99, Least Cash: pulled early for exploits, so its times are approximate. Fandom has 9 Jun and an end marked
    // "(?)"; BTD6 Index's Discord (#bosses) talked about it from 22:43 UTC on 8 Jun until it was gone at 19:30 UTC on
    // 10 Jun. The id isn't known.
    Bloonarius40: {
        start: Date.UTC(2023, 5, 8, 22), end: Date.UTC(2023, 5, 10, 19, 30), approximate: true, scoringType: 'LeastCash',
        sourceUrl: fandomPage(37, 458745),
    },
};

// The archiver's API files (source "api") are never replaced by a backfill (only the archiver updates them, until the
// event ends); a backfill source only replaces itself or a worse one
const sourceRank = { 'jxu-api': 1, 'nk-dcm': 2, bloonswiki: 3 };
export function replaces(record, old) {
    const oldRank = sourceRank[old.source] ?? 0;
    return oldRank !== 0 && sourceRank[record.source] <= oldRank;
}

function envelope(entry, source, sourceUrls, license) {
    return { formatVersion, ...entry, source, sourceUrls, license };
}

function pick(object, fields) {
    const result = {};
    for (const field of fields) if (object[field] !== undefined) result[field] = object[field];
    return result;
}

// The API's older entries have one scoringType for both difficulties; the wiki may know each
function scoringOf(entry, wikiEntry) {
    const scoring = {};
    const lossy = [];
    for (const field of ['normalScoringType', 'eliteScoringType']) {
        const value = entry[field] ?? wikiEntry?.[field] ?? entry.scoringType;
        if (value) scoring[field] = value;
        else if (!lossy.includes('scoringType')) lossy.push('scoringType');
    }
    return { scoring, lossy };
}

const bossTypeOf = name => /^[A-Za-z]+/.exec(name)[0].toLowerCase();

// Bloonarius40 goes in the one free week between Bloonarius39 and Bloonarius41, if there is exactly one
function freeWeek(name, timeline) {
    const match = /^([A-Za-z]+)(\d+)$/.exec(name);
    if (!match) return null;
    const [, boss, number] = match;
    const previous = timeline.find(event => event.name === `${boss}${Number(number) - 1}`);
    const next = timeline.find(event => event.name === `${boss}${Number(number) + 1}`);
    if (!previous || !next) return null;
    const between = timeline.filter(event => event.start >= previous.start && event.start <= next.start);
    const free = [];
    for (let i = 1; i < between.length; i++) {
        const gap = between[i].start - between[i - 1].start;
        if (gap > 10 * day) {
            if (Math.round(gap / (7 * day)) !== 2) return null;
            free.push(between[i - 1]);
        }
    }
    if (free.length !== 1) return null;
    return { start: free[0].start + 7 * day, end: free[0].end + 7 * day };
}
