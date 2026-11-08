// The Challenge Editor's rules in the Studio (#97): the name, map, difficulty and mode, how the game starts, the bloon
// modifiers and the on/off rules. One table drives both the edit functions and the page. Every value is held to
// Config/ChallengeLimits.cs's limits (GAME.limits, written by WebDataTest), so the mod keeps it as it is on load.
// Tower restrictions are not here yet. The end round isn't either: in a boss game it always follows the last tier.

import { GAME, type Json, type JsonObject } from "../data/types";
import { clamp } from "../math/dotnet";
import { orDefault } from "../math/limits";
import { MAX_ROUND } from "../math/tiers";
import type { StudioSetup } from "./state";

export type RuleGroup = "Game" | "Start" | "Bloons" | "Rules";

interface RuleBase {
  /** Where it is in the game's JSON of the challenge, e.g. ["startRules", "cash"] */
  path: string[];
  label: string;
  group: RuleGroup;
}

export type Rule = RuleBase &
  (
    | { kind: "text"; maxLength: number }
    | { kind: "choice"; choices: Choice[] }
    /** A whole number, or -1 for the game's default */
    | { kind: "count"; min: number; max: number }
    /** A multiplier, edited in percent */
    | { kind: "percent"; min: number; max: number }
    | { kind: "toggle" }
  );

/** A choice in a list; choices with a group are shown under it */
export interface Choice {
  value: string;
  label: string;
  disabled: boolean;
  group?: string;
}

const limits = GAME.limits;

/** The game's map groups, easiest first */
export const MAP_GROUPS = ["Beginner", "Intermediate", "Advanced", "Expert"];

/** The group for a map the game data has no group for, e.g. one added after the website's data was made */
export const OTHER_MAPS = "Other";

/** The maps in the game's own order, each under its group like in the game's map lists (#97) */
export const mapChoices = (): Choice[] =>
  Object.entries(GAME.maps).map(([value, label]) => {
    const group = GAME.mapDifficulties[value];
    return { value, label, disabled: false, group: MAP_GROUPS.includes(group) ? group : OTHER_MAPS };
  });

const named = (names: Record<string, string>, disabled: string[] = [], note = ""): Choice[] =>
  Object.entries(names).map(([value, label]) => ({
    value,
    label: disabled.includes(value) ? `${label} ${note}` : label,
    disabled: disabled.includes(value),
  }));

const percent = (path: string[], label: string): Rule => ({
  path,
  label,
  group: "Bloons",
  kind: "percent",
  min: limits.minMultiplier,
  max: limits.maxMultiplier,
});

const toggle = (path: string[], label: string, group: RuleGroup): Rule => ({ path, label, group, kind: "toggle" });

/** The rules in the order the page shows them */
export const RULES: Rule[] = [
  { path: ["name"], label: "Challenge name", group: "Game", kind: "text", maxLength: limits.maxNameLength },
  { path: ["map"], label: "Map", group: "Game", kind: "choice", choices: mapChoices() },
  { path: ["difficulty"], label: "Difficulty", group: "Game", kind: "choice", choices: named(GAME.difficulties) },
  {
    path: ["mode"],
    label: "Mode",
    group: "Game",
    kind: "choice",
    choices: named(GAME.modes, GAME.unsupportedModes, "(can't be used for boss games)"),
  },
  { path: ["startRules", "cash"], label: "Starting cash", group: "Start", kind: "count", min: 0, max: limits.maxCash },
  { path: ["startRules", "lives"], label: "Lives", group: "Start", kind: "count", min: 1, max: limits.maxLives },
  { path: ["startRules", "maxLives"], label: "Max lives", group: "Start", kind: "count", min: 1, max: limits.maxLives },
  { path: ["startRules", "round"], label: "Start round", group: "Start", kind: "count", min: 1, max: MAX_ROUND },
  percent(["bloonModifiers", "speedMultiplier"], "Bloon speed"),
  percent(["bloonModifiers", "moabSpeedMultiplier"], "MOAB speed"),
  percent(["bloonModifiers", "regrowRateMultiplier"], "Regrow rate"),
  percent(["bloonModifiers", "healthMultipliers", "bloons"], "Bloon health"),
  percent(["bloonModifiers", "healthMultipliers", "moabs"], "MOAB health"),
  toggle(["bloonModifiers", "allCamo"], "All Camo", "Bloons"),
  toggle(["bloonModifiers", "allRegen"], "All Regrow", "Bloons"),
  toggle(["bloonModifiers", "allFortified"], "All Fortified", "Bloons"),
  { path: ["maxTowers"], label: "Max towers", group: "Rules", kind: "count", min: 0, max: limits.maxTowerCount },
  toggle(["disableMK"], "No Monkey Knowledge", "Rules"),
  toggle(["disableSelling"], "No selling", "Rules"),
  toggle(["disablePowers"], "No powers", "Rules"),
  toggle(["noContinues"], "No continues", "Rules"),
  toggle(["disableDoubleCash"], "No Double Cash", "Rules"),
  toggle(["disableFastTrack"], "No Fast Track", "Rules"),
  toggle(["noInstaReward"], "No Insta Monkey reward", "Rules"),
];

