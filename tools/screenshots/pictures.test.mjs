// Checks that pictures.json, which Make-Screenshots.ps1 makes the README's pictures from (#269), covers every picture
// the README uses and nothing else. Run with
//   node --test "tools/screenshots/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const manifest = JSON.parse(readFileSync(join(here, 'pictures.json'), 'utf8'));
const names = manifest.pictures.map(picture => picture.name);
const readme = readFileSync(join(root, 'README.md'), 'utf8');
const used = [...readme.matchAll(/\]\(docs\/([\w-]+)\.jpg\)/g)].map(match => match[1]);

test('every picture the README uses is made by the script', () => {
    assert.ok(used.length > 0, 'The README has pictures in docs/');
    assert.deepEqual(used.filter(name => !names.includes(name)), []);
});

test('the script only makes pictures the README uses, each once', () => {
    assert.deepEqual(names.filter(name => !used.includes(name)), []);
    assert.equal(new Set(names).size, names.length);
});

test('every picture in docs/ is one the script makes', () => {
    const pictures = readdirSync(join(root, 'docs')).filter(file => file.endsWith('.jpg')).map(file => file.slice(0, -4));
    assert.deepEqual(pictures.filter(name => !names.includes(name)), []);
    for (const name of names) assert.ok(existsSync(join(root, 'docs', `${name}.jpg`)), `docs/${name}.jpg`);
});

test('the screenshots are taken at a 16:9 size', () => {
    const [width, height] = manifest.resolution.split('x').map(Number);
    assert.equal(width * 9, height * 16);
});

test('every picture is made in one way only, with sane numbers', () => {
    for (const picture of manifest.pictures) {
        const ways = ['crop', 'region', 'tierLook'].filter(way => picture[way]);
        assert.ok(ways.length <= 1, `${picture.name}: ${ways.join(' and ')}`);
        if (picture.tolerance !== undefined) assert.ok(picture.tolerance > 0 && picture.tolerance < 0.3, `${picture.name}'s tolerance`);
        if (picture.crop) {
            const { x, y, width, height } = picture.crop;
            assert.ok(x >= 0 && y >= 0 && width > 0 && height > 0 && x + width <= 1280 && y + height <= 720, `${picture.name}'s crop`);
        }
        if (picture.tierLook) {
            assert.ok(picture.tierLook.bosses.length > 0 && picture.tierLook.tiers.every(tier => tier >= 5 && tier <= 10), `${picture.name}'s tiers`);
        }
    }
});
