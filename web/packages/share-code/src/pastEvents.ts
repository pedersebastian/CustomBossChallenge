// Port of the mod's Past Events (#146, #213): reading boss-events.json (Persistence/PastEventsFormat.cs), mapping an
// event's rules onto a Boss Creator setup (Config/PastEventRules.cs), its setup name (Config/PastEventDates.cs) and
// the list's search (Config/PastEventFilter.cs). web/test/past-event-cases.json, written by the mod's unit tests, keeps
// both sides giving the same setup for the same event.

import { DATA, type Json, type JsonObject } from "./data";
import { apply, filterChallenge } from "./decode";
import { encode } from "./encode";
import { CHIMPS_MODE, defaultOptions, type BossOptions, type ChimpsRounds } from "./options";

/** The newest formatVersion this version reads, of the file and of each event */
export const PAST_EVENTS_VERSION = 1;

/** One official Boss Bloon Event, as data/boss-events/README.md describes it */
export interface PastEvent {
  id: string;
  name: string;
  displayName: string;
  /** The API's boss, lowercase: "lych" */
  bossKey: string;
  /** Unix time in milliseconds */
  start: number;
  end: number;
  normalScoring: Scoring | null;
  eliteScoring: Scoring | null;
  /** The archive only knows the dates roughly (lossy: ["dates"]) */
  datesGuessed: boolean;
  standard: JsonObject | null;
  elite: JsonObject | null;
  replaces: string[];
  replacedBy: string[];
  /** "CC BY-NC-SA 4.0" or "CC BY-SA 3.0" when the event uses a wiki's content, or null */
  license: string | null;
  sourceUrls: string[];
}

export type Scoring = "GameTime" | "LeastCash" | "LeastTiers";

export interface PastEventsFile {
  generated: string;
  license: string;
  events: PastEvent[];
}

export type PastEventsRead =
  | { problem: "None"; file: PastEventsFile; changes: string[] }
  | { problem: "TooNew" | "Invalid"; error: string };

const isObject = (value: unknown): value is JsonObject =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const text = (e: JsonObject, field: string): string | null =>
  typeof e[field] === "string" && (e[field] as string).length > 0 ? (e[field] as string) : null;
const scoring = (e: JsonObject, field: string): Scoring | null => {
  const value = text(e, field);
  return value === "GameTime" || value === "LeastCash" || value === "LeastTiers" ? value : null;
};
const ids = (value: Json | undefined): string[] =>
  Array.isArray(value) ? value.filter((id): id is string => typeof id === "string") : [];

/**
 * Reads boss-events.json like PastEventsFormat.Read: a file from a newer format is "TooNew" and must be left alone;
 * an event from a newer format, or without what every event has, is left out and named in the changes
 */
export function readPastEvents(json: string): PastEventsRead {
  let root: unknown;
  try {
    root = JSON.parse(json);
  } catch (e) {
    return { problem: "Invalid", error: (e as Error).message };
  }
  if (!isObject(root) || !Number.isInteger(root.formatVersion)) return { problem: "Invalid", error: "no formatVersion" };
  const version = root.formatVersion as number;
  if (version > PAST_EVENTS_VERSION) return { problem: "TooNew", error: `Version ${version} is from a newer version` };
  if (!Array.isArray(root.events)) return { problem: "Invalid", error: "no events" };
  if (typeof root.generated !== "string" || Number.isNaN(Date.parse(root.generated))) {
    return { problem: "Invalid", error: "no generated time" };
  }

  const events: PastEvent[] = [];
  const changes: string[] = [];
  for (const item of root.events) {
    if (!isObject(item)) {
      changes.push("skipped an event that isn't an object");
      continue;
    }
    const id = text(item, "id");
    if (Number.isInteger(item.formatVersion) && (item.formatVersion as number) > PAST_EVENTS_VERSION) {
      changes.push(`skipped ${id ?? "an event"}: format version ${item.formatVersion} is newer`);
      continue;
    }
    const name = text(item, "name");
    const boss = text(item, "bossType");
    if (id === null || name === null || boss === null || !Number.isInteger(item.start) || !Number.isInteger(item.end)) {
      changes.push(`skipped ${id ?? "an event"}: no id, name, boss or dates`);
      continue;
    }
    events.push({
      id,
      name,
      displayName: text(item, "displayName") ?? name,
      bossKey: boss.toLowerCase(),
      start: item.start as number,
      end: item.end as number,
      normalScoring: scoring(item, "normalScoringType"),
      eliteScoring: scoring(item, "eliteScoringType"),
      datesGuessed: Array.isArray(item.lossy) && item.lossy.includes("dates"),
      standard: isObject(item.standard) ? item.standard : null,
      elite: isObject(item.elite) ? item.elite : null,
      replaces: ids(item.replaces),
      replacedBy: ids(item.replacedBy),
      license: text(item, "license"),
      sourceUrls: ids(item.sourceUrls),
    });
  }
  return {
    problem: "None",
    file: { generated: root.generated, license: typeof root.license === "string" ? root.license : "", events },
    changes,
  };
}

