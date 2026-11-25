// Port of Persistence/SetupCode.cs (writing) and CodeFormat.Payload/Diff/Pack: setup -> share code (#12).
// The JSON inside is the mod's, key for key; the deflated bytes can differ from the mod's, which is fine, since the mod
// reads any raw deflate.

import { DATA, type Json, type JsonObject } from "./data";
import { PREFIX, VERSION2_PREFIX, VERSION3_PREFIX, VERSION4_PREFIX, VERSION5_PREFIX, VERSION6_PREFIX, VERSION7_PREFIX, filterChallenge } from "./decode";
import { parseTierBosses } from "./math/tierBosses";
import { defaultOptions, optionsJson, readOptions, sanitize, type BossOptions } from "./options";
import { UnsupportedBrowserError, canUseCodes } from "./support";

const ITEMS_KEY = "$items";

const isObject = (value: Json | undefined): value is JsonObject =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const clone = <T extends Json>(value: T): T => JSON.parse(JSON.stringify(value)) as T;

/** JToken.DeepEquals: the same value, with object properties in any order */
export function deepEqual(a: Json | undefined, b: Json | undefined): boolean {
  if (a === b) return true;
  if (Array.isArray(a) || Array.isArray(b)) {
    return Array.isArray(a) && Array.isArray(b) && a.length === b.length && a.every((item, i) => deepEqual(item, b[i]));
  }
  if (isObject(a) && isObject(b)) {
    const keys = Object.keys(a);
    return keys.length === Object.keys(b).length &&
      keys.every((key) => Object.prototype.hasOwnProperty.call(b, key) && deepEqual(a[key], b[key]));
  }
  return false;
}

/**
 * CodeFormat.Diff: what differs in value from defaults, or undefined if nothing does. Objects are compared property by
 * property and lists of the same length item by item, so changing one tower keeps the code short.
 */
export function diff(value: Json, defaults: Json | undefined): Json | undefined {
  if (isObject(value) && isObject(defaults)) {
    const changed: JsonObject = {};
    for (const [name, inner] of Object.entries(value)) {
      const standard = Object.prototype.hasOwnProperty.call(defaults, name) ? defaults[name] : undefined;
      const innerDiff = diff(inner, standard);
      if (innerDiff !== undefined) changed[name] = innerDiff;
    }
    return Object.keys(changed).length > 0 ? changed : undefined;
  }

  if (Array.isArray(value) && Array.isArray(defaults) && value.length === defaults.length) {
    const items: JsonObject = {};
    value.forEach((item, i) => {
      const innerDiff = diff(item, defaults[i]);
      if (innerDiff !== undefined) items[String(i)] = innerDiff;
    });
    return Object.keys(items).length > 0 ? { [ITEMS_KEY]: items } : undefined;
  }

  return deepEqual(value, defaults) ? undefined : clone(value);
}

/** The options brought into the Boss Creator's ranges first, so the mod reads back exactly what the code holds */
export const normalized = (options: BossOptions): BossOptions => sanitize(readOptions(optionsJson(options)), true);

/** The Boss Health range version 5 readers keep a value in, and the step they put a tier's own health on (#250) */
const VERSION5_MIN_BOSS_HEALTH = 0.1;
const VERSION5_MAX_BOSS_HEALTH = 20;
const VERSION5_HEALTH_STEP = 10;

/** CodeFormat.NeedsVersion6: whether version 5 readers would change the Boss Health or a tier's own health (#250) */
export function needsVersion6(options: BossOptions, challenge?: JsonObject): boolean {
  const modifiers = challenge?.bloonModifiers as JsonObject | undefined;
  const health = (modifiers?.healthMultipliers as JsonObject | undefined)?.boss;
  const bossHealth = typeof health === "number" ? health : 1;
  if (bossHealth < VERSION5_MIN_BOSS_HEALTH - 1e-6 || bossHealth > VERSION5_MAX_BOSS_HEALTH + 1e-6) return true;
  return parseTierBosses(options.TierBosses).some(
    (tier) =>
      tier.health < VERSION5_MIN_BOSS_HEALTH * 100 - 1e-6 ||
      tier.health > VERSION5_MAX_BOSS_HEALTH * 100 + 1e-6 ||
      tier.health % VERSION5_HEALTH_STEP !== 0,
  );
}

/** SetupCode.PrefixFor: the oldest code version that holds everything in the options */
export function prefixFor(options: BossOptions, challenge?: JsonObject): string {
  // The bosses a game picked for its Random tiers, which version 6 readers drop (#270)
  if (options.RandomTiers != null) return VERSION7_PREFIX;
  if (needsVersion6(options, challenge)) return VERSION6_PREFIX;
  if (options.ChimpsRounds === "Own") return VERSION5_PREFIX;
  if (options.BossEventRounds) return VERSION4_PREFIX;
  if (options.TierBosses != null) return VERSION3_PREFIX;
  if (options.SpeedPerTier !== 0 || options.HealthPerTier !== 0) return VERSION2_PREFIX;
  return PREFIX;
}

/**
 * CodeFormat.Payload: the options ("o") and the challenge ("c"), each as what differs from its defaults. The challenge
 * keeps only what a code may set (CodeFormat.ChallengeFields), as the mod would ignore the rest.
 */
export function payload(options: BossOptions, challenge: JsonObject = DATA.defaultChallenge): JsonObject {
  const o = diff(optionsJson(options), optionsJson(defaultOptions()));
  const c = diff(challenge, DATA.defaultChallenge);
  return {
    o: isObject(o) ? o : {},
    c: isObject(c) ? filterChallenge(c, DATA.defaultChallenge) : {},
  };
}

/** Base64 without the characters chat apps like to change: no +, / or = padding */
function toBase64Url(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

/** CodeFormat.Pack: JSON to a code, without any checks. Throws UnsupportedBrowserError without CompressionStream (#181). */
export async function pack(json: string, prefix = PREFIX): Promise<string> {
  if (!canUseCodes()) throw new UnsupportedBrowserError();
  const stream = new Blob([json]).stream().pipeThrough(new CompressionStream("deflate-raw"));
  const bytes = new Uint8Array(await new Response(stream).arrayBuffer());
  return prefix + toBase64Url(bytes);
}

/**
 * SetupCode.Encode: a code the mod loads as this setup. The options are brought into range first; the challenge is the
 * game's JSON of it, like the json decode gives, and defaults to the game's new challenge.
 */
export async function encode(options: BossOptions, challenge: JsonObject = DATA.defaultChallenge): Promise<string> {
  const setup = normalized(options);
  return pack(JSON.stringify(payload(setup, challenge)), prefixFor(setup, challenge));
}
