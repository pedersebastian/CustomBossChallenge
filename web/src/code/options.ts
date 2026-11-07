// Port of Config/BossOptions.cs: the boss part of a setup, read the way Json.NET reads it, then brought into range

import { GAME, type Json, type JsonObject } from "../data/types";
import { MAX_SKULLS, evenlySpaced, normalizeCustom, parseCustom } from "../math/skulls";
import {
  clampHealthPerTier,
  clampSpeedPerTier,
  clampTiers,
  healthPerTierFromExtraTierHealth,
  spawnRounds,
} from "../math/tiers";
import { clamp } from "../math/dotnet";
import { FOLLOWS, normalizeTierBosses, tierEntry, tierPercents, type TierBoss } from "../math/tierBosses";

export const SKULL_MODES = ["Vanilla", "EvenlySpaced", "Custom"] as const;
export type SkullMode = (typeof SKULL_MODES)[number];

export const BOSSES = Object.keys(GAME.bosses);

export interface BossOptions {
  Boss: string;
  Elite: boolean;
  SkullMode: SkullMode;
  SkullCount: number;
  CustomSkulls: string;
  Tiers: number;
  SpeedPerTier: number;
  HealthPerTier: number;
  SpawnRounds: string | null;
  /** The boss of each tier in Advanced Mode, or null for the same boss on every tier (codes from 2.9, CBC3) */
  TierBosses?: string | null;
  /** The boss events' own rounds, only with one boss for every tier (codes from 2.10, CBC4) */
  BossEventRounds: boolean;
  /** Codes from before 2.3; sanitize turns it into HealthPerTier */
  ExtraTierHealth?: number | null;
}

/** A value Json.NET can't turn into the option's type; the whole code is then unreadable, like in the mod */
export class OptionTypeError extends Error {}

const DEFAULTS = GAME.defaultOptions as unknown as BossOptions;

export const defaultOptions = (): BossOptions => ({ ...DEFAULTS });

/** The options as JSON, the way the mod writes them (OptionsJson in Persistence/SetupCode.cs) */
export function optionsJson(options: BossOptions): JsonObject {
  const json: JsonObject = {
    Boss: options.Boss,
    Elite: options.Elite,
    SkullMode: options.SkullMode,
    SkullCount: options.SkullCount,
    CustomSkulls: options.CustomSkulls,
    Tiers: options.Tiers,
    SpeedPerTier: options.SpeedPerTier,
    HealthPerTier: options.HealthPerTier,
    SpawnRounds: options.SpawnRounds,
  };
  if (options.ExtraTierHealth != null) json.ExtraTierHealth = options.ExtraTierHealth;
  if (options.TierBosses != null) json.TierBosses = options.TierBosses;
  json.BossEventRounds = options.BossEventRounds;
  return json;
}

function toInt(value: Json, name: string): number {
  const number = typeof value === "string" && /^\s*[+-]?\d+\s*$/.test(value) ? Number(value) : value;
  if (typeof number !== "number" || !Number.isInteger(number) || number < -2_147_483_648 || number > 2_147_483_647) {
    throw new OptionTypeError(`${name} is not a whole number`);
  }
  return number;
}

function toNullableString(value: Json, name: string): string | null {
  if (value === null) return null;
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  throw new OptionTypeError(`${name} is not text`);
}

function toEnum(value: Json, names: readonly string[], name: string): string | number {
  if (typeof value === "string") {
    const found = names.find((candidate) => candidate.toLowerCase() === value.trim().toLowerCase());
    if (found === undefined) throw new OptionTypeError(`${name} '${value}' is unknown`);
    return found;
  }
  // A number is the enum's value; an undefined one is replaced by sanitize
  return toInt(value, name);
}

/** The options a code can hold, and the JSON type each must have; a "?" type may also be null */
const OPTION_TYPES: Record<string, string> = {
  Boss: "enum", Elite: "bool", SkullMode: "enum", SkullCount: "int", CustomSkulls: "text?", Tiers: "int",
  SpeedPerTier: "int", HealthPerTier: "int", ExtraTierHealth: "int?", SpawnRounds: "text?", TierBosses: "text?",
  RandomTiers: "text?", BossEventRounds: "bool",
};

