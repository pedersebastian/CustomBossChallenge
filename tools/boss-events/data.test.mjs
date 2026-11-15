// Checks every file in data/boss-events/ (#180). The archiver's commits don't start checks.yml, so pages.yml runs these
// tests on the archive it is about to publish, and a broken or duplicate file stops the deploy. Run with
//   node --test "tools/boss-events/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { bossNames, validMetadata } from './archive.mjs';
import { formatRecord } from './format.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const archiveDir = join(here, '..', '..', 'data', 'boss-events');
const files = readdirSync(archiveDir).filter(name => name.endsWith('.json')).sort();
const records = files.map(name => {
    const text = readFileSync(join(archiveDir, name), 'utf8').replaceAll('\r\n', '\n');
    return { name, text, record: JSON.parse(text) };
});

const fields = ['formatVersion', 'id', 'name', 'bossType', 'start', 'end', 'source', 'sourceUrls', 'license', 'lossy',
    'standard', 'elite'];
const sources = ['api', 'jxu-api', 'nk-dcm', 'bloonswiki'];
const licenses = [null, 'CC BY-NC-SA 4.0', 'CC BY-SA 3.0'];
const lossyValues = ['scoringType', 'maxParagons', 'id', 'dates'];
// "0" is kept as the source had it (see the README's odd values)
const scoringTypes = ['GameTime', 'LeastCash', 'LeastTiers', '0'];

/** What is wrong with a file, as a list of problems; empty when it is fine. Exported for the tests below. */
export function problemsOf(fileName, record) {
    const problems = [];
    const check = (ok, problem) => { if (!ok) problems.push(problem); };
    check(record && typeof record === 'object' && !Array.isArray(record), 'not an object');
    if (problems.length) return problems;
    for (const field of fields) check(field in record, `no ${field}`);
    check(record.formatVersion === 1, `formatVersion ${record.formatVersion}`);
    check(typeof record.id === 'string' && /^[A-Za-z0-9_-]+$/.test(record.id), `id ${JSON.stringify(record.id)}`);
    check(fileName === `${record.id}.json`, `file name isn't the id ${record.id}`);
    check(typeof record.name === 'string' && record.name.trim() !== '', 'no name');
    check(record.bossType in bossNames, `unknown bossType ${record.bossType}`);
    check(Number.isSafeInteger(record.start) && Number.isSafeInteger(record.end), 'start and end must be whole ms');
    check(record.start < record.end, 'start must be before end');
    // From the first event (July 2021) until a year from now
    check(record.start >= Date.UTC(2021, 0, 1) && record.end <= Date.now() + 365 * 24 * 3600 * 1000, 'dates out of range');
    for (const field of ['normalScoringType', 'eliteScoringType'])
        if (field in record) check(scoringTypes.includes(record[field]), `${field} ${record[field]}`);
    check(sources.includes(record.source), `source ${record.source}`);
    check(Array.isArray(record.sourceUrls) && record.sourceUrls.length > 0
        && record.sourceUrls.every(url => typeof url === 'string' && url.startsWith('https://')), 'sourceUrls');
    check(licenses.includes(record.license), `license ${record.license}`);
    check(record.source !== 'api' || record.license === null, 'a file from the API has no wiki licence');
    check(Array.isArray(record.lossy) && record.lossy.every(value => lossyValues.includes(value))
        && new Set(record.lossy).size === record.lossy.length, `lossy ${JSON.stringify(record.lossy)}`);
    if (Array.isArray(record.lossy)) {
        check(!record.lossy.includes('scoringType') || !('normalScoringType' in record && 'eliteScoringType' in record),
            'lossy scoringType, but both scoring types are there');
        check(record.lossy.includes('scoringType') || ('normalScoringType' in record && 'eliteScoringType' in record),
            'a scoring type is missing without lossy scoringType');
    }
    for (const level of ['standard', 'elite']) check(validMetadata(record[level]), `${level} metadata is incomplete`);
    return problems;
}

