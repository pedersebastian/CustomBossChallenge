// Turns the backfill's sources into the archive's per-event format: the API's event list entry plus the standard and
// Elite rules in the shape of the API's metadata body (https://data.ninjakiwi.com/btd6/bosses/<id>/metadata/standard).
import { templatesNamed } from './wikitext.mjs';

export const formatVersion = 1;

// The rule fields the archive keeps from every source, in the API's order. API files are kept as the API gave them.
const ruleFields = ['name', 'map', 'mode', 'difficulty', 'disableDoubleCash', 'disableInstas', 'disableMK',
    'disablePowers', 'disableSelling', 'disableFastTrack', 'displayIncludedTowers', 'startingCash',
    'abilityCooldownReductionMultiplier', 'leastCashUsed', 'leastTiersUsed', 'noContinues', 'seed',
    'removeableCostMultiplier', 'roundSets', 'lives', 'maxLives', 'startRound', 'endRound', 'maxTowers', 'maxParagons',
    '_bloonModifiers', '_towers'];

// The wiki writes some modes by their name in the game's menus
const modeAliases = { CHIMPS: 'Clicks', 'Double HP MOABs': 'DoubleMoabHealth' };

/**
 * Maps the wiki's names to the game's ids, from web/src/data/game.json (`maps`, `modes`, `towers`). A value that is
 * already an id is kept, and an unknown one is kept as it is and named in `problems`.
 */
export function makeNames(game) {
    const byName = table => new Map(Object.entries(table).flatMap(([id, name]) => [[id, id], [name, id]]));
    const maps = byName(game.maps);
    const modes = byName(game.modes);
    for (const [alias, id] of Object.entries(modeAliases)) modes.set(alias, id);
    const towers = new Set(Object.keys(game.towers));
    return { maps, modes, towers };
}

const isEmpty = value => value === undefined || value === '';
const flag = value => !isEmpty(value) && value !== '0' && value.toLowerCase?.() !== 'false';

// Numbers as the API writes them; empty or missing values get the game's default, junk is kept as text so the mod's
// sanitizing sees it rather than the converter guessing
function number(value, fallback) {
    if (isEmpty(value)) return fallback;
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : value;
}

// customRounds=vortex is roundSets ["default","vortex"] in the API: the API lists the default set first
export function roundSets(sets) {
    const list = (Array.isArray(sets) ? sets : String(sets ?? '').split(',')).map(set => set.trim()).filter(Boolean);
    return list[0] === 'default' ? list : ['default', ...list];
}

/** One {{BTD6 challenge rules}} as an API metadata body. `problems` collects what couldn't be mapped. */
export function wikiRulesToApi(wikitext, eventName, names, problems) {
    const [rules] = templatesNamed(wikitext, 'BTD6 challenge rules');
    if (!rules) return null;
    const p = rules.params;
    const map = names.maps.get(p.map);
    if (!map) problems.push(`${eventName}: unknown map "${p.map}"`);
    const mode = names.modes.get(p.mode);
    if (!mode) problems.push(`${eventName}: unknown mode "${p.mode}"`);
    const towers = templatesNamed(p._towers ?? '', 'BTD6 restrictions').map(({ params }) => {
        if (!names.towers.has(params.tower)) problems.push(`${eventName}: unknown tower "${params.tower}"`);
        return {
            tower: params.tower,
            max: number(params.max, 0),
            path1NumBlockedTiers: number(params.path1NumBlockedTiers, 0),
            path2NumBlockedTiers: number(params.path2NumBlockedTiers, 0),
            path3NumBlockedTiers: number(params.path3NumBlockedTiers, 0),
            isHero: flag(params.isHero),
        };
    });
    return ordered({
        name: eventName,
        map: map ?? p.map,
        mode: mode ?? p.mode,
        difficulty: p.difficulty,
        disableDoubleCash: flag(p.disableDoubleCash),
        disableInstas: flag(p.noInstaReward),
        disableMK: flag(p.disableMK),
        disablePowers: flag(p.disablePowers),
        disableSelling: flag(p.disableSelling),
        disableFastTrack: flag(p.disableFastTrack),
        displayIncludedTowers: flag(p.displayIncludedTowers),
        startingCash: number(p.startingCash, 650),
        abilityCooldownReductionMultiplier: number(p.abilityCooldownReductionMultiplier, 1),
        leastCashUsed: number(p.leastCashUsed, -1),
        leastTiersUsed: number(p.leastTiersUsed, -1),
        noContinues: flag(p.noContinues),
        seed: number(p.seed, 0),
        removeableCostMultiplier: number(p.removeableCostMultiplier, 1),
        roundSets: roundSets(p.customRounds),
        lives: number(p.lives, 200),
        maxLives: number(p.maxLives, -1),
        startRound: number(p.startRound, 1),
        endRound: number(p.endRound, 140),
        maxTowers: number(p.maxTowers, -1),
        maxParagons: number(p.maxParagons, undefined),
        _bloonModifiers: {
            speedMultiplier: number(p.speedMultiplier, 1),
            moabSpeedMultiplier: number(p.moabSpeedMultiplier, 1),
            bossSpeedMultiplier: number(p.bossSpeedMultiplier, 1),
            regrowRateMultiplier: number(p.regrowRateMultiplier, 1),
            healthMultipliers: {
                bloons: number(p.bloonHealthMultiplier, 1),
                moabs: number(p.moabHealthMultiplier, 1),
                boss: number(p.bossHealthMultiplier, 1),
            },
            allCamo: flag(p.allCamo),
            allRegen: flag(p.allRegen),
        },
        // Towers that aren't listed are not allowed, like in the API (which also lists some of those with max 0)
        _towers: towers,
    });
}

