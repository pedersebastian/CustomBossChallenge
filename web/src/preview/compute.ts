// What the Boss Creator's Tier Preview and Skull Preview show (Menus/TierStrip.cs, Menus/SkullStrip.cs)

import { GAME } from "../data/types";
import type { Challenge } from "../code/decode";
import { effectiveSpawnRounds, skullPercents, type BossOptions } from "../code/options";
import { float } from "../math/dotnet";
import { clampTiers, extraTierFactor, formatHealth, healthFactor, maxModelHealth, speedFactor, VANILLA_TIERS } from "../math/tiers";

export interface TierCard {
  tier: number;
  round: number;
  /** Speed compared with the normal game; above 1 is faster */
  speed: number;
  /** Health compared with the normal game; above 1 is tougher */
  healthFactor: number;
  /** Health shortened like the game does, e.g. 1.2M, or "?" for a boss the website doesn't know */
  health: string;
  /** Held to the most health the game can take */
  capped: boolean;
}

export function tierCards(options: BossOptions, challenge: Challenge): TierCard[] {
  const rounds = effectiveSpawnRounds(options);
  // The game keeps these multipliers as floats
  const bossSpeed = float(challenge.bossSpeed);
  const bossHealth = float(challenge.bossHealth);
  const cap = maxModelHealth(bossHealth);
  const models = GAME.bosses[options.Boss]?.[options.Elite ? "elite" : "normal"];

  return Array.from({ length: clampTiers(options.Tiers) }, (_, i) => {
    const tier = i + 1;
    const source = models?.[Math.min(tier, VANILLA_TIERS) - 1];
    let health = "?";
    let capped = false;
    if (source) {
      const model = source.health * extraTierFactor(tier) * healthFactor(tier, options.HealthPerTier);
      capped = model > cap;
      health = formatHealth(Math.min(model, cap) * bossHealth);
    }
    return {
      tier,
      round: rounds[i],
      speed: speedFactor(tier, options.SpeedPerTier) * bossSpeed,
      healthFactor: healthFactor(tier, options.HealthPerTier) * bossHealth,
      health,
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

  const models = GAME.bosses[options.Boss]?.[options.Elite ? "elite" : "normal"] ?? [];
  const tiers = models
    .slice(0, Math.min(clampTiers(options.Tiers), VANILLA_TIERS))
    .map((model) => model.skulls.map(float).sort((a, b) => b - a));
  if (tiers.length === 0) return { percents: [], tier: null };

  const first = tiers[0];
  const same = tiers.every((other) => other.length === first.length && other.every((value, i) => value === first[i]));
  return { percents: first, tier: same ? null : 1 };
}
