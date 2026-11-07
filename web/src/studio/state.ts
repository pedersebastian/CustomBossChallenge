// The setup the Studio edits (#12), and one pure change per Boss Creator control (Menus/BossEditorSection.cs).
// The challenge is the game's JSON of it, as decode gives it, so everything the Studio doesn't edit yet (map, rules,
// towers, Advanced Mode's tier bosses) stays as it was in the code.

import { GAME, type Json, type JsonObject } from "../data/types";
import { defaultOptions, effectiveSpawnRounds, type BossOptions, type SkullMode } from "../code/options";
import { DEFAULT_COUNT, DEFAULT_CUSTOM, MAX_SKULLS, normalizeCustom } from "../math/skulls";
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