export const ruleId = (rule: Rule): string => rule.path.join(".");

const isObject = (value: Json | undefined): value is JsonObject =>
  typeof value === "object" && value !== null && !Array.isArray(value);

const at = (json: JsonObject, path: string[]): Json | undefined =>
  path.reduce<Json | undefined>((value, key) => (isObject(value) && Object.prototype.hasOwnProperty.call(value, key) ? value[key] : undefined), json);

/** The rule's value in the setup, or the new challenge's when the code doesn't have it */
export function ruleValue(setup: StudioSetup, rule: Rule): Json {
  return at(setup.challenge, rule.path) ?? at(GAME.defaultChallenge, rule.path) ?? null;
}

/** A name as the mod keeps it: no control characters, at most the limit; empty is no name of its own */
export function cleanText(text: string, maxLength: number): string {
  // eslint-disable-next-line no-control-regex
  const clean = text.replace(/[\u0000-\u001f\u007f-\u009f]/g, "").trim();
  return clean.length > maxLength ? clean.slice(0, maxLength).trimEnd() : clean;
}

/**
 * The value a control gives, held to the rule's limits: text cleaned, an unknown choice refused (null), an empty or
 * negative count the game's default (-1), a percent as its multiplier
 */
export function ruleInput(rule: Rule, input: string | boolean): Json | null {
  switch (rule.kind) {
    case "text":
      return cleanText(String(input), rule.maxLength);
    case "choice":
      return rule.choices.some((choice) => choice.value === input) ? String(input) : null;
    case "count": {
      const text = String(input).trim();
      const value = text === "" ? -1 : Math.round(Number(text));
      return Number.isFinite(value) ? orDefault(value, rule.min, rule.max) : null;
    }
    case "percent": {
      const value = Number(String(input).trim()) / 100;
      return Number.isFinite(value) ? clamp(value, rule.min, rule.max) : null;
    }
    case "toggle":
      return input === true;
  }
}

/** A copy of the setup with the rule set to value, making the objects on its path when the code has none */
export function setRule(setup: StudioSetup, rule: Rule, value: Json | null): StudioSetup {
  if (value === null) return setup;
  const challenge = JSON.parse(JSON.stringify(setup.challenge)) as JsonObject;
  let parent = challenge;
  for (const key of rule.path.slice(0, -1)) {
    if (!isObject(parent[key])) parent[key] = {};
    parent = parent[key] as JsonObject;
  }
  parent[rule.path[rule.path.length - 1]] = value;
  return { ...setup, challenge };
}

/** Back to the new challenge's value */
export const resetRule = (setup: StudioSetup, rule: Rule): StudioSetup => setRule(setup, rule, at(GAME.defaultChallenge, rule.path) ?? null);

/** A percent as the page shows it, e.g. 0.375 -> "37.5" */
export const percentText = (multiplier: number): string => String(Math.round(multiplier * 10000) / 100);
