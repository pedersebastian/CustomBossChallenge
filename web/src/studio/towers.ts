// The Challenge Editor's tower restrictions in the Studio (#128): each tower's limit, the upgrade tiers blocked on its
// three paths and its paragon lock, held to Config/ChallengeLimits.cs's limits. Towers are found by their "tower" key,
// and only the fields a code may set (CodeFormat.ChallengeFields.towers) are written, so a code keeps just the changed
// entries of the list.

import { GAME, type Json, type JsonObject } from "../data/types";
import { clamp } from "../math/dotnet";
import { orDefault } from "../math/limits";
import type { StudioSetup } from "./state";

/** The classes in the game's order; a tower the game data has no class for goes under OTHER_TOWERS */
export const TOWER_SETS = ["Hero", "Primary", "Military", "Magic", "Support"];
export const OTHER_TOWERS = "Other";

export const NO_LIMIT = -1;
export const NOT_ALLOWED = 0;

/** A tower path has five upgrade tiers */
export const PATH_TIERS = 5;

export type LimitMode = "none" | "notAllowed" | "limited";

/** The fields of a tower a code may set */
const FIELDS = ["max", "path1NumBlockedTiers", "path2NumBlockedTiers", "path3NumBlockedTiers", "restrictParagon"] as const;

export interface TowerEntry {
  tower: string;
  name: string;
  set: string;
  isHero: boolean;
  hasParagon: boolean;
  max: number;
  /** The blocked tiers of paths 1, 2 and 3: 0 blocks nothing, 5 the whole path */
  blocked: [number, number, number];
  restrictParagon: boolean;
}

const isObject = (value: Json | undefined): value is JsonObject =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const DEFAULT_TOWERS = (GAME.defaultChallenge.towers as JsonObject[]).filter(isObject);

const setOf = (tower: string): string => {
  const set = GAME.towerSets[tower];
  return TOWER_SETS.includes(set) ? set : OTHER_TOWERS;
};

const int = (value: Json | undefined, fallback: number): number => (typeof value === "number" && Number.isFinite(value) ? value : fallback);

function entryOf(item: JsonObject, standard: JsonObject | undefined): TowerEntry {
  const tower = String(item.tower);
  const read = (key: string, fallback: number) => int(item[key], int(standard?.[key], fallback));
  return {
    tower,
    name: GAME.towers[tower] ?? tower,
    set: setOf(tower),
    isHero: (item.isHero ?? standard?.isHero) === true,
    hasParagon: GAME.paragonTowers.includes(tower),
    max: read("max", NO_LIMIT),
    blocked: [read("path1NumBlockedTiers", 0), read("path2NumBlockedTiers", 0), read("path3NumBlockedTiers", 0)],
    restrictParagon: (item.restrictParagon ?? standard?.restrictParagon) === true,
  };
}

/** The setup's towers in the game's order, each with the new challenge's values where the code has none */
export function towerEntries(setup: StudioSetup): TowerEntry[] {
  const own = Array.isArray(setup.challenge.towers) ? setup.challenge.towers.filter(isObject) : DEFAULT_TOWERS;
  return own.map((item) => entryOf(item, DEFAULT_TOWERS.find((standard) => standard.tower === item.tower)));
}

/** The new challenge's values of a tower */
export function defaultTower(tower: string): TowerEntry | undefined {
  const standard = DEFAULT_TOWERS.find((item) => item.tower === tower);
  return standard ? entryOf(standard, standard) : undefined;
}

/** Whether a tower differs from the new challenge in anything a code may set */
export function changed(entry: TowerEntry): boolean {
  const standard = defaultTower(entry.tower);
  return (
    !standard ||
    standard.max !== entry.max ||
    standard.restrictParagon !== entry.restrictParagon ||
    standard.blocked.some((tiers, i) => tiers !== entry.blocked[i])
  );
}

/** A copy of the setup with one tower's fields changed; the code's own list is kept, or the new one is made */
function changeTower(setup: StudioSetup, tower: string, change: Partial<Record<(typeof FIELDS)[number], number | boolean>>): StudioSetup {
  const source = Array.isArray(setup.challenge.towers) ? setup.challenge.towers : DEFAULT_TOWERS;
  const towers = (JSON.parse(JSON.stringify(source)) as Json[]).map((item) => {
    if (!isObject(item) || item.tower !== tower) return item;
    for (const [field, value] of Object.entries(change)) {
      if ((FIELDS as readonly string[]).includes(field) && value !== undefined) item[field] = value;
    }
    return item;
  });
  return { ...setup, challenge: { ...setup.challenge, towers } };
}

export const limitMode = (max: number): LimitMode => (max < 0 ? "none" : max === NOT_ALLOWED ? "notAllowed" : "limited");

/**
 * A tower's limit from the page: no limit (-1), not allowed (0) or limited to a number, held like ChallengeLimits'
 * OrDefault; a limit of 0 is not allowed, and an empty or broken number keeps the limit as it was
 */
export function setTowerLimit(setup: StudioSetup, tower: string, mode: LimitMode, limit?: number | string): StudioSetup {
  let max: number;
  if (mode === "none") max = NO_LIMIT;
  else if (mode === "notAllowed") max = NOT_ALLOWED;
  else {
    const current = towerEntries(setup).find((entry) => entry.tower === tower)?.max ?? NO_LIMIT;
    const value = Math.round(Number(limit ?? (current > 0 ? current : 1)));
    if (!Number.isFinite(value) || String(limit ?? "x").trim() === "") return setup;
    max = orDefault(Math.max(value, 0), 0, GAME.limits.maxTowerCount);
  }
  return changeTower(setup, tower, { max });
}

/** How many of a path's top tiers are blocked (1-based path), held to 0-5 */
export const setBlockedTiers = (setup: StudioSetup, tower: string, path: number, tiers: number): StudioSetup =>
  path >= 1 && path <= 3
    ? changeTower(setup, tower, { [`path${path}NumBlockedTiers`]: clamp(Math.round(tiers), 0, GAME.limits.maxBlockedTiers) })
    : setup;

/** The highest tier a path can be upgraded to, as the page shows it: 5 when nothing is blocked */
export const highestTier = (blocked: number): number => PATH_TIERS - clamp(blocked, 0, PATH_TIERS);

/** The paragon lock, only for towers that have a paragon */
export const setParagonLock = (setup: StudioSetup, tower: string, locked: boolean): StudioSetup =>
  GAME.paragonTowers.includes(tower) ? changeTower(setup, tower, { restrictParagon: locked }) : setup;

/** Every tower of a class allowed without a limit, or not allowed at all */
export const setClassAllowed = (setup: StudioSetup, set: string, allowed: boolean): StudioSetup =>
  towerEntries(setup)
    .filter((entry) => entry.set === set)
    .reduce((current, entry) => changeTower(current, entry.tower, { max: allowed ? NO_LIMIT : NOT_ALLOWED }), setup);

/** One tower back to the new challenge's values */
export function resetTower(setup: StudioSetup, tower: string): StudioSetup {
  const standard = defaultTower(tower);
  if (!standard) return setup;
  return changeTower(setup, tower, {
    max: standard.max,
    path1NumBlockedTiers: standard.blocked[0],
    path2NumBlockedTiers: standard.blocked[1],
    path3NumBlockedTiers: standard.blocked[2],
    restrictParagon: standard.restrictParagon,
  });
}

/** Every tower back to the new challenge's values */
export const resetTowers = (setup: StudioSetup): StudioSetup =>
  towerEntries(setup).reduce((current, entry) => resetTower(current, entry.tower), setup);