/** The rules of the normal or the Elite event, or null when the archive has none */
export const rulesOf = (event: PastEvent, elite: boolean): JsonObject | null => (elite ? event.elite : event.standard);

export const hasRules = (event: PastEvent): boolean => event.standard !== null || event.elite !== null;

/** The events newest first, as the list shows them (PastEventsFormat.NewestFirst) */
export const newestFirst = (events: readonly PastEvent[]): PastEvent[] =>
  [...events].sort((a, b) => b.start - a.start || (a.id < b.id ? 1 : a.id > b.id ? -1 : 0));

/** Why an event can't be opened (PastEventProblem) */
export type PastEventProblem = "None" | "NoRules" | "UnknownBoss" | "MissingMap";

export interface PastEventSetup {
  problem: PastEventProblem;
  options: BossOptions;
  /** A share code's challenge diff */
  challenge: JsonObject;
  /** The event's rules the Boss Creator can't play, which are left out */
  unsupported: string[];
  map: string | null;
}

/** What the mapping needs to know of the game; the website's game data by default */
export interface PastEventGame {
  defaults: JsonObject;
  mapExists: (map: string) => boolean;
  unsupportedModes: readonly string[];
}

export const WEBSITE_GAME: PastEventGame = {
  defaults: DATA.defaultChallenge,
  mapExists: (map) => Object.prototype.hasOwnProperty.call(DATA.maps, map),
  unsupportedModes: DATA.unsupportedModes as readonly string[],
};

const SAME_NAME = [
  "map", "mode", "difficulty", "maxTowers", "maxParagons", "leastCashUsed", "leastTiersUsed", "seed",
  "removeableCostMultiplier", "abilityCooldownReductionMultiplier", "disableMK", "disableSelling",
  "disablePowers", "noContinues", "disableDoubleCash", "disableFastTrack", "displayIncludedTowers",
];

const START_RULES: [string, string][] = [
  ["startingCash", "cash"], ["lives", "lives"], ["maxLives", "maxLives"], ["startRound", "round"], ["endRound", "endRound"],
];

const NOT_RULES = new Set([
  "name", "id", "creator", "createdAt", "gameVersion", "mapURL", "plays", "wins", "restarts", "losses", "upvotes",
  "playsUnique", "winsUnique", "lossesUnique", "roundSets", "_bloonModifiers", "_towers",
]);

/** The round set the first Bloonarius events played (2021), the boss's own event rounds under their old name */
export const OLD_BOSS_ROUND_SET = "bossRounds";

const clone = <T extends Json>(value: T): T => JSON.parse(JSON.stringify(value)) as T;

/** The boss of the API's lowercase name, in the mod's spelling, or null for one this version doesn't know */
export function pastEventBoss(key: string): string | null {
  const lower = key.toLowerCase();
  return DATA.bosses.find((boss) => boss.toLowerCase() === lower) ?? null;
}

/**
 * The setup for the normal or the Elite event, like PastEventRules.Map: the boss and its rounds onto the options, the
 * rest onto the challenge fields a share code may hold. Rules the Boss Creator can't play are listed, not half
 * applied. The name is English, as the website is.
 */