/** A whole number in text, only ASCII digits and spaces, like CodeFormat.IntText */
const INT_TEXT = /^[ \t\r\n]*[+-]?[0-9]+[ \t\r\n]*$/;
/** An enum's name in text, like CodeFormat.EnumText */
const ENUM_TEXT = /^[ \t\r\n]*[A-Za-z]+[ \t\r\n]*$/;

const isInt32 = (value: Json): value is number =>
  typeof value === "number" && Number.isInteger(value) && value >= -2_147_483_648 && value <= 2_147_483_647;

/**
 * CodeFormat.TypedOptions: a code's options, each with the JSON type it must have, or an OptionTypeError (#113). The mod
 * reads codes the same way, so neither takes Json.NET's guesses like 5.5 for 6, "1" for Lych or 3 for true. Options the
 * mod doesn't have are left out.
 */
export function typedOptions(diff: JsonObject): JsonObject {
  const typed: JsonObject = {};
  for (const [name, value] of Object.entries(diff)) {
    const type = Object.prototype.hasOwnProperty.call(OPTION_TYPES, name) ? OPTION_TYPES[name] : undefined;
    if (type === undefined) continue;
    if (value === null) {
      if (!type.endsWith("?")) throw new OptionTypeError(`${name} is null`);
      typed[name] = null;
      continue;
    }
    const base = type.replace("?", "");
    if (base === "bool" && typeof value === "boolean") typed[name] = value;
    else if (base === "text" && typeof value === "string") typed[name] = value;
    else if ((base === "int" || base === "enum") && isInt32(value)) typed[name] = value === 0 ? 0 : value;
    else if (base === "int" && typeof value === "string" && INT_TEXT.test(value) && isInt32(Number(value))) typed[name] = Number(value);
    else if (base === "enum" && typeof value === "string" && ENUM_TEXT.test(value)) typed[name] = value;
    else throw new OptionTypeError(`${name} has the wrong type`);
  }
  return typed;
}

/** Options as read, before sanitize: an enum can still be a number the game doesn't know */
export type RawOptions = Omit<BossOptions, "Boss" | "SkullMode"> & { Boss: string | number; SkullMode: string | number };

/** Reads the options JSON with the types BossOptions has; throws OptionTypeError like Json.NET would */
export function readOptions(json: JsonObject): RawOptions {
  // Missing keys keep the default; a null where a value is needed can't be read, like in Json.NET
  const get = (key: keyof BossOptions): Json =>
    Object.prototype.hasOwnProperty.call(json, key) ? json[key] : ((DEFAULTS[key] ?? null) as Json);
  const boolean = (value: Json, name: string): boolean => {
    if (typeof value !== "boolean") throw new OptionTypeError(`${name} is not true or false`);
    return value;
  };
  return {
    Boss: toEnum(get("Boss"), BOSSES, "Boss"),
    Elite: boolean(get("Elite"), "Elite"),
    SkullMode: toEnum(get("SkullMode"), SKULL_MODES, "SkullMode"),
    SkullCount: toInt(get("SkullCount"), "SkullCount"),
    CustomSkulls: toNullableString(get("CustomSkulls"), "CustomSkulls") as string,
    Tiers: toInt(get("Tiers"), "Tiers"),
    SpeedPerTier: toInt(get("SpeedPerTier"), "SpeedPerTier"),
    HealthPerTier: toInt(get("HealthPerTier"), "HealthPerTier"),
    SpawnRounds: toNullableString(get("SpawnRounds"), "SpawnRounds"),
    ExtraTierHealth: get("ExtraTierHealth") === null ? null : toInt(get("ExtraTierHealth"), "ExtraTierHealth"),
    TierBosses: toNullableString(get("TierBosses"), "TierBosses"),
    BossEventRounds: boolean(get("BossEventRounds"), "BossEventRounds"),
  };
}

const enumName = (value: string | number, names: readonly string[], fallback: string): string =>
  typeof value === "string" ? value : Number.isInteger(value) && value >= 0 && value < names.length ? names[value] : fallback;

