// The shapes of the JSON files WebDataTest (Tests/WebDataTest.cs) exports from the game

import game from "./game.json";
import examples from "./examples.json";

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

export interface Example {
  name: string;
  description: string;
  code: string;
}

export const GAME = game as unknown as GameData;
export const EXAMPLES = examples as Example[];