/** Events that are the same one twice, or whose dates collide, as a list of problems */
export function collisionsOf(records) {
    const problems = [];
    const ids = new Map();
    for (const record of records) {
        // Windows and macOS don't tell file names apart by case
        const key = record.id.toLowerCase();
        if (ids.has(key)) problems.push(`${record.id} and ${ids.get(key)} are the same id`);
        ids.set(key, record.id);
    }
    const sorted = [...records].sort((a, b) => a.start - b.start);
    for (let i = 0; i < sorted.length; i++) {
        for (let j = i + 1; j < sorted.length && sorted[j].start < sorted[i].end; j++) {
            const [a, b] = [sorted[i], sorted[j]];
            // The same event saved twice: the same name, rules and dates
            if (a.name === b.name && a.start === b.start && a.end === b.end
                && JSON.stringify(a.standard) === JSON.stringify(b.standard))
                problems.push(`${a.id} and ${b.id} are the same event`);
            // One boss event runs at a time. Two of the same boss are one replacing the other (see replacements() in
            // archive.mjs); two different bosses at once mean a date is wrong, unless the file says it is guessed.
            else if (a.bossType !== b.bossType && !a.lossy.includes('dates') && !b.lossy.includes('dates'))
                problems.push(`${a.id} (${a.bossType}) and ${b.id} (${b.bossType}) run at the same time`);
        }
    }
    return problems;
}

test('the archive has events', () => assert.ok(records.length > 250, `${records.length} files`));

test('every file is a complete event, named by its id (#180)', () => {
    const problems = records.flatMap(({ name, record }) => problemsOf(name, record).map(problem => `${name}: ${problem}`));
    assert.deepEqual(problems, []);
});

test('every file is written the way the archiver and the backfill write it', () => {
    const differ = records.filter(({ text, record }) => formatRecord(record) !== text).map(({ name }) => name);
    assert.deepEqual(differ, []);
});

test('no event is in the archive twice, and no two bosses run at the same time (#180)', () =>
    assert.deepEqual(collisionsOf(records.map(({ record }) => record)), []));

test('a broken file is caught', () => {
    const [{ name, record }] = records;
    assert.deepEqual(problemsOf(name, record), []);
    const broken = changes => problemsOf(name, { ...structuredClone(record), ...changes });
    assert.match(broken({ id: 'Other_x' }).join(), /file name isn't the id/);
    assert.match(broken({ start: record.end }).join(), /start must be before end/);
    assert.match(broken({ bossType: 'moab' }).join(), /unknown bossType/);
    assert.match(broken({ lossy: ['dates', 'dates'] }).join(), /lossy/);
    assert.match(broken({ standard: {} }).join(), /standard metadata is incomplete/);
    assert.match(broken({ source: 'somewhere' }).join(), /source/);
    assert.match(problemsOf(name, []).join(), /not an object/);
    const withoutName = structuredClone(record);
    delete withoutName.name;
    assert.match(problemsOf(name, withoutName).join(), /no name/);
});

test('a duplicate or colliding event is caught', () => {
    const [a] = records.map(({ record }) => record).filter(record => record.bossType === 'lych');
    assert.match(collisionsOf([a, { ...a, id: a.id.toUpperCase() }]).join(), /same id/);
    assert.match(collisionsOf([a, { ...a, id: `${a.id}x` }]).join(), /same event/);
    const other = { ...a, id: 'Other_x', name: 'Vortex1', bossType: 'vortex', standard: {} };
    assert.match(collisionsOf([a, other]).join(), /run at the same time/);
    assert.deepEqual(collisionsOf([a, { ...other, lossy: ['dates'] }]), []);
    // Two of the same boss at once is one replacing the other
    assert.deepEqual(collisionsOf([a, { ...a, id: 'Lych99New_x', name: 'Lych99New', standard: {} }]), []);
});
