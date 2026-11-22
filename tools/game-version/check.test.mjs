// Tests for the game version check, on Steam's answers saved in fixtures/ (see fixtures/README.md). Run with
//   node --test "tools/game-version/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { apply, buildMarker, compareVersions, decide, gitHub, issueBody, label, listIssues, markers, newsVersion,
    publicBuild, readChecklist, readWorksOnVersion, versionMarker } from './check.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const fixture = name => JSON.parse(readFileSync(join(here, 'fixtures', name), 'utf8'));
const url = 'https://github.com/o/r/blob/master/CONTRIBUTING.md';

const build = { id: '26122510', time: 1792634055000 };
const newBuild = { id: '26300000', time: 1793500000000 };
const issue = (number, state, ...markerLines) => ({ number, state, body: `Text\n\n${markerLines.join('\n')}` });

test('newsVersion finds the newest update in the saved news', () => {
    assert.equal(newsVersion(fixture('steam-news.json')), '57.0');
});

test('newsVersion reads the title styles Ninja Kiwi uses and compares as numbers', () => {
    const news = titles => ({ appnews: { newsitems: titles.map(title => ({ title })) } });
    assert.equal(newsVersion(news(['Bloons TD 6 - Update 55.0', 'Update 57.0!', 'Update 56.0!'])), '57.0');
    assert.equal(newsVersion(news(['Update 9.1', 'Update 10.0'])), '10.0');
    assert.equal(newsVersion(news(['update v58.2 is here'])), '58.2');
    assert.equal(newsVersion(news(['Social Seasons Is Live!', 'Plushies'])), null);
    assert.equal(newsVersion(news(['Updated 58.0', 'Update 58'])), null);
    assert.equal(newsVersion({}), null);
    assert.equal(newsVersion(null), null);
    assert.equal(newsVersion({ appnews: { newsitems: [null, { title: 5 }] } }), null);
});

test('publicBuild reads the public branch and nothing else', () => {
    assert.deepEqual(publicBuild(fixture('steamcmd-info.json')), build);
    const answer = (id, status = 'success') => ({ status, data: { 960090: { depots: { branches: { public: { buildid: id } } } } } });
    assert.deepEqual(publicBuild(answer('123')), { id: '123', time: null });
    assert.equal(publicBuild(answer('123', 'error')), null);
    assert.equal(publicBuild(answer('12a')), null);
    assert.equal(publicBuild(answer('1\n<!-- btd6-version: 99.0 -->')), null);
    assert.equal(publicBuild(answer(123)), null);
    assert.equal(publicBuild({ status: 'success', data: {} }), null);
});

test('compareVersions compares each part as a number', () => {
    assert.ok(compareVersions('57.10', '57.4') > 0);
    assert.ok(compareVersions('57.0', '57.4') < 0);
    assert.ok(compareVersions('58.0', '57.4') > 0);
    assert.equal(compareVersions('57.4', '57.4'), 0);
});

test('readWorksOnVersion reads ModHelperData.cs', () => {
    assert.equal(readWorksOnVersion('public const string WorksOnVersion = "57.4";'), '57.4');
    assert.equal(readWorksOnVersion('public const string Version = "2.14.0";'), null);
    assert.match(readWorksOnVersion(readFileSync(join(root, 'ModHelperData.cs'), 'utf8')), /^\d+\.\d+$/);
});

test('readChecklist turns the numbered list into tasks and stops at the next heading', () => {
    const md = ['# Contributing', '## Updating for a new BTD6 version', '', 'Intro.', '', '1. First step,',
        '   continued.', '2. See [Releases](#releases).', '', '## Website', '1. Not a task'].join('\r\n');
    assert.equal(readChecklist(md, url),
        `- [ ] First step,\n  continued.\n- [ ] See [Releases](${url}#releases).`);
    assert.equal(readChecklist('# Contributing\n## Website\n1. x', url), null);
    assert.equal(readChecklist('## Updating for a new BTD6 version\n\nNo list.\n## Next', url), null);
});

