// The Boss Creator's Random button in the Studio. The boss part is a port of Config/BossRandomizer.cs: each option has
// a chance to change and otherwise keeps its default, with values close to the normal game more likely than extreme
// ones. The game's own randomizer for the Challenge Editor's rules isn't part of the mod, so the Studio only picks a
// map, a difficulty and a mode, and keeps the other rules a new challenge has. The name comes from randomNames.ts,
// like BossMode.RandomName.

import {
  BOSSES,
  bossOf,
  defaultName,
  defaultOptions,
  effectiveSpawnRounds,
  entryOf,
  isMixed,
  normalized,
  tierSkullPercents,
  type BossOptions,
} from "@cbc/share-code";
import { FOLLOWS, NORMAL, RANDOM, TIER_BOSSES, formatTierBosses, type TierBoss } from "@cbc/share-code/tier-bosses";
import { MAX_TIERS, MIN_TIERS, PER_TIER_STEP, VANILLA_TIERS } from "@cbc/share-code/tiers";
import { EXAMPLES, GAME, type Json, type JsonObject } from "../data/types";
import { NO_LIMIT } from "../math/paragons";
import { ALL_NAMES, randomName, type NameTraits } from "./randomNames";
import type { StudioSetup } from "./state";

export const ELITE_CHANCE = 0.3;
export const SPEED_CHANCE = 0.4;
export const HEALTH_CHANCE = 0.4;
export const FORTIFIED_CHANCE = 0.15;
export const PARAGON_CHANCE = 0.2;
export const SPEED_PER_TIER_CHANCE = 0.4;
export const HEALTH_PER_TIER_CHANCE = 0.4;
export const SKULLS_CHANCE = 0.3;

/** Boss speed and health range in percent; values near 100 % are the most likely, see percentWeight */
export const MIN_PERCENT = 50;
export const MAX_PERCENT = 400;
export const PERCENT_STEP = 10;
/** How far from 100 % a boss speed or health still is about half as likely as 100 % itself */
export const PERCENT_SPREAD = 40;
/** The spread of the number of tiers around the normal 5, like a bell curve */
export const TIER_SPREAD = 2;

/** In Advanced Mode: the chance that a tier is Random, keeps the boss before it, and that tier 1 or 10 is Elite */
export const RANDOM_TIER_CHANCE = 0.1;
export const SAME_BOSS_CHANCE = 0.2;
export const FIRST_TIER_ELITE_CHANCE = 0.15;
export const LAST_TIER_ELITE_CHANCE = 0.5;

export const MAX_RANDOM_PARAGONS = 3;
export const MAX_RANDOM_SPEED_PER_TIER = 25;
export const MAX_RANDOM_HEALTH_PER_TIER = 50;

/** A random number from 0 up to 1, like Math.random; tests pass a seeded one */
export type Rng = () => number;

/** How likely a boss speed or health is, compared with 100 %: 1 at 100 %, about 0.94 at 110 % and 0.02 at 400 % */
export function percentWeight(percent: number): number {
  const distance = (percent - 100) / PERCENT_SPREAD;
  return 1 / (1 + distance * distance);
}

/** How likely a number of tiers is, compared with the normal 5: 1 at 5, about 0.88 at 4 and 6, 0.04 at 10 */
export function tierWeight(tiers: number): number {
  const distance = (tiers - VANILLA_TIERS) / TIER_SPREAD;
  return Math.exp((-distance * distance) / 2);
}

const range = (from: number, count: number): number[] => Array.from({ length: count }, (_, i) => from + i);

/** System.Random.Next(max), or Next(min, max) with two numbers */
const next = (rng: Rng, a: number, b?: number): number => (b === undefined ? Math.floor(rng() * a) : a + Math.floor(rng() * (b - a)));

const chance = (rng: Rng, odds: number): boolean => rng() < odds;

/** One of the values, each as likely as its weight */
function weighted(rng: Rng, values: number[], weight: (value: number) => number): number {
  let pick = rng() * values.reduce((sum, value) => sum + weight(value), 0);
  for (const value of values) {
    pick -= weight(value);
    if (pick < 0) return value;
  }
  return values[values.length - 1];
}

/** A boss speed or health from 50 to 400 % in steps of 10, picked by percentWeight */
export const weightedPercent = (rng: Rng): number =>
  weighted(rng, range(0, (MAX_PERCENT - MIN_PERCENT) / PERCENT_STEP + 1).map((i) => MIN_PERCENT + i * PERCENT_STEP), percentWeight);

