// The Studio's edit functions (#12) and the Boss Creator math they use: each control gives what the Boss Creator would

import { describe, expect, it } from "vitest";
import { evenRounds, firstAndGap, formatRounds } from "@cbc/share-code/tiers";
import { NO_LIMIT, fromSlider, toSlider } from "../src/math/paragons";
import { GAME } from "../src/data/types";
import { effectiveSpawnRounds } from "@cbc/share-code";
import * as edit from "../src/studio/state";
import { FOLLOWS } from "@cbc/share-code/tier-bosses";

const fresh = edit.newSetup;

describe("Boss Creator math", () => {
  it("spreads rounds for every possible tier (TierMath.EvenRounds)", () => {
    expect(evenRounds(40, 20)).toBe("40, 60, 80, 100, 120, 140, 160, 180, 200, 220");
    expect(evenRounds(5, 0)).toBe("5, 6, 7, 8, 9, 10, 11, 12, 13, 14");
  });

  it("reads the first round and gap back (TierMath.FirstAndGap)", () => {
    expect(firstAndGap([30, 45, 60], 20)).toEqual({ first: 30, gap: 15 });
    expect(firstAndGap([30], 12)).toEqual({ first: 30, gap: 12 });
    expect(firstAndGap([], 12)).toEqual({ first: 40, gap: 12 });
    expect(formatRounds([1, 2])).toBe("1, 2");
  });

  it("maps the Max Paragons slider like ParagonMath", () => {
    const all = GAME.limits.paragons;
    expect(fromSlider(all)).toBe(NO_LIMIT);
    expect(fromSlider(0)).toBe(0);
    expect(fromSlider(-3)).toBe(0);
    expect(toSlider(NO_LIMIT)).toBe(all);
    expect(toSlider(all + 5)).toBe(all);
    expect(toSlider(4)).toBe(4);
  });
});

describe("edit functions", () => {
  it("never change the setup they get", () => {
    const setup = fresh();
    const before = JSON.stringify(setup);
    edit.setBossSpeed(setup, 300);
    edit.setTiers(setup, 9);
    edit.setParagonSlider(setup, 0);
    expect(JSON.stringify(setup)).toBe(before);
  });

  it("keep Boss Speed and Boss Health on the Boss Creator's steps and ranges", () => {
    expect(edit.bossSpeed(edit.setBossSpeed(fresh(), 254))).toBe(250);
    expect(edit.bossSpeed(edit.setBossSpeed(fresh(), 5000))).toBe(1000);
    expect(edit.bossHealth(edit.setBossHealth(fresh(), 1))).toBe(10);
    expect(edit.bossHealth(edit.setBossHealth(fresh(), 1990))).toBe(1990);
    const health = edit.setBossHealth(fresh(), 150).challenge.bloonModifiers as { healthMultipliers: { boss: number } };
    expect(health.healthMultipliers.boss).toBe(1.5);
  });

  it("make the challenge parts a code without them lacks", () => {
    const setup = { ...fresh(), challenge: {} };
    expect(edit.bossSpeed(setup)).toBe(100);
    expect(edit.bossSpeed(edit.setBossSpeed(setup, 200))).toBe(200);
    expect(edit.bossHealth(edit.setBossHealth(setup, 300))).toBe(300);
  });

  it("keep tiers, per-tier changes and skulls in range", () => {
    expect(edit.setTiers(fresh(), 99).options.Tiers).toBe(10);
    expect(edit.setTiers(fresh(), 0).options.Tiers).toBe(1);
    expect(edit.setSpeedPerTier(fresh(), 80).options.SpeedPerTier).toBe(50);
    expect(edit.setHealthPerTier(fresh(), -50).options.HealthPerTier).toBe(-10);
    expect(edit.setSkullCount(fresh(), 40).options.SkullCount).toBe(10);
  });

  it("spread the spawn rounds from the two sliders, and show them back", () => {
    const setup = edit.spreadSpawnRounds(edit.setTiers(fresh(), 3), 25, 10);
    expect(effectiveSpawnRounds(setup.options)).toEqual([25, 35, 45]);
    expect(edit.spawnSliders(setup)).toEqual({ first: 25, gap: 10 });
    expect(edit.resetSpawnRounds(setup).options.SpawnRounds).toBeNull();
    expect(edit.setSpawnRounds(setup, "  ").options.SpawnRounds).toBeNull();
  });

  it("show sliders inside their range for rounds they can't make", () => {
    const setup = edit.setSpawnRounds(edit.setTiers(fresh(), 2), "500, 900");
    expect(edit.spawnSliders(setup)).toEqual({ first: 200, gap: 50 });
  });

  it("keep the skulls as they were when Custom % has nothing valid", () => {
    const setup = edit.setCustomSkulls(fresh(), "60, 30");
    expect(setup.options.CustomSkulls).toBe("60, 30");
    expect(edit.setCustomSkulls(setup, "junk").options.CustomSkulls).toBe("60, 30");
    expect(edit.resetSkulls(setup).options).toMatchObject({ SkullMode: "Vanilla", SkullCount: 3, CustomSkulls: "75, 50, 25" });
  });

  it("reset the boss and the paragon limit", () => {
    const setup = edit.setElite(edit.setBoss(fresh(), "Lych"), true);
    expect(edit.resetBoss(setup).options).toMatchObject({ Boss: "Bloonarius", Elite: false });
    expect(edit.paragonSlider(edit.setParagonSlider(fresh(), GAME.limits.paragons))).toBe(GAME.limits.paragons);
    expect(edit.setParagonSlider(fresh(), GAME.limits.paragons).challenge.maxParagons).toBe(NO_LIMIT);
  });
});

