// The game version check (#272): opens an issue with the update checklist when BTD6 gets a new version, so an update
// is noticed the same day and not when players report that the mod broke. Runs every day in
// .github/workflows/game-version.yml, and locally too:
//
//   node tools/game-version/check.mjs --dry-run    print what it would do, without a token
//
// Two public sources, neither needs a login or a key:
// - Steam's news for the game: Ninja Kiwi posts "Update 57.0!" for every major update, compared with WorksOnVersion in
//   ModHelperData.cs. Hotfixes (57.1, 57.2, ...) get no post.
// - The public branch's build id from steamcmd.net, which changes with every update, hotfixes included, but says
//   nothing about the version.
// What has been seen is kept in the issues themselves (hidden markers, label game-update), so the workflow needs no
// write access to the repository, and a version or build never opens a second issue, also after it was closed.
import { readFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fetchText } from '../boss-events/sources.mjs';

export const appId = '960090';
export const newsUrl = `https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=${appId}&count=20&maxlength=1`
    + '&feeds=steam_community_announcements';
export const buildUrl = `https://api.steamcmd.net/v1/info/${appId}`;
export const label = 'game-update';
export const checklistHeading = '## Updating for a new BTD6 version';
export const checklistAnchor = 'updating-for-a-new-btd6-version';

const versionTitle = /\bUpdate\s+v?(\d{1,4})\.(\d{1,4})\b/i;
const versionText = /^\d{1,4}\.\d{1,4}$/;
const buildText = /^\d{1,12}$/;

/** The highest version named in a title like "Update 57.0!" in Steam's news, or null. */
export function newsVersion(json) {
    const items = json?.appnews?.newsitems;
    if (!Array.isArray(items)) return null;
    let best = null;
    for (const item of items) {
        const match = typeof item?.title === 'string' && versionTitle.exec(item.title);
        if (!match) continue;
        const version = `${Number(match[1])}.${Number(match[2])}`;
        if (!best || compareVersions(version, best) > 0) best = version;
    }
    return best;
}

/** The public branch's build id and when it was built (ms), or null if the answer doesn't have one. */
export function publicBuild(json) {
    const branch = json?.status === 'success' ? json.data?.[appId]?.depots?.branches?.public : null;
    const id = branch?.buildid;
    if (typeof id !== 'string' || !buildText.test(id)) return null;
    const seconds = Number(branch.timebuildupdated);
    return { id, time: Number.isFinite(seconds) && seconds > 0 ? seconds * 1000 : null };
}

/** Compares "57.10" and "57.4" part by part as numbers: < 0, 0 or > 0. */
export function compareVersions(a, b) {
    const pa = a.split('.').map(Number);
    const pb = b.split('.').map(Number);
    for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
        const diff = (pa[i] ?? 0) - (pb[i] ?? 0);
        if (diff) return diff;
    }
    return 0;
}

/** WorksOnVersion from the text of ModHelperData.cs, or null. */
export function readWorksOnVersion(csText) {
    const match = /\bWorksOnVersion\s*=\s*"(\d{1,4}\.\d{1,4})"/.exec(csText);
    return match ? match[1] : null;
}

/**
 * The numbered list under "## Updating for a new BTD6 version" in CONTRIBUTING.md as task list lines ("- [ ] ..."),
 * with the lines that continue an item. Links to a heading of CONTRIBUTING.md (#...) point at the file on GitHub.
 */
