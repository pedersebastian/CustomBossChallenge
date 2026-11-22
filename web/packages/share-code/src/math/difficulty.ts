// A rough difficulty score for a setup, 1 to 5 stars (#263), like Config/DifficultyRating.cs: an estimate from the
// setup's numbers, not from playing it. web/test/difficulty-cases.json is shared with UnitTests/DifficultyRatingTests.cs,
// and WebDataTest exports the mod's score for each of web/src/data/fixtures.json's setups.
//
// The score is in points, roughly doublings of how hard the setup is compared with a normal boss event: the game's own
// five Bloonarius tiers on Medium score about 0. Twice the boss's health and speed, or twice the bloons' health, adds
// about 1 point, and so does a rule that takes a big tool away. STAR_STEPS turns the points into stars.

import { bossOf, CHIMPS_MODE, chimpsEventRoundSet, effectiveSpawnRounds, entryOf, tierSkullPercents } from "../options";
import { DEFAULT_COUNT } from "./skulls";
import { clampTiers, extraTierFactor, FIRST_SPAWN_ROUND, healthFactor, ROUNDS_BETWEEN_TIERS, speedFactor, VANILLA_TIERS } from "./tiers";
import type { SetupFacts } from "./warnings";

export const MAX_STARS = 5;

/** The points at which 2, 3, 4 and 5 stars begin */
export const STAR_STEPS = [1, 2.5, 3.25, 5] as const;

/** How much harder each boss is than Bloonarius, in points (DifficultyRating.BossWeight) */
export const BOSS_WEIGHTS: Record<string, number> = {
  Bloonarius: 0,
  Vortex: 0.25,
  Dreadbloon: 0.5,
  Blastapopoulos: 0.5,
  Diamondback: 0.5,
  Lych: 0.75,
  Phayze: 1,
};

/** A Random tier's boss isn't known before the game, so it counts as an average boss */
export const RANDOM_BOSS_WEIGHT = 0.5;

/** What an Elite tier adds to its boss: the Elite bosses have several times the health and are faster */
export const ELITE_WEIGHT = 3;

/** What each tier more than the game's five adds, and each tier fewer takes away */
export const TIER_COUNT_WEIGHT = 0.25;

/** What each skull fewer than the default three adds: fewer skulls, fewer breathers */
export const SKULL_WEIGHT = 0.2;

/** The stars for `facts`, 1 to MAX_STARS */
export const difficultyStars = (facts: SetupFacts): number => starsOf(difficultyPoints(facts));

/** The stars for a score in points */
export const starsOf = (points: number): number => 1 + STAR_STEPS.filter((step) => points >= step).length;

/** The score in points: the bosses, the rounds and the rules */
export const difficultyPoints = (facts: SetupFacts): number => bossPoints(facts) + roundPoints(facts) + rulePoints(facts);

/**
 * The bosses: each tier's health × speed compared with the normal game, its boss and Elite, weighted by the tier's
 * number so the last tiers count the most; and the number of tiers
 */
export function bossPoints(facts: SetupFacts): number {
  const options = facts.options;
  const tiers = clampTiers(options.Tiers);
  let sum = 0;
  let weights = 0;
  for (let tier = 1; tier <= tiers; tier++) {
    const entry = entryOf(options, tier);
    const strength =
      healthFactor(tier, options.HealthPerTier) *
      (entry.health / 100) *
      extraTierFactor(tier) *
      Math.max(facts.bossHealth, 0.01) *
      speedFactor(tier, options.SpeedPerTier) *
      (entry.speed / 100) *
      Math.max(facts.bossSpeed, 0.01);
    const random = entry.boss === "Random";
    const { boss, elite } = bossOf(options, tier);
    const bossWeight = random ? RANDOM_BOSS_WEIGHT : (BOSS_WEIGHTS[boss] ?? RANDOM_BOSS_WEIGHT);
    const isElite = random ? entry.elite : elite;
    sum += tier * (log(strength) + bossWeight + (isElite ? ELITE_WEIGHT : 0));
    weights += tier;
  }
  return sum / weights + TIER_COUNT_WEIGHT * (tiers - VANILLA_TIERS);
}

/**
 * The rounds: tiers arriving earlier than in the normal game (40, 60, 80, ...) leave less time to build up; fewer skulls
 * give fewer breathers; and the boss events' rounds are harder than the normal ones, the CHIMPS events' much harder
 */
export function roundPoints(facts: SetupFacts): number {
  const options = facts.options;
  const rounds = effectiveSpawnRounds(options);
  let early = 0;
  let skulls = 0;
  let weights = 0;
  for (let tier = 1; tier <= rounds.length; tier++) {
    const normal = FIRST_SPAWN_ROUND + ROUNDS_BETWEEN_TIERS * (tier - 1);
    early += tier * Math.min(Math.max(Math.log2(normal / Math.max(rounds[tier - 1], 1)), -1), 2);
    weights += tier;
    const percents = tierSkullPercents(options, tier);
    if (percents !== null) skulls += SKULL_WEIGHT * (DEFAULT_COUNT - percents.length);
  }

  let points = early / weights + skulls / rounds.length;
  if (options.BossEventRounds) points += facts.mode === CHIMPS_MODE && chimpsEventRoundSet(options, facts.mode) !== null ? 1 : 0.25;
  return points;
}

/**
 * The Challenge Editor's rules: the bloons' health and speed, lives, cash, towers, paragons, the rules that take a tool
 * away, the difficulty and the mode
 */
export function rulePoints(facts: SetupFacts): number {
  let points = log(facts.bloonHealth) + log(facts.moabHealth) + log(facts.bloonSpeed) + log(facts.moabSpeed);
  points += facts.lives < 0 ? 0 : facts.lives <= 1 ? 2 : facts.lives <= 10 ? 1 : facts.lives <= 50 ? 0.5 : 0;
  points += facts.cash >= 0 && facts.cash < 400 ? 1 : 0;
  points += facts.maxTowers >= 0 && facts.maxTowers <= 5 ? 1.5 : facts.maxTowers > 5 && facts.maxTowers <= 15 ? 0.5 : 0;
  points += facts.maxParagons === 0 ? 0.5 : 0;
  points += facts.allTowers > 0 ? (2 * Math.min(Math.max(facts.bannedTowers, 0), facts.allTowers)) / facts.allTowers : 0;
  points += (facts.noSelling ? 1 : 0) + (facts.noKnowledge ? 0.5 : 0) + (facts.noPowers ? 0.25 : 0);
  points += (facts.allCamo ? 0.5 : 0) + (facts.allRegrow ? 0.5 : 0) + (facts.allFortified ? 1 : 0);
  points += facts.difficulty === "Easy" ? -0.5 : facts.difficulty === "Hard" ? 0.5 : 0;
  points += MODE_POINTS[facts.mode] ?? 0;
  return points;
}

/** What each mode adds */
const MODE_POINTS: Record<string, number> = {
  [CHIMPS_MODE]: 1.5,
  Impoppable: 1.5,
  HalfCash: 1,
  DoubleMoabHealth: 1,
  PrimaryOnly: 1,
  MilitaryOnly: 1,
  MagicOnly: 1,
  Apopalypse: 0.5,
  Deflation: 0.5,
  AlternateBloonsRounds: 0.5,
};

const log = (factor: number): number => Math.log2(Math.max(factor, 0.01));