describe("CHIMPS event rounds (#139)", () => {
  it("picks the CHIMPS events' rounds like the Boss Creator's dropdown", () => {
    const setup = fresh();
    expect(edit.chimpsEventChoice(setup)).toBe("Off");
    const earlier = edit.setChimpsEventRounds(setup, "Earlier");
    expect(earlier.options).toMatchObject({ BossEventRounds: true, ChimpsRounds: "Earlier" });
    expect(edit.chimpsEventChoice(earlier)).toBe("Earlier");
    expect(edit.chimpsEventChoice(edit.setChimpsEventRounds(earlier, "Newest"))).toBe("Newest");
    // Off keeps which rounds were picked, like the game
    const off = edit.setChimpsEventRounds(earlier, "Off");
    expect(off.options).toMatchObject({ BossEventRounds: false, ChimpsRounds: "Earlier" });
    expect(edit.chimpsEventChoice(off)).toBe("Off");
  });
});

describe("Advanced Mode (#95)", () => {
  /** Lych Elite at 150 % speed, 80 % health and 4 skulls in Standard */
  const standard = () =>
    [
      (s: edit.StudioSetup) => edit.setBoss(s, "Lych"),
      (s: edit.StudioSetup) => edit.setElite(s, true),
      (s: edit.StudioSetup) => edit.setBossSpeed(s, 150),
      (s: edit.StudioSetup) => edit.setBossHealth(s, 80),
      (s: edit.StudioSetup) => edit.setSkullMode(s, "EvenlySpaced"),
      (s: edit.StudioSetup) => edit.setSkullCount(s, 4),
    ].reduce((s, change) => change(s), fresh());

  it("starts every tier from Standard's boss and values, which go back to normal", () => {
    const { setup, memory } = edit.setAdvanced(standard(), true, edit.NO_MEMORY);
    expect(setup.options.TierBosses).toBe(Array(10).fill("LychElite@150/80#4").join(","));
    expect(edit.bossSpeed(setup)).toBe(100);
    expect(edit.bossHealth(setup)).toBe(100);
    expect(setup.options.SkullMode).toBe("Vanilla");
    expect(memory.standardValues).toMatchObject({ speed: 150, health: 80, skulls: "EvenlySpaced", skullCount: 4 });
  });

  it("brings Standard's values back, and forgets tiers that were never changed", () => {
    const on = edit.setAdvanced(standard(), true, edit.NO_MEMORY);
    const off = edit.setAdvanced(on.setup, false, on.memory);
    expect(off.setup.options.TierBosses).toBeNull();
    expect(edit.bossSpeed(off.setup)).toBe(150);
    expect(edit.bossHealth(off.setup)).toBe(80);
    expect(off.setup.options).toMatchObject({ SkullMode: "EvenlySpaced", SkullCount: 4 });
    expect(off.memory.lastTierBosses).toBeNull();
  });

  it("remembers changed tiers for going back to Advanced", () => {
    const on = edit.setAdvanced(standard(), true, edit.NO_MEMORY);
    const changed = edit.pickTierBoss(on.setup, 2, "Vortex");
    const off = edit.setAdvanced(changed, false, on.memory);
    expect(off.memory.lastTierBosses).toBe(changed.options.TierBosses);
    const back = edit.setAdvanced(off.setup, true, off.memory);
    expect(back.setup.options.TierBosses).toBe(changed.options.TierBosses);
  });

  it("does nothing when the tab is already on", () => {
    const setup = standard();
    expect(edit.setAdvanced(setup, false, edit.NO_MEMORY).setup).toBe(setup);
  });

  it("picks, steps and resets a tier's boss, keeping its own values", () => {
    const on = edit.setAdvanced(standard(), true, edit.NO_MEMORY).setup;
    const picked = edit.pickTierBoss(on, 3, "Random");
    expect(edit.shownTier(picked, 3)).toMatchObject({ boss: "Random", elite: true, speed: 150, health: 80, skulls: "EvenlySpaced" });
    expect(edit.shownTier(edit.stepTierBoss(picked, 3, 1), 3).boss).toBe("Bloonarius");
    expect(edit.shownTier(edit.stepTierBoss(picked, 3, -1), 3).boss).toBe("Diamondback");
    // A tier that follows the setup shows the setup's boss
    const follows = edit.setTierBoss(picked, 3, FOLLOWS);
    expect(edit.shownTier(follows, 3)).toMatchObject({ boss: "Lych", elite: true, speed: 100 });
  });

  it("keeps a tier's values in range", () => {
    const on = edit.setAdvanced(fresh(), true, edit.NO_MEMORY).setup;
    expect(edit.shownTier(edit.setTierSpeed(on, 1, 4999), 1).speed).toBe(1000);
    expect(edit.shownTier(edit.setTierHealth(on, 1, 3), 1).health).toBe(10);
    expect(edit.shownTier(edit.setTierHealth(on, 1, 255), 1).health).toBe(260);
    // The count is only kept in the token with Evenly Spaced skulls, like in the mod
    const spaced = edit.setTierSkullMode(on, 1, "EvenlySpaced");
    expect(edit.shownTier(edit.setTierSkullCount(spaced, 1, 30), 1).skullCount).toBe(10);
    const custom = edit.setTierCustomSkulls(edit.setTierSkullMode(on, 1, "Custom"), 1, "90, 45");
    expect(edit.shownTier(custom, 1).customSkulls).toBe("90, 45");
    expect(edit.shownTier(edit.setTierCustomSkulls(custom, 1, "junk"), 1).customSkulls).toBe("90, 45");
    expect(edit.shownTier(edit.resetTierSkulls(custom, 1), 1).skulls).toBe("Vanilla");
  });

  it("changes no tier in Standard", () => {
    const setup = fresh();
    expect(edit.pickTierBoss(setup, 2, "Lych")).toBe(setup);
  });

  it("resets every tier back to a new Standard setup with Standard's old values", () => {
    const on = edit.setAdvanced(standard(), true, edit.NO_MEMORY);
    const { setup, memory } = edit.resetAllTiers(edit.pickTierBoss(on.setup, 1, "Phayze"), on.memory);
    expect(setup.options).toMatchObject({ Boss: "Bloonarius", Elite: false, TierBosses: null, SkullMode: "EvenlySpaced" });
    expect(edit.bossSpeed(setup)).toBe(150);
    expect(memory).toEqual(edit.NO_MEMORY);
  });
});
