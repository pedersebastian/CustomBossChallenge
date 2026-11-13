// The Studio's Random button: Config/BossRandomizer.cs and RandomNames.cs ported (#12). The names are checked against
// random-name-cases.json, which UnitTests/RandomNamesTests.cs reads too; the randomizer like Tests/OptionsTests.cs
// checks the mod's in the game.

import { describe, expect, it } from "vitest";
import { BOSSES, effectiveSpawnRounds, isMixed, normalized, playProblem } from "@cbc/share-code";
import { parseTierBosses } from "@cbc/share-code/tier-bosses";
import { decode, encode } from "@cbc/share-code";
import { MAX_TIERS } from "@cbc/share-code/tiers";
import { GAME, type JsonObject } from "../src/data/types";
import { NO_LIMIT } from "../src/math/paragons";
import * as edit from "../src/studio/state";
import {
  MAX_PERCENT,
  MAX_RANDOM_HEALTH_PER_TIER,
  MAX_RANDOM_PARAGONS,
  MAX_RANDOM_SPEED_PER_TIER,
  MIN_PERCENT,
  isGivenName,
  nameTraits,
  percentWeight,
  randomizeSetup,
  tierWeight,
  type Rng,
} from "../src/studio/random";
import { ALL_NAMES, NORMAL_TRAITS, namesFor, randomName, type NameTraits } from "../src/studio/randomNames";
import cases from "./random-name-cases.json";

