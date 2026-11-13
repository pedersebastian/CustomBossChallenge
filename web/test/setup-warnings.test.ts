// The Boss Creator's warnings before Play (#186): the cases in setup-warnings-cases.json, which
// UnitTests/SetupWarningsTests.cs reads too, and the cases of that file's other tests, so both sides warn alike

import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { defaultOptions, type BossOptions } from "@cbc/share-code";
import {
  closeTiers,
  difficulty,
  sameTiers,
  setupFacts,
  setupWarnings,
  type SetupFacts,
  type TierBase,
} from "@cbc/share-code/warnings";

interface Case {
  name: string;
  facts: Record<string, unknown> & { Options?: Partial<BossOptions>; TierBases?: ({ Health: number; Speed: number } | null)[] };
  warnings: string[];
}

const file = JSON.parse(readFileSync(new URL("./setup-warnings-cases.json", import.meta.url), "utf8")) as { cases: Case[] };

/** The case's facts, written like the C# SetupFacts, as the package's */
function read(facts: Case["facts"]): SetupFacts {
  const { Options, TierBases, ...rest } = facts;
  const read: Partial<SetupFacts> = { options: { ...defaultOptions(), ...Options } };
  for (const [key, value] of Object.entries(rest)) {
    (read as Record<string, unknown>)[key[0].toLowerCase() + key.slice(1)] = value;
  }
  if (TierBases) read.tierBases = TierBases.map((base) => (base ? { health: base.Health, speed: base.Speed } : null));
  return setupFacts(read);
}

const kinds = (facts: SetupFacts): string => setupWarnings(facts).map((w) => w.kind).join(",");

const options = (changes: Partial<BossOptions> = {}): BossOptions => ({ ...defaultOptions(), ...changes });

/** Like the C# tests' Bases: tier 5's health, the tiers before it a share of it, every one at speed 2 */
const bases = (tiers: number, health: number): TierBase[] =>
  Array.from({ length: tiers }, (_, i) => ({ health: (health * Math.min(i + 1, 5)) / 5, speed: 2 }));

describe("the shared cases", () => {
  it("has them", () => expect(file.cases.length).toBeGreaterThanOrEqual(20));

  for (const entry of file.cases) {
    it(entry.name, () => expect(kinds(read(entry.facts))).toBe(entry.warnings.join(",")));
  }
});