export function readChecklist(mdText, contributingUrl) {
    const lines = mdText.split(/\r?\n/);
    const start = lines.findIndex(line => line.trim() === checklistHeading);
    if (start < 0) return null;
    const tasks = [];
    for (const line of lines.slice(start + 1)) {
        if (/^#{1,2} /.test(line)) break;
        const item = /^\d+\.\s+(.*)$/.exec(line);
        if (item) tasks.push(`- [ ] ${item[1]}`);
        else if (tasks.length && /^\s{2,}\S/.test(line)) tasks[tasks.length - 1] += `\n  ${line.trim()}`;
        else if (tasks.length && line.trim() !== '') break;
    }
    if (!tasks.length) return null;
    return tasks.join('\n').replace(/\]\(#([a-z0-9-]+)\)/g, `](${contributingUrl}#$1)`);
}

/** The versions and build ids in an issue's hidden markers. */
export function markers(body) {
    const text = typeof body === 'string' ? body : '';
    const all = re => [...text.matchAll(re)].map(match => match[1]);
    return {
        versions: all(/<!-- btd6-version: (\d{1,4}\.\d{1,4}) -->/g),
        builds: all(/<!-- steam-build: (\d{1,12}) -->/g),
    };
}

export const versionMarker = version => `<!-- btd6-version: ${version} -->`;
export const buildMarker = build => `<!-- steam-build: ${build} -->`;
export const versionIssueTitle = version => `BTD6 ${version} is out`;
export const buildIssueTitle = build => `New BTD6 build on Steam (${build})`;

const day = time => time ? new Date(time).toISOString().slice(0, 10) : 'an unknown day';

/**
 * What to do, from what the sources say and the issues with the label (state all, newest first, as the API lists
 * them). One action at most, so an update opens exactly one issue:
 * - news: the version in Steam's news, or null; build: { id, time } of the public branch
 * - returns { type: 'none' | 'create' | 'update', ... }; 'create' with close: true is the baseline of the first run.
 */
export function decide({ news, build, worksOn, issues }) {
    const seen = issues.map(issue => ({ issue, ...markers(issue.body) }));
    const seenVersion = version => seen.some(entry => entry.versions.includes(version));
    const seenBuild = id => seen.some(entry => entry.builds.includes(id));
    const open = seen.filter(entry => entry.issue.state === 'open');

    if (news && compareVersions(news, worksOn) > 0 && !seenVersion(news)) {
        // The new build usually comes before the news post: its issue becomes this version's
        const buildIssue = open.find(entry => !entry.versions.length && entry.builds.includes(build.id));
        if (buildIssue) {
            return {
                type: 'update', number: buildIssue.issue.number, title: versionIssueTitle(news),
                append: `Steam's news now says it is **BTD6 ${news}**.\n\n${versionMarker(news)}`,
            };
        }
        return {
            type: 'create', title: versionIssueTitle(news), kind: 'version', version: news,
            builds: seenBuild(build.id) ? [] : [build.id],
        };
    }
    if (!issues.length) {
        // First run: what is live now is what the mod was checked on, nothing to do
        return { type: 'create', title: `BTD6 version check started (build ${build.id})`, kind: 'baseline', close: true,
            version: news && !seenVersion(news) ? news : null, builds: [build.id] };
    }
    if (seenBuild(build.id)) return { type: 'none' };
    // A hotfix while an update's issue is still open goes into that issue
    if (open.length) {
        return {
            type: 'update', number: open[0].issue.number,
            append: `Another Steam build came out: **${build.id}**, built ${day(build.time)}. Check the version on the `
                + `title screen again before closing this.\n\n${buildMarker(build.id)}`,
        };
    }
    return { type: 'create', title: buildIssueTitle(build.id), kind: 'build', version: null, builds: [build.id] };
}

/** The body of a new issue. */
export function issueBody(action, { worksOn, news, build, checklist, contributingUrl }) {
    const sources = `Sources: [Steam news](https://store.steampowered.com/news/app/${appId}) (newest update `
        + `${news ?? 'not found'}) and the public branch on [SteamDB](https://steamdb.info/app/${appId}/depots/) `
        + `(build ${build.id}, built ${day(build.time)}).`;
    const markerLines = [action.version ? versionMarker(action.version) : null, ...action.builds.map(buildMarker)]
        .filter(Boolean).join('\n');
    const footer = `\n\nOpened by \`.github/workflows/game-version.yml\` (#272).\n\n${markerLines}\n`;
    if (action.kind === 'baseline') {
        return `The game version check ran for the first time. This is the build that was live then; the mod `
            + `supports BTD6 ${worksOn} (\`WorksOnVersion\`), so there is nothing to do. Later builds and versions are `
            + `compared with this one.\n\n${sources}${footer}`;
    }
    const intro = action.kind === 'version'
        ? `Steam's news says **BTD6 ${action.version}** is out. \`WorksOnVersion\` in \`ModHelperData.cs\` is `
            + `${worksOn}.`
        : `The public Steam branch has a new build, **${build.id}**, built ${day(build.time)}. Steam's news has no new `
            + `update (newest ${news ?? 'not found'}, \`WorksOnVersion\` ${worksOn}), so it is probably a hotfix. Check `
            + `the version on the game's title screen: if it is still ${worksOn}, close this.`;
    return `${intro}\n\n${sources}\n\n## Checklist\n\nFrom [Updating for a new BTD6 version](${contributingUrl}#`
        + `${checklistAnchor}) in CONTRIBUTING.md:\n\n${checklist}${footer}`;
}

/** Calls GitHub's REST API with the token; `fetch` is for the tests. */
export function gitHub({ token, repo, fetch = globalThis.fetch }) {
    return async function call(method, path, body) {
        const response = await fetch(`https://api.github.com/repos/${repo}${path}`, {
            method,
            headers: {
                Accept: 'application/vnd.github+json',
                Authorization: `Bearer ${token}`,
                'X-GitHub-Api-Version': '2022-11-28',
                'User-Agent': 'CustomBossChallenge-game-version',
                ...(body ? { 'Content-Type': 'application/json' } : {}),
            },
            body: body ? JSON.stringify(body) : undefined,
            signal: AbortSignal.timeout(30_000),
        });
        const text = await response.text();
        if (!response.ok) {
            const error = new Error(`${response.status} ${response.statusText} for ${method} ${path}: ${text.slice(0, 200)}`);
            error.status = response.status;
            throw error;
        }
        return text ? JSON.parse(text) : null;
    };
}

/** Every issue with the label, open and closed, newest first (pull requests left out). */
export async function listIssues(call) {
    const issues = [];
    for (let page = 1; ; page++) {
        const batch = await call('GET', `/issues?state=all&labels=${label}&per_page=100&page=${page}`);
        issues.push(...batch.filter(issue => !issue.pull_request));
        if (batch.length < 100) return issues;
    }
}

/** Carries out the action; returns a line for the log. */
export async function apply(call, action, body) {
    if (action.type === 'none') return 'Nothing new';
    if (action.type === 'update') {
        const issue = await call('GET', `/issues/${action.number}`);
        await call('PATCH', `/issues/${action.number}`, {
            ...(action.title ? { title: action.title } : {}),
            body: `${issue.body ?? ''}\n\n${action.append}\n`,
        });
        return `Updated #${action.number}${action.title ? `: ${action.title}` : ''}`;
    }
    try {
        await call('POST', '/labels', { name: label, color: '5319E7', description: 'A new BTD6 version or build' });
    } catch (error) {
        if (error.status !== 422) throw error; // 422: the label exists
    }
    const issue = await call('POST', '/issues', { title: action.title, body, labels: [label] });
    if (action.close) await call('PATCH', `/issues/${issue.number}`, { state: 'closed', state_reason: 'completed' });
    return `Opened #${issue.number}: ${action.title}${action.close ? ' (closed, baseline)' : ''}`;
}

async function main(args) {
    const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
    const dryRun = args.includes('--dry-run');
    const repo = process.env.GITHUB_REPOSITORY || 'pedersebastian/CustomBossChallenge';
    const contributingUrl = `https://github.com/${repo}/blob/master/CONTRIBUTING.md`;

    const worksOn = readWorksOnVersion(await readFile(join(root, 'ModHelperData.cs'), 'utf8'));
    if (!worksOn) throw new Error('No WorksOnVersion in ModHelperData.cs');
    const checklist = readChecklist(await readFile(join(root, 'CONTRIBUTING.md'), 'utf8'), contributingUrl);
    if (!checklist) throw new Error(`No numbered list under "${checklistHeading}" in CONTRIBUTING.md`);

    const parse = (text, url) => {
        try {
            return JSON.parse(text);
        } catch {
            throw new Error(`${url} didn't answer JSON`);
        }
    };
    const news = newsVersion(parse(await fetchText(newsUrl, 'application/json'), newsUrl));
    const build = publicBuild(parse(await fetchText(buildUrl, 'application/json'), buildUrl));
    if (!build) throw new Error(`${buildUrl} has no build id for the public branch`);
    console.log(`WorksOnVersion ${worksOn}, Steam news ${news ?? 'has no update'}, public build ${build.id} `
        + `(${day(build.time)})`);

    const token = process.env.GITHUB_TOKEN;
    if (dryRun && !token) {
        const action = decide({ news, build, worksOn, issues: [] });
        console.log(`Dry run without a token, as if no issue had the label yet: ${JSON.stringify(action)}`);
        return;
    }
    if (!token) throw new Error('GITHUB_TOKEN is needed (or pass --dry-run)');
    const call = gitHub({ token, repo });
    const action = decide({ news, build, worksOn, issues: await listIssues(call) });
    if (dryRun) {
        console.log(`Dry run: ${JSON.stringify(action)}`);
        return;
    }
    const body = action.type === 'create' ? issueBody(action, { worksOn, news, build, checklist, contributingUrl }) : null;
    console.log(await apply(call, action, body));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        await main(process.argv.slice(2));
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
