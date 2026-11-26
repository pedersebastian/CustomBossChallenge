// @vitest-environment happy-dom
// Codes with the bosses a game picked for its Random tiers (#270): only CBC7 keeps which tiers were Random, the preview
// and the Studio show them as Random, and picking a boss for such a tier forgets it

import { describe, expect, it } from "vitest";
import { decode, defaultOptions, encode, hideRandomPicks, pack, tierBossName, wasRandom, withoutRandomMark } from "@cbc/share-code";
import { parseTierList } from "@cbc/share-code/tier-bosses";
import { renderSetup } from "../src/preview/render";
import * as edit from "../src/studio/state";

const picked = { ...defaultOptions(), Tiers: 3, TierBosses: "Lych,VortexElite@150/100,Phayze", RandomTiers: "2,3" };

describe("codes with the bosses a game picked (#270)", () => {
  it("are version 7 and read back with the tiers that were Random", async () => {
    const code = await encode(picked);
    expect(code.slice(0, 5)).toBe("CBC7:");
    const read = await decode(code);
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.TierBosses).toBe(picked.TierBosses);
    expect(read.options.RandomTiers).toBe("2,3");
  });

  it("only keep them in version 7, brought into range like the mod", async () => {
    const json = JSON.stringify({ o: { Tiers: 3, TierBosses: "Lych,Vortex,Phayze", RandomTiers: "3, 2,2,9,x" }, c: {} });
    const old = await decode(await pack(json, "CBC6:"));
    const now = await decode(await pack(json, "CBC7:"));
    if (!old.ok || !now.ok) throw new Error("not read");
    expect(old.options.RandomTiers).toBeUndefined();
    expect(now.options.RandomTiers).toBe("2,3");
  });

  it("drop them in Standard Mode", async () => {
    const read = await decode(await pack(JSON.stringify({ o: { RandomTiers: "1" }, c: {} }), "CBC7:"));
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.RandomTiers).toBeUndefined();
  });

  it("show those tiers as Random, keeping their Elite and values", () => {
    const shown = hideRandomPicks(picked);
    expect(tierBossName(shown, 1)).toBe("Lych");
    expect(tierBossName(shown, 2)).toBe("Random Elite");
    expect(tierBossName(shown, 3)).toBe("Random");
    expect(shown.TierBosses).toBe("Lych,RandomElite@150/100,Random");
    expect(hideRandomPicks(defaultOptions())).toEqual(defaultOptions());
  });

  it("never name the picked bosses in the preview", async () => {
    const read = await decode(await encode(picked));
    if (!read.ok) throw new Error(read.reason);
    const text = renderSetup(read.options, read.challenge).textContent ?? "";
    expect(text).not.toMatch(/Vortex|Phayze/);
    expect(text).toMatch(/Random/);
  });

  it("forget a tier when its boss is picked, not when its values change", () => {
    const setup = { ...edit.newSetup(), options: picked };
    expect(wasRandom(edit.setTierSpeed(setup, 2, 200).options, 2)).toBe(true);
    expect(wasRandom(edit.setTierElite(setup, 2, false).options, 2)).toBe(true);
    expect(edit.pickTierBoss(setup, 2, "Lych").options.RandomTiers).toBe("3");
    expect(edit.setTierBoss(setup, 3, { ...edit.shownTier(setup, 3), boss: null }).options.RandomTiers).toBe("2");
    expect(withoutRandomMark(withoutRandomMark(picked, 2), 3).RandomTiers).toBeUndefined();
  });

  it("step on from Random, without giving the boss away", () => {
    const setup = { ...edit.newSetup(), options: picked };
    const stepped = edit.stepTierBoss(setup, 3, 1);
    // After Random the bosses start over
    expect(edit.shownTier(stepped, 3).boss).toBe("Bloonarius");
    expect(parseTierList(stepped.options.RandomTiers)).toEqual([2]);
  });
});
