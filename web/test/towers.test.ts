// The Challenge Editor's tower restrictions in the Studio (#128): held to ChallengeLimits, only the fields a code may
// set, the game's classes and paragons, and the website reads back what the Studio writes

import { describe, expect, it } from "vitest";
import { decode } from "../src/code/decode";
import { encode } from "../src/code/encode";
import { GAME, type JsonObject } from "../src/data/types";
import { newSetup } from "../src/studio/state";
import {
  TOWER_SETS,
  changed,
  highestTier,
  limitMode,
  resetTower,
  resetTowers,
  setBlockedTiers,
  setClassAllowed,
  setParagonLock,
  setTowerLimit,
  towerEntries,
} from "../src/studio/towers";

const entry = (setup: ReturnType<typeof newSetup>, tower: string) => towerEntries(setup).find((e) => e.tower === tower)!;

describe("the game's tower data", () => {
  it("has a class for every tower and hero, in the game's classes", () => {
    for (const tower of GAME.defaultChallenge.towers as JsonObject[]) {
      expect(TOWER_SETS, String(tower.tower)).toContain(GAME.towerSets[String(tower.tower)]);
      expect(GAME.towerSets[String(tower.tower)] === "Hero", String(tower.tower)).toBe(tower.isHero === true);
    }
  });

  it("knows which towers have a paragon, as many as Max Paragons counts", () => {
    expect(GAME.paragonTowers).toHaveLength(GAME.limits.paragons);
    for (const tower of GAME.paragonTowers) expect(GAME.towers[tower], tower).toBeDefined();
  });
});

describe("tower limits", () => {
  it("maps the three modes to -1, 0 and a number", () => {
    expect(entry(setTowerLimit(newSetup(), "DartMonkey", "notAllowed"), "DartMonkey").max).toBe(0);
    expect(entry(setTowerLimit(newSetup(), "Quincy", "none"), "Quincy").max).toBe(-1);
    expect(entry(setTowerLimit(newSetup(), "DartMonkey", "limited", "3"), "DartMonkey").max).toBe(3);
    // Limited without a number starts at 1
    expect(entry(setTowerLimit(newSetup(), "DartMonkey", "limited"), "DartMonkey").max).toBe(1);
  });

  it("holds a limit like ChallengeLimits.OrDefault, and 0 is not allowed", () => {
    expect(entry(setTowerLimit(newSetup(), "DartMonkey", "limited", "5000"), "DartMonkey").max).toBe(GAME.limits.maxTowerCount);
    expect(entry(setTowerLimit(newSetup(), "DartMonkey", "limited", "0"), "DartMonkey").max).toBe(0);
    expect(entry(setTowerLimit(newSetup(), "DartMonkey", "limited", "2.6"), "DartMonkey").max).toBe(3);
    const setup = newSetup();
    expect(setTowerLimit(setup, "DartMonkey", "limited", "")).toBe(setup);
    expect(setTowerLimit(setup, "DartMonkey", "limited", "lots")).toBe(setup);
    expect(limitMode(-1)).toBe("none");
    expect(limitMode(0)).toBe("notAllowed");
    expect(limitMode(7)).toBe("limited");
  });
});

describe("paths and paragons", () => {
  it("blocks 0-5 tiers of a path, shown as the highest tier left", () => {
    const setup = setBlockedTiers(newSetup(), "NinjaMonkey", 3, 3);
    expect(entry(setup, "NinjaMonkey").blocked).toEqual([0, 0, 3]);
    expect(highestTier(3)).toBe(2);
    expect(entry(setBlockedTiers(newSetup(), "NinjaMonkey", 1, 9), "NinjaMonkey").blocked[0]).toBe(GAME.limits.maxBlockedTiers);
    expect(entry(setBlockedTiers(newSetup(), "NinjaMonkey", 1, -2), "NinjaMonkey").blocked[0]).toBe(0);
    expect(setBlockedTiers(setup, "NinjaMonkey", 4, 1)).toBe(setup);
  });

  it("locks a paragon only on a tower that has one", () => {
    expect(entry(setParagonLock(newSetup(), "DartMonkey", true), "DartMonkey").restrictParagon).toBe(true);
    const setup = newSetup();
    expect(setParagonLock(setup, "SuperMonkey", true)).toBe(setup);
  });
});

describe("classes and reset", () => {
  it("allows or forbids a whole class and nothing else", () => {
    const none = setClassAllowed(newSetup(), "Military", false);
    const entries = towerEntries(none);
    expect(entries.filter((e) => e.set === "Military").every((e) => e.max === 0)).toBe(true);
    expect(entries.filter((e) => e.set !== "Military").every((e) => !changed(e))).toBe(true);
    expect(towerEntries(setClassAllowed(none, "Military", true)).filter((e) => e.set === "Military").every((e) => e.max === -1)).toBe(true);
  });

  it("puts a tower and every tower back to the new challenge's values", () => {
    const setup = setBlockedTiers(setTowerLimit(setClassAllowed(newSetup(), "Hero", false), "DartMonkey", "notAllowed"), "DartMonkey", 2, 2);
    expect(changed(entry(resetTower(setup, "DartMonkey"), "DartMonkey"))).toBe(false);
    expect(towerEntries(resetTowers(setup)).some(changed)).toBe(false);
  });

  it("only writes the fields a code may set, and keeps the list in the game's order", () => {
    const setup = setParagonLock(setBlockedTiers(setTowerLimit(newSetup(), "DartMonkey", "limited", "3"), "DartMonkey", 1, 2), "DartMonkey", true);
    const before = GAME.defaultChallenge.towers as JsonObject[];
    const after = setup.challenge.towers as JsonObject[];
    expect(after.map((t) => t.tower)).toEqual(before.map((t) => t.tower));
    const dart = after.find((t) => t.tower === "DartMonkey")!;
    expect(Object.keys(dart).sort()).toEqual(Object.keys(before.find((t) => t.tower === "DartMonkey")!).sort());
    expect(dart.isHero).toBe(false);
  });
});

it("writes towers the website reads back unchanged, as a short list diff", async () => {
  let setup = setTowerLimit(newSetup(), "DartMonkey", "limited", "3");
  setup = setBlockedTiers(setup, "NinjaMonkey", 3, 3);
  setup = setParagonLock(setup, "DartMonkey", true);
  setup = setClassAllowed(setup, "Hero", false);
  const code = await encode(setup.options, setup.challenge);
  const read = await decode(code);
  if (!read.ok) throw new Error(read.reason);
  expect(read.json.towers).toEqual(setup.challenge.towers);
  expect(read.challenge.towers).toMatchObject({ DartMonkey: 3, Ezili: 0 });
});
