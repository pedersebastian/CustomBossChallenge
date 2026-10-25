// Port of Persistence/SetupCode.cs (reading) and Config/ChallengeLimits.Sanitize: share code -> setup.
// Like the mod, a code is only data: it is never run, never throws and is held to what the game can play.

import { GAME, type Json, type JsonObject } from "../data/types";
import { clamp, float } from "../math/dotnet";
import { MAX_ROUND } from "../math/tiers";
import { OptionTypeError, defaultName, defaultOptions, optionsJson, readOptions, sanitize, type BossOptions } from "./options";

export const PREFIX = "CBC1:";
export const VERSION2_PREFIX = "CBC2:";
const PREFIX_LENGTH = 5;
const ITEMS_KEY = "$items";

export type CodeProblem = "None" | "Invalid" | "TooNew";

/** The challenge values the website shows, named like WebDataTest's fixtures */
export interface Challenge {
  /** Empty when the code has no name of its own: the game then uses the name of whoever opens it */
  name: string;
  map: string;
  difficulty: string;
  mode: string;
  cash: number;
  lives: number;
  maxLives: number;
  round: number;
  bossSpeed: number;
  bossHealth: number;
  bloonSpeed: number;
  moabSpeed: number;
  bloonHealth: number;
  moabHealth: number;
  allCamo: boolean;
  allRegen: boolean;
  allFortified: boolean;
  maxParagons: number;
  maxTowers: number;
  disableMK: boolean;
  disableSelling: boolean;
  disablePowers: boolean;
  /** Towers whose limit differs from the default, by id: 0 is not allowed, -1 no limit */
  towers: Record<string, number>;
}

export type DecodeResult =
  | { ok: true; problem: "None"; options: BossOptions; challenge: Challenge; json: JsonObject }
  | { ok: false; problem: Exclude<CodeProblem, "None">; reason: string };

const fail = (problem: Exclude<CodeProblem, "None">, reason: string): DecodeResult => ({ ok: false, problem, reason });

const isObject = (value: Json | undefined): value is JsonObject =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const clone = <T extends Json>(value: T): T => JSON.parse(JSON.stringify(value)) as T;

/** Puts a diff back on top of the defaults it was made against (SetupCode.Apply) */
export function apply(target: Json, diff: Json): Json {
  if (isObject(diff) && Object.keys(diff).length === 1 && isObject(diff[ITEMS_KEY]) && Array.isArray(target)) {
    for (const [index, inner] of Object.entries(diff[ITEMS_KEY])) {
      if (!/^\d+$/.test(index)) throw new Error("Bad list index");
      const i = Number(index);
      if (i < target.length && inner !== null) target[i] = apply(target[i], inner);
    }
    return target;
  }

  if (isObject(diff) && isObject(target)) {
    for (const [name, inner] of Object.entries(diff)) {
      // A real property, never the object's prototype
      if (inner === null || name === "__proto__") continue;
      const current = Object.prototype.hasOwnProperty.call(target, name) ? target[name] : undefined;
      target[name] = current === undefined || current === null ? clone(inner) : apply(current, inner);
    }
    return target;
  }

  return clone(diff);
}

function fromBase64Url(text: string): Uint8Array {
  const base64 = text.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");
  if (!/^[A-Za-z0-9+/]*={0,2}$/.test(padded) || padded.length % 4 !== 0) throw new Error("Not base64");
  const binary = atob(padded);
  return Uint8Array.from(binary, (c) => c.charCodeAt(0));
}

/** Unpacks raw deflate, stopping once the text gets longer than the mod allows */
async function inflate(data: Uint8Array): Promise<string> {
  const stream = new Blob([data as BlobPart]).stream().pipeThrough(new DecompressionStream("deflate-raw"));
  const reader = stream.getReader();
  const decoder = new TextDecoder("utf-8");
  let text = "";
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    text += decoder.decode(value, { stream: true });
    if (text.length > GAME.limits.maxJsonLength) {
      await reader.cancel();
      throw new Error("Setup code unpacks to too much data");
    }
  }
  return text + decoder.decode();
}

/** Reads a code. Never throws: anything that isn't a usable code gives a problem and a reason. */
export async function decode(code: string | null | undefined): Promise<DecodeResult> {
  const trimmed = (code ?? "").trim();
  if (trimmed === "") return fail("Invalid", "The code is empty.");
  if (trimmed.length > GAME.limits.maxCodeLength) return fail("Invalid", "The code is too long.");
  if (!trimmed.startsWith(PREFIX) && !trimmed.startsWith(VERSION2_PREFIX)) {
    if (trimmed.startsWith("CBC") && trimmed.length > 4 && /\d/.test(trimmed[3])) {
      return fail("TooNew", "This code comes from a newer version of Custom Boss Challenge.");
    }
    return fail("Invalid", "This isn't a Boss Creator code. Codes start with CBC1: or CBC2:.");
  }

  try {
    const payload: Json = JSON.parse(await inflate(fromBase64Url(trimmed.slice(PREFIX_LENGTH))));
    if (!isObject(payload)) return fail("Invalid", "The code is damaged.");

    const optionsJsonValue = optionsJson(defaultOptions());
    if (isObject(payload.o)) apply(optionsJsonValue, payload.o);
    const options = sanitize(readOptions(optionsJsonValue));

    const challengeJson = clone(GAME.defaultChallenge);
    if (isObject(payload.c)) apply(challengeJson, payload.c);
    const challenge = readChallenge(challengeJson, options);

    return { ok: true, problem: "None", options, challenge, json: challengeJson };
  } catch (e) {
    return fail("Invalid", e instanceof OptionTypeError ? `The code has a broken value: ${e.message}.` : "The code is damaged or cut short.");
  }
}