/**
 * The game's own challenge model (Ninja Kiwi's BossDataDecrypted files: `normalDcm` / `eliteDcm`) as an API metadata
 * body. Those don't have maxParagons; `fallback` (the wiki's rules for the same event) fills what is missing.
 */
export function dcmToApi(dcm, eventName, fallback) {
    const start = dcm.startRules ?? {};
    const modifiers = dcm.bloonModifiers ?? {};
    const health = modifiers.healthMultipliers ?? {};
    const fallbackModifiers = fallback?._bloonModifiers ?? {};
    return ordered({
        name: eventName,
        map: dcm.map,
        mode: dcm.mode,
        difficulty: dcm.difficulty,
        disableDoubleCash: dcm.disableDoubleCash ?? false,
        disableInstas: dcm.noInstaReward ?? false,
        disableMK: dcm.disableMK ?? false,
        disablePowers: dcm.disablePowers ?? false,
        disableSelling: dcm.disableSelling ?? false,
        disableFastTrack: dcm.disableFastTrack ?? fallback?.disableFastTrack ?? false,
        displayIncludedTowers: dcm.displayIncludedTowers ?? false,
        startingCash: start.cash,
        abilityCooldownReductionMultiplier: dcm.abilityCooldownReductionMultiplier ?? 1,
        leastCashUsed: dcm.leastCashUsed ?? -1,
        leastTiersUsed: dcm.leastTiersUsed ?? -1,
        noContinues: dcm.noContinues ?? false,
        seed: dcm.seed,
        removeableCostMultiplier: dcm.removeableCostMultiplier ?? 1,
        roundSets: roundSets(dcm.roundSets),
        lives: start.lives,
        maxLives: start.maxLives,
        startRound: start.round,
        endRound: start.endRound,
        maxTowers: dcm.maxTowers,
        maxParagons: dcm.maxParagons ?? fallback?.maxParagons,
        _bloonModifiers: {
            speedMultiplier: modifiers.speedMultiplier ?? 1,
            moabSpeedMultiplier: modifiers.moabSpeedMultiplier ?? 1,
            bossSpeedMultiplier: modifiers.bossSpeedMultiplier ?? fallbackModifiers.bossSpeedMultiplier ?? 1,
            regrowRateMultiplier: modifiers.regrowRateMultiplier ?? 1,
            healthMultipliers: {
                bloons: health.bloons ?? 1,
                moabs: health.moabs ?? 1,
                boss: health.boss ?? fallbackModifiers.healthMultipliers?.boss ?? 1,
            },
            allCamo: modifiers.allCamo ?? false,
            allRegen: modifiers.allRegen ?? false,
        },
        _towers: (dcm.towers ?? []).map(tower => ({
            tower: tower.tower,
            max: tower.max,
            path1NumBlockedTiers: tower.path1NumBlockedTiers ?? 0,
            path2NumBlockedTiers: tower.path2NumBlockedTiers ?? 0,
            path3NumBlockedTiers: tower.path3NumBlockedTiers ?? 0,
            isHero: tower.isHero ?? false,
        })),
    });
}

function ordered(rules) {
    const result = {};
    for (const field of ruleFields) if (rules[field] !== undefined) result[field] = rules[field];
    return result;
}

/** The list entry of one {{BTD6 boss event rules}}, in the API's shape (times in milliseconds). */
export function wikiEvent(params) {
    const entry = {
        id: params.id,
        name: params.name,
        bossType: params.bossType,
        start: Number(params.start) * 1000,
        end: Number(params.end) * 1000,
    };
    // Older events have one leaderboard type for both, and the oldest none (they were ranked on time)
    const scoring = params.leaderboardScoringType;
    const normal = params.normalScoringType || scoring;
    const elite = params.eliteScoringType || scoring;
    if (normal) entry.normalScoringType = normal;
    if (elite) entry.eliteScoringType = elite;
    return entry;
}
