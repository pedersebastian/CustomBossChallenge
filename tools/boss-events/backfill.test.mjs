// Tests for the backfill's conversion, on saved samples in fixtures/ (see fixtures/README.md). Run with
//   node --test tools/boss-events/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { templates, templatesNamed } from './wikitext.mjs';
import { dcmToApi, makeNames, roundSets, wikiEvent, wikiRulesToApi } from './convert.mjs';
import { buildRecords, replaces } from './records.mjs';
import { formatRecord } from './format.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const fixture = name => readFileSync(join(here, 'fixtures', name), 'utf8');
const json = name => JSON.parse(fixture(name));
const game = JSON.parse(readFileSync(join(here, '..', '..', 'web', 'src', 'data', 'game.json'), 'utf8'));
const names = makeNames(game);
const wikitext = fixture('bloonswiki-sample.txt');
const wikiEvents = new Map(templatesNamed(wikitext, 'BTD6 boss event rules').map(t => [t.params.name, t.params]));
const urls = { jxuFileUrl: (commit, path) => `jxu/${commit}/${path}`, wikiPageUrl: year => `wiki/${year}` };

function convert(name, level) {
    const problems = [];
    const rules = wikiRulesToApi(wikiEvents.get(name)[`${level} rules`], name, names, problems);
    assert.deepEqual(problems, []);
    return rules;
}

// The API also lists towers that aren't allowed, with max 0; the wiki leaves them out
const allowed = towers => towers.filter(tower => tower.max !== 0);

test('templates nest, and a repeated param keeps its first value', () => {
    const [outer] = templates('x {{A|id=1|inner={{B|id=2|c={{C|d=3}}}}|id=4}} y {{D}}');
    assert.equal(outer.name, 'A');
    assert.equal(outer.params.id, '1');
    assert.equal(outer.params.inner, '{{B|id=2|c={{C|d=3}}}}');
    const [inner] = templates(outer.params.inner);
    assert.equal(inner.params.c, '{{C|d=3}}');
    assert.equal(templates('{{A}} {{D}}').length, 2);
});

test('the sample has the three events', () => {
    assert.deepEqual([...wikiEvents.keys()], ['Vortex10', 'Bloonarius33', 'Dreadbloon32New']);
});

test('the list entry is in the API shape, times in milliseconds', () => {
    const entry = wikiEvent(wikiEvents.get('Dreadbloon32New'));
    const api = json('jxu-Dreadbloon32New-standard.json');
    assert.equal(entry.id, 'Dreadbloon32New_mi9oruss');
    assert.equal(entry.bossType, 'dreadbloon');
    assert.equal(entry.start, 1763776800000);
    assert.equal(api.body.name, entry.name);
    // Older events have one leaderboard type for both difficulties, the oldest none
    assert.equal(wikiEvent({ id: 'x', name: 'x', start: '1', end: '2', leaderboardScoringType: 'LeastCash' })
        .eliteScoringType, 'LeastCash');
    assert.equal(wikiEvent({ id: 'x', name: 'x', start: '1', end: '2' }).normalScoringType, undefined);
});

test("the wiki's rules convert to exactly the API's, apart from its defaults", () => {
    for (const level of ['standard', 'elite']) {
        const wiki = convert('Dreadbloon32New', level === 'standard' ? 'normal' : 'elite');
        const api = json(`jxu-Dreadbloon32New-${level}.json`).body;
        // Display names on the wiki: Three Mines 'Round, CHIMPS
        assert.equal(wiki.map, 'ThreeMinesAround');
        assert.equal(wiki.mode, 'Clicks');
        assert.equal(api.map, wiki.map);
        assert.equal(api.mode, wiki.mode);
        assert.deepEqual(wiki.roundSets, ['default', 'chimpsbloonarius']);
        assert.deepEqual(wiki.roundSets, api.roundSets);
        assert.deepEqual(wiki._bloonModifiers, api._bloonModifiers);
        assert.deepEqual(allowed(wiki._towers).sort(byTower), allowed(api._towers).sort(byTower));
        for (const field of ['difficulty', 'startingCash', 'lives', 'startRound', 'endRound', 'maxParagons', 'seed',
            'disableMK', 'disableSelling', 'disablePowers', 'noContinues', 'leastCashUsed', 'leastTiersUsed'])
            assert.deepEqual(wiki[field], api[field], field);
    }
});

