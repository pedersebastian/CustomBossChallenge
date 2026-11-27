// Reading boss-events.json and mapping an event's rules onto a setup: details the mutation tests found unchecked (#296).
// Same rules as PastEventsFormat.Read and PastEventRules.Map in the mod.

import { describe, expect, it } from "vitest";
import { hasRules, mapPastEvent, newestFirst, readPastEvents, type PastEvent } from "@cbc/share-code";

const GENERATED = "2026-01-01T00:00:00Z";
const file = (events: unknown[], extra: object = {}) => JSON.stringify({ formatVersion: 1, generated: GENERATED, events, ...extra });

describe("a boss-events.json that can't be read", () => {
  it.each([
    ["[]", "Invalid", "no formatVersion"],
    ['{"formatVersion":1.5}', "Invalid", "no formatVersion"],
    ['{"formatVersion":2,"events":[]}', "TooNew", "Version 2 is from a newer version"],
    ['{"formatVersion":1}', "Invalid", "no events"],
    ['{"formatVersion":1,"events":[]}', "Invalid", "no generated time"],
    ['{"formatVersion":1,"events":[],"generated":"nope"}', "Invalid", "no generated time"],
  ])("%s is %s: %s", (json, problem, error) => expect(readPastEvents(json)).toEqual({ problem, error }));

  it("says why JSON that isn't JSON is broken", () => {
    const read = readPastEvents("{");
    expect(read.problem).toBe("Invalid");
    expect(read.problem === "Invalid" && read.error).not.toBe("");
  });
});

describe("the events in boss-events.json", () => {
  const event = { id: "c", name: "C", bossType: "Lych", start: 1, end: 2 };

  it("reads a file without a license as one with an empty license", () => {
    expect(readPastEvents(file([]))).toEqual({ problem: "None", file: { generated: GENERATED, license: "", events: [] }, changes: [] });
  });

  it("skips events it can't use and says which", () => {
    const read = readPastEvents(file([5, { id: "a", formatVersion: 2 }, { formatVersion: 2 }, { id: "b", name: "B" }, { name: "x" }, event]));
    expect(read.problem === "None" && read.changes).toEqual([
      "skipped an event that isn't an object",
      "skipped a: format version 2 is newer",
      "skipped an event: format version 2 is newer",
      "skipped b: no id, name, boss or dates",
      "skipped an event: no id, name, boss or dates",
    ]);
    expect(read.problem === "None" && read.file.events.map((e) => e.id)).toEqual(["c"]);
  });

  it("reads an event's optional fields", () => {
    const read = readPastEvents(
      file([{ ...event, displayName: "Cc", lossy: ["dates"], replaces: ["x", 3], normalScoringType: "LeastCash", eliteScoringType: "Nope" }], {
        license: "L",
      }),
    );
    expect(read.problem === "None" && read.file.license).toBe("L");
    expect(read.problem === "None" && read.file.events[0]).toMatchObject({
      displayName: "Cc",
      bossKey: "lych",
      datesGuessed: true,
      replaces: ["x"],
      normalScoring: "LeastCash",
      eliteScoring: null,
      standard: null,
      elite: null,
    });
  });
});

describe("an event's rules as a setup", () => {
  const base: PastEvent = {
    id: "e",
    name: "E",
    displayName: "E",
    bossKey: "lych",
    start: 1,
    end: 2,
    normalScoring: null,
    eliteScoring: null,
    datesGuessed: false,
    standard: null,
    elite: null,
    replaces: [],
    replacedBy: [],
    license: null,
    sourceUrls: [],
  };
  const map = (rules: object) => mapPastEvent({ ...base, standard: rules as never }, false);
  const allowed = (towers: unknown) => (towers as { tower: string; max: number }[]).filter((tower) => tower.max !== 0);

  it("has rules when the normal or the Elite event has them", () => {
    expect(hasRules(base)).toBe(false);
    expect(hasRules({ ...base, standard: {} })).toBe(true);
    expect(hasRules({ ...base, elite: {} })).toBe(true);
  });

  it("lists the newest first, and the later id first on the same day", () => {
    const events = [{ ...base, id: "a", start: 1 }, { ...base, id: "b", start: 1 }, { ...base, id: "c", start: 2 }];
    expect(newestFirst(events).map((event) => event.id)).toEqual(["c", "b", "a"]);
  });

  it("copies the rules that are set, leaves null ones out, and lists the ones it can't play", () => {
    const setup = map({
      map: "Tutorial", mode: "Clicks", maxTowers: null, seed: 5, startingCash: 650, lives: null, plays: 9, somethingNew: 3,
      _towers: [{ tower: "Gwendolin", max: 2, path1NumBlockedTiers: 1.5 }, { tower: "NoSuchTower", max: 1 }, 7],
    });
    expect(setup.problem).toBe("None");
    expect(setup.challenge).toMatchObject({ map: "Tutorial", mode: "Clicks", seed: 5, startRules: { cash: 650 } });
    expect(setup.challenge).not.toHaveProperty("maxTowers");
    expect(allowed(setup.challenge.towers)).toEqual([{ tower: "Gwendolin", max: 2 }]);
    expect(setup.unsupported).toEqual(["tower NoSuchTower", "somethingNew"]);
  });

  it("plays a mode the Boss Creator can't as Standard, and says so", () => {
    const setup = map({ map: "Tutorial", mode: "Deflation", maxTowers: 3 });
    expect(setup.challenge).toMatchObject({ mode: "Standard", maxTowers: 3 });
    expect(setup.challenge).not.toHaveProperty("startRules");
    expect(allowed(setup.challenge.towers)).toEqual([]);
    expect(setup.unsupported).toEqual(["mode Deflation"]);
  });
});
