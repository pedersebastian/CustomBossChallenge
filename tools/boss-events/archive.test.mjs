// Tests for the archiver, on the API's answers saved in fixtures/ (see fixtures/README.md). Run with
//   node --test "tools/boss-events/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildBundle, bundleLicense, changedRecords, metadataUrl, newEntries, newRecords, openEntries, parseApi,
    readArchive, sameEvent } from './archive.mjs';
import { formatRecord } from './format.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const fixture = name => readFileSync(join(here, 'fixtures', name), 'utf8');
const archiveDir = join(here, '..', '..', 'data', 'boss-events');

// The list has Dreadbloon39 (new, metadata fails below), Vortex48 (new) and Lych62 (already archived)
const list = parseApi(fixture('api-bosses.json'));
const vortex48 = { standard: parseApi(fixture('api-Vortex48-standard.json')), elite: parseApi(fixture('api-Vortex48-elite.json')) };
const lych62 = { id: 'Lych62_mvu7hzt9', source: 'jxu-api' };
const existing = new Map([[lych62.id, lych62]]);

function run(archive, metadata) {
    const { entries, skipped } = newEntries(list, archive);
    const result = newRecords(entries, metadata);
    return { records: result.records, skipped: [...skipped, ...result.skipped] };
}

test('only answers with success: true are read', () => {
    assert.equal(parseApi('<!DOCTYPE html><html><body>Error</body></html>'), null);
    assert.equal(parseApi(''), null);
    assert.equal(parseApi('{"error":"Not found","success":false,"body":null}'), null);
    assert.equal(parseApi('{"error":null,"success":true}'), null);
    assert.equal(parseApi('{"error":null,"success":"true","body":{}}'), null);
    assert.equal(list.length, 3);
    assert.equal(vortex48.standard.name, 'Vortex48');
});

test('a new event is saved in the format of the archive', () => {
    const { records } = run(existing, new Map([['Vortex48_mw4au3ui', vortex48]]));
    assert.equal(records.length, 1);
    const [record] = records;
    assert.deepEqual(Object.keys(record), ['formatVersion', 'id', 'name', 'bossType', 'start', 'end',
        'normalScoringType', 'eliteScoringType', 'source', 'sourceUrls', 'license', 'lossy', 'standard', 'elite']);
    assert.deepEqual({ ...record, standard: undefined, elite: undefined }, {
        formatVersion: 1, id: 'Vortex48_mw4au3ui', name: 'Vortex48', bossType: 'vortex',
        start: 1794016800000, end: 1794452400000, normalScoringType: 'LeastCash', eliteScoringType: 'GameTime',
        source: 'api', sourceUrls: [metadataUrl('Vortex48_mw4au3ui', 'standard'), metadataUrl('Vortex48_mw4au3ui', 'elite')],
        license: null, lossy: [], standard: undefined, elite: undefined,
    });
    // The metadata's body as the API gave it, without the error/success wrapper
    assert.deepEqual(record.standard, JSON.parse(fixture('api-Vortex48-standard.json')).body);
    assert.deepEqual(record.elite, JSON.parse(fixture('api-Vortex48-elite.json')).body);
});

test('the file looks like the ones already in the archive', () => {
    const [record] = run(existing, new Map([['Vortex48_mw4au3ui', vortex48]])).records;
    const text = formatRecord(record);
    const old = readFileSync(join(archiveDir, 'Vortex45_mqa0rl1r.json'), 'utf8').replaceAll('\r\n', '\n');
    const head = file => file.split('\n').slice(0, 9).map(line => line.replace(/: .*/, ''));
    assert.deepEqual(head(text), head(old));
    assert.match(text, /\n {6}\{ "tower": "DartMonkey", [^\n]* \},?\n/);
    assert.ok(text.endsWith('}\n'));
});

test('an event already in the archive is left untouched, whatever its source', () => {
    const metadata = new Map([['Vortex48_mw4au3ui', vortex48], ['Lych62_mvu7hzt9', vortex48]]);
    for (const source of ['api', 'jxu-api', 'nk-dcm', 'bloonswiki']) {
        const { records } = run(new Map([[lych62.id, { ...lych62, source }]]), metadata);
        assert.deepEqual(records.map(record => record.name), ['Vortex48'], source);
    }
});

test('an event with bad or missing metadata is skipped and tried again', () => {
    const half = { standard: vortex48.standard, elite: parseApi('<html>Error</html>') };
    const { records, skipped } = run(existing, new Map([['Dreadbloon39_mwcyylfk', half]]));
    assert.deepEqual(records, []);
    assert.equal(skipped.length, 2);
    assert.match(skipped[0], /^Dreadbloon39 \(Dreadbloon39_mwcyylfk\): no elite metadata/);
    assert.match(skipped[1], /^Vortex48 \(Vortex48_mw4au3ui\): no standard or elite metadata/);
});

