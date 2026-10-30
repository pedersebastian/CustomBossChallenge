// The look of tiers above 5 must match the game's. The data comes from the mod (Tests/WebDataTest.cs, tierlook.json).

import { describe, expect, it } from "vitest";
import { GAME, TIER_LOOK } from "../src/data/types";
import { MAX_TIERS, VANILLA_TIERS } from "../src/math/tiers";
import { tierGlow, tierGlowStyle } from "../src/preview/tierLook";
import css from "../src/style.css?raw";

const STEPS = MAX_TIERS - VANILLA_TIERS;
const HEX = /^#[0-9A-F]{6}$/;

describe("tier look data", () => {
  it("has a colour for each tier above 5 of every boss", () => {
    expect(Object.keys(TIER_LOOK.themes).sort()).toEqual(Object.keys(GAME.bosses).sort());
    for (const theme of [...Object.values(TIER_LOOK.themes), TIER_LOOK.defaultTheme]) {
      expect(theme).toHaveLength(STEPS);
      for (const color of theme) expect(color).toMatch(HEX);
      // Each tier looks different from the one before
      expect(new Set(theme).size).toBe(STEPS);
    }
  });

  it("gets stronger and faster with each tier", () => {
    expect(TIER_LOOK.tiers.map((entry) => entry.tier)).toEqual([6, 7, 8, 9, 10]);
    for (let i = 1; i < STEPS; i++) {
      const [before, after] = [TIER_LOOK.tiers[i - 1], TIER_LOOK.tiers[i]];
      for (const key of ["glowAlpha", "auraSize", "auraAlpha", "scale", "pulseAmplitude"] as const) {
        expect(after[key], `${key} of tier ${after.tier}`).toBeGreaterThan(before[key]);
      }
      expect(after.pulsePeriod).toBeLessThan(before.pulsePeriod);
    }
    for (const entry of TIER_LOOK.tiers) {
      expect(entry.pulsePeriod).toBeGreaterThan(0);
      expect(entry.pulseAmplitude).toBeLessThan(1);
    }
  });
});

describe("tier glow on the website", () => {
  it("leaves tiers 1-5 alone", () => {
    for (let tier = 1; tier <= VANILLA_TIERS; tier++) expect(tierGlow("Bloonarius", tier)).toBeNull();
  });

  it("uses the boss's colour and the game's pulse for tiers 6-10", () => {
    for (const [boss, theme] of Object.entries(TIER_LOOK.themes)) {
      for (const curve of TIER_LOOK.tiers) {
        const glow = tierGlow(boss, curve.tier)!;
        expect(glow.color).toBe(theme[curve.tier - VANILLA_TIERS - 1]);
        expect(glow.period).toBe(curve.pulsePeriod);
        // TierLook.Pulse swings the strength by 1 ± the amplitude
        expect(glow.glow[0]).toBeCloseTo(curve.glowAlpha * (1 - curve.pulseAmplitude), 6);
        expect(glow.glow[1]).toBeCloseTo(Math.min(curve.glowAlpha * (1 + curve.pulseAmplitude), 1), 6);
        expect(glow.aura[0]).toBeCloseTo(curve.auraAlpha * (1 - curve.pulseAmplitude), 6);
        expect(glow.aura[1]).toBeCloseTo(Math.min(curve.auraAlpha * (1 + curve.pulseAmplitude), 1), 6);
        for (const opacity of [...glow.glow, ...glow.aura]) {
          expect(opacity).toBeGreaterThan(0);
          expect(opacity).toBeLessThanOrEqual(1);
        }
      }
    }
  });

  it("gives bosses the website doesn't know the default colours", () => {
    expect(tierGlow("NoSuchBoss", 6)!.color).toBe(TIER_LOOK.defaultTheme[0]);
    expect(tierGlow("NoSuchBoss", 10)!.color).toBe(TIER_LOOK.defaultTheme[STEPS - 1]);
  });

  it("only sets variables the stylesheet uses", () => {
    const names = tierGlowStyle(tierGlow("Lych", 10)!).match(/--[a-z-]+(?=:)/g)!;
    expect(names.length).toBeGreaterThan(0);
    for (const name of names) expect(css, `style.css uses ${name}`).toContain(`var(${name})`);
  });
});
