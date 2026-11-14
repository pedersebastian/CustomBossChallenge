// Port of Persistence/SetupCode.cs (reading) and Config/ChallengeLimits.Sanitize: share code -> setup.
// Like the mod, a code is only data: it is never run, never throws and is held to what the game can play.

import { DATA, type Json, type JsonObject } from "./data";
import { netTrim, netTrimEnd } from "./math/dotnet";
import { clampMultiplier, orDefault } from "./math/limits";
import { MAX_ROUND } from "./math/tiers";
import { OptionTypeError, defaultName, defaultOptions, optionsJson, readOptions, sanitize, typedOptions, type BossOptions } from "./options";

export const PREFIX = "CBC1:";
export const VERSION2_PREFIX = "CBC2:";
export const VERSION3_PREFIX = "CBC3:";
export const VERSION4_PREFIX = "CBC4:";
export const VERSION5_PREFIX = "CBC5:";
const KNOWN_PREFIXES = [PREFIX, VERSION2_PREFIX, VERSION3_PREFIX, VERSION4_PREFIX, VERSION5_PREFIX];
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

const isInt = (value: Json): boolean => typeof value === "number" && Number.isInteger(value) && value >= -2147483648 && value <= 2147483647;

function hasType(value: Json, type: Json): boolean {
  switch (type) {
    case "text":
      return typeof value === "string";
    case "bool":
      return typeof value === "boolean";
    case "int":
      return isInt(value);
    case "float":
      return typeof value === "number" && Number.isFinite(value);
    default:
      return false;
  }
}

function filterObject(diff: JsonObject, fields: JsonObject, defaults: Json | undefined, path: string, dropped: string[]): JsonObject {
  const kept: JsonObject = {};
  for (const [name, value] of Object.entries(diff)) {
    const at = path === "" ? name : `${path}.${name}`;
    const field = name.startsWith("$") || !Object.prototype.hasOwnProperty.call(fields, name) ? undefined : fields[name];
    const before = dropped.length;
    const standard = isObject(defaults) ? defaults[name] : undefined;
    const filtered = field === undefined || value === null ? undefined : filterValue(value, field, standard, at, dropped);
    if (filtered !== undefined) kept[name] = filtered;
    // Name what was left out, unless a part of it already says what; an empty object changes nothing
    else if (field === undefined || (dropped.length === before && !(isObject(value) && Object.keys(value).length === 0))) dropped.push(at);
  }
  return kept;
}

function filterValue(value: Json, field: Json, defaults: Json | undefined, path: string, dropped: string[]): Json | undefined {
  if (isObject(field)) {
    if (!isObject(value)) return undefined;
    const inner = filterObject(value, field, defaults, path, dropped);
    return Object.keys(inner).length > 0 ? inner : undefined;
  }
  if (Array.isArray(field) && field.length === 1 && isObject(field[0])) {
    return filterList(value, field[0], Array.isArray(defaults) ? defaults : undefined, path, dropped);
  }
  return hasType(value, field) ? clone(value) : undefined;
}

function filterList(value: Json, entry: JsonObject, defaults: Json[] | undefined, path: string, dropped: string[]): Json | undefined {
  const key = typeof entry.$key === "string" ? entry.$key : undefined;
  const items: JsonObject = {};
  const add = (index: number, change: JsonObject, at: string) => {
    const inner = filterObject(change, entry, defaults?.[index], at, dropped);
    if (Object.keys(inner).length > 0) items[String(index)] = inner;
  };

  if (isObject(value) && Object.keys(value).length === 1 && isObject(value[ITEMS_KEY])) {
    for (const [index, change] of Object.entries(value[ITEMS_KEY])) {
      const at = `${path}[${index}]`;
      const i = Number(index);
      if (isObject(change) && /^\d+$/.test(index) && String(i) === index && i < (defaults?.length ?? 0)) add(i, change, at);
      else dropped.push(at);
    }
  } else if (Array.isArray(value) && key !== undefined && defaults !== undefined) {
    value.forEach((item, n) => {
      const at = `${path}[${n}]`;
      const id = isObject(item) && typeof item[key] === "string" ? item[key] : undefined;
      const i = id === undefined ? -1 : defaults.findIndex((standard) => isObject(standard) && standard[key] === id);
      if (i < 0 || !isObject(item)) {
        dropped.push(at);
        return;
      }
      // The entry's key says which entry it is; only its settings are changes
      const standard = defaults[i];
      const change: JsonObject = {};
      for (const [name, setting] of Object.entries(item)) {
        if (name === key) continue;
        if (isObject(standard) && JSON.stringify(standard[name]) === JSON.stringify(setting)) continue;
        change[name] = setting;
      }
      add(i, change, at);
    });
  } else {
    return undefined;
  }

  return Object.keys(items).length > 0 ? { [ITEMS_KEY]: items } : undefined;
}