test('a broken list entry is skipped, not written', () => {
    const { entries, skipped } = newEntries([{ id: '../evil', name: 'x', start: 1, end: 2 }, null,
        { id: 'NoDates_x', name: 'NoDates' }], new Map());
    assert.deepEqual(entries, []);
    assert.equal(skipped.length, 3);
});

test('a second run with nothing new adds nothing', () => {
    const metadata = new Map([['Vortex48_mw4au3ui', vortex48], ['Dreadbloon39_mwcyylfk', vortex48]]);
    const first = run(existing, metadata).records;
    assert.equal(first.length, 2);
    const after = new Map([...existing, ...first.map(record => [record.id, record])]);
    assert.deepEqual(run(after, metadata), { records: [], skipped: [] });
});

// Two snapshots of the API: Vortex48 saved from the first, then fetched again from the second
function recheck(saved, metadata, now) {
    const archive = new Map([...existing, [saved.id, saved]]);
    const open = openEntries(list, archive, now);
    return { open: open.map(entry => entry.id), ...changedRecords(open, metadata, archive) };
}
const [saved] = run(existing, new Map([['Vortex48_mw4au3ui', vortex48]])).records;
const beforeEnd = saved.end - 1;

test('an event Ninja Kiwi changes before it ends is updated', () => {
    const second = structuredClone(vortex48);
    second.standard.startingCash += 250;
    second.elite._towers[1].path3NumBlockedTiers = 3;
    const { open, records, skipped } = recheck(saved, new Map([[saved.id, second]]), beforeEnd);
    assert.deepEqual(open, [saved.id]);
    assert.deepEqual(skipped, []);
    assert.equal(records.length, 1);
    assert.equal(records[0].standard.startingCash, vortex48.standard.startingCash + 250);
    assert.equal(records[0].elite._towers[1].path3NumBlockedTiers, 3);

    // A new end in the list is a change too
    const later = { ...saved, end: saved.end - 60 * 60 * 1000 };
    assert.equal(recheck(later, new Map([[saved.id, vortex48]]), beforeEnd).records.length, 1);
});

test('only the play counts changing is not an update', () => {
    const second = structuredClone(vortex48);
    for (const rules of [second.standard, second.elite]) {
        rules.plays += 1000;
        rules.wins += 10;
        rules.playsUnique += 500;
    }
    assert.deepEqual(recheck(saved, new Map([[saved.id, second]]), beforeEnd).records, []);
});

test('an event is not checked again after its end, nor a backfilled one', () => {
    const second = structuredClone(vortex48);
    second.standard.startingCash += 250;
    const metadata = new Map([[saved.id, second]]);
    assert.deepEqual(recheck(saved, metadata, Math.max(saved.end, list[1].end)).open, []);
    assert.deepEqual(recheck({ ...saved, source: 'jxu-api' }, metadata, beforeEnd).open, []);
    assert.deepEqual(openEntries(list, existing, 0), [], 'Lych62 is backfilled');
});

test('an open event whose metadata fails keeps its file', () => {
    const half = { standard: vortex48.standard, elite: null };
    const { records, skipped } = recheck(saved, new Map([[saved.id, half]]), beforeEnd);
    assert.deepEqual(records, []);
    assert.deepEqual(skipped, ['Vortex48 (Vortex48_mw4au3ui): no elite metadata yet, kept the saved file, checked again '
        + 'next run']);
});

test('sameEvent ignores only the play counts', () => {
    assert.ok(sameEvent(saved, structuredClone(saved)));
    assert.ok(!sameEvent(saved, { ...saved, name: 'Vortex48b' }));
    assert.ok(!sameEvent(saved, { ...saved, standard: { ...saved.standard, lives: 1 } }));
    assert.ok(sameEvent(saved, { ...saved, elite: { ...saved.elite, upvotes: 99 } }));
});

test('boss-events.json has every file, oldest first, with its licence and sources', async () => {
    const archive = await readArchive(archiveDir);
    const bundle = buildBundle([...archive.values()].reverse(), Date.UTC(2026, 10, 12));
    assert.equal(bundle.formatVersion, 1);
    assert.equal(bundle.generated, '2026-11-12T00:00:00.000Z');
    assert.equal(bundle.license, bundleLicense);
    assert.equal(bundle.events.length, archive.size);
    for (let i = 1; i < bundle.events.length; i++) assert.ok(bundle.events[i - 1].start <= bundle.events[i].start);
    assert.ok(bundle.events.some(event => event.license === 'CC BY-NC-SA 4.0'));
    assert.ok(bundle.events.every(event => Array.isArray(event.sourceUrls) && 'license' in event));
});