describe("the warnings, like the C# tests", () => {
  it.each([
    [1, ""],
    [35, ""],
    [36, "StartRoundClose"],
    [39, "StartRoundClose"],
    // On or after the first boss the game can't be played, which playProblem says
    [40, ""],
    [41, ""],
  ])("start round %i warns: %s", (start, expected) => expect(kinds(setupFacts({ startRound: start }))).toBe(expected));

  it("says how many rounds are left", () =>
    expect(setupWarnings(setupFacts({ startRound: 37 }))[0].args).toEqual([37, 3, 40]));

  it.each([
    ["40, 60, 80, 100, 120", ""],
    ["40, 45, 50, 55, 60", ""],
    ["40, 60, 80, 81, 100", "80-81"],
    ["40, 44, 80, 81, 83", "40-44, 80-81, 81-83"],
    // Rounds the setup repeats are moved one round apart, which is close too
    ["40, 40, 40", "40-41, 41-42"],
  ])("tiers on %s are close: %s", (spawnRounds, expected) => {
    const facts = setupFacts({ options: options({ SpawnRounds: spawnRounds, Tiers: spawnRounds.split(",").length }) });
    const warning = setupWarnings(facts).find((w) => w.kind === "TiersClose");
    expect(warning ? warning.args[0] : "").toBe(expected);
    expect(closeTiers(facts.options).map((pair) => `${pair.before}-${pair.after}`).join(", ")).toBe(expected);
  });

  it.each([
    [1, 2_000_000_000, false],
    [1, 2_000_000_001, true],
    // Boss Health is applied on top of the model, so it lowers the most a model can have
    [5, 400_000_000, false],
    [5, 400_000_001, true],
  ])("Boss Health %d with tier 5 at %d is held to the limit: %s", (bossHealth, tier5Health, capped) => {
    const facts = setupFacts({ bossHealth, options: options({ HealthPerTier: 100 }), tierBases: bases(5, tier5Health / 5) });
    expect(setupWarnings(facts).some((w) => w.kind === "HealthCapped")).toBe(capped);
  });

  it("says the most health a tier can have with Boss Health", () => {
    const warning = setupWarnings(setupFacts({ bossHealth: 4, tierBases: bases(5, 1e9) })).find((w) => w.kind === "HealthCapped");
    expect(warning?.args[0]).toBe(2e9);
  });

  it("doesn't lower the limit for a Boss Health below 100 %", () => {
    const warning = setupWarnings(setupFacts({ bossHealth: 0.5, tierBases: bases(5, 1e10) })).find((w) => w.kind === "HealthCapped");
    expect(warning?.args[0]).toBe(2e9);
  });

  it("finds nothing capped or the same without the boss models", () =>
    expect(kinds(setupFacts({ options: options({ Tiers: 10, SpeedPerTier: 50, HealthPerTier: 200 }) })).replace("HardSetup", "").replace(/^,|,$/g, "")).toBe(""));

  it("finds the tiers the speed limit makes equal", () => {
    const facts = setupFacts({ options: options({ Tiers: 10, SpeedPerTier: 50, HealthPerTier: 200 }), tierBases: bases(10, 1e8) });
    expect(sameTiers(facts)).toEqual([{ first: 9, last: 10 }]);
    expect(setupWarnings(facts).find((w) => w.kind === "SameTiers")?.args[0]).toBe("9-10");
  });

  it("counts the health limit, not different models", () => {
    const facts = setupFacts({
      options: options({ Tiers: 5, SpeedPerTier: -10, HealthPerTier: -10 }),
      tierBases: [1e2, 1e3, 1e4, 1e6, 1e7].map((health) => ({ health, speed: 1 })),
    });
    expect(sameTiers(facts)).toEqual([]);
    const capped = { ...facts, bossHealth: 20, options: { ...facts.options, HealthPerTier: 200, SpeedPerTier: 0 }, tierBases: bases(5, 1e8) };
    expect(sameTiers(capped)).toEqual([{ first: 2, last: 5 }]);
  });

  it("leaves tiers the settings keep equal alone", () => expect(sameTiers(setupFacts({ tierBases: bases(5, 1e5) }))).toEqual([]));

  it("skips tiers with another boss", () => {
    const facts = setupFacts({ bossHealth: 20, options: options({ HealthPerTier: 200, TierBosses: ",,Lych,," }), tierBases: bases(5, 6e8) });
    expect(sameTiers(facts)).toEqual([
      { first: 1, last: 2 },
      { first: 4, last: 5 },
    ]);
  });

  it.each([
    [false, 0],
    [true, 1.5],
  ])("scores the game's own bosses low (Elite %s)", (elite, expected) =>
    expect(difficulty(setupFacts({ options: options({ Elite: elite }) }))).toBeCloseTo(expected, 6));

  it("counts doublings of the boss", () => expect(difficulty(setupFacts({ bossHealth: 2, bossSpeed: 2 }))).toBeCloseTo(2, 6));

  it.each([
    [1, 2],
    [10, 1],
    [50, 0.5],
    [150, 0],
    // The difficulty's own lives
    [-1, 0],
  ])("scores %i lives %d", (lives, expected) => expect(difficulty(setupFacts({ lives }))).toBeCloseTo(expected, 6));

  it("puts HardSetup last, in a fixed order", () => {
    const facts = setupFacts({
      startRound: 38,
      bossHealth: 20,
      lives: 1,
      noSelling: true,
      options: options({ Elite: true, Tiers: 7, HealthPerTier: 200 }),
      tierBases: bases(7, 1e8),
    });
    expect(kinds(facts)).toBe("StartRoundClose,HealthCapped,SameTiers,HardSetup");
    expect(kinds({ ...facts, options: { ...facts.options, SpawnRounds: "40, 60, 80, 81, 100, 120, 140" } })).toBe(
      "StartRoundClose,TiersClose,HealthCapped,SameTiers,HardSetup",
    );
  });

  it.each([
    ["Clicks", true, true],
    ["Clicks", false, false],
    ["Standard", true, false],
    ["Impoppable", true, false],
  ])("warns about Boss Event Rounds in %s (%s): %s", (mode, eventRounds, warned) =>
    expect(setupWarnings(setupFacts({ mode, options: options({ BossEventRounds: eventRounds }) })).some((w) => w.kind === "ChimpsEventRounds")).toBe(warned));

  it.each([
    ["Newest", true],
    ["Earlier", true],
    ["Own", false],
  ] as const)("warns about the %s CHIMPS rounds: %s, naming them", (chimps, warned) => {
    const warning = setupWarnings(setupFacts({ mode: "Clicks", options: options({ BossEventRounds: true, ChimpsRounds: chimps }) })).find(
      (w) => w.kind === "ChimpsEventRounds",
    );
    expect(warning?.args).toEqual(warned ? [chimps] : undefined);
  });

  it("puts the CHIMPS warning just before HardSetup", () => {
    const facts = setupFacts({
      mode: "Clicks",
      startRound: 38,
      bossHealth: 20,
      lives: 1,
      noSelling: true,
      options: options({ Elite: true, BossEventRounds: true, Tiers: 3, TierBosses: "Lych" }),
    });
    expect(kinds(facts)).toBe("StartRoundClose,ChimpsEventRounds,HardSetup");
  });
});
