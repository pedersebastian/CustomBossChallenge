// The shapes of the JSON files WebDataTest (Tests/WebDataTest.cs) exports from the game

import game from "./game.json";
import examples from "./examples.json";
import tierLook from "./tierlook.json";

export type Json = null | boolean | number | string | Json[] | { [key: string]: Json };
export type JsonObject = { [key: string]: Json };

export interface BossTier {
  health: number;
  speed: number;
  /** Vanilla skull thresholds, 0-1 */
  skulls: number[];
}

export interface GameData {
  gameVersion: string;
  /** The challenge share codes are made against, without the player's name and seed */
  defaultChallenge: JsonObject;
  defaultOptions: JsonObject;
  bosses: Record<string, { normal: BossTier[]; elite: BossTier[] }>;
  maps: Record<string, string>;
  towers: Record<string, string>;
  modes: Record<string, string>;
  difficulties: Record<string, string>;
  unsupportedModes: string[];
  limits: {
    maxCodeLength: number;
    maxJsonLength: number;
    maxNameLength: number;
    minMultiplier: number;
    maxMultiplier: number;
    minBossSpeed: number;
    maxBossSpeed: number;
    minBossHealth: number;
    maxBossHealth: number;
    maxCash: number;
    maxLives: number;
    maxRevives: number;
    maxTowerCount: number;
  };
}

/** How strong one tier above 5 looks (Config/TierLook.cs) */
export interface TierLookTier {
  tier: number;
  /** How strongly the glow over the boss shows, before pulsing */
  glowAlpha: number;
  /** The size of the aura behind the boss compared with the boss */
  auraSize: number;
  auraAlpha: number;
  /** How much bigger the boss is drawn than tier 5 */
  scale: number;
  /** How far the glow and aura swing around their strength, as a share of it */
  pulseAmplitude: number;
  /** Seconds per pulse */
  pulsePeriod: number;
}

export interface TierLookData {
  /** The colours of tiers 6 to 10 per boss, as hex codes like #9BFF2E */
  themes: Record<string, string[]>;
  /** The colours of bosses the game adds later */
  defaultTheme: string[];
  /** Tiers 6 to 10 */
  tiers: TierLookTier[];
}

export interface Example {
  name: string;
  description: string;
  code: string;
}

export const GAME = game as unknown as GameData;
export const EXAMPLES = examples as Example[];
export const TIER_LOOK = tierLook as TierLookData;