test('the real CONTRIBUTING.md has the checklist', () => {
    const checklist = readChecklist(readFileSync(join(root, 'CONTRIBUTING.md'), 'utf8'), url);
    assert.ok(checklist, 'CONTRIBUTING.md needs a numbered list under "## Updating for a new BTD6 version"');
    const tasks = checklist.split('\n').filter(line => line.startsWith('- [ ] '));
    assert.ok(tasks.length >= 5, checklist);
    assert.ok(checklist.includes('WorksOnVersion'));
    assert.doesNotMatch(checklist, /\]\(#/, 'links to headings must point at GitHub');
});

test('markers reads only well-formed markers', () => {
    assert.deepEqual(markers(`a ${versionMarker('58.0')} ${buildMarker('1')}\n${buildMarker('2')}`),
        { versions: ['58.0'], builds: ['1', '2'] });
    assert.deepEqual(markers('<!-- btd6-version: x -->'), { versions: [], builds: [] });
    assert.deepEqual(markers(null), { versions: [], builds: [] });
});

test('decide: the first run makes a closed baseline issue', () => {
    const action = decide({ news: '57.0', build, worksOn: '57.4', issues: [] });
    assert.equal(action.type, 'create');
    assert.equal(action.kind, 'baseline');
    assert.equal(action.close, true);
    assert.deepEqual(action.builds, [build.id]);
});

test('decide: a newer version on the first run opens its issue, not a baseline', () => {
    const action = decide({ news: '58.0', build, worksOn: '57.4', issues: [] });
    assert.deepEqual(action, { type: 'create', title: 'BTD6 58.0 is out', kind: 'version', version: '58.0', builds: [build.id] });
});

test('decide: nothing new', () => {
    const issues = [issue(5, 'closed', buildMarker(build.id))];
    assert.deepEqual(decide({ news: '57.0', build, worksOn: '57.4', issues }), { type: 'none' });
});

test('decide: a new version opens one issue, and never a second, also after it was closed', () => {
    const issues = [issue(5, 'closed', buildMarker(build.id))];
    const action = decide({ news: '58.0', build: newBuild, worksOn: '57.4', issues });
    assert.equal(action.title, 'BTD6 58.0 is out');
    assert.deepEqual(action.builds, [newBuild.id]);
    const later = [issue(6, 'closed', versionMarker('58.0'), buildMarker(newBuild.id)), ...issues];
    assert.deepEqual(decide({ news: '58.0', build: newBuild, worksOn: '57.4', issues: later }), { type: 'none' });
});

test('decide: a version the mod already supports opens nothing', () => {
    const issues = [issue(5, 'closed', buildMarker(build.id))];
    assert.deepEqual(decide({ news: '58.0', build, worksOn: '58.0', issues }), { type: 'none' });
    assert.deepEqual(decide({ news: null, build, worksOn: '58.0', issues }), { type: 'none' });
});

test('decide: a new build without news opens a build issue', () => {
    const issues = [issue(5, 'closed', buildMarker(build.id))];
    const action = decide({ news: '57.0', build: newBuild, worksOn: '57.4', issues });
    assert.deepEqual(action, { type: 'create', title: `New BTD6 build on Steam (${newBuild.id})`, kind: 'build',
        version: null, builds: [newBuild.id] });
});

test('decide: the news after the build renames the build issue', () => {
    const issues = [issue(7, 'open', buildMarker(newBuild.id)), issue(5, 'closed', buildMarker(build.id))];
    const action = decide({ news: '58.0', build: newBuild, worksOn: '57.4', issues });
    assert.equal(action.type, 'update');
    assert.equal(action.number, 7);
    assert.equal(action.title, 'BTD6 58.0 is out');
    assert.ok(action.append.includes(versionMarker('58.0')));
});

test('decide: a closed build issue is not renamed, the version gets its own', () => {
    const issues = [issue(7, 'closed', buildMarker(newBuild.id))];
    const action = decide({ news: '58.0', build: newBuild, worksOn: '57.4', issues });
    assert.equal(action.type, 'create');
    assert.deepEqual(action.builds, []);
});

test('decide: a hotfix while an update is open goes into that issue', () => {
    const issues = [issue(8, 'open', versionMarker('58.0'), buildMarker(newBuild.id))];
    const hotfix = { id: '26400000', time: null };
    const action = decide({ news: '58.0', build: hotfix, worksOn: '57.4', issues });
    assert.equal(action.type, 'update');
    assert.equal(action.number, 8);
    assert.equal(action.title, undefined);
    assert.ok(action.append.includes(buildMarker(hotfix.id)));
    // Seen now: nothing the next day
    const after = [{ ...issues[0], body: `${issues[0].body}\n${action.append}` }];
    assert.deepEqual(decide({ news: '58.0', build: hotfix, worksOn: '57.4', issues: after }), { type: 'none' });
});

test('issueBody has the checklist and the markers', () => {
    const checklist = '- [ ] One\n- [ ] Two';
    const context = { worksOn: '57.4', news: '58.0', build: newBuild, checklist, contributingUrl: url };
    const body = issueBody({ kind: 'version', version: '58.0', builds: [newBuild.id] }, context);
    assert.ok(body.includes(checklist));
    assert.ok(body.includes('**BTD6 58.0**'));
    assert.deepEqual(markers(body), { versions: ['58.0'], builds: [newBuild.id] });
    const buildBody = issueBody({ kind: 'build', version: null, builds: [newBuild.id] }, { ...context, news: '57.0' });
    assert.ok(buildBody.includes('probably a hotfix'));
    assert.ok(buildBody.includes(checklist));
    const baseline = issueBody({ kind: 'baseline', version: null, builds: [build.id] }, { ...context, build });
    assert.ok(!baseline.includes(checklist));
    assert.deepEqual(markers(baseline), { versions: [], builds: [build.id] });
});

/** A fake GitHub: records the calls, answers from `answers` by "METHOD path" */
function fakeGitHub(answers) {
    const calls = [];
    const fetch = async (requestUrl, options) => {
        const path = requestUrl.replace('https://api.github.com/repos/o/r', '');
        const key = `${options.method} ${path}`;
        calls.push({ key, body: options.body ? JSON.parse(options.body) : undefined, auth: options.headers.Authorization });
        const answer = answers[key] ?? { status: 200, json: {} };
        return { ok: answer.status < 300, status: answer.status, statusText: 'x', text: async () => JSON.stringify(answer.json) };
    };
    return { calls, call: gitHub({ token: 't', repo: 'o/r', fetch }) };
}

test('listIssues pages through every issue with the label and leaves out pull requests', async () => {
    const page1 = Array.from({ length: 100 }, (_, i) => ({ number: i + 1, state: 'closed', body: '' }));
    const { calls, call } = fakeGitHub({
        [`GET /issues?state=all&labels=${label}&per_page=100&page=1`]: { status: 200, json: page1 },
        [`GET /issues?state=all&labels=${label}&per_page=100&page=2`]: { status: 200, json: [{ number: 101, pull_request: {} }, { number: 102 }] },
    });
    const issues = await listIssues(call);
    assert.equal(issues.length, 101);
    assert.equal(calls.length, 2);
    assert.equal(calls[0].auth, 'Bearer t');
});

test('apply creates the label if needed, the issue, and closes a baseline', async () => {
    const { calls, call } = fakeGitHub({
        'POST /labels': { status: 422, json: { message: 'already_exists' } },
        'POST /issues': { status: 201, json: { number: 9 } },
    });
    const line = await apply(call, { type: 'create', title: 'T', close: true }, 'B');
    assert.deepEqual(calls.map(c => c.key), ['POST /labels', 'POST /issues', 'PATCH /issues/9']);
    assert.deepEqual(calls[1].body, { title: 'T', body: 'B', labels: [label] });
    assert.equal(calls[2].body.state, 'closed');
    assert.match(line, /#9/);
});

test('apply fails on other errors', async () => {
    const { call } = fakeGitHub({ 'POST /labels': { status: 403, json: { message: 'no' } } });
    await assert.rejects(apply(call, { type: 'create', title: 'T' }, 'B'), /403/);
});

test('apply updates an issue by adding to its body', async () => {
    const { calls, call } = fakeGitHub({ 'GET /issues/7': { status: 200, json: { body: 'Old' } } });
    await apply(call, { type: 'update', number: 7, title: 'BTD6 58.0 is out', append: 'New' }, null);
    assert.deepEqual(calls[1], { key: 'PATCH /issues/7', body: { title: 'BTD6 58.0 is out', body: 'Old\n\nNew\n' }, auth: 'Bearer t' });
});

test('apply does nothing for none', async () => {
    const { calls, call } = fakeGitHub({});
    assert.equal(await apply(call, { type: 'none' }, null), 'Nothing new');
    assert.equal(calls.length, 0);
});
