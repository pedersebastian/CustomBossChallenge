// The setup the Studio edits (#12), and one pure change per Boss Creator control (Menus/BossEditorSection.cs).
// The challenge is the game's JSON of it, as decode gives it, so everything the Studio doesn't edit yet (map, rules,
// towers, Advanced Mode's tier bosses) stays as it was in the code.

import { GAME, type Json, type JsonObject } from "../data/types";
import { defaultOptions, effectiveSpawnRounds, entryOf, isMixed, type BossOptions, type SkullMode } from "../code/options";
import {
  FOLLOWS,
  MAX_HEALTH,
  MAX_SPEED,
  MIN_HEALTH,
  MIN_SPEED,
  clampValue,
  resolveTiers,
  stepTier,
  withTier,
  type TierBoss,
} from "../math/tierBosses";
import { DEFAULT_COUNT, DEFAULT_CUSTOM, MAX_SKULLS, normalizeCustom } from "../math/skulls";
import { MAX_TIERS } from "../math/tiers";
import { clamp } from "../math/dotnet";
import { NO_LIMIT, fromSlider, toSlider } from "../math/paragons";
import {
  BOSS_HEALTH_SLIDER,
  BOSS_SPEED_SLIDER,
  MAX_FIRST_SPAWN_ROUND,
  MAX_ROUNDS_BETWEEN_TIERS,
  ROUNDS_BETWEEN_TIERS,
  clampHealthPerTier,
  clampSpeedPerTier,
  clampTiers,
  evenRounds,
  firstAndGap,
} from "../math/tiers";

export interface StudioSetup {
  options: BossOptions;
  /** The game's JSON of the challenge, like decode's json */
  challenge: JsonObject;
}

const clone = <T extends Json>(value: T): T => JSON.parse(JSON.stringify(value)) as T;

const isObject = (value: Json | undefined): value is JsonObject =>
  typeof value === "object" && value !== null && !Array.isArray(value);

/** A new setup: the Boss Creator's defaults on the game's new challenge */
export const newSetup = (): StudioSetup => ({ options: defaultOptions(), challenge: clone(GAME.defaultChallenge) });

const withOptions = (setup: StudioSetup, change: Partial<BossOptions>): StudioSetup => ({
  ...setup,
  options: { ...setup.options, ...change },
});

/** A copy of the challenge with one change made to it */
function withChallenge(setup: StudioSetup, change: (challenge: JsonObject) => void): StudioSetup {
  const challenge = clone(setup.challenge);
  change(challenge);
  return { ...setup, challenge };
}

/** An object in the challenge, made when a code's challenge has none */
function objectAt(parent: JsonObject, key: string): JsonObject {
  const value = parent[key];
  if (isObject(value)) return value;
  const made: JsonObject = {};
  parent[key] = made;
  return made;
}

const snap = (value: number, slider: { min: number; max: number; step: number }): number =>
  clamp(Math.round(value / slider.step) * slider.step, slider.min, slider.max);

const number = (value: Json | undefined, fallback: number): number => (typeof value === "number" ? value : fallback);

// ---------- Boss ----------

export const setBoss = (setup: StudioSetup, boss: string): StudioSetup => withOptions(setup, { Boss: boss });
export const setElite = (setup: StudioSetup, elite: boolean): StudioSetup => withOptions(setup, { Elite: elite });
export const resetBoss = (setup: StudioSetup): StudioSetup =>
  withOptions(setup, { Boss: defaultOptions().Boss, Elite: defaultOptions().Elite });

// ---------- Boss Speed, Boss Health and Max Paragons (in the challenge) ----------

const modifiers = (challenge: JsonObject): JsonObject => (isObject(challenge.bloonModifiers) ? challenge.bloonModifiers : {});

/** Boss Speed in percent, as the slider shows it */
export const bossSpeed = (setup: StudioSetup): number => Math.round(number(modifiers(setup.challenge).bossSpeedMultiplier, 1) * 100);

/** Boss Health in percent, as the slider shows it */
export const bossHealth = (setup: StudioSetup): number => {
  const health = modifiers(setup.challenge).healthMultipliers;
  return Math.round(number(isObject(health) ? health.boss : undefined, 1) * 100);
};

export const setBossSpeed = (setup: StudioSetup, percent: number): StudioSetup =>
  withChallenge(setup, (challenge) => {
    objectAt(challenge, "bloonModifiers").bossSpeedMultiplier = snap(percent, BOSS_SPEED_SLIDER) / 100;
  });

export const setBossHealth = (setup: StudioSetup, percent: number): StudioSetup =>
  withChallenge(setup, (challenge) => {
    objectAt(objectAt(challenge, "bloonModifiers"), "healthMultipliers").boss = snap(percent, BOSS_HEALTH_SLIDER) / 100;
  });

/** The Max Paragons slider's position: the last one is no limit */
export const paragonSlider = (setup: StudioSetup): number => toSlider(number(setup.challenge.maxParagons, NO_LIMIT));

