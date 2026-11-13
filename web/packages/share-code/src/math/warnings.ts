// The warnings the Boss Creator shows under its Tier Preview before Play (#70, #186), like Config/SetupWarnings.cs:
// things in a setup that are allowed but probably not what the player meant. They never block Play; a setup that
// can't be played is playProblem's. web/test/setup-warnings-cases.json is shared with UnitTests/SetupWarningsTests.cs,
// and WebDataTest exports the mod's warnings for its fixtures, made with the game's own boss models.

import type { Challenge } from "../decode";
import {
  bossOf,
  chimpsEventRoundSet,
  defaultOptions,
  effectiveSpawnRounds,
  entryOf,
  type BossOptions,
  type ChimpsRounds,
} from "../options";
import { float } from "./dotnet";
import { clampTiers, extraTierFactor, healthFactor, maxModelHealth, speedFactor } from "./tiers";

/** Health and speed of a boss model as the game has it (TierBase) */
export interface TierBase {
  health: number;
  speed: number;
}

/** The parts of a setup the warnings look at (SetupFacts); missing values are the editor's defaults */
export interface SetupFacts {
  options: BossOptions;
  /** The round the game starts on, as the player sees it; below 1 for the game's own */
  startRound: number;
  difficulty: string;
  mode: string;
  /** Below 0 for the difficulty's own, like the game */
  lives: number;
  /** Below 0 for the difficulty's own, like the game */
  cash: number;
  /** Below 0 for no limit, like the game */
  maxTowers: number;
  bloonSpeed: number;
  moabSpeed: number;
  bossSpeed: number;
  bloonHealth: number;
  moabHealth: number;
  bossHealth: number;
  noSelling: boolean;
  noKnowledge: boolean;
  noPowers: boolean;
  allCamo: boolean;
  allRegrow: boolean;
  allFortified: boolean;
  /**
   * Health and speed of the game's boss model each tier is made from, by tier from 1; null where it isn't known, like
   * for a Random tier or a boss the caller doesn't know
   */
  tierBases: (TierBase | null)[] | null;
}

/** The warnings, in the order the Boss Creator shows them (SetupWarning) */
export const SETUP_WARNINGS = [
  "StartRoundClose",
  "TiersClose",
  "HealthCapped",
  "SameTiers",
  "ChimpsEventRounds",
  "HardSetup",
] as const;
export type SetupWarning = (typeof SETUP_WARNINGS)[number];

/**
 * One warning and what it names:
 * - StartRoundClose: the start round, the rounds left and the first boss's round
 * - TiersClose: the rounds, like "80-81, 100-102"
 * - HealthCapped: the most health a tier can have, Boss Health included
 * - SameTiers: the tiers, like "6-8"
 * - ChimpsEventRounds: which CHIMPS events' rounds
 * - HardSetup: nothing
 */
export interface Warning {
  kind: SetupWarning;
  args: (number | string)[];
}

/** Starting fewer rounds than this before the first boss, or a tier this close after the one before, leaves hardly any time */
export const CLOSE_ROUNDS = 5;

/** A setup scores at least this in difficulty before it counts as very hard */
export const HARD_THRESHOLD = 5;

const SAME = 1e-6;

/** Facts with the editor's defaults for everything not given */
export const setupFacts = (facts: Partial<SetupFacts> = {}): SetupFacts => ({
  options: defaultOptions(),
  startRound: 1,
  difficulty: "Medium",
  mode: "Standard",
  lives: -1,
  cash: -1,
  maxTowers: -1,
  bloonSpeed: 1,
  moabSpeed: 1,
  bossSpeed: 1,
  bloonHealth: 1,
  moabHealth: 1,
  bossHealth: 1,
  noSelling: false,
  noKnowledge: false,
  noPowers: false,
  allCamo: false,
  allRegrow: false,
  allFortified: false,
  tierBases: null,
  ...facts,
});

/**
 * The facts of a decoded setup, like TierStrip.Facts: the challenge's rules, with its multipliers as the game's
 * floats, and the boss model of each tier from `tierBases`
 */
