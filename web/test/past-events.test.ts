// The website's Past Events (#213) give the same setup as the mod's (#146): the cases in past-event-cases.json are
// written by UnitTests/PastEventCasesTests.cs, and every event of the archive (data/boss-events/) maps without losing
// anything, as UnitTests/PastEventsTests.cs checks for the mod.

import { readdirSync, readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import {
  ANY_EVENT,
  data,
  decode,
  deepEqual,
  filterChallenge,
  mapPastEvent,
  matchesPastEvent,
  newestFirst,
  pastEventCode,
  pastEventModes,
  pastEventYears,
  readPastEvents,
  searchText,
  setupName,
  yearOf,
  type PastEvent,
} from "@cbc/share-code";
import cases from "./past-event-cases.json";

const ARCHIVE = new URL("../../data/boss-events/", import.meta.url);

const bundle = (events: unknown[], formatVersion = 1) =>
  JSON.stringify({ formatVersion, generated: "2026-11-16T00:00:00.000Z", license: "test", events });

function read(events: unknown[]): PastEvent[] {
  const result = readPastEvents(bundle(events));
  if (result.problem !== "None") throw new Error(result.error);
  return result.file.events;
}

const archive = (): unknown[] =>
  readdirSync(ARCHIVE)
    .filter((name) => name.endsWith(".json"))
    .map((name) => JSON.parse(readFileSync(new URL(name, ARCHIVE), "utf8")));

const casesById = new Map(read(cases.events).map((event) => [event.id, event]));

describe("past events, like the mod", () => {
  it.each(cases.cases)("$name", (expected) => {
    const event = casesById.get(expected.id)!;
    const setup = mapPastEvent(event, expected.elite);
    expect(setup.problem).toBe(expected.problem);
    expect({
      Boss: setup.options.Boss,
      Elite: setup.options.Elite,
      BossEventRounds: setup.options.BossEventRounds,
      ChimpsRounds: setup.options.ChimpsRounds ?? "Newest",
    }).toEqual(expected.options);

    const dropped: string[] = [];
    const challenge = setup.problem === "None" ? filterChallenge(setup.challenge, data.defaultChallenge, dropped) : {};
    expect(deepEqual(challenge, expected.challenge as never)).toBe(true);
    expect([...setup.unsupported, ...dropped]).toEqual(expected.unsupported);
    expect(setupName(event, expected.elite)).toBe(expected.setupName);
  });

  it("maps every event in the archive, normal and Elite, without losing anything", () => {
    const events = read(archive());
    expect(events.length).toBeGreaterThanOrEqual(284);
    for (const event of events) {
      for (const elite of [false, true]) {
        const setup = mapPastEvent(event, elite);
        expect(setup.problem, `${event.id} ${elite}`).toBe("None");
        const dropped: string[] = [];
        filterChallenge(setup.challenge, data.defaultChallenge, dropped);
        expect(dropped, `${event.id} ${elite}`).toEqual([]);
        expect(setupName(event, elite)).toMatch(/^[\p{L}\p{Nd} ]+, [\p{L}\p{Nd} .]+$/u);
      }
    }
  });

  it("makes a code the mod reads with the event's rules and name", async () => {
    const lych = casesById.get("Lych47_m5zq59de")!;
    const { setup, code } = await pastEventCode(lych, true);
    expect(setup.unsupported).toEqual([]);
    const read = await decode(code);
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.Boss).toBe("Lych");
    expect(read.options.Elite).toBe(true);
    expect(read.options.BossEventRounds).toBe(true);
    expect(read.challenge.name).toBe("Lych47 Elite, 17 Jan 2025");
    expect(read.challenge.map).toBe("Tinkerton");
    expect(read.challenge.cash).toBe(3000);
    expect(read.challenge.maxParagons).toBe(4);
    expect(read.challenge.towers.DartMonkey).toBe(0);
    expect(read.challenge.towers.Benjamin).toBe(99);
  });

  it("makes no code of an event it can't open", async () => {
    const result = await pastEventCode(casesById.get("MadeUpMap")!, false);
    expect(result.setup.problem).toBe("MissingMap");
    expect(result.code).toBeNull();
  });
});

describe("reading boss-events.json", () => {
  it("leaves a file from a newer format alone", () => {
    expect(readPastEvents(bundle([], 2)).problem).toBe("TooNew");
    expect(readPastEvents("<!DOCTYPE html>").problem).toBe("Invalid");
    expect(readPastEvents(JSON.stringify({ formatVersion: 1 })).problem).toBe("Invalid");
  });

  it("skips an event from a newer format or without what every event has", () => {
    const lych = cases.events[0] as Record<string, unknown>;
    const result = readPastEvents(
      bundle([lych, { ...lych, id: "Future", formatVersion: 2 }, { ...lych, id: "NoBoss", bossType: undefined }]),
    );
    if (result.problem !== "None") throw new Error(result.error);
    expect(result.file.events.map((event) => event.id)).toEqual(["Lych47_m5zq59de"]);
    expect(result.changes).toHaveLength(2);
  });

  it("knows which events have guessed dates and wiki content", () => {
    const events = read(archive());
    expect(events.some((event) => event.datesGuessed)).toBe(true);
    expect(events.some((event) => event.license === "CC BY-NC-SA 4.0" && event.sourceUrls.length > 0)).toBe(true);
  });
});

describe("the list's search, like the mod's", () => {
  const events = read(archive());
  const shown = (id: string) => (id === "Tutorial" ? "Monkey Meadow" : id === "Clicks" ? "CHIMPS" : id);
  const find = (query: Partial<typeof ANY_EVENT>) =>
    events.filter((event) => matchesPastEvent(event, { ...ANY_EVENT, ...query }, () => searchText(event, shown)));

  it("finds events by name, boss, year, map and mode", () => {
    expect(find({})).toHaveLength(events.length);
    expect(find({ text: "lych47" }).map((event) => event.id)).toContain("Lych47_m5zq59de");
    expect(find({ text: "TINKERTON 2025" }).map((event) => event.id)).toContain("Lych47_m5zq59de");
    expect(find({ boss: "vortex" }).every((event) => event.bossKey === "vortex")).toBe(true);
    expect(find({ year: 2024 }).every((event) => yearOf(event) === 2024)).toBe(true);
    expect(find({ text: "chimps" })).toHaveLength(find({ mode: "Clicks" }).length);
    expect(find({ text: "lych", boss: "vortex" })).toEqual([]);
    expect(find({ text: "no such event" })).toEqual([]);
  });

  it("offers the years and modes the events have", () => {
    const years = pastEventYears(events);
    expect(years).toEqual([...years].sort((a, b) => b - a));
    expect(years).toContain(2021);
    expect(pastEventModes(events)[0]).toBe("Standard");
    expect(newestFirst(events)[0].start).toBe(Math.max(...events.map((event) => event.start)));
  });
});