/** A number of tiers from 1 to 10, picked by tierWeight */
export const weightedTiers = (rng: Rng): number => weighted(rng, range(MIN_TIERS, MAX_TIERS - MIN_TIERS + 1), tierWeight);

/** A random multiple of by, from min to max */
const step = (rng: Rng, min: number, max: number, by: number): number => min + next(rng, (max - min) / by + 1) * by;

/** Two to four different skulls between 10 and 90 %, e.g. "85, 50, 15" */
function randomSkulls(rng: Rng): string {
  const count = next(rng, 2, 5);
  const pool = range(2, 17).map((i) => i * 5);
  for (let i = pool.length - 1; i > 0; i--) {
    const j = next(rng, i + 1);
    [pool[i], pool[j]] = [pool[j], pool[i]];
  }
  return pool
    .slice(0, count)
    .sort((a, b) => b - a)
    .join(", ");
}

/** The tier with its own skulls now and then: evenly spaced or a custom list, as likely as each other */
function randomSkullsOf(rng: Rng, tier: TierBoss): TierBoss {
  if (!chance(rng, SKULLS_CHANCE)) return tier;
  return next(rng, 2) === 0
    ? { ...tier, skulls: "EvenlySpaced", skullCount: next(rng, 0, 7) }
    : { ...tier, skulls: "Custom", customSkulls: randomSkulls(rng) };
}

/**
 * A boss for each of the 10 tiers, so changing the number of tiers later keeps them. A tier is Random now and then,
 * rarely has the same boss as the tier before it, and is Elite more often the later it comes.
 */
export function randomTierBosses(rng: Rng): string {
  const tiers: TierBoss[] = [];
  let previous: string | null = null;
  for (let tier = 1; tier <= MAX_TIERS; tier++) {
    const eliteChance = FIRST_TIER_ELITE_CHANCE + ((LAST_TIER_ELITE_CHANCE - FIRST_TIER_ELITE_CHANCE) * (tier - 1)) / (MAX_TIERS - 1);
    const elite = chance(rng, eliteChance);
    const speed = chance(rng, SPEED_CHANCE) ? weightedPercent(rng) : NORMAL;
    const health = chance(rng, HEALTH_CHANCE) ? weightedPercent(rng) : NORMAL;
    const values = randomSkullsOf(rng, { ...FOLLOWS, elite, speed, health });
    if (chance(rng, RANDOM_TIER_CHANCE)) {
      tiers.push({ ...values, boss: RANDOM });
      continue;
    }
    const others = TIER_BOSSES.filter((boss) => boss !== previous);
    const boss: string = previous !== null && chance(rng, SAME_BOSS_CHANCE) ? previous : others[next(rng, others.length)];
    tiers.push({ ...values, boss });
    previous = boss;
  }
  return formatTierBosses(tiers);
}

const isObject = (value: Json | undefined): value is JsonObject => typeof value === "object" && value !== null && !Array.isArray(value);

function objectAt(parent: JsonObject, key: string): JsonObject {
  const value = parent[key];
  if (isObject(value)) return value;
  const made: JsonObject = {};
  parent[key] = made;
  return made;
}

/**
 * New random boss options, with the boss values in the challenge randomized to match (BossRandomizer.Randomize).
 * Fixes what a boss game can't use: an unsupported mode and a start round after the first tier. Changes the challenge.
 */
export function randomizeBoss(challenge: JsonObject, rng: Rng, advanced: boolean): BossOptions {
  let options: BossOptions = {
    ...defaultOptions(),
    Boss: BOSSES[next(rng, BOSSES.length)],
    Elite: chance(rng, ELITE_CHANCE),
    Tiers: weightedTiers(rng),
  };
  if (advanced) options = { ...options, TierBosses: randomTierBosses(rng) };
  if (chance(rng, SPEED_PER_TIER_CHANCE)) {
    options = { ...options, SpeedPerTier: step(rng, PER_TIER_STEP, MAX_RANDOM_SPEED_PER_TIER, PER_TIER_STEP) };
  }
  if (chance(rng, HEALTH_PER_TIER_CHANCE)) {
    options = { ...options, HealthPerTier: step(rng, PER_TIER_STEP, MAX_RANDOM_HEALTH_PER_TIER, PER_TIER_STEP) };
  }
  // In Advanced every tier has its own skulls, and the Skulls option stays Vanilla
  if (!advanced) {
    const skulls = randomSkullsOf(rng, FOLLOWS);
    options = { ...options, SkullMode: skulls.skulls, SkullCount: skulls.skullCount, CustomSkulls: skulls.customSkulls };
  }

  const modifiers = objectAt(challenge, "bloonModifiers");
  const health = objectAt(modifiers, "healthMultipliers");
  // In Advanced every tier has its own speed and health, and Boss Speed and Boss Health stay at 100 %
  modifiers.bossSpeedMultiplier = !advanced && chance(rng, SPEED_CHANCE) ? weightedPercent(rng) / 100 : 1;
  health.boss = !advanced && chance(rng, HEALTH_CHANCE) ? weightedPercent(rng) / 100 : 1;
  modifiers.allFortified = chance(rng, FORTIFIED_CHANCE);
  challenge.maxParagons = chance(rng, PARAGON_CHANCE) ? next(rng, 0, MAX_RANDOM_PARAGONS + 1) : NO_LIMIT;

  if (GAME.unsupportedModes.includes(String(challenge.mode))) challenge.mode = "Standard";

  // Starting after the first tier's round would skip it
  const rules = objectAt(challenge, "startRules");
  const firstSpawn = effectiveSpawnRounds(options)[0];
  if (typeof rules.round === "number" && rules.round >= firstSpawn) rules.round = next(rng, 1, firstSpawn);

  return normalized(options);
}

