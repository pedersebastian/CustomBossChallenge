// Port of Config/TierMath.cs: boss tiers, the rounds they spawn on and how much stronger they get

import { clamp, roundHalfEven } from "./dotnet";

export const VANILLA_TIERS = 5;
export const MIN_TIERS = 1;
export const MAX_TIERS = 10;
export const FIRST_SPAWN_ROUND = 40;
export const ROUNDS_BETWEEN_TIERS = 20;
export const MAX_ROUND = 1000;
export const MIN_SPEED_PER_TIER = -10;
export const MAX_SPEED_PER_TIER = 50;
export const MIN_HEALTH_PER_TIER = -10;
export const MAX_HEALTH_PER_TIER = 200;
export const PER_TIER_STEP = 5;
export const EXTRA_TIER_STEP = 1.5;
export const MAX_SPEED_FACTOR = 5;
export const MIN_PER_TIER_FACTOR = 0.1;
export const MAX_GAME_HEALTH = 2_000_000_000;

export const clampTiers = (tiers: number): number => clamp(tiers, MIN_TIERS, MAX_TIERS);
export const clampSpeedPerTier = (percent: number): number => clamp(percent, MIN_SPEED_PER_TIER, MAX_SPEED_PER_TIER);
export const clampHealthPerTier = (percent: number): number => clamp(percent, MIN_HEALTH_PER_TIER, MAX_HEALTH_PER_TIER);

/** The most health a boss model can have so that Boss Health doesn't take it past the game's limit */
export const maxModelHealth = (bossHealth: number): number => Math.floor(MAX_GAME_HEALTH / Math.max(bossHealth, 1));

/** Positive whole numbers from a list like "40, 60;80", in the given order */
export function parseRounds(text: string | null | undefined): number[] {
  if (text == null || text.trim() === "") return [];
  return text
    .split(/[,; ]/)
    .filter((part) => part !== "")
    .map((part) => (/^\s*[+-]?\d+\s*$/.test(part) ? Number(part) : 0))
    .map((round) => (Number.isSafeInteger(round) && Math.abs(round) <= 2_147_483_647 ? round : 0))
    .filter((round) => round > 0);
}

/**
 * Exactly `tiers` strictly ascending spawn rounds. Values from `custom` are used where valid; missing ones continue 20
 * rounds after the previous tier.
 */
export function spawnRounds(tiers: number, custom: string | null | undefined): number[] {
  tiers = clampTiers(tiers);
  const given = parseRounds(custom);
  const result: number[] = [];
  for (let i = 0; i < tiers; i++) {
    const previous = i === 0 ? 0 : result[i - 1];
    const value = i < given.length ? given[i] : i === 0 ? FIRST_SPAWN_ROUND : previous + ROUNDS_BETWEEN_TIERS;
    // Leave a round for every later tier below the cap, so no two tiers share a round
    const latest = MAX_ROUND - (tiers - 1 - i);
    result.push(Math.min(Math.max(value, previous + 1), latest));
  }
  return result;
}

/** The round the game ends on: the last tier always gets 20 rounds */
export const endRound = (rounds: number[]): number =>
  (rounds.length > 0 ? rounds[rounds.length - 1] : FIRST_SPAWN_ROUND) + ROUNDS_BETWEEN_TIERS;

const perTierFactor = (tier: number, percentPerTier: number): number =>
  Math.max(1 + (percentPerTier / 100) * Math.max(tier - 1, 0), MIN_PER_TIER_FACTOR);

/** How much faster a tier is than the game makes it, at most 5 */
export const speedFactor = (tier: number, percentPerTier: number): number =>
  Math.min(perTierFactor(tier, percentPerTier), MAX_SPEED_FACTOR);

/** How much more health a tier has than the game would give it */
export const healthFactor = (tier: number, percentPerTier: number): number => perTierFactor(tier, percentPerTier);

/** Health of a tier relative to the game's tier of the same number, or to tier 5 above that */
export const extraTierFactor = (tier: number): number =>
  tier <= VANILLA_TIERS ? 1 : EXTRA_TIER_STEP ** (tier - VANILLA_TIERS);

/** The Health per tier that gives tier 6 the health the old Extra Tier Health setting gave it */
export function healthPerTierFromExtraTierHealth(extraTierHealth: number): number {
  const percent = ((extraTierHealth / (EXTRA_TIER_STEP * 100) - 1) / VANILLA_TIERS) * 100;
  return clampHealthPerTier(roundHalfEven(percent / PER_TIER_STEP) * PER_TIER_STEP);
}

/** Health the way the game shortens big numbers, e.g. 950, 7.5k, 112k, 3.4M, 1.2B (Menus/TierStrip.cs) */
export function formatHealth(health: number): string {
  const short = (value: number, unit: string): string =>
    (value < 10 ? value.toFixed(1).replace(/\.0$/, "") : value.toFixed(0)) + unit;
  if (health >= 1e9) return short(health / 1e9, "B");
  if (health >= 1e6) return short(health / 1e6, "M");
  if (health >= 1e3) return short(health / 1e3, "k");
  return String(roundHalfEven(health));
}