export function factsOf(options: BossOptions, challenge: Challenge, tierBases: (TierBase | null)[] | null = null): SetupFacts {
  return {
    options,
    startRound: challenge.round,
    difficulty: challenge.difficulty,
    mode: challenge.mode,
    lives: challenge.lives,
    cash: challenge.cash,
    maxTowers: challenge.maxTowers,
    bloonSpeed: float(challenge.bloonSpeed),
    moabSpeed: float(challenge.moabSpeed),
    bossSpeed: float(challenge.bossSpeed),
    bloonHealth: float(challenge.bloonHealth),
    moabHealth: float(challenge.moabHealth),
    bossHealth: float(challenge.bossHealth),
    noSelling: challenge.disableSelling,
    noKnowledge: challenge.disableMK,
    noPowers: challenge.disablePowers,
    allCamo: challenge.allCamo,
    allRegrow: challenge.allRegen,
    allFortified: challenge.allFortified,
    tierBases,
  };
}

const baseOf = (facts: SetupFacts, tier: number): TierBase | null =>
  facts.tierBases !== null && tier >= 1 && tier <= facts.tierBases.length ? facts.tierBases[tier - 1] ?? null : null;

/** The warnings for `facts`, in the order of SETUP_WARNINGS (SetupWarnings.Check) */
export function setupWarnings(facts: SetupFacts): Warning[] {
  const warnings: Warning[] = [];
  const options = facts.options;
  const tiers = clampTiers(options.Tiers);

  const bossRound = effectiveSpawnRounds(options)[0];
  const left = bossRound - facts.startRound;
  if (left > 0 && left < CLOSE_ROUNDS) warnings.push({ kind: "StartRoundClose", args: [facts.startRound, left, bossRound] });

  const close = closeTiers(options);
  if (close.length > 0) warnings.push({ kind: "TiersClose", args: [close.map((pair) => `${pair.before}-${pair.after}`).join(", ")] });

  const cap = maxModelHealth(facts.bossHealth);
  if (Array.from({ length: tiers }, (_, i) => modelHealth(facts, i + 1)).some((health) => health !== null && health > cap))
    warnings.push({ kind: "HealthCapped", args: [cap * Math.max(facts.bossHealth, 1)] });

  const same = sameTiers(facts);
  if (same.length > 0) warnings.push({ kind: "SameTiers", args: [same.map((run) => `${run.first}-${run.last}`).join(", ")] });

  if (options.BossEventRounds && chimpsEventRoundSet(options, facts.mode) !== null)
    warnings.push({ kind: "ChimpsEventRounds", args: [options.ChimpsRounds ?? ("Newest" satisfies ChimpsRounds)] });

  if (difficulty(facts) >= HARD_THRESHOLD) warnings.push({ kind: "HardSetup", args: [] });
  return warnings;
}

/**
 * The spawn rounds of tiers that come fewer than CLOSE_ROUNDS rounds after the tier before, with the round of the tier
 * before. The last tier always gets 20 rounds, so only the gaps between tiers count.
 */
export function closeTiers(options: BossOptions): { before: number; after: number }[] {
  const rounds = effectiveSpawnRounds(options);
  return rounds
    .slice(1)
    .map((after, i) => ({ before: rounds[i], after }))
    .filter((pair) => pair.after - pair.before < CLOSE_ROUNDS);
}

/** A tier's model health before the game's limit, or null when its boss model isn't known */
export function modelHealth(facts: SetupFacts, tier: number): number | null {
  const source = baseOf(facts, tier);
  const entry = entryOf(facts.options, tier);
  if (source === null || entry.boss === "Random") return null;
  return source.health * extraTierFactor(tier) * healthFactor(tier, facts.options.HealthPerTier) * (entry.health / 100);
}

/**
 * Runs of tiers in a row that get the same health and speed from the same boss although the settings ask for a change
 * from tier to tier: the speed limit, the lowest share of the game's values or the health limit made them equal
 */