/** A seeded random source (mulberry32), so a failing run can be repeated */
function seeded(seed: number): Rng {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** NameTraits from the C# names in the case file */
const traitsOf = (json: Record<string, unknown>): NameTraits => ({
  ...NORMAL_TRAITS,
  ...Object.fromEntries(Object.entries(json).map(([key, value]) => [key[0].toLowerCase() + key.slice(1), value])),
});

const modifiers = (challenge: JsonObject): JsonObject => challenge.bloonModifiers as JsonObject;

describe("names for random setups, like the mod", () => {
  it.each(cases.cases.map((c) => [JSON.stringify(c.traits), c.traits, c.names]))("%s", (_, traits, names) => {
    const setup = traitsOf(traits as Record<string, unknown>);
    expect([...namesFor(setup)].sort()).toEqual([...(names as string[])].sort());
    const rng = seeded(1);
    const given = new Set(Array.from({ length: 100 }, () => randomName(setup, rng)));
    expect([...given].sort()).toEqual((names as string[]).length === 0 ? [null] : [...(names as string[])].sort());
  });

  it("can make as many names as the mod, each one that fits the game's name field", () => {
    expect(ALL_NAMES.size).toBe(cases.allNamesCount);
    for (const name of ALL_NAMES) {
      expect(name.length).toBeLessThanOrEqual(40);
      expect(name).not.toContain("{");
      expect(name).toMatch(/^[A-Za-z0-9 !?;/#%',.]+$/);
    }
  });
});

describe("the weights, like BossRandomizer", () => {
  it("makes values near normal far more likely than extreme ones", () => {
    expect(percentWeight(100)).toBe(1);
    expect(percentWeight(110)).toBeCloseTo(0.941, 3);
    expect(percentWeight(400)).toBeCloseTo(0.0175, 4);
    expect(tierWeight(5)).toBe(1);
    expect(tierWeight(4)).toBeCloseTo(0.8825, 4);
    expect(tierWeight(10)).toBeCloseTo(0.0439, 4);
  });
});

describe("Random", () => {
  const RUNS = 1000;

  it("always makes a setup the mod can play, Standard staying Standard", () => {
    const rng = seeded(42);
    const bosses = new Set<string>();
    const tierCounts = new Array<number>(MAX_TIERS + 1).fill(0);
    let nearNormal = 0;
    let extreme = 0;
    let named = 0;
    for (let i = 0; i < RUNS; i++) {
      const { options, challenge } = randomizeSetup(edit.newSetup(), rng);
      expect(options).toEqual(normalized(options));
      expect(isMixed(options)).toBe(false);
      expect(GAME.unsupportedModes).not.toContain(challenge.mode);
      expect(GAME.maps).toHaveProperty(String(challenge.map));
      const speed = modifiers(challenge).bossSpeedMultiplier as number;
      const health = (modifiers(challenge).healthMultipliers as JsonObject).boss as number;
      for (const value of [speed, health]) {
        expect(value * 100).toBeGreaterThanOrEqual(MIN_PERCENT);
        expect(value * 100).toBeLessThanOrEqual(MAX_PERCENT);
        if (value !== 1 && value <= 1.2) nearNormal++;
        if (value >= 3) extreme++;
      }
      const paragons = challenge.maxParagons as number;
      expect(paragons === NO_LIMIT || (paragons >= 0 && paragons <= MAX_RANDOM_PARAGONS)).toBe(true);
      expect(options.SpeedPerTier % 5).toBe(0);
      expect(options.SpeedPerTier).toBeGreaterThanOrEqual(0);
      expect(options.SpeedPerTier).toBeLessThanOrEqual(MAX_RANDOM_SPEED_PER_TIER);
      expect(options.HealthPerTier).toBeLessThanOrEqual(MAX_RANDOM_HEALTH_PER_TIER);
      bosses.add(options.Boss);
      tierCounts[options.Tiers]++;
      if (challenge.name !== "") named++;
    }
    expect(bosses.size).toBe(BOSSES.length);
    expect(extreme).toBeGreaterThan(0);
    expect(nearNormal).toBeGreaterThan(extreme * 5);
    // Most often around 5 tiers, now and then 1 or 10
    expect(tierCounts[5]).toBeGreaterThan(tierCounts[1] * 5);
    expect(tierCounts[10]).toBeGreaterThan(0);
    // Only a setup that stands out gets a name
    expect(named).toBeGreaterThan(0);
    expect(named).toBeLessThan(RUNS);
  });

  it("gives every tier a boss in Advanced Mode, sometimes Random, Elite more often later", () => {
    const rng = seeded(99);
    const advanced = edit.setAdvanced(edit.newSetup(), true, edit.NO_MEMORY).setup;
    let random = 0;
    let eliteFirst = 0;
    let eliteLast = 0;
    for (let i = 0; i < RUNS; i++) {
      const { options, challenge } = randomizeSetup(advanced, rng);
      expect(isMixed(options)).toBe(true);
      expect(modifiers(challenge).bossSpeedMultiplier).toBe(1);
      expect(options.SkullMode).toBe("Vanilla");
      expect(options).toEqual(normalized(options));
      const tiers = parseTierBosses(options.TierBosses);
      expect(tiers).toHaveLength(MAX_TIERS);
      for (const tier of tiers) {
        expect(tier.boss).not.toBeNull();
        if (tier.boss === "Random") random++;
      }
      if (tiers[0].elite) eliteFirst++;
      if (tiers[MAX_TIERS - 1].elite) eliteLast++;
    }
    expect(random).toBeGreaterThan(0);
    expect(random).toBeLessThan((RUNS * MAX_TIERS) / 5);
    expect(eliteLast).toBeGreaterThan(eliteFirst * 2);
  });

  it("makes codes the website reads back unchanged, which Play accepts", async () => {
    const rng = seeded(7);
    for (let i = 0; i < 200; i++) {
      const start = i % 2 === 0 ? edit.newSetup() : edit.setAdvanced(edit.newSetup(), true, edit.NO_MEMORY).setup;
      const setup = randomizeSetup(start, rng);
      const read = await decode(await encode(setup.options, setup.challenge));
      if (!read.ok) throw new Error(read.reason);
      expect(read.options).toEqual(setup.options);
      expect(playProblem(read.challenge, effectiveSpawnRounds(read.options))).toBeNull();
    }
  });

  it("keeps a name the player typed, and replaces the Boss Creator's and the examples'", () => {
    const rng = seeded(3);
    const named = (name: string) => ({ ...edit.newSetup(), challenge: { ...edit.newSetup().challenge, name } });
    for (let i = 0; i < 50; i++) expect(randomizeSetup(named("My Own Challenge"), rng).challenge.name).toBe("My Own Challenge");
    expect(isGivenName("")).toBe(true);
    expect(isGivenName("Custom Elite Lych")).toBe(true);
    expect(isGivenName("Custom Boss Gauntlet")).toBe(true);
    expect(isGivenName("The Unkillable Snail")).toBe(true);
    expect(isGivenName("Turbo Elite Blastapopoulos")).toBe(true);
    expect(isGivenName("turbo lych")).toBe(false);
  });

  it("looks at the setup like BossMode.NameTraits", () => {
    const setup = edit.setBossHealth(edit.setBossSpeed(edit.setElite(edit.setBoss(edit.newSetup(), "Lych"), true), 50), 400);
    expect(nameTraits(setup.options, setup.challenge)).toMatchObject({ boss: "Elite Lych", elite: true, bossSpeed: 50, bossHealth: 400 });
    const none = edit.setSkullCount(edit.setSkullMode(edit.newSetup(), "EvenlySpaced"), 0);
    expect(nameTraits(none.options, none.challenge).noSkulls).toBe(true);
  });
});

describe("Reset Boss Options", () => {
  it("puts every boss option back, and keeps the Challenge Editor's rules", () => {
    let setup = edit.setBoss(edit.setTiers(edit.setBossSpeed(edit.newSetup(), 300), 8), "Vortex");
    setup = edit.setParagonSlider(setup, 2);
    setup = { ...setup, challenge: { ...setup.challenge, map: "Infernal", name: "Mine" } };
    const reset = edit.resetBossOptions(setup);
    expect(reset.options).toEqual(edit.newSetup().options);
    expect(edit.bossSpeed(reset)).toBe(100);
    expect(edit.paragonSlider(reset)).toBe(edit.paragonSlider(edit.newSetup()));
    expect(reset.challenge.map).toBe("Infernal");
    expect(reset.challenge.name).toBe("Mine");
  });
});
