// @vitest-environment happy-dom
// The spawn-round timeline (#94) and the strength chart next to the normal game (#96), in the setup view

import { describe, expect, it } from "vitest";
import fixtures from "../src/data/fixtures.json";
import { decode, type Challenge } from "../src/code/decode";
import { defaultOptions, type BossOptions } from "../src/code/options";
import { tierPairs } from "../src/compare/diff";
import { normalGame, tierCards } from "../src/preview/compute";
import { renderSetup } from "../src/preview/render";
import { labelRows, position, renderTimeline, timeline } from "../src/preview/timeline";
import { EXAMPLES } from "../src/data/types";

async function read(code: string): Promise<{ options: BossOptions; challenge: Challenge }> {
  const result = await decode(code);
  if (!result.ok) throw new Error(result.reason);
  return { options: result.options, challenge: result.challenge };
}

const exampleCode = (prefix: string, has = ""): string => EXAMPLES.find((e) => e.code.startsWith(prefix) && e.name.includes(has))!.code;

describe("timeline", () => {
  it("gives every code's spawn rounds and end round exactly as the mod reads them", async () => {
    for (const fixture of fixtures.valid) {
      const { options, challenge } = await read(fixture.code);
      const line = timeline(options, challenge);
      expect(line.tiers.map((tier) => tier.round), fixture.code).toEqual(fixture.spawnRounds);
      expect(line.end, fixture.code).toBe(fixture.endRound);
    }
  });

  it("names each tier's boss, Random included, in Advanced Mode", async () => {
    const { options, challenge } = await read(exampleCode("CBC3:"));
    expect(timeline(options, challenge).tiers.map((tier) => tier.boss)).toEqual([
      "Bloonarius", "Lych", "Vortex", "Dreadbloon", "Phayze", "Blastapopoulos", "Diamondback",
    ]);
    const random = { ...options, TierBosses: "RandomElite,Lych" };
    expect(timeline(random, challenge).tiers[0].boss).toBe("Random Elite");
  });

  it("gives tiers 1-5 the event's rounds with Boss Event Rounds, and later tiers none", async () => {
    const options = { ...defaultOptions(), Tiers: 7, BossEventRounds: true, SpawnRounds: "20, 25" };
    const { challenge } = await read(exampleCode("CBC1:"));
    const tiers = timeline(options, challenge).tiers;
    expect(tiers.map((tier) => tier.eventRound)).toEqual([40, 60, 80, 100, 120, null, null]);
    expect(timeline({ ...options, BossEventRounds: false }, challenge).tiers.every((tier) => tier.eventRound === null)).toBe(true);
  });

  it("has a start only when the code sets one", async () => {
    const { options, challenge } = await read(exampleCode("CBC1:"));
    expect(timeline(options, challenge).start).toBeNull();
    expect(timeline(options, { ...challenge, round: 12 }).start).toBe(12);
  });

  it("puts rounds on the bar and stacks labels that would cover each other", () => {
    expect(position(1, 1, 101)).toBe(0);
    expect(position(51, 1, 101)).toBe(0.5);
    expect(position(500, 1, 101)).toBe(1);
    expect(position(5, 5, 5)).toBe(0);
    expect(labelRows([0, 0.5, 1], 0.13)).toEqual([0, 0, 0]);
    expect(labelRows([0.1, 0.12, 0.14, 0.5], 0.13)).toEqual([0, 1, 2, 0]);
    expect(labelRows([0.1, 0.12, 0.3], 0.13)).toEqual([0, 1, 0]);
  });

  it("says every round as text, for screen readers too", async () => {
    const options = { ...defaultOptions(), Boss: "Lych", Elite: true, Tiers: 2, BossEventRounds: true };
    const { challenge } = await read(exampleCode("CBC1:"));
    const block = renderTimeline(options, { ...challenge, round: 5 });
    expect([...block.querySelectorAll("li")].map((li) => li.textContent)).toEqual([
      "The game starts on round 5",
      "Tier 1: round 40, Elite Lych (plays the event's round 40)",
      "Tier 2: round 60, Elite Lych (plays the event's round 60)",
      "The game ends on round 80",
    ]);
    expect(block.querySelector(".timeline-bar")?.getAttribute("aria-hidden")).toBe("true");
  });
});

describe("the normal game to compare with", () => {
  it("drops every change to speed, health and rounds, and keeps the bosses", async () => {
    const { options, challenge } = await read(exampleCode("CBC2:"));
    const normal = normalGame({ ...options, SpawnRounds: "10, 20" }, { ...challenge, bossSpeed: 2, bossHealth: 3 });
    expect(normal.options).toMatchObject({ Boss: options.Boss, Elite: options.Elite, Tiers: 5, SpeedPerTier: 0, HealthPerTier: 0, SpawnRounds: null });
    expect(normal.challenge).toMatchObject({ bossSpeed: 1, bossHealth: 1 });
    expect(tierCards(normal.options, normal.challenge).map((card) => card.round)).toEqual([40, 60, 80, 100, 120]);
  });

  it("gives every Advanced tier its normal speed and health, with the same boss", () => {
    const options = { ...defaultOptions(), Tiers: 3, TierBosses: "LychElite@300/50#4,Random@20/2000,Vortex" };
    const normal = normalGame(options, { bossSpeed: 1, bossHealth: 1 } as Challenge);
    expect(normal.options.TierBosses).toBe("LychElite#4,Random,Vortex");
  });

  it("has nothing to compare with after tier 5", async () => {
    const { options, challenge } = await read(exampleCode("CBC2:"));
    const pairs = tierPairs({ options, challenge }, normalGame(options, challenge));
    expect(pairs).toHaveLength(10);
    expect(pairs.slice(5).every((pair) => pair.b === null)).toBe(true);
    expect(pairs[4].b?.healthValue).toBeGreaterThan(0);
  });
});

describe("the full setup view", () => {
  it("has the timeline and the strength chart, and the compact view neither", async () => {
    const { options, challenge } = await read(exampleCode("CBC3:"));
    const full = renderSetup(options, challenge);
    const headings = [...full.querySelectorAll("h4")].map((h) => h.textContent);
    expect(headings).toEqual(["Tier Preview", "Timeline", expect.stringContaining("Skulls"), "Strength per tier", "Rules"]);
    // Advanced Mode names each tier's boss in the chart's table
    expect([...full.querySelectorAll(".compare-table tbody th")].map((th) => th.textContent).slice(0, 2)).toEqual(["T1 · Bloonarius", "T2 · Lych"]);
    expect(full.querySelector(".legend")?.textContent).toContain("The normal game");

    const compact = renderSetup(options, challenge, true);
    expect(compact.querySelector(".timeline")).toBeNull();
    expect(compact.querySelector(".curves")).toBeNull();
  });
});
