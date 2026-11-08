// Comparing two setups (issue #35): which options differ, the tiers side by side, the verdict and links to a comparison

import { describe, expect, it } from "vitest";
import { decode } from "@cbc/share-code";
import { compareCodesFromHash, compareHashFor, compareLinkTo } from "../src/code/link";
import { compareRows, differences, tierPairs, verdict, type Setup } from "../src/compare/diff";
import { EXAMPLES } from "../src/data/types";
import { encode } from "./encode";


async function setup(code: string): Promise<Setup> {
  const result = await decode(code);
  if (!result.ok) throw new Error(`Didn't load: ${result.reason}`);
  return result;
}

const labels = (a: Setup, b: Setup): string[] => differences(a, b).map((row) => row.label);

describe("differences", () => {
  it("finds none between a code and itself", async () => {
    for (const example of EXAMPLES) {
      const a = await setup(example.code);
      expect(differences(a, a)).toEqual([]);
      expect(verdict(a, a)).toEqual([]);
      expect(compareRows(a, a).length).toBeGreaterThan(20);
    }
  });

  it("finds the one option that changed", async () => {
    const a = await setup(await encode({ o: { Boss: "Lych", Tiers: 5 } }));
    const b = await setup(await encode({ o: { Boss: "Lych", Tiers: 5, HealthPerTier: 20 } }));
    expect(differences(a, b)).toEqual([{ group: "Tiers", label: "Health per Tier", a: "Off", b: "+20 %", same: false }]);
    expect(verdict(a, b).some((line) => line.startsWith("Different bosses"))).toBe(false);
  });

  it("finds a changed Challenge Editor rule", async () => {
    const a = await setup(await encode({ c: { startRules: { cash: 650 } } }));
    const b = await setup(await encode({ c: { startRules: { cash: 20000 }, bloonModifiers: { allCamo: true } } }));
    expect(labels(a, b)).toEqual(["Starting cash", "All Camo"]);
    const cash = differences(a, b)[0];
    expect([cash.a, cash.b]).toEqual(["650", "20,000"]);
  });

  it("lists a tower limit only one of them changes", async () => {
    const a = await setup(await encode({}));
    const b = await setup(await encode({ c: { towers: { $items: { 0: { max: 3 } } } } }));
    const towers = differences(a, b).filter((row) => row.group === "Towers");
    expect(towers).toHaveLength(1);
    expect([towers[0].a, towers[0].b]).toEqual(["Default", "Max 3"]);
  });

  it("compares different bosses", async () => {
    const a = await setup(await encode({ o: { Boss: "Bloonarius" } }));
    const b = await setup(await encode({ o: { Boss: "Lych", Elite: true } }));
    expect(labels(a, b)).toEqual(expect.arrayContaining(["Boss", "Elite"]));
    expect(labels(a, b)).not.toContain("Tiers");
    expect(verdict(a, b)[0]).toBe("Different bosses: A is Bloonarius, B is Elite Lych.");
    // Different bosses have different health, so the verdict says how much
    expect(verdict(a, b).some((line) => line.includes("boss health at tier"))).toBe(true);
  });

  it("reads a version 1 code like the same setup as a version 2 code", async () => {
    // CBC1 codes from before 2.3 have Extra Tier Health, which became Health per Tier
    const a = await setup(await encode({ o: { Tiers: 7, ExtraTierHealth: 300 } }, "CBC1:"));
    const b = await setup(await encode({ o: { Tiers: 7, HealthPerTier: 20 } }));
    expect(differences(a, b)).toEqual([]);

    const c = await setup(await encode({ o: { Tiers: 7, HealthPerTier: 40 } }));
    expect(labels(a, c)).toEqual(["Health per Tier"]);
  });
});

