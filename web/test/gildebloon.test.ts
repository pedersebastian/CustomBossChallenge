// The mod's own boss Gildebloon (#25): codes with it are version 6 and read back the same; nothing offers it yet
import { describe, expect, it } from "vitest";
import {
  BOSSES,
  KNOWN_BOSSES,
  VERSION5_PREFIX,
  VERSION6_PREFIX,
  bossOf,
  decode,
  defaultOptions,
  encode,
  pack,
  prefixFor,
  usesGildebloon,
} from "@cbc/share-code";
import { CHOICES, TIER_BOSSES, parseTierBosses } from "@cbc/share-code/tier-bosses";

describe("Gildebloon", () => {
  it("is known but not offered", () => {
    expect(KNOWN_BOSSES).toContain("Gildebloon");
    expect(BOSSES).not.toContain("Gildebloon");
    expect(TIER_BOSSES).not.toContain("Gildebloon");
    expect(CHOICES).not.toContain("Gildebloon");
  });

  it("makes version 6 codes that read back the same", async () => {
    for (const setup of [
      { ...defaultOptions(), Boss: "Gildebloon" },
      { ...defaultOptions(), Boss: "Gildebloon", Elite: true, Tiers: 7 },
      { ...defaultOptions(), Tiers: 3, TierBosses: "Lych,GildebloonElite@120/80" },
    ]) {
      expect(usesGildebloon(setup)).toBe(true);
      const code = await encode(setup);
      expect(code.startsWith(VERSION6_PREFIX)).toBe(true);
      const read = await decode(code);
      if (!read.ok) throw new Error(read.reason);
      expect(read.options).toMatchObject(setup);
    }
  });

  it("only makes codes version 6 when a tier that is played has it", () => {
    const options = { ...defaultOptions(), Tiers: 1, TierBosses: ",GildebloonElite", ChimpsRounds: "Own" as const };
    expect(usesGildebloon(options)).toBe(false);
    expect(prefixFor(options)).toBe(VERSION5_PREFIX);
    expect(bossOf({ ...options, Tiers: 2 }, 2)).toEqual({ boss: "Gildebloon", elite: true });
    expect(parseTierBosses("gildebloonelite")[0]).toMatchObject({ boss: "Gildebloon", elite: true });
  });

  it("goes by its name only: its number is no boss, as before it existed", async () => {
    const read = await decode(await pack(JSON.stringify({ o: { Boss: 100 }, c: {} }), VERSION6_PREFIX));
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.Boss).toBe("Bloonarius");
  });
});
