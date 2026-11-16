// Tests for the archiver, on the API's answers saved in fixtures/ (see fixtures/README.md). Run with
//   node --test "tools/boss-events/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';
import { buildBundle, bundleLicense, changedRecords, displayNameOf, madeAt, metadataUrl, newEntries, newRecords,
    openEntries, outputNames, parseApi, readArchive, replacements, sameEvent, validMetadata, writeBundle } from './archive.mjs';
import { formatRecord } from './format.mjs';
import { fetchText } from './sources.mjs';

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

test('a bundle path ending in .gz is gzipped, the copy built into the mod (#146)', async () => {
    const archive = await readArchive(archiveDir);
    const dir = mkdtempSync(join(tmpdir(), 'boss-events-'));
    try {
        await writeBundle(join(dir, 'boss-events.json.gz'), archive.values(), 0);
        await writeBundle(join(dir, 'boss-events.json'), archive.values(), 0);
        const zipped = readFileSync(join(dir, 'boss-events.json.gz'));
        const plain = readFileSync(join(dir, 'boss-events.json'), 'utf8');
        assert.equal(gunzipSync(zipped).toString('utf8'), plain);
        assert.ok(zipped.length < plain.length / 5, `${zipped.length} bytes gzipped of ${plain.length}`);
        assert.equal(JSON.parse(plain).events.length, archive.size);
    } finally {
        rmSync(dir, { recursive: true, force: true });
    }
});

test('the bundle says which events replaced which (#180)', async () => {
    const bundle = buildBundle((await readArchive(archiveDir)).values(), 0);
    const pairs = bundle.events.flatMap(event => event.replaces.map(old => `${event.id} replaces ${old}`));
    assert.deepEqual(pairs.sort(), [
        // Made on 9 Oct 2026 in place of the one listed on 8 Oct, before the event started
        'Blastapopoulos20_mv08g41v replaces Blastapopoulos20_mux48qjr',
        // Pulled after a few hours (Fandom weeks 87a/b and 168a/b)
        'Vortex17EX_lfg12s34 replaces Vortex17_lfbrv7rb',
        'Vortex33New_m1w0dd1c replaces Vortex33_m1tpa2fs',
    ]);
    const byId = new Map(bundle.events.map(event => [event.id, event]));
    assert.deepEqual(byId.get('Vortex33_m1tpa2fs').replacedBy, ['Vortex33New_m1w0dd1c']);
    assert.deepEqual(byId.get('Vortex45_mqa0rl1r').replaces, []);
    assert.deepEqual(byId.get('Vortex45_mqa0rl1r').replacedBy, []);
    // The files themselves stay as they are
    const file = (await readArchive(archiveDir)).get('Vortex33_m1tpa2fs');
    assert.ok(!('replacedBy' in file) && !('displayName' in file));
});

test('without both ids made at known times, the event that started later replaced the other', () => {
    const event = (id, start, end) => ({ id, bossType: 'lych', start, end });
    const links = replacements([event('Lych23', 0, 100), event('Lych23New', 50, 100),
        { ...event('Vortex1', 0, 100), bossType: 'vortex' }]);
    assert.deepEqual(links.get('Lych23New'), { replaces: ['Lych23'], replacedBy: [] });
    assert.deepEqual(links.get('Lych23'), { replaces: [], replacedBy: ['Lych23New'] });
    assert.equal(links.get('Vortex1'), undefined);
    // Different bosses at once are not a replacement
    assert.equal(replacements([event('Lych1', 0, 100), { ...event('Vortex1', 0, 100), bossType: 'vortex' }]).size, 0);
});

test("an id's end is when Ninja Kiwi made it", () => {
    assert.equal(new Date(madeAt('Vortex33_m1tpa2fs')).toISOString(), '2024-10-03T19:39:31.672Z');
    assert.equal(new Date(madeAt('m2dppzns')).toISOString().slice(0, 10), '2024-10-17');
    assert.equal(madeAt('Lych23'), null);
    assert.equal(madeAt('Bloonarius40'), null);
});

test('every event gets a display name and number (#180)', async () => {
    const shown = name => displayNameOf({ name, bossType: name.startsWith('Bl') && name.includes('pop') ? 'blastapopoulos'
        : name.startsWith('Bloon') ? 'bloonarius' : 'vortex' });
    assert.deepEqual(shown('Vortex45'), { displayName: 'Vortex 45', number: 45 });
    assert.deepEqual(shown('Blastapopolous18'), { displayName: 'Blastapopoulos 18', number: 18 });
    assert.deepEqual(shown('Bloonsarius16'), { displayName: 'Bloonarius 16', number: 16 });
    assert.deepEqual(shown('Vortex_28'), { displayName: 'Vortex 28', number: 28 });
    assert.deepEqual(shown('Vortex33New'), { displayName: 'Vortex 33 New', number: 33 });
    assert.deepEqual(shown('Blastapopoulos15New2'), { displayName: 'Blastapopoulos 15 New2', number: 15 });
    assert.deepEqual(shown('Bloonarius52ABR'), { displayName: 'Bloonarius 52 ABR', number: 52 });
    assert.deepEqual(displayNameOf({ name: 'Got you', bossType: 'bloonarius' }), { displayName: 'Got you', number: null });

    const bundle = buildBundle((await readArchive(archiveDir)).values(), 0);
    assert.ok(bundle.events.every(event => typeof event.displayName === 'string' && event.displayName !== ''));
    // Only the April Fools' events and the first Bloonarius have no number
    assert.deepEqual(bundle.events.filter(event => event.number === null).map(event => event.name).sort(),
        ['Bloonarius', 'Got you', 'Lol']);
});