/** A name without control characters, at most 60 characters long (ChallengeLimits.CleanName) */
export function cleanName(name: string, fallback: string): string {
  // eslint-disable-next-line no-control-regex
  let clean = name.replace(/[\u0000-\u001f\u007f-\u009f]/g, "").trim();
  if (clean.length > GAME.limits.maxNameLength) clean = clean.slice(0, GAME.limits.maxNameLength).trimEnd();
  return clean.length > 0 ? clean : fallback;
}

/** The challenge with the types the game reads it as, brought into range like ChallengeLimits.Sanitize */
function readChallenge(json: JsonObject, options: BossOptions): Challenge {
  const limits = GAME.limits;
  const defaults = GAME.defaultChallenge;
  const object = (value: Json | undefined): JsonObject => (isObject(value) ? value : {});
  const modifiers = object(json.bloonModifiers);
  const health = object(modifiers.healthMultipliers);
  const rules = object(json.startRules);

  const number = (value: Json | undefined, fallback: number): number => {
    if (value === undefined) return fallback;
    if (typeof value !== "number") throw new Error("Not a number");
    return value;
  };
  const int = (value: Json | undefined, fallback: number): number => {
    const read = number(value, fallback);
    if (!Number.isInteger(read)) throw new Error("Not a whole number");
    return read;
  };
  const bool = (value: Json | undefined): boolean => {
    if (value === undefined) return false;
    if (typeof value !== "boolean") throw new Error("Not true or false");
    return value;
  };
  const text = (value: Json | undefined): string | null => {
    if (value === undefined || value === null) return null;
    if (typeof value !== "string") throw new Error("Not text");
    return value;
  };
  // A finite float within the limits; NaN becomes the lower limit
  const multiplier = (value: Json | undefined, min: number, max: number): number => {
    const read = float(number(value, 1));
    return Number.isNaN(read) ? float(min) : clamp(read, float(min), float(max));
  };
  // The game uses negative values for "default"; anything else is kept within the limits
  const orDefault = (value: number, min: number, max: number): number => (value < 0 ? -1 : clamp(value, min, max));

  const known = (value: string | null, names: string[], fallback: string): string =>
    value !== null && names.includes(value) ? value : fallback;

  const name = text(json.name) ?? "";
  const defaultTowers = new Map(
    (Array.isArray(defaults.towers) ? defaults.towers : []).filter(isObject).map((t) => [t.tower as string, t.max as number]),
  );
  const towers: Record<string, number> = {};
  for (const tower of Array.isArray(json.towers) ? json.towers : []) {
    if (!isObject(tower)) continue;
    const id = text(tower.tower);
    if (id === null) continue;
    const max = orDefault(int(tower.max, 0), 0, limits.maxTowerCount);
    if (!defaultTowers.has(id) || defaultTowers.get(id) !== max) towers[id] = max;
  }

  return {
    // No name of its own means the name of whoever opens the code, so none is shown
    name: name === "" ? "" : cleanName(name, defaultName(options)),
    map: known(text(json.map), Object.keys(GAME.maps), defaults.map as string),
    difficulty: known(text(json.difficulty), Object.keys(GAME.difficulties), defaults.difficulty as string),
    mode: known(text(json.mode), Object.keys(GAME.modes), defaults.mode as string),
    cash: orDefault(int(rules.cash, -1), 0, limits.maxCash),
    lives: orDefault(int(rules.lives, -1), 1, limits.maxLives),
    maxLives: orDefault(int(rules.maxLives, -1), 1, limits.maxLives),
    round: orDefault(int(rules.round, -1), 1, MAX_ROUND),
    bossSpeed: multiplier(modifiers.bossSpeedMultiplier, limits.minBossSpeed, limits.maxBossSpeed),
    bossHealth: multiplier(health.boss, limits.minBossHealth, limits.maxBossHealth),
    bloonSpeed: multiplier(modifiers.speedMultiplier, limits.minMultiplier, limits.maxMultiplier),
    moabSpeed: multiplier(modifiers.moabSpeedMultiplier, limits.minMultiplier, limits.maxMultiplier),
    bloonHealth: multiplier(health.bloons, limits.minMultiplier, limits.maxMultiplier),
    moabHealth: multiplier(health.moabs, limits.minMultiplier, limits.maxMultiplier),
    allCamo: bool(modifiers.allCamo),
    allRegen: bool(modifiers.allRegen),
    allFortified: bool(modifiers.allFortified),
    maxParagons: int(json.maxParagons, -1),
    maxTowers: orDefault(int(json.maxTowers, -1), 0, limits.maxTowerCount),
    disableMK: bool(json.disableMK),
    disableSelling: bool(json.disableSelling),
    disablePowers: bool(json.disablePowers),
    towers,
  };
}

/** Why Play would refuse this setup in the game, or null (BossEditorSection.PlayProblem, without the model check) */
export function playProblem(challenge: Challenge, rounds: number[]): string | null {
  if (GAME.unsupportedModes.includes(challenge.mode)) {
    return `The ${GAME.modes[challenge.mode] ?? challenge.mode} game mode can't be used for boss games.`;
  }
  if (challenge.round >= rounds[0]) {
    return `The game starts on round ${challenge.round}, but the first boss tier comes on round ${rounds[0]} and would be skipped.`;
  }
  return null;
}