/**
 * The part of a challenge diff that DATA.challengeFields allows, with the right types (CodeFormat.FilterChallenge):
 * a code can only set what the Challenge Editor and the Boss Creator can (#108). The paths left out go to dropped.
 */
export function filterChallenge(diff: JsonObject, defaults: JsonObject, dropped: string[] = []): JsonObject {
  return filterObject(diff, DATA.challengeFields, defaults, "", dropped);
}

function fromBase64Url(text: string): Uint8Array {
  const base64 = text.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");
  if (!/^[A-Za-z0-9+/]*={0,2}$/.test(padded) || padded.length % 4 !== 0) throw new Error("Not base64");
  const binary = atob(padded);
  return Uint8Array.from(binary, (c) => c.charCodeAt(0));
}

/** CodeFormat.MaxJsonDepth: the deepest nesting of objects and arrays Json.NET reads (#174) */
export const MAX_JSON_DEPTH = 64;

/** How deeply objects and arrays nest in the JSON text, counting only brackets outside of strings */
export function jsonDepth(json: string): number {
  let depth = 0;
  let deepest = 0;
  let inString = false;
  for (let i = 0; i < json.length; i++) {
    const c = json[i];
    if (inString) {
      if (c === "\\") i++;
      else if (c === '"') inString = false;
    } else if (c === '"') inString = true;
    else if (c === "{" || c === "[") deepest = Math.max(deepest, ++depth);
    else if (c === "}" || c === "]") depth--;
  }
  return deepest;
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
    if (text.length > DATA.limits.maxJsonLength) {
      await reader.cancel();
      throw new Error("Setup code unpacks to too much data");
    }
  }
  return text + decoder.decode();
}

/** What comes before the code in a link: #code=CBC2:… */
export const LINK_KEY = "code=";

/**
 * The code in pasted text: the text itself, or the part after # in a link to the website (#code=CBC2:… or #CBC2:…,
 * also %-encoded or wrapped in &lt;…&gt; as Discord does). Text without a code in a link comes back trimmed, so decode
 * still says what's wrong with it. Same rules as CodeFormat.ExtractCode in the mod.
 */
/**
 * Uri.UnescapeDataString, which the mod uses: every %XX that makes a valid UTF-8 character is decoded, and every other
 * % is left as it is. decodeURIComponent instead gives up on the whole text at the first bad one (#113).
 */
export function unescapeDataString(text: string): string {
  const utf8 = new TextDecoder("utf-8", { fatal: true });
  return text.replace(/(?:%[0-9A-Fa-f]{2})+/g, (run) => {
    const bytes = run.slice(1).split("%").map((hex) => parseInt(hex, 16));
    let result = "";
    for (let i = 0; i < bytes.length; ) {
      const lead = bytes[i];
      const length = lead < 0x80 ? 1 : lead >= 0xc2 && lead < 0xe0 ? 2 : lead >= 0xe0 && lead < 0xf0 ? 3 : lead >= 0xf0 && lead < 0xf5 ? 4 : 0;
      let decoded: string | null = null;
      if (length > 0 && i + length <= bytes.length) {
        try {
          decoded = utf8.decode(new Uint8Array(bytes.slice(i, i + length)));
        } catch {
          decoded = null;
        }
      }
      if (decoded === null) {
        // Not a character: kept as the text it was
        result += run.slice(i * 3, i * 3 + 3);
        i++;
      } else {
        result += decoded;
        i += length;
      }
    }
    return result;
  });
}

