// The look of tiers above 5, like in the game (Config/TierLook.cs): each boss has a colour per tier, and the glow and
// aura get stronger and pulse faster with each tier. The numbers come from the game through tierlook.json.

import { TIER_LOOK } from "../data/types";
import { VANILLA_TIERS } from "../math/tiers";

export interface TierGlow {
  /** The tier's colour, e.g. #9BFF2E */
  color: string;
  /** The opacity of the glow on the card, at the bottom and the top of a pulse */
  glow: [number, number];
  /** The opacity of the aura around the card, at the bottom and the top of a pulse */
  aura: [number, number];
  /** How far the aura reaches out, in pixels */
  auraReach: number;
  /** Seconds per pulse */
  period: number;
}

/** How far the aura reaches per step of the game's aura size (1.1 for tier 6 to 1.5 for tier 10) */
const AURA_REACH = 40;

/** The colour and pulse of a tier, or null for tiers 1-5, which keep the game's look */
export function tierGlow(boss: string, tier: number): TierGlow | null {
  const curve = TIER_LOOK.tiers.find((entry) => entry.tier === tier);
  if (!curve) return null;

  const theme = TIER_LOOK.themes[boss] ?? TIER_LOOK.defaultTheme;
  // Like TierLook.Pulse: the strength times 1 ± the amplitude
  const swing = (alpha: number): [number, number] => [alpha * (1 - curve.pulseAmplitude), Math.min(alpha * (1 + curve.pulseAmplitude), 1)];
  return {
    color: theme[tier - VANILLA_TIERS - 1],
    glow: swing(curve.glowAlpha),
    aura: swing(curve.auraAlpha),
    auraReach: Math.round((curve.auraSize - 1) * AURA_REACH),
    period: curve.pulsePeriod,
  };
}

/** The CSS variables the .tier-look class in style.css draws the glow with */
export function tierGlowStyle(glow: TierGlow): string {
  return [
    `--tier-color: ${glow.color}`,
    `--glow-low: ${glow.glow[0].toFixed(3)}`,
    `--glow-high: ${glow.glow[1].toFixed(3)}`,
    `--aura-low: ${glow.aura[0].toFixed(3)}`,
    `--aura-high: ${glow.aura[1].toFixed(3)}`,
    `--aura-reach: ${glow.auraReach}px`,
    `--pulse-period: ${glow.period}s`,
  ].join("; ");
}