export function sameTiers(facts: SetupFacts): { first: number; last: number }[] {
  const options = facts.options;
  const tiers = clampTiers(options.Tiers);
  const cap = maxModelHealth(facts.bossHealth);
  const runs: { first: number; last: number }[] = [];
  let start: number | null = null;
  for (let tier = 1; tier < tiers; tier++) {
    const next = tier + 1;
    const a = values(facts, tier, cap);
    const b = values(facts, next, cap);
    const bossA = bossOf(options, tier);
    const bossB = bossOf(options, next);
    const isSame =
      a !== null &&
      b !== null &&
      bossA.boss === bossB.boss &&
      bossA.elite === bossB.elite &&
      equal(a.health, b.health) &&
      equal(a.speed, b.speed) &&
      (!equal(a.askedHealth, b.askedHealth) || !equal(a.askedSpeed, b.askedSpeed));
    if (isSame) {
      start ??= tier;
      continue;
    }

    if (start !== null) runs.push({ first: start, last: tier });
    start = null;
  }

  if (start !== null) runs.push({ first: start, last: tiers });
  return runs;
}

function values(
  facts: SetupFacts,
  tier: number,
  cap: number,
): { health: number; speed: number; askedHealth: number; askedSpeed: number } | null {
  const source = baseOf(facts, tier);
  const health = modelHealth(facts, tier);
  if (source === null || health === null) return null;

  const options = facts.options;
  const entry = entryOf(options, tier);
  const tierSpeed = entry.speed / 100;
  const tierHealth = entry.health / 100;
  // What the settings ask for, without the limits
  return {
    health: Math.min(health, cap),
    speed: source.speed * speedFactor(tier, options.SpeedPerTier) * tierSpeed,
    askedHealth: source.health * extraTierFactor(tier) * unlimited(tier, options.HealthPerTier) * tierHealth,
    askedSpeed: source.speed * unlimited(tier, options.SpeedPerTier) * tierSpeed,
  };
}

const unlimited = (tier: number, percentPerTier: number): number => 1 + (percentPerTier / 100) * Math.max(tier - 1, 0);

const equal = (a: number, b: number): boolean => Math.abs(a - b) <= SAME * Math.max(Math.abs(a), Math.abs(b));

/**
 * How hard the setup is compared with a normal boss game, roughly in doublings: 0 for the game's own bosses with the
 * editor's default rules. Twice the boss's health and speed or twice the bloons' health adds 1, and so do the rules
 * that take a big tool away from the player (SetupWarnings.Difficulty).
 */
export function difficulty(facts: SetupFacts): number {
  const options = facts.options;
  const last = clampTiers(options.Tiers);
  const lastEntry = entryOf(options, last);

  // The last tier compared with the game's own tier of that number; the game's steps for tiers above 5 are left out
  const boss =
    healthFactor(last, options.HealthPerTier) *
    (lastEntry.health / 100) *
    Math.max(facts.bossHealth, 0.01) *
    speedFactor(last, options.SpeedPerTier) *
    (lastEntry.speed / 100) *
    Math.max(facts.bossSpeed, 0.01);
  const elite = Array.from({ length: last }, (_, i) => i + 1).some((tier) => {
    const entry = entryOf(options, tier);
    return bossOf(options, tier).elite || (entry.boss === "Random" && entry.elite);
  });

  let score =
    log(boss) + (elite ? 1.5 : 0) + log(facts.bloonHealth) + log(facts.moabHealth) + log(facts.bloonSpeed) + log(facts.moabSpeed);

  score += facts.lives < 0 ? 0 : facts.lives <= 1 ? 2 : facts.lives <= 10 ? 1 : facts.lives <= 50 ? 0.5 : 0;
  score += facts.cash >= 0 && facts.cash < 400 ? 1 : 0;
  score += facts.maxTowers >= 0 && facts.maxTowers <= 5 ? 1.5 : facts.maxTowers > 5 && facts.maxTowers <= 15 ? 0.5 : 0;
  score += (facts.noSelling ? 1 : 0) + (facts.noKnowledge ? 0.5 : 0) + (facts.noPowers ? 0.25 : 0);
  score += (facts.allCamo ? 0.5 : 0) + (facts.allRegrow ? 0.5 : 0) + (facts.allFortified ? 1 : 0);
  score += facts.difficulty === "Hard" ? 0.5 : 0;
  score +=
    facts.mode === "Impoppable" ? 1.5 : facts.mode === "HalfCash" || facts.mode === "DoubleMoabHealth" ? 1 : facts.mode === "Apopalypse" ? 0.5 : 0;
  return score;
}

const log = (factor: number): number => Math.log2(Math.max(factor, 0.01));