export const setParagonSlider = (setup: StudioSetup, position: number): StudioSetup =>
  withChallenge(setup, (challenge) => {
    challenge.maxParagons = fromSlider(clamp(Math.round(position), 0, GAME.limits.paragons));
  });

// ---------- Tiers ----------

export const setTiers = (setup: StudioSetup, tiers: number): StudioSetup => withOptions(setup, { Tiers: clampTiers(Math.round(tiers)) });

export const setSpeedPerTier = (setup: StudioSetup, percent: number): StudioSetup =>
  withOptions(setup, { SpeedPerTier: clampSpeedPerTier(Math.round(percent)) });

export const setHealthPerTier = (setup: StudioSetup, percent: number): StudioSetup =>
  withOptions(setup, { HealthPerTier: clampHealthPerTier(Math.round(percent)) });

// ---------- Spawn rounds ----------

/** First Boss Round and Rounds Between Tiers as the sliders show them (BossEditorSection.ShowSpawnRounds) */
export function spawnSliders(setup: StudioSetup, gap = ROUNDS_BETWEEN_TIERS): { first: number; gap: number } {
  const shown = firstAndGap(effectiveSpawnRounds(setup.options), gap);
  return { first: clamp(shown.first, 1, MAX_FIRST_SPAWN_ROUND), gap: clamp(shown.gap, 1, MAX_ROUNDS_BETWEEN_TIERS) };
}

/** Evenly spaced spawn rounds from the two sliders (BossEditorSection.SpreadSpawnRounds) */
export const spreadSpawnRounds = (setup: StudioSetup, first: number, gap: number): StudioSetup =>
  withOptions(setup, {
    SpawnRounds: evenRounds(clamp(Math.round(first), 1, MAX_FIRST_SPAWN_ROUND), clamp(Math.round(gap), 1, MAX_ROUNDS_BETWEEN_TIERS)),
  });

/** Spawn Rounds as typed; empty follows the defaults again */
export const setSpawnRounds = (setup: StudioSetup, text: string): StudioSetup =>
  withOptions(setup, { SpawnRounds: text.trim() === "" ? null : text });

export const resetSpawnRounds = (setup: StudioSetup): StudioSetup => withOptions(setup, { SpawnRounds: null });

export const setBossEventRounds = (setup: StudioSetup, on: boolean): StudioSetup => withOptions(setup, { BossEventRounds: on });

// ---------- Skulls ----------

export const setSkullMode = (setup: StudioSetup, mode: SkullMode): StudioSetup => withOptions(setup, { SkullMode: mode });

export const setSkullCount = (setup: StudioSetup, count: number): StudioSetup =>
  withOptions(setup, { SkullCount: clamp(Math.round(count), 0, MAX_SKULLS) });

/** Custom % as typed: what was understood, or the skulls as they were when nothing in it is valid */
export const setCustomSkulls = (setup: StudioSetup, text: string): StudioSetup =>
  withOptions(setup, { CustomSkulls: normalizeCustom(text, setup.options.CustomSkulls) });

export const resetSkulls = (setup: StudioSetup): StudioSetup =>
  withOptions(setup, { SkullMode: "Vanilla", SkullCount: DEFAULT_COUNT, CustomSkulls: DEFAULT_CUSTOM });

// ---------- Advanced Mode: a boss per tier (#95) ----------

/**
 * What the Boss Creator remembers while switching between Standard and Advanced (BossEditorSection's lastTierBosses
 * and standardValues): the tier bosses to come back to, and Standard's speed, health and skulls
 */
export interface AdvancedMemory {
  lastTierBosses: string | null;
  standardValues: TierBoss | null;
}

export const NO_MEMORY: AdvancedMemory = { lastTierBosses: null, standardValues: null };

/** What a tier is now, with the setup's boss and Elite filled in for a tier that follows it (TierBossPicker.Shown) */
export function shownTier(setup: StudioSetup, tier: number): TierBoss {
  const entry = entryOf(setup.options, tier);
  return entry.boss === null ? { ...entry, boss: setup.options.Boss, elite: setup.options.Elite } : entry;
}

/** Gives a tier its own boss, or makes it follow the setup's with FOLLOWS (BossEditorSection.SetTierBoss) */
export const setTierBoss = (setup: StudioSetup, tier: number, boss: TierBoss): StudioSetup =>
  isMixed(setup.options) ? withOptions(setup, { TierBosses: withTier(setup.options.TierBosses, tier, boss) }) : setup;

/** A boss or Random for a tier, keeping its Elite, speed, health and skulls (TierBossPicker.Pick) */
export const pickTierBoss = (setup: StudioSetup, tier: number, choice: string): StudioSetup =>
  setTierBoss(setup, tier, { ...shownTier(setup, tier), boss: choice });