test("the wiki's rules match Ninja Kiwi's own models, path blocks included", () => {
    for (const name of ['Bloonarius33', 'Vortex10']) {
        const dcm = json(`dcm-${name}.json`);
        for (const [level, key] of [['normal', 'normalDcm'], ['elite', 'eliteDcm']]) {
            const wiki = convert(name, level);
            const nk = dcmToApi(dcm[key], name, wiki);
            assert.deepEqual(allowed(nk._towers).sort(byTower), allowed(wiki._towers).sort(byTower));
            const { _towers: a, ...wikiRest } = wiki;
            const { _towers: b, ...nkRest } = nk;
            assert.deepEqual(nkRest, wikiRest);
        }
    }
    const bloonarius = convert('Bloonarius33', 'normal');
    assert.equal(bloonarius.mode, 'DoubleMoabHealth');
    assert.ok(bloonarius._towers.some(tower => tower.path1NumBlockedTiers + tower.path2NumBlockedTiers
        + tower.path3NumBlockedTiers > 0));
    assert.deepEqual(convert('Vortex10', 'normal').roundSets, ['default', 'vortex', 'default1']);
});

test('round sets get the default set first, like the API', () => {
    assert.deepEqual(roundSets('vortex'), ['default', 'vortex']);
    assert.deepEqual(roundSets(['lych']), ['default', 'lych']);
    assert.deepEqual(roundSets(['default', 'lych']), ['default', 'lych']);
    assert.deepEqual(roundSets(''), ['default']);
});

test('an unknown map is kept as it is and reported', () => {
    const problems = [];
    const rules = wikiRulesToApi('{{BTD6 challenge rules|map=Nowhere|mode=Standard}}', 'X', names, problems);
    assert.equal(rules.map, 'Nowhere');
    assert.deepEqual(problems, ['X: unknown map "Nowhere"']);
});

test('records: the API copy wins over the wiki, NK models over the wiki text', () => {
    const jxu = jxuSample();
    const { records } = buildRecords({ wiki: [{ year: 2022, text: wikitext }], jxu, game, urls });
    const byName = new Map(records.map(record => [record.name, record]));
    const dreadbloon = byName.get('Dreadbloon32New');
    assert.equal(dreadbloon.source, 'jxu-api');
    assert.equal(dreadbloon.standard.mapURL, json('jxu-Dreadbloon32New-standard.json').body.mapURL);
    // The sample's list entry has one scoringType, so the wiki's per-difficulty scoring is used, with its licence
    assert.deepEqual(dreadbloon.sourceUrls, ['jxu/old/bosses/Dreadbloon32New_mi9oruss/metadata/standard.json',
        'jxu/head/bosses/Dreadbloon32New_mi9oruss/metadata/elite.json', 'wiki/2022']);
    assert.equal(dreadbloon.license, 'CC BY-NC-SA 4.0');
    assert.equal(dreadbloon.eliteScoringType, wikiEvent(wikiEvents.get('Dreadbloon32New')).eliteScoringType);
    assert.equal(byName.get('Bloonarius33').source, 'nk-dcm');
    assert.equal(byName.get('Bloonarius33').license, 'CC BY-NC-SA 4.0');
    assert.equal(byName.get('Bloonarius33').standard.maxParagons, convert('Bloonarius33', 'normal').maxParagons);
    assert.deepEqual(records.map(record => record.start), records.map(record => record.start).sort((a, b) => a - b));
});

