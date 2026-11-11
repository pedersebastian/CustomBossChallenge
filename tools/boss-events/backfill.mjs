// One-time backfill of the boss events archive (data/boss-events/<id>.json) with the events from before the archiver
// (#144) started. Run by hand; the output is committed:
//
//   node tools/boss-events/backfill.mjs [--dry-run] [--refresh] [--jxu-commit <sha>]
//
// Sources, best first (see data/boss-events/README.md):
//   jxu-api     the API's own metadata, as archived weekly by github.com/jxu/btd6-events since Nov 2024
//   nk-dcm      the game's challenge models Ninja Kiwi gave that archive for 2022-2024, dated with the wiki
//   bloonswiki  Blooncyclopedia's event history pages, 2021-2025
// A file the archiver made from the API (source "api") is never changed.
import { readFile, readdir, writeFile, mkdir } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadJxu, loadWiki, jxuFileUrl, wikiPageUrl } from './sources.mjs';
import { buildRecords, replaces } from './records.mjs';
import { formatRecord } from './format.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const outDir = join(root, 'data', 'boss-events');

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const refresh = args.includes('--refresh');
const commitArg = args.indexOf('--jxu-commit');
const jxuCommit = commitArg >= 0 ? args[commitArg + 1] : undefined;

const game = JSON.parse(await readFile(join(root, 'web', 'src', 'data', 'game.json'), 'utf8'));
const [wiki, jxu] = await Promise.all([loadWiki({ refresh }), loadJxu({ refresh, commit: jxuCommit })]);
const { records, report } = buildRecords({ wiki, jxu, game, urls: { jxuFileUrl, wikiPageUrl } });

await mkdir(outDir, { recursive: true });
const existing = new Map();
for (const name of await readdir(outDir)) {
    if (!name.endsWith('.json')) continue;
    existing.set(name.slice(0, -5), JSON.parse(await readFile(join(outDir, name), 'utf8')));
}

const counts = { written: 0, unchanged: 0, kept: 0 };
for (const record of records) {
    const old = existing.get(record.id);
    if (old && !replaces(record, old)) {
        counts.kept++;
        continue;
    }
    const text = formatRecord(record);
    const path = join(outDir, `${record.id}.json`);
    if (old && formatRecord(old) === text) {
        counts.unchanged++;
        continue;
    }
    counts.written++;
    if (!dryRun) await writeFile(path, text);
}

for (const line of report) console.log(line);
console.log(`${records.length} events: ${counts.written} ${dryRun ? 'would be written' : 'written'}, `
    + `${counts.unchanged} unchanged, ${counts.kept} kept from a better source`);