export function mapPastEvent(event: PastEvent, elite: boolean, game: PastEventGame = WEBSITE_GAME): PastEventSetup {
  const rules = rulesOf(event, elite);
  let options: BossOptions = { ...defaultOptions(), Elite: elite };
  const challenge: JsonObject = {};
  const unsupported: string[] = [];
  if (rules === null) return { problem: "NoRules", options, challenge, unsupported, map: null };

  const boss = pastEventBoss(event.bossKey);
  if (boss === null) return { problem: "UnknownBoss", options, challenge, unsupported, map: null };
  options = { ...options, Boss: boss };

  const map = typeof rules.map === "string" ? rules.map : null;
  if (map === null || !game.mapExists(map)) return { problem: "MissingMap", options, challenge, unsupported, map };

  challenge.name = setupName(event, elite);
  for (const field of SAME_NAME) {
    if (rules[field] !== undefined && rules[field] !== null) challenge[field] = clone(rules[field]);
  }

  const start: JsonObject = {};
  for (const [from, to] of START_RULES) {
    if (rules[from] !== undefined && rules[from] !== null) start[to] = clone(rules[from]);
  }
  if (Object.keys(start).length > 0) challenge.startRules = start;

  if (isObject(rules._bloonModifiers)) challenge.bloonModifiers = clone(rules._bloonModifiers);
  challenge.towers = towers(Array.isArray(rules._towers) ? rules._towers : null, game.defaults, unsupported);

  let mode = typeof rules.mode === "string" ? rules.mode : null;
  if (mode !== null && game.unsupportedModes.includes(mode)) {
    unsupported.push(`mode ${mode}`);
    challenge.mode = "Standard";
    mode = "Standard";
  }

  options = rounds(options, Array.isArray(rules.roundSets) ? rules.roundSets : null, mode, unsupported);

  // Rules the API may add later, or that no player can set in the Challenge Editor: listed when they change anything
  for (const [name, value] of Object.entries(rules)) {
    if (value === undefined || NOT_RULES.has(name) || SAME_NAME.includes(name) || START_RULES.some(([from]) => from === name)) {
      continue;
    }
    if (!isDefault(value)) unsupported.push(name);
  }

  return { problem: "None", options, challenge, unsupported, map };
}

/**
 * Every tower of the game, as a whole list a share code can hold: a tower the event doesn't list wasn't allowed
 * (max 0). A tower the game doesn't have is listed as unsupported.
 */
function towers(list: Json[] | null, defaults: JsonObject, unsupported: string[]): Json[] {
  const listed = new Map<string, JsonObject>();
  for (const token of list ?? []) {
    if (isObject(token) && typeof token.tower === "string") listed.set(token.tower, token);
  }

  const result: Json[] = [];
  const known = new Set<string>();
  for (const token of Array.isArray(defaults.towers) ? defaults.towers : []) {
    if (!isObject(token) || typeof token.tower !== "string") continue;
    const name = token.tower;
    known.add(name);
    const entry: JsonObject = { tower: name, max: 0 };
    const tower = listed.get(name);
    if (tower) {
      for (const field of ["max", "path1NumBlockedTiers", "path2NumBlockedTiers", "path3NumBlockedTiers"]) {
        if (Number.isInteger(tower[field])) entry[field] = tower[field] as number;
      }
    }
    result.push(entry);
  }

  for (const [name, tower] of listed) {
    if (!known.has(name) && tower.max !== 0) unsupported.push(`tower ${name}`);
  }
  return result;
}

/**
 * The event's round sets as Boss Event Rounds: the boss's own (also under its old name), or in CHIMPS the CHIMPS
 * events' sets the mod carries. Any other set is listed as unsupported, and the match plays the default rounds.
 */
function rounds(options: BossOptions, sets: Json[] | null, mode: string | null, unsupported: string[]): BossOptions {
  const own = options.Boss.toLowerCase();
  for (const set of (sets ?? []).filter((s): s is string => typeof s === "string" && s !== "default")) {
    if (set === own || set === OLD_BOSS_ROUND_SET) {
      options = {
        ...options,
        BossEventRounds: true,
        ChimpsRounds: mode === CHIMPS_MODE ? "Own" : options.ChimpsRounds,
      };
    } else if (set === "chimpsboss" && mode === CHIMPS_MODE) {
      options = { ...options, BossEventRounds: true, ChimpsRounds: "Newest" satisfies ChimpsRounds };
    } else if (set === "chimpsbloonarius" && mode === CHIMPS_MODE) {
      options = { ...options, BossEventRounds: true, ChimpsRounds: "Earlier" satisfies ChimpsRounds };
    } else {
      unsupported.push(`round set ${set}`);
    }
  }
  return options;
}

/** Whether a value the mapping doesn't know changes nothing: false, 0, -1, null, empty or 1 (a multiplier) */
function isDefault(value: Json): boolean {
  if (value === null) return true;
  if (typeof value === "boolean") return !value;
  if (typeof value === "number") return value === 0 || value === -1 || value === 1;
  if (typeof value === "string") return value.length === 0;
  if (Array.isArray(value)) return value.length === 0;
  return Object.keys(value).length === 0;
}

export const ENGLISH_MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** "17 Oct 2026", in UTC like the mod (PastEventDates.English) */
export function englishDate(time: number): string {
  const date = new Date(time);
  return `${date.getUTCDate()} ${ENGLISH_MONTHS[date.getUTCMonth()]} ${date.getUTCFullYear()}`;
}