/** What a describing name looks at (BossMode.NameTraits) */
export function nameTraits(options: BossOptions, challenge: JsonObject): NameTraits {
  const tiers = Array.from({ length: options.Tiers }, (_, i) => i + 1);
  const bosses = tiers.map((tier) => bossOf(options, tier));
  const modifiers = isObject(challenge.bloonModifiers) ? challenge.bloonModifiers : {};
  const health = isObject(modifiers.healthMultipliers) ? modifiers.healthMultipliers : {};
  const percent = (value: Json | undefined): number => Math.round((typeof value === "number" ? value : 1) * 100);
  const mixed = isMixed(options);
  const hasRandom = mixed && tiers.some((tier) => entryOf(options, tier).boss === RANDOM);
  return {
    boss: mixed ? "" : options.Elite ? `Elite ${options.Boss}` : options.Boss,
    elite: bosses.every((boss) => boss.elite),
    tiers: options.Tiers,
    bossSpeed: percent(modifiers.bossSpeedMultiplier),
    bossHealth: percent(health.boss),
    speedPerTier: options.SpeedPerTier,
    healthPerTier: options.HealthPerTier,
    noSkulls: tiers.every((tier) => tierSkullPercents(options, tier)?.length === 0),
    allFortified: modifiers.allFortified === true,
    sameBoss: mixed && !hasRandom && new Set(bosses.map((boss) => boss.boss)).size === 1 ? bosses[0].boss : null,
  };
}

/** The name of a setup with a boss per tier (BossMode.MixedName) */
const MIXED_NAME = "Custom Boss Gauntlet";

/**
 * Whether the challenge still has a name the Boss Creator gave it, or an example's, rather than one the player typed:
 * only such a name is replaced (BossMode.IsDefaultName, ExampleSetups.Named). On the website no name is the player's
 * own one in the game, which Random leaves to the game unless a name describes the setup.
 */
export const isGivenName = (name: string): boolean =>
  name === "" ||
  name === MIXED_NAME ||
  ALL_NAMES.has(name) ||
  BOSSES.some((boss) => name === `Custom ${boss}` || name === `Custom Elite ${boss}`) ||
  EXAMPLES.some((example) => example.name === name);

/** The maps Random picks from: every map with a group in the game, not the editor's own base map */
const randomMaps = (): string[] => Object.keys(GAME.mapDifficulties).filter((map) => map !== "BaseEditorMap" && map in GAME.maps);

/**
 * The setup after the Random button: a random map, difficulty and mode, random boss options (in Advanced Mode when the
 * setup is), and a name that describes it unless the player typed one. The other rules are a new challenge's.
 */
export function randomizeSetup(setup: StudioSetup, rng: Rng = Math.random): StudioSetup {
  const challenge = JSON.parse(JSON.stringify(GAME.defaultChallenge)) as JsonObject;
  const maps = randomMaps();
  challenge.map = maps[next(rng, maps.length)];
  const difficulties = Object.keys(GAME.difficulties);
  challenge.difficulty = difficulties[next(rng, difficulties.length)];
  const modes = Object.keys(GAME.modes);
  challenge.mode = modes[next(rng, modes.length)];

  const options = randomizeBoss(challenge, rng, isMixed(setup.options));
  const name = typeof setup.challenge.name === "string" ? setup.challenge.name : "";
  challenge.name = isGivenName(name) ? (randomName(nameTraits(options, challenge), rng) ?? (name === "" ? "" : defaultName(options))) : name;
  return { options, challenge };
}
