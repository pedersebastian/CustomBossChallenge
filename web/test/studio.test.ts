// The Studio's edit functions (#12) and the Boss Creator math they use: each control gives what the Boss Creator would

import { describe, expect, it } from "vitest";
import { evenRounds, firstAndGap, formatRounds } from "../src/math/tiers";
import { NO_LIMIT, fromSlider, toSlider } from "../src/math/paragons";
import { GAME } from "../src/data/types";
import { effectiveSpawnRounds } from "../src/code/options";
import * as edit from "../src/studio/state";

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