export function extractCode(text: string | null | undefined): string {
  let trimmed = netTrim(text ?? "");
  if (trimmed.startsWith("<") && trimmed.endsWith(">")) trimmed = netTrim(trimmed.slice(1, -1));
  const hash = trimmed.lastIndexOf("#");
  if (hash < 0) return trimmed;

  let fragment = trimmed.slice(hash + 1);
  if (fragment.startsWith(LINK_KEY)) fragment = fragment.slice(LINK_KEY.length);
  fragment = netTrim(unescapeDataString(fragment));
  return fragment.startsWith("CBC") ? fragment : trimmed;
}

/** Reads a code, or a link to one. Never throws: anything that isn't a usable code gives a problem and a reason. */
export async function decode(code: string | null | undefined): Promise<DecodeResult> {
  const trimmed = extractCode(code);
  if (trimmed === "") return fail("Invalid", "The code is empty.");
  if (trimmed.length > DATA.limits.maxCodeLength) return fail("Invalid", "The code is too long.");
  if (!KNOWN_PREFIXES.some((prefix) => trimmed.startsWith(prefix))) {
    if (trimmed.startsWith("CBC") && trimmed.length > 4 && /\d/.test(trimmed[3])) {
      return fail("TooNew", "This code comes from a newer version of Custom Boss Challenge.");
    }
    return fail("Invalid", "This isn't a Boss Creator code. Codes start with CBC1:, CBC2:, CBC3:, CBC4: or CBC5:.");
  }

  try {
    const text = await inflate(fromBase64Url(trimmed.slice(PREFIX_LENGTH)));
    if (jsonDepth(text) > MAX_JSON_DEPTH) return fail("Invalid", "The code is damaged or cut short.");
    const payload: Json = JSON.parse(text);
    if (!isObject(payload)) return fail("Invalid", "The code is damaged.");

    const optionsJsonValue = optionsJson(defaultOptions());
    if (isObject(payload.o)) apply(optionsJsonValue, typedOptions(payload.o));
    const options = sanitize(readOptions(optionsJsonValue));

    const challengeJson = clone(DATA.defaultChallenge);
    if (isObject(payload.c)) apply(challengeJson, filterChallenge(payload.c, DATA.defaultChallenge));
    const challenge = readChallenge(challengeJson, options);

    return { ok: true, problem: "None", options, challenge, json: challengeJson };
  } catch (e) {
    return fail("Invalid", e instanceof OptionTypeError ? `The code has a broken value: ${e.message}.` : "The code is damaged or cut short.");
  }
}

/** A name without control characters, at most 60 characters long (ChallengeLimits.CleanName) */
export function cleanName(name: string, fallback: string): string {
  // eslint-disable-next-line no-control-regex
  let clean = netTrim(name.replace(/[\u0000-\u001f\u007f-\u009f]/g, ""));
  if (clean.length > DATA.limits.maxNameLength) clean = netTrimEnd(clean.slice(0, DATA.limits.maxNameLength));
  return clean.length > 0 ? clean : fallback;
}

/** The challenge with the types the game reads it as, brought into range like ChallengeLimits.Sanitize */
function readChallenge(json: JsonObject, options: BossOptions): Challenge {
  const limits = DATA.limits;
  const defaults = DATA.defaultChallenge;
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
  const multiplier = (value: Json | undefined, min: number, max: number): number => clampMultiplier(number(value, 1), min, max);

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
    map: known(text(json.map), Object.keys(DATA.maps), defaults.map as string),
    difficulty: known(text(json.difficulty), Object.keys(DATA.difficulties), defaults.difficulty as string),
    mode: known(text(json.mode), Object.keys(DATA.modes), defaults.mode as string),
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
    maxParagons: orDefault(int(json.maxParagons, -1), 0, limits.maxTowerCount),
    maxTowers: orDefault(int(json.maxTowers, -1), 0, limits.maxTowerCount),
    disableMK: bool(json.disableMK),
    disableSelling: bool(json.disableSelling),
    disablePowers: bool(json.disablePowers),
    towers,
  };
}

/** Why Play would refuse this setup in the game, or null (BossEditorSection.PlayProblem, without the model check) */
export function playProblem(challenge: Challenge, rounds: number[]): string | null {
  if (DATA.unsupportedModes.includes(challenge.mode)) {
    return `The ${DATA.modes[challenge.mode] ?? challenge.mode} game mode can't be used for boss games.`;
  }
  if (challenge.round >= rounds[0]) {
    return `The game starts on round ${challenge.round}, but the first boss tier comes on round ${rounds[0]} and would be skipped.`;
  }
  return null;
}