test('records: an event only in the wiki keeps the wiki as its source', () => {
    const jxu = { commit: 'head', files: new Map() };
    const { records } = buildRecords({ wiki: [{ year: 2022, text: wikitext }], jxu, game, urls });
    assert.deepEqual(records.map(record => record.source), ['bloonswiki', 'bloonswiki', 'bloonswiki']);
    assert.deepEqual(records[0].sourceUrls, ['wiki/2022']);
});

test('records: NK models the wiki lacks are dated only by a single free week', () => {
    const week = 7 * 24 * 60 * 60 * 1000;
    const event = (name, start) => `{{BTD6 boss event rules|id=${name}_x|name=${name}|start=${start / 1000}`
        + `|end=${(start + 5 * 24 * 3600 * 1000) / 1000}|bossType=lych|normal rules={{BTD6 challenge rules|map=Cubism|`
        + `mode=Standard}}|elite rules={{BTD6 challenge rules|map=Cubism|mode=Standard}}}}`;
    const t0 = 1_700_000_000_000;
    const text = [event('Lych1', t0), event('Vortex1', t0 + week), event('Lych3', t0 + 3 * week)].join('\n');
    const dcm = json('dcm-Vortex10.json');
    const files = new Map([
        ['BossDataDecrypted/Lych2.json', { commit: 'head', json: dcm }],
        ['BossDataDecrypted/Phayze_2.json', { commit: 'head', json: dcm }],
    ]);
    const { records, report } = buildRecords({ wiki: [{ year: 2023, text }], jxu: { commit: 'head', files }, game, urls });
    const lych2 = records.find(record => record.name === 'Lych2');
    assert.equal(lych2.start, t0 + 2 * week);
    assert.deepEqual(lych2.lossy, ['id', 'dates', 'scoringType', 'maxParagons']);
    assert.ok(!records.some(record => record.name === 'Phayze_2'));
    assert.ok(report.some(line => line.startsWith('Phayze_2:')));
});

test("the archiver's API files are never replaced", () => {
    assert.equal(replaces({ source: 'jxu-api' }, { source: 'api' }), false);
    assert.equal(replaces({ source: 'bloonswiki' }, { source: 'api' }), false);
    assert.equal(replaces({ source: 'jxu-api' }, { source: 'bloonswiki' }), true);
    assert.equal(replaces({ source: 'bloonswiki' }, { source: 'nk-dcm' }), false);
    assert.equal(replaces({ source: 'nk-dcm' }, { source: 'nk-dcm' }), true);
});

test('files read back as the same record', () => {
    const { records } = buildRecords({ wiki: [{ year: 2022, text: wikitext }], jxu: jxuSample(), game, urls });
    for (const record of records) assert.deepEqual(JSON.parse(formatRecord(record)), record);
    assert.ok(formatRecord(records[0]).includes('\n      { "tower": '));
});

const byTower = (a, b) => a.tower.localeCompare(b.tower);

// The jxu archive for the sample: the event list, Dreadbloon32New's metadata (the standard file only good in an older
// commit, like Lych56) and the two NK models
function jxuSample() {
    const standard = json('jxu-Dreadbloon32New-standard.json');
    const entry = {
        id: 'Dreadbloon32New_mi9oruss', name: 'Dreadbloon32New', bossType: 'dreadbloon',
        start: 1763776800000, end: 1764212400000, scoringType: 'GameTime',
    };
    return {
        commit: 'head',
        files: new Map([
            ['bosses/bosses20251127.json', { commit: 'head', json: { success: true, body: [entry] } }],
            ['bosses/Dreadbloon32New_mi9oruss/metadata/standard.json', { commit: 'old', json: standard }],
            ['bosses/Dreadbloon32New_mi9oruss/metadata/elite.json',
                { commit: 'head', json: json('jxu-Dreadbloon32New-elite.json') }],
            ['BossDataDecrypted/Bloonarius33.json', { commit: 'head', json: json('dcm-Bloonarius33.json') }],
            ['BossDataDecrypted/Vortex10.json', { commit: 'head', json: json('dcm-Vortex10.json') }],
        ]),
    };
}
