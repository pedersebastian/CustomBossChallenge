// The game data the share codes are made against: a copy of part of web/src/data/game.json, which the mod's
// WebDataTest writes from the game. web/scripts/sync-share-code-data.mjs makes the copy, and a test fails when it is
// out of date, so it always matches the mod.

import data from "./data.json";

export type Json = null | boolean | number | string | Json[] | { [key: string]: Json };
export type JsonObject = { [key: string]: Json };

export interface ShareCodeData {
  /** The game version the data was taken from */
  gameVersion: string;
  /** The bosses, in the order of the game's BossType */
  bosses: string[];
  /** A new Boss Creator setup's options, as the mod writes them */
  defaultOptions: JsonObject;
  /** The challenge codes are made against: the game's new Challenge Editor challenge, without the player's name and seed */
  defaultChallenge: JsonObject;
  /** What a code may set in the challenge, with the JSON type of each field (CodeFormat.ChallengeFields) */
  challengeFields: JsonObject;
  /** Map, difficulty and mode ids, each with its English name */
  maps: Record<string, string>;
  difficulties: Record<string, string>;
  modes: Record<string, string>;
  /** The modes Play refuses in a boss game */
  unsupportedModes: string[];
  /** The mod's limits (Config/ChallengeLimits.cs and SetupCode) */
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
    maxTiersUsed: number;
    maxBlockedTiers: number;
    paragons: number;
  };
}

export const DATA = data as unknown as ShareCodeData;
