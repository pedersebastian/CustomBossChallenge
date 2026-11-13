// What the Boss Creator's Tier Preview and Skull Preview show (Menus/TierStrip.cs, Menus/SkullStrip.cs)

import { GAME } from "../data/types";
import type { Challenge } from "@cbc/share-code";
import { bossOf, effectiveSpawnRounds, entryOf, isMixed, skullPercents, tierSkullPercents, type BossOptions } from "@cbc/share-code";
import { float } from "@cbc/share-code/dotnet";
import { clampTiers, extraTierFactor, formatHealth, healthFactor, maxModelHealth, speedFactor, VANILLA_TIERS } from "@cbc/share-code/tiers";
import { NORMAL, formatTierBosses, parseTierBosses } from "@cbc/share-code/tier-bosses";

export interface TierCard {
  tier: number;
  round: number;
  /** Speed compared with the normal game; above 1 is faster */
  speed: number;
  /** How fast the boss really moves, so bosses can be compared with each other, or null for one the website doesn't know */
  speedValue: number | null;
  /** Health compared with the normal game; above 1 is tougher */
  healthFactor: number;
  /** Health shortened like the game does, e.g. 1.2M, or "?" for a boss the website doesn't know */
  health: string;
  /** The health itself, or null for a boss the website doesn't know */
  healthValue: number | null;
  /** Held to the most health the game can take */
  capped: boolean;
  /** How many skulls the tier's boss has, or null for a Random tier or a boss the website doesn't know */
  skulls: number | null;
}

export function tierCards(options: BossOptions, challenge: Challenge): TierCard[] {
  const rounds = effectiveSpawnRounds(options);
  // The game keeps these multipliers as floats
  const bossSpeed = float(challenge.bossSpeed);
  const bossHealth = float(challenge.bossHealth);
  const cap = maxModelHealth(bossHealth);

  return Array.from({ length: clampTiers(options.Tiers) }, (_, i) => {
    const tier = i + 1;
    // Each tier is made from its own boss; a Random tier's boss isn't known until the game starts
    const entry = entryOf(options, tier);
    const source = entry.boss === "Random" ? undefined : modelOf(options, Math.min(tier, VANILLA_TIERS), tier);
    // In Advanced Mode each tier has its own speed and health on top (BossOptions.TierSpeedFactor)
    const tierSpeed = entry.speed / 100;
    const tierHealth = entry.health / 100;
    let healthValue: number | null = null;
    let capped = false;
    if (source) {
      const model = source.health * extraTierFactor(tier) * healthFactor(tier, options.HealthPerTier) * tierHealth;
      capped = model > cap;
      healthValue = Math.min(model, cap) * bossHealth;
    }
    const speed = speedFactor(tier, options.SpeedPerTier) * tierSpeed * bossSpeed;
    return {
      tier,
      round: rounds[i],
      speed,
      speedValue: source ? source.speed * speed : null,
      healthFactor: healthFactor(tier, options.HealthPerTier) * tierHealth * bossHealth,
      health: healthValue === null ? "?" : formatHealth(healthValue),
      healthValue,
      capped,
      skulls: tierSkulls(options, tier)?.length ?? null,
    };
  });
}

/**
 * The skull thresholds (0-1, from full health down) the boss gets, and the tier they belong to when the boss's own
 * skulls differ between its tiers. With a boss per tier those are `shownTier`'s, null for a Random tier.
 */
export function skulls(options: BossOptions, shownTier = 1): { percents: number[] | null; tier: number | null } {
  if (isMixed(options)) {
    const tier = Math.min(Math.max(shownTier, 1), clampTiers(options.Tiers));
    return { percents: tierSkulls(options, tier), tier };
  }

  const custom = skullPercents(options);
  if (custom) return { percents: custom, tier: null };

  const tiers = Array.from({ length: Math.min(clampTiers(options.Tiers), VANILLA_TIERS) }, (_, i) => modelOf(options, i + 1, i + 1))
    .filter((model) => model !== undefined)
    .map((model) => model.skulls.map(float).sort((a, b) => b - a));
  if (tiers.length === 0) return { percents: [], tier: null };

  const first = tiers[0];
  const same = tiers.every((other) => other.length === first.length && other.every((value, i) => value === first[i]));
  return { percents: first, tier: same ? null : 1 };
}

/**
 * The skulls a tier's boss gets, like SkullStrip.TierSkulls: null for a Random tier that keeps its boss's own or a boss
 * the website doesn't know
 */
export function tierSkulls(options: BossOptions, tier: number): number[] | null {
  const custom = tierSkullPercents(options, tier);
  if (custom) return custom;
  if (entryOf(options, tier).boss === "Random") return null;

  // Tiers after 5 are copies of tier 5, so they have its skulls
  const model = modelOf(options, Math.min(tier, VANILLA_TIERS), tier);
  return model ? model.skulls.map(float).sort((a, b) => b - a) : null;
}

/** The game's model of tier `modelTier` (1-5) of the boss `tier` has, like BossOptions.SourceId */
export function modelOf(options: BossOptions, modelTier: number, tier: number) {
  const { boss, elite } = bossOf(options, tier);
  return GAME.bosses[boss]?.[elite ? "elite" : "normal"]?.[modelTier - 1];
}

/**
 * The same setup as the normal game would have it, to compare with (#96): the game's own spawn rounds, no Speed or
 * Health per Tier, Boss Speed and Boss Health at 100 % and every Advanced tier at its normal speed and health, with the
 * same bosses, Elite and Random.
 * The normal game has 5 tiers, so the tiers after them have nothing to compare with.
 */
export function normalGame(options: BossOptions, challenge: Challenge): { options: BossOptions; challenge: Challenge } {
  const tierBosses =
    options.TierBosses == null
      ? options.TierBosses
      : formatTierBosses(parseTierBosses(options.TierBosses).map((tier) => ({ ...tier, speed: NORMAL, health: NORMAL })));
  return {
    options: {
      ...options,
      Tiers: Math.min(clampTiers(options.Tiers), VANILLA_TIERS),
      SpawnRounds: null,
      SpeedPerTier: 0,
      HealthPerTier: 0,
      TierBosses: tierBosses,
    },
    challenge: { ...challenge, bossSpeed: 1, bossHealth: 1 },
  };
}
