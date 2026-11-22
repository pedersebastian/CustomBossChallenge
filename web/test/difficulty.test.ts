// The difficulty rating (#263): the cases in difficulty-cases.json, which UnitTests/DifficultyRatingTests.cs reads too,
// so the mod and the website rate a setup alike; and the calibration: the examples and the past boss events land where
// players would put them.

import { readdirSync, readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { decode, defaultOptions, pastEventCode, readPastEvents, type BossOptions, type Challenge } from "@cbc/share-code";
import { difficultyPoints, difficultyStars, MAX_STARS, starsOf, STAR_STEPS } from "@cbc/share-code/difficulty";
import { factsOf, setupFacts, towerCounts, type SetupFacts } from "@cbc/share-code/warnings";
import { difficultyOf } from "../src/preview/difficulty";
import examples from "../src/data/examples.json";

interface Case {
  name: string;
  facts: Record<string, unknown> & { Options?: Partial<BossOptions> };
  points: number;
  stars: number;
}

const file = JSON.parse(readFileSync(new URL("./difficulty-cases.json", import.meta.url), "utf8")) as { cases: Case[] };

/** The case's facts, written like the C# SetupFacts, as the package's */
function read(facts: Case["facts"]): SetupFacts {
  const { Options, ...rest } = facts;
  const read: Partial<SetupFacts> = { options: { ...defaultOptions(), ...Options } };
  for (const [key, value] of Object.entries(rest)) {
    (read as Record<string, unknown>)[key[0].toLowerCase() + key.slice(1)] = value;
  }
  return setupFacts(read);
}

describe("the shared cases", () => {
  it("has them", () => expect(file.cases.length).toBeGreaterThanOrEqual(40));

  for (const entry of file.cases) {
    it(entry.name, () => {
      const facts = read(entry.facts);
      expect(difficultyPoints(facts)).toBeCloseTo(entry.points, 3);
      expect(difficultyStars(facts)).toBe(entry.stars);
    });
  }
});

describe("stars", () => {
  it("go from 1 to 10", () => {
    expect(starsOf(-100)).toBe(1);
    expect(starsOf(100)).toBe(MAX_STARS);
    for (const [i, step] of STAR_STEPS.entries()) {
      expect(starsOf(step)).toBe(i + 2);
      expect(starsOf(step - 0.001)).toBe(i + 1);
    }
  });
});

describe("the facts of a decoded setup", () => {
  it("count the towers that can't be placed, not the heroes", () => {
    const challenge = { towers: { DartMonkey: 0, BombShooter: 0, Quincy: 0, TackShooter: 3 } } as unknown as Challenge;
    const counts = towerCounts(challenge);
    expect(counts.bannedTowers).toBe(2);
    expect(counts.allTowers).toBeGreaterThanOrEqual(20);
    expect(towerCounts({ towers: {} } as unknown as Challenge).bannedTowers).toBe(0);
  });
});

const decoded = async (code: string): Promise<{ options: BossOptions; challenge: Challenge }> => {
  const result = await decode(code);
  if (!result.ok) throw new Error(result.reason);
  return result;
};

describe("the examples", () => {
  const expected: Record<string, number> = {
    "Marathon Lych": 9,
    "Speed Demon Vortex": 2,
    "Glass Cannon": 1,
    "No Skulls, No Mercy": 8,
    "Speedrun Dreadbloon": 5,
    "Paragon Ban": 7,
    "CHIMPS Throwback": 4,
    "Boss Gauntlet": 2,
  };

  it("are all rated", () => expect(examples.map((example) => example.name).sort()).toEqual(Object.keys(expected).sort()));

  it.each(examples.map((example) => [example.name, example.code]))("%s", async (name, code) => {
    const { options, challenge } = await decoded(code);
    expect(difficultyOf(options, challenge).stars).toBe(expected[name]);
  });
});

describe("the past events", () => {
  const ARCHIVE = new URL("../../data/boss-events/", import.meta.url);

  /** How many of the normal and the Elite events get each number of stars, by index */
  async function spread(): Promise<{ normal: number[]; elite: number[] }> {
    const events = readdirSync(ARCHIVE)
      .filter((name) => name.endsWith(".json"))
      .map((name) => JSON.parse(readFileSync(new URL(name, ARCHIVE), "utf8")) as unknown);
    const read = readPastEvents(JSON.stringify({ formatVersion: 1, generated: "2026-11-22T00:00:00.000Z", license: "test", events }));
    if (read.problem !== "None") throw new Error(read.error);
    const counts = { normal: Array<number>(MAX_STARS + 1).fill(0), elite: Array<number>(MAX_STARS + 1).fill(0) };
    for (const event of read.file.events) {
      for (const elite of [false, true]) {
        const { code } = await pastEventCode(event, elite);
        if (code === null) continue;
        const { options, challenge } = await decoded(code);
        counts[elite ? "elite" : "normal"][difficultyOf(options, challenge).stars]++;
      }
    }
    return counts;
  }

  it("Elite events mostly get 5 to 7 stars, normal ones 1 to 3, and none 10", async () => {
    const { normal, elite } = await spread();
    const share = (counts: number[], from: number, to: number): number =>
      counts.slice(from, to + 1).reduce((a, b) => a + b, 0) / counts.reduce((a, b) => a + b, 0);
    expect(share(elite, 5, 7)).toBeGreaterThanOrEqual(0.8);
    expect(share(normal, 1, 3)).toBeGreaterThanOrEqual(0.8);
    expect(elite[MAX_STARS] + normal[MAX_STARS]).toBe(0);
  }, 60_000);
});

describe("an estimate from the setup's numbers", () => {
  it("rates an Elite past event above Glass Cannon", async () => {
    const glass = examples.find((example) => example.name === "Glass Cannon")!;
    const { options, challenge } = await decoded(glass.code);
    const elite = factsOf({ ...defaultOptions(), Elite: true, BossEventRounds: true }, challenge);
    expect(difficultyStars(elite)).toBeGreaterThan(difficultyOf(options, challenge).stars);
  });
});