// A fetch that answers from a list, one answer per call: a status, an error to throw, or 'hang' until the signal ends
function fakeFetch(answers) {
    const calls = [];
    const fetch = async (url, options) => {
        calls.push(url);
        const answer = answers[calls.length - 1];
        if (answer === 'hang') {
            return new Promise((_, reject) => options.signal.addEventListener('abort', () => reject(options.signal.reason)));
        }
        if (answer instanceof Error) throw answer;
        return new Response(answer === 200 ? 'ok' : 'error', { status: answer });
    };
    return { fetch, calls };
}
const noDelay = async () => {};

test('a request that hangs is given up after the timeout and tried again (#179)', async () => {
    const { fetch, calls } = fakeFetch(['hang', 200]);
    assert.equal(await fetchText('https://example.test/a', 'text/plain', { fetch, timeout: 20, delay: noDelay }), 'ok');
    assert.equal(calls.length, 2);
    const always = fakeFetch(['hang', 'hang', 'hang']);
    await assert.rejects(fetchText('https://example.test/b', 'text/plain', { fetch: always.fetch, timeout: 20, delay: noDelay }),
        /no answer within 20 ms for https:\/\/example\.test\/b/);
    assert.equal(always.calls.length, 3);
});

test('a fetch that throws (DNS, reset) is tried again (#179)', async () => {
    const { fetch, calls } = fakeFetch([new TypeError('fetch failed'), new TypeError('fetch failed'), 200]);
    assert.equal(await fetchText('https://example.test/', 'text/plain', { fetch, delay: noDelay }), 'ok');
    assert.equal(calls.length, 3);
    const always = fakeFetch([new TypeError('fetch failed'), new TypeError('fetch failed'), new TypeError('fetch failed')]);
    await assert.rejects(fetchText('https://example.test/', 'text/plain', { fetch: always.fetch, delay: noDelay }), /fetch failed/);
});

test('429 and server errors are tried again, other errors are not (#179)', async () => {
    const tooMany = fakeFetch([429, 503, 200]);
    assert.equal(await fetchText('https://example.test/', 'text/plain', { fetch: tooMany.fetch, delay: noDelay }), 'ok');
    assert.equal(tooMany.calls.length, 3);
    const notFound = fakeFetch([404, 200]);
    await assert.rejects(fetchText('https://example.test/', 'text/plain', { fetch: notFound.fetch, delay: noDelay }), /^Error: 404/);
    assert.equal(notFound.calls.length, 1);
});

test('metadata without what every event has is skipped and tried again (#179)', () => {
    const vortexEntry = list.find(entry => entry.name === 'Vortex48');
    assert.ok(validMetadata(vortex48.standard) && validMetadata(vortex48.elite));
    const without = key => Object.fromEntries(Object.entries(vortex48.standard).filter(([k]) => k !== key));
    for (const bad of [{}, [], null, 'Vortex48', without('map'), without('mode'), without('roundSets'), without('_towers'),
        without('_bloonModifiers'), { ...vortex48.standard, _towers: [] }, { ...vortex48.standard, map: '' },
        { ...vortex48.standard, _bloonModifiers: { ...vortex48.standard._bloonModifiers, healthMultipliers: null } }]) {
        assert.ok(!validMetadata(bad), JSON.stringify(bad).slice(0, 80));
        const { records, skipped } = newRecords([vortexEntry], new Map([[vortexEntry.id, { standard: bad, elite: vortex48.elite }]]));
        assert.deepEqual(records, []);
        assert.match(skipped[0], /no standard metadata yet, tried again next run$/);
    }
});

test('every event in the archive has metadata that passes the check', async () => {
    for (const [id, record] of await readArchive(archiveDir))
        for (const level of ['standard', 'elite']) assert.ok(validMetadata(record[level]), `${id} ${level}`);
});

test('a name with a line break stays on one output line (#179)', () => {
    const line = outputNames(['Vortex48\nevil=1', 'Lych\r56', 'Tab\there x', 'Plain']);
    assert.equal(line, 'Vortex48 evil=1, Lych 56, Tab here x, Plain');
    assert.ok(!/[\n\r]/.test(line));
    assert.equal(outputNames([]), '');
});
