// What the Boss Creator's Tier Preview and Skull Preview show (Menus/TierStrip.cs, Menus/SkullStrip.cs)

import { GAME } from "../data/types";
import type { Challenge } from "../code/decode";
import { bossOf, effectiveSpawnRounds, entryOf, skullPercents, type BossOptions } from "../code/options";
import { float } from "../math/dotnet";
import { clampTiers, extraTierFactor, formatHealth, healthFactor, maxModelHealth, speedFactor, VANILLA_TIERS } from "../math/tiers";

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
    const source = entryOf(options, tier).boss === "Random" ? undefined : modelOf(options, Math.min(tier, VANILLA_TIERS), tier);
    let healthValue: number | null = null;
    let capped = false;
    if (source) {
      const model = source.health * extraTierFactor(tier) * healthFactor(tier, options.HealthPerTier);
      capped = model > cap;
      healthValue = Math.min(model, cap) * bossHealth;
    }
    const speed = speedFactor(tier, options.SpeedPerTier) * bossSpeed;
    return {
      tier,
      round: rounds[i],
      speed,
      speedValue: source ? source.speed * speed : null,
      healthFactor: healthFactor(tier, options.HealthPerTier) * bossHealth,
      health: healthValue === null ? "?" : formatHealth(healthValue),
      healthValue,
      capped,
    };
  });
}

/**
 * The skull thresholds (0-1, from full health down) the boss gets, and the tier they belong to when the boss's own
 * skulls differ between its tiers
 */
export function skulls(options: BossOptions): { percents: number[]; tier: number | null } {
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

/** The game's model of tier `modelTier` (1-5) of the boss `tier` has, like BossOptions.SourceId */
function modelOf(options: BossOptions, modelTier: number, tier: number) {
  const { boss, elite } = bossOf(options, tier);
  return GAME.bosses[boss]?.[elite ? "elite" : "normal"]?.[modelTier - 1];
}