/** The next or previous boss of a tier, as the arrows do on the Tier Preview (BossEditorSection.StepTierBoss) */
export const stepTierBoss = (setup: StudioSetup, tier: number, direction: number): StudioSetup =>
  setTierBoss(setup, tier, stepTier(entryOf(setup.options, tier), setup.options.Boss, setup.options.Elite, direction));

const changeTier = (setup: StudioSetup, tier: number, change: Partial<TierBoss>): StudioSetup =>
  setTierBoss(setup, tier, { ...shownTier(setup, tier), ...change });

export const setTierElite = (setup: StudioSetup, tier: number, elite: boolean): StudioSetup => changeTier(setup, tier, { elite });

export const setTierSpeed = (setup: StudioSetup, tier: number, percent: number): StudioSetup =>
  changeTier(setup, tier, { speed: clampValue(percent, MIN_SPEED, MAX_SPEED) });

export const setTierHealth = (setup: StudioSetup, tier: number, percent: number): StudioSetup =>
  changeTier(setup, tier, { health: clampValue(percent, MIN_HEALTH, MAX_HEALTH) });

export const setTierSkullMode = (setup: StudioSetup, tier: number, skulls: SkullMode): StudioSetup => changeTier(setup, tier, { skulls });

export const setTierSkullCount = (setup: StudioSetup, tier: number, count: number): StudioSetup =>
  changeTier(setup, tier, { skullCount: clamp(Math.round(count), 0, MAX_SKULLS) });

/** A tier's Custom % as typed: what was understood, or its skulls as they were when nothing in it is valid */
export const setTierCustomSkulls = (setup: StudioSetup, tier: number, text: string): StudioSetup =>
  changeTier(setup, tier, { customSkulls: normalizeCustom(text, shownTier(setup, tier).customSkulls) });

export const resetTierSkulls = (setup: StudioSetup, tier: number): StudioSetup =>
  changeTier(setup, tier, { skulls: "Vanilla", skullCount: DEFAULT_COUNT, customSkulls: DEFAULT_CUSTOM });

/** Standard's Boss Speed, Boss Health and skulls, as a tier's own values */
const standardOf = (setup: StudioSetup): TierBoss => ({
  ...FOLLOWS,
  speed: bossSpeed(setup),
  health: bossHealth(setup),
  skulls: setup.options.SkullMode,
  skullCount: setup.options.SkullCount,
  customSkulls: setup.options.CustomSkulls,
});

/** Standard's speed, health and skulls set from a tier's (BossEditorSection.SetBossValues) */
const setBossValues = (setup: StudioSetup, values: TierBoss): StudioSetup =>
  setBossHealth(
    setBossSpeed(
      withOptions(setup, { SkullMode: values.skulls, SkullCount: values.skullCount, CustomSkulls: values.customSkulls }),
      values.speed,
    ),
    values.health,
  );

/** What every tier starts with in Advanced Mode: Standard's boss, Elite, speed, health and skulls */
const startingTierBosses = (options: BossOptions, values: TierBoss): string =>
  resolveTiers(null, options.Boss, options.Elite, MAX_TIERS, () => 0, values);

/**
 * The Advanced (on) or Standard tab (BossEditorSection.SetAdvanced). Advanced starts every tier from Standard's boss,
 * Elite, speed, health and skulls, which then stay at 100 % and Vanilla; Standard brings them back, and keeps the
 * tiers' bosses for going back to Advanced when they were changed.
 */
export function setAdvanced(setup: StudioSetup, on: boolean, memory: AdvancedMemory): { setup: StudioSetup; memory: AdvancedMemory } {
  if (on === isMixed(setup.options)) return { setup, memory };
  const current = standardOf(setup);
  const standard = on ? current : (memory.standardValues ?? FOLLOWS);
  const lastTierBosses = on
    ? memory.lastTierBosses
    : setup.options.TierBosses === startingTierBosses(setup.options, standard)
      ? null
      : (setup.options.TierBosses ?? null);
  const switched = withOptions(setup, {
    TierBosses: on ? (lastTierBosses ?? startingTierBosses(setup.options, standard)) : null,
  });
  return {
    setup: setBossValues(switched, on ? FOLLOWS : standard),
    memory: { lastTierBosses, standardValues: on ? current : null },
  };
}

/**
 * Back to a new setup's boss: Bloonarius, not Elite, the same for every tier, with Standard's speed, health and skulls
 * as they were before Advanced (BossEditorSection.ResetBoss)
 */
export function resetAllTiers(setup: StudioSetup, memory: AdvancedMemory): { setup: StudioSetup; memory: AdvancedMemory } {
  const defaults = defaultOptions();
  if (!isMixed(setup.options)) return { setup: resetBoss(setup), memory: NO_MEMORY };
  const standard = setBossValues(setup, memory.standardValues ?? FOLLOWS);
  return { setup: withOptions(standard, { Boss: defaults.Boss, Elite: defaults.Elite, TierBosses: null }), memory: NO_MEMORY };
}