const LETTER_OR_DIGIT = /[\p{L}\p{Nd}]/u;

/** Letters, digits and `keep`, one space between the words */
function nameText(value: string, keep: string): string {
  const name = [...value].map((c) => (LETTER_OR_DIGIT.test(c) || keep.includes(c) ? c : " ")).join("");
  return name.split(" ").filter((word) => word.length > 0).join(" ");
}

/**
 * The setup's name, like PastEventRules.SetupName in English: "Lych 47 Elite, 17 Jan 2025". The Challenge Editor's
 * name field keeps only letters, digits, spaces and . , ! ' / # ? ~
 */
export const setupName = (event: PastEvent, elite: boolean): string =>
  `${nameText(`${event.displayName}${elite ? " Elite" : ""}`, "")}, ${nameText(englishDate(event.start), ".")}`;

/**
 * The share code of the normal or the Elite event, made the way the mod reads an event: only what a code may hold,
 * from the game's new challenge. What the code can't hold is added to the unsupported rules. Null when the event can't
 * be opened.
 */
export async function pastEventCode(
  event: PastEvent,
  elite: boolean,
  game: PastEventGame = WEBSITE_GAME,
): Promise<{ setup: PastEventSetup; code: string | null }> {
  const setup = mapPastEvent(event, elite, game);
  if (setup.problem !== "None") return { setup, code: null };
  const dropped: string[] = [];
  const json = clone(DATA.defaultChallenge);
  apply(json, filterChallenge(setup.challenge, DATA.defaultChallenge, dropped));
  const withDropped = { ...setup, unsupported: [...setup.unsupported, ...dropped] };
  return { setup: withDropped, code: await encode(setup.options, json) };
}

/** What the list shows: the text searched for, and a boss, year and mode, each null for any (PastEventQuery) */
export interface PastEventQuery {
  text: string;
  /** The API's lowercase boss name, like "lych" */
  boss: string | null;
  year: number | null;
  /** The game's mode id, like "Clicks" */
  mode: string | null;
}

export const ANY_EVENT: PastEventQuery = { text: "", boss: null, year: null, mode: null };

/** The year an event started, in UTC like the mod */
export const yearOf = (event: PastEvent): number => new Date(event.start).getUTCFullYear();

/** The modes of the normal and the Elite event */
export function modesOf(event: PastEvent): string[] {
  const modes = [event.standard, event.elite].map((rules) => (typeof rules?.mode === "string" ? rules.mode : null));
  return [...new Set(modes.filter((mode): mode is string => mode !== null))];
}

/** The years events started in, newest first, for the Year filter */
export const pastEventYears = (events: readonly PastEvent[]): number[] =>
  [...new Set(events.map(yearOf))].sort((a, b) => b - a);

/** The modes events were played in, the most played first, for the Mode filter */
export function pastEventModes(events: readonly PastEvent[]): string[] {
  const counts = new Map<string, number>();
  for (const mode of events.flatMap(modesOf)) counts.set(mode, (counts.get(mode) ?? 0) + 1);
  return [...counts.entries()]
    .sort((a, b) => b[1] - a[1] || (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0))
    .map(([mode]) => mode);
}

/**
 * What a search looks in, lowercase: the event's names, boss and year, and its maps, modes and difficulties as ids and
 * as `shownName` gives them (PastEventFilter.SearchText)
 */
export function searchText(event: PastEvent, shownName: (id: string) => string): string {
  const parts = [event.name, event.displayName, event.bossKey, String(yearOf(event))];
  for (const rules of [event.standard, event.elite]) {
    if (rules === null) continue;
    for (const field of ["map", "mode", "difficulty"]) {
      const id = rules[field];
      if (typeof id !== "string") continue;
      parts.push(id, shownName(id));
    }
  }
  return parts.join(" ").toLowerCase();
}

/**
 * Whether the event is shown: every word of the text must be found in its searchText, and the boss, year and mode
 * must match (PastEventFilter.Matches)
 */
export function matchesPastEvent(event: PastEvent, query: PastEventQuery, search: () => string): boolean {
  if (query.boss !== null && event.bossKey !== query.boss.toLowerCase()) return false;
  if (query.year !== null && yearOf(event) !== query.year) return false;
  if (query.mode !== null && !modesOf(event).includes(query.mode)) return false;
  const words = query.text.toLowerCase().split(/\s+/).filter((word) => word.length > 0);
  if (words.length === 0) return true;
  const haystack = search();
  return words.every((word) => haystack.includes(word));
}