describe("tiers", () => {
  it("pairs up tiers, with nothing where a setup has fewer", async () => {
    const pairs = tierPairs(await setup(await encode({ o: { Tiers: 3 } })), await setup(await encode({ o: { Tiers: 5 } })));
    expect(pairs.map((pair) => pair.tier)).toEqual([1, 2, 3, 4, 5]);
    expect(pairs[2].a).not.toBeNull();
    expect(pairs[3].a).toBeNull();
    expect(pairs[4].b?.healthValue).toBeGreaterThan(0);
  });
});

describe("verdict", () => {
  it("says how much more health B has at the last tier both have", async () => {
    const a = await setup(await encode({ o: { Tiers: 5 } }));
    const b = await setup(await encode({ c: { bloonModifiers: { healthMultipliers: { boss: 1.4 } } }, o: { Tiers: 5 } }));
    expect(verdict(a, b)).toEqual([expect.stringMatching(/^B has 40 % more boss health at tier 5 /)]);
    expect(verdict(b, a)).toEqual([expect.stringMatching(/^B has 29 % less boss health at tier 5 /)]);
  });

  it("uses times for big differences and names the side that has more", async () => {
    const a = await setup(await encode({ o: { Tiers: 5 } }));
    const b = await setup(await encode({ c: { bloonModifiers: { healthMultipliers: { boss: 4 } } }, o: { Tiers: 5 } }));
    expect(verdict(a, b)[0]).toMatch(/^B has 4 times the boss health at tier 5/);
    expect(verdict(b, a)[0]).toMatch(/^A has 4 times the boss health at tier 5/);
  });

  it("speaks of tiers and speed", async () => {
    const a = await setup(await encode({ o: { Tiers: 5 } }));
    const b = await setup(await encode({ o: { Tiers: 7, SpeedPerTier: 10 } }));
    const lines = verdict(a, b);
    expect(lines[0]).toBe("B has 7 tiers, A has 5.");
    // Tier 5 of B has 4 × 10 % more speed: 40 % faster
    expect(lines).toContain("B's boss is 40 % faster at tier 5.");
  });

  it("compares how fast different bosses really move", async () => {
    // Boss Speed ×3 on Bloonarius (1.25) against Lych (2.0) at normal speed: 3.75 against 2, not ×3 against ×1
    const a = await setup(await encode({ o: { Boss: "Lych", Tiers: 1 } }));
    const b = await setup(await encode({ o: { Boss: "Bloonarius", Tiers: 1 }, c: { bloonModifiers: { bossSpeedMultiplier: 3 } } }));
    expect(verdict(a, b)).toContain("B's boss is 88 % faster at tier 1.");

    // Same speed compared with the normal game, but Vortex is faster than Phayze
    const vortex = await setup(await encode({ o: { Boss: "Vortex", Tiers: 1 } }));
    const phayze = await setup(await encode({ o: { Boss: "Phayze", Tiers: 1 } }));
    expect(verdict(vortex, phayze)).toContain("A's boss is 4.5 times as fast at tier 1.");
  });
});

describe("comparison links", () => {
  const PAGE = "https://pedersebastian.github.io/CustomBossChallenge/compare.html";

  it.each(EXAMPLES.map((example, i) => [i, example.code, EXAMPLES[(i + 1) % EXAMPLES.length].code] as const))(
    "example %i survives a link",
    (_, a, b) => {
      const link = compareLinkTo(a, b, PAGE);
      expect(link).toBe(`${PAGE}#a=${a}&b=${b}`);
      expect(compareCodesFromHash(new URL(link).hash)).toEqual({ a, b });
    },
  );

  it("leaves out a missing code", () => {
    expect(compareHashFor("CBC2:abc", "")).toBe("#a=CBC2:abc");
    expect(compareHashFor("", "")).toBe("");
    expect(compareCodesFromHash("#b=CBC2:abc")).toEqual({ a: "", b: "CBC2:abc" });
  });

  it("ignores what isn't a code", () => {
    expect(compareCodesFromHash("#top")).toEqual({ a: "", b: "" });
    expect(compareCodesFromHash("#a=hello&b=CBC2%3Aabc")).toEqual({ a: "", b: "CBC2:abc" });
  });
});