/** BossOptions.Sanitized: every value in the range the Boss Creator allows */
export function sanitize(raw: RawOptions): BossOptions {
  return {
    Boss: enumName(raw.Boss, BOSSES, DEFAULTS.Boss),
    Elite: raw.Elite,
    SkullMode: enumName(raw.SkullMode, SKULL_MODES, DEFAULTS.SkullMode) as SkullMode,
    SkullCount: clamp(raw.SkullCount, 0, MAX_SKULLS),
    CustomSkulls: normalizeCustom(raw.CustomSkulls, DEFAULTS.CustomSkulls),
    Tiers: clampTiers(raw.Tiers),
    SpeedPerTier: clampSpeedPerTier(raw.SpeedPerTier),
    HealthPerTier: clampHealthPerTier(
      raw.ExtraTierHealth != null && raw.HealthPerTier === 0
        ? healthPerTierFromExtraTierHealth(raw.ExtraTierHealth)
        : raw.HealthPerTier,
    ),
    SpawnRounds: raw.SpawnRounds,
    TierBosses: normalizeTierBosses(raw.TierBosses),
    BossEventRounds: raw.BossEventRounds,
    ExtraTierHealth: null,
  };
}

/** The skull thresholds (0-1, descending) the options ask for, or null to keep the vanilla skulls */
export function skullPercents(options: BossOptions): number[] | null {
  if (options.SkullMode === "EvenlySpaced") return evenlySpaced(options.SkullCount);
  if (options.SkullMode === "Custom") return parseCustom(options.CustomSkulls);
  return null;
}

/**
 * The skull thresholds a tier asks for, or null to keep its boss's own: in Advanced Mode the tier's own skulls, and the
 * Skulls option for a tier that keeps Vanilla (BossOptions.TierSkullPercents)
 */
export const tierSkullPercents = (options: BossOptions, tier: number): number[] | null =>
  tierPercents(entryOf(options, tier)) ?? skullPercents(options);

/** The skull mode a tier has, the tier's own in Advanced Mode */
export function tierSkullMode(options: BossOptions, tier: number): SkullMode {
  const own = entryOf(options, tier).skulls;
  return own === "Vanilla" ? options.SkullMode : own;
}

/** The round each tier spawns on */
export const effectiveSpawnRounds = (options: BossOptions): number[] => spawnRounds(options.Tiers, options.SpawnRounds);

/** Whether the tiers can have different bosses */
export const isMixed = (options: BossOptions): boolean => options.TierBosses != null;

/** BossOptions.UsesBossEventRounds: the event rounds are played in Standard and in Advanced Mode (#84) */
export const usesBossEventRounds = (options: BossOptions): boolean => options.BossEventRounds;

/** What a tier was set to: the setup's boss (boss null), a boss of its own, or Random (BossOptions.EntryOf) */
export const entryOf = (options: BossOptions, tier: number): TierBoss =>
  isMixed(options) ? tierEntry(options.TierBosses, tier) : FOLLOWS;

/** The boss and Elite of a tier; a Random tier has the setup's boss until the game starts (BossOptions.BossOf) */
export function bossOf(options: BossOptions, tier: number): { boss: string; elite: boolean } {
  const entry = entryOf(options, tier);
  return entry.boss === null || entry.boss === "Random" || !BOSSES.includes(entry.boss)
    ? { boss: options.Boss, elite: options.Elite }
    : { boss: entry.boss, elite: entry.elite };
}

/** The boss a tier has in Advanced Mode, e.g. "Elite Lych" or "Random" */
export function tierBossName(options: BossOptions, tier: number): string {
  const entry = entryOf(options, tier);
  if (entry.boss === "Random") return entry.elite ? "Random Elite" : "Random";
  const { boss, elite } = bossOf(options, tier);
  return `${elite ? "Elite " : ""}${boss}`;
}

/** The name a new setup gets, e.g. "Custom Lych" or "Custom Elite Lych" */
export const defaultName = (options: BossOptions): string =>
  isMixed(options) ? "Custom Boss Gauntlet" : `Custom ${options.Elite ? "Elite " : ""}${options.Boss}`;
