// The numbers of the Stats page (#265), from the same boss-events.json as Past Events. Nothing here is fixed to the
// events of today: a new event in the archive changes the numbers the next time the site is built.

import { data, modesOf, pastEventBoss, yearOf, type PastEvent, type Scoring } from "@cbc/share-code";

const DAY = 24 * 60 * 60 * 1000;

/**
 * The events the stats count: one pulled early and run again (Vortex17, replaced by Vortex17EX) is the same event,
 * so only the one that replaced it counts. Oldest first.
 */
export function countedEvents(events: readonly PastEvent[]): PastEvent[] {
  return events
    .filter((event) => event.replacedBy.length === 0)
    .sort((a, b) => a.start - b.start || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
}

export interface Gap {
  /** Whole days from the end of one event to the start of the next */
  days: number;
  /** The events before and after it */
  from: PastEvent;
  to: PastEvent;
}

export interface BossStats {
  /** The API's lowercase name: "lych" */
  key: string;
  /** The mod's spelling, or the key for a boss this version doesn't know */
  name: string;
  events: number;
  /** Events with rules for the normal and the Elite level */
  normal: number;
  elite: number;
  first: PastEvent;
  last: PastEvent;
  /** Whole days since the last event ended; 0 the day it ends, null while it is on */
  daysSince: number | null;
  /** The longest wait between two of its events, null with only one */
  longestGap: Gap | null;
}

/** Whole days between two times, never below 0 */
const daysBetween = (from: number, to: number): number => Math.max(0, Math.floor((to - from) / DAY));

/** The boss's events, oldest first, as countedEvents gives them */
function bossStats(key: string, events: PastEvent[], now: number): BossStats {
  let longestGap: Gap | null = null;
  for (let i = 1; i < events.length; i++) {
    const days = daysBetween(events[i - 1].end, events[i].start);
    if (!longestGap || days > longestGap.days) longestGap = { days, from: events[i - 1], to: events[i] };
  }
  const last = events[events.length - 1];
  return {
    key,
    name: pastEventBoss(key) ?? key,
    events: events.length,
    normal: events.filter((event) => event.standard !== null).length,
    elite: events.filter((event) => event.elite !== null).length,
    first: events[0],
    last,
    daysSince: now < last.end ? null : daysBetween(last.end, now),
    longestGap,
  };
}

/** Where a boss is in the game's order, bosses this version doesn't know last */
const bossOrder = (key: string): number => {
  const index = data.bosses.findIndex((boss) => boss.toLowerCase() === key);
  return index < 0 ? data.bosses.length : index;
};

/** Each boss with events, the most events first, then in the game's order */
export function statsPerBoss(events: readonly PastEvent[], now: number): BossStats[] {
  const byBoss = new Map<string, PastEvent[]>();
  for (const event of countedEvents(events)) byBoss.set(event.bossKey, [...(byBoss.get(event.bossKey) ?? []), event]);
  return [...byBoss.entries()]
    .map(([key, list]) => bossStats(key, list, now))
    .sort((a, b) => b.events - a.events || bossOrder(a.key) - bossOrder(b.key) || (a.key < b.key ? -1 : 1));
}

/** A name and how many events had it */
export interface Count {
  id: string;
  count: number;
}

/** The ids counted, the most first and ties by id */
function ranked(ids: Iterable<string>): Count[] {
  const counts = new Map<string, number>();
  for (const id of ids) counts.set(id, (counts.get(id) ?? 0) + 1);
  return [...counts.entries()]
    .map(([id, count]) => ({ id, count }))
    .sort((a, b) => b.count - a.count || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
}

/** The maps of the normal and the Elite event: an event on one map counts once, one with two maps once for each */
export function mapsOf(event: PastEvent): string[] {
  const maps = [event.standard, event.elite].map((rules) => (typeof rules?.map === "string" ? rules.map : null));
  return [...new Set(maps.filter((map): map is string => map !== null))];
}

export interface Usage {
  /** A boss key or a year */
  group: string;
  events: number;
  maps: Count[];
  modes: Count[];
}

function usage(group: string, events: PastEvent[]): Usage {
  return { group, events: events.length, maps: ranked(events.flatMap(mapsOf)), modes: ranked(events.flatMap(modesOf)) };
}

/** The maps and modes used, for every event, per boss (in statsPerBoss's order) and per year (newest first) */
export function mapsAndModes(events: readonly PastEvent[], now: number): { all: Usage; perBoss: Usage[]; perYear: Usage[] } {
  const counted = countedEvents(events);
  return {
    all: usage("all", counted),
    perBoss: statsPerBoss(events, now).map((boss) => usage(boss.key, counted.filter((event) => event.bossKey === boss.key))),
    perYear: years(counted).map((year) => usage(String(year), counted.filter((event) => yearOf(event) === year))),
  };
}

/** The years with events, newest first */
const years = (events: readonly PastEvent[]): number[] => [...new Set(events.map(yearOf))].sort((a, b) => b - a);

/** The scoring types in the order the page shows them, and "Unknown" for the events the archive has none for */
export const SCORINGS = ["GameTime", "LeastCash", "LeastTiers", "Unknown"] as const;
export type ScoringOrUnknown = (typeof SCORINGS)[number];

export interface ScoringYear {
  year: number;
  normal: Record<ScoringOrUnknown, number>;
  elite: Record<ScoringOrUnknown, number>;
}

const noScorings = (): Record<ScoringOrUnknown, number> => ({ GameTime: 0, LeastCash: 0, LeastTiers: 0, Unknown: 0 });
const known = (scoring: Scoring | null): ScoringOrUnknown => scoring ?? "Unknown";

/** How the normal and the Elite events of each year were ranked, oldest year first */
export function scoringPerYear(events: readonly PastEvent[]): ScoringYear[] {
  const counted = countedEvents(events);
  return years(counted)
    .reverse()
    .map((year) => {
      const row: ScoringYear = { year, normal: noScorings(), elite: noScorings() };
      for (const event of counted.filter((e) => yearOf(e) === year)) {
        if (event.standard !== null) row.normal[known(event.normalScoring)]++;
        if (event.elite !== null) row.elite[known(event.eliteScoring)]++;
      }
      return row;
    });
}

/** How many events each boss had each year: the timeline's table. Years oldest first, bosses as statsPerBoss */
export function eventsPerYear(events: readonly PastEvent[], now: number): { years: number[]; rows: { key: string; name: string; counts: number[] }[] } {
  const counted = countedEvents(events);
  const all = years(counted).reverse();
  return {
    years: all,
    rows: statsPerBoss(events, now).map((boss) => ({
      key: boss.key,
      name: boss.name,
      counts: all.map((year) => counted.filter((event) => event.bossKey === boss.key && yearOf(event) === year).length),
    })),
  };
}
