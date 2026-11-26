// The mod's own boss Gildebloon (#25): codes with it are version 8 and read back the same, its numbers are known for
// the preview, and only the Studio's hidden switch (?experimental=gildebloon, or the dev server) offers it
import { describe, expect, it } from "vitest";
import {
  BOSSES,
  KNOWN_BOSSES,
  VERSION5_PREFIX,
  VERSION8_PREFIX,
  bossOf,
  decode,
  defaultOptions,
  encode,
  pack,
  prefixFor,
  usesGildebloon,
} from "@cbc/share-code";
import { CHOICES, RANDOM, TIER_BOSSES, parseTierBosses } from "@cbc/share-code/tier-bosses";
import type { Challenge } from "@cbc/share-code";
import { GAME } from "../src/data/types";
import { tierCards } from "../src/preview/compute";
import { experimentalOn, studioBosses, studioChoices } from "../src/studio/experimental";

describe("Gildebloon", () => {
  it("is known but not offered", () => {
    expect(KNOWN_BOSSES).toContain("Gildebloon");
    expect(BOSSES).not.toContain("Gildebloon");
    expect(TIER_BOSSES).not.toContain("Gildebloon");
    expect(CHOICES).not.toContain("Gildebloon");
  });

  it("makes version 8 codes that read back the same", async () => {
    for (const setup of [
      { ...defaultOptions(), Boss: "Gildebloon" },
      { ...defaultOptions(), Boss: "Gildebloon", Elite: true, Tiers: 7 },
      { ...defaultOptions(), Tiers: 3, TierBosses: "Lych,GildebloonElite@120/80" },
    ]) {
      expect(usesGildebloon(setup)).toBe(true);
      const code = await encode(setup);
      expect(code.startsWith(VERSION8_PREFIX)).toBe(true);
      const read = await decode(code);
      if (!read.ok) throw new Error(read.reason);
      expect(read.options).toMatchObject(setup);
    }
  });

  it("only makes codes version 8 when a tier that is played has it", () => {
    const options = { ...defaultOptions(), Tiers: 1, TierBosses: ",GildebloonElite", ChimpsRounds: "Own" as const };
    expect(usesGildebloon(options)).toBe(false);
    expect(prefixFor(options)).toBe(VERSION5_PREFIX);
    expect(bossOf({ ...options, Tiers: 2 }, 2)).toEqual({ boss: "Gildebloon", elite: true });
    expect(parseTierBosses("gildebloonelite")[0]).toMatchObject({ boss: "Gildebloon", elite: true });
  });

  it("goes by its name only: its number is no boss, as before it existed", async () => {
    const read = await decode(await pack(JSON.stringify({ o: { Boss: 100 }, c: {} }), VERSION8_PREFIX));
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.Boss).toBe("Bloonarius");
  });

  it("has its numbers for the preview, from the mod's own tables", () => {
    expect(GAME.bosses.Gildebloon.normal.map((tier) => tier.health)).toEqual([12000, 45000, 200000, 450000, 1900000]);
    expect(GAME.bosses.Gildebloon.elite[4].health).toBe(22000000);
    expect(GAME.bosses.Gildebloon.normal[0].skulls).toHaveLength(4);
    expect(GAME.bosses.Gildebloon.elite[0].skulls).toHaveLength(7);
    for (const options of [
      { ...defaultOptions(), Boss: "Gildebloon" },
      { ...defaultOptions(), Boss: "Gildebloon", Elite: true, Tiers: 8 },
      { ...defaultOptions(), Tiers: 2, TierBosses: "Lych,Gildebloon" },
    ]) {
      for (const card of tierCards(options, { bossSpeed: 1, bossHealth: 1 } as Challenge)) {
        expect(card.health).not.toBe("?");
        expect(card.skulls).not.toBeNull();
      }
    }
  });

  it("is in the Studio only with the hidden switch or on the dev server", () => {
    expect(experimentalOn("", false)).toBe(false);
    expect(experimentalOn("?code=CBC8:abc", false)).toBe(false);
    expect(experimentalOn("?experimental=gildebloon", false)).toBe(true);
    expect(experimentalOn("?code=x&experimental=Gildebloon", false)).toBe(true);
    expect(experimentalOn("?experimental=other,gildebloon", false)).toBe(true);
    expect(experimentalOn("", true)).toBe(true);

    expect(studioBosses(false)).toEqual(BOSSES);
    expect(studioBosses(true)).toEqual([...BOSSES, "Gildebloon"]);
    expect(studioChoices(false)).toEqual(CHOICES);
    const choices = studioChoices(true);
    expect(choices).toContain("Gildebloon");
    expect(choices[choices.length - 1]).toBe(RANDOM);
  });
});
