// The same cases as the mod's unit tests (UnitTests/TierMathTests.cs, UnitTests/SkullMathTests.cs)

import { describe, expect, it } from "vitest";
import { evenlySpaced, formatCustom, formatPercent, normalizeCustom, parseCustom } from "../src/math/skulls";
import { endRound, formatHealth, healthPerTierFromExtraTierHealth, spawnRounds } from "../src/math/tiers";
import { roundHalfEven } from "../src/math/dotnet";

const f = (values: number[]) => values.map(Math.fround);
const show = (rounds: number[]) => rounds.join(", ");

describe("skulls", () => {
  it.each([
    [3, [0.75, 0.5, 0.25]],
    [4, [0.8, 0.6, 0.4, 0.2]],
    [1, [0.5]],
    [0, []],
    [-2, []],
  ])("splits the health bar into %i equal parts", (count, expected) => expect(evenlySpaced(count)).toEqual(f(expected)));

  it("caps evenly spaced skulls", () => expect(evenlySpaced(99)).toHaveLength(10));

  it.each([
    ["75,50,25", [0.75, 0.5, 0.25]],
    [" 25 ; 90%, 50, 50 ", [0.9, 0.5, 0.25]],
    ["abc, 0, 100, 150, 33.5", [0.335]],
    ["1, 99", [0.99, 0.01]],
  ])("parses %s", (text, expected) => expect(parseCustom(text)).toEqual(f(expected)));

  it.each(["nothing valid", "", "   ", null])("gives nothing for %s", (text) => expect(parseCustom(text)).toBeNull());

  it.each([
    ["25,25,50", "50, 25"],
    [" 33.5% ; 90 abc", "90, 33.5"],
    ["33,5", "33, 5"],
    ["junk, 0, 100", "fallback"],
    [null, "fallback"],
  ])("normalizes %s", (text, expected) => expect(normalizeCustom(text, "fallback")).toBe(expected));

  it("writes percentages without trailing zeros", () => {
    expect(formatCustom(f([0.5, 0.335]))).toBe("50, 33.5");
    expect(formatPercent(Math.fround(0.3333))).toBe("33.33%");
    expect(formatPercent(Math.fround(0.5))).toBe("50%");
  });
});

describe("tiers", () => {
  it.each([
    [5, null, "40, 60, 80, 100, 120"],
    [5, "  ", "40, 60, 80, 100, 120"],
    [3, "30, 50,70", "30, 50, 70"],
    [4, "30;45", "30, 45, 65, 85"],
    [2, "30, 50, 70", "30, 50"],
    [3, "50, 50, 20", "50, 51, 52"],
    [3, "40, 60, 50", "40, 60, 61"],
    [2, "x, -5, 0, 60", "60, 80"],
    [3, "40, 0, 80", "40, 80, 100"],
    [1, "5000", "1000"],
    [2, "1000", "999, 1000"],
    [3, "995, 2000", "995, 999, 1000"],
    [99, null, "40, 60, 80, 100, 120, 140, 160, 180, 200, 220"],
  ])("%i tiers from %s", (tiers, custom, expected) => expect(show(spawnRounds(tiers, custom))).toBe(expected));

  it("gives the last tier 20 rounds", () => {
    expect(endRound([40, 60, 80, 100, 120])).toBe(140);
    expect(endRound([40, 51])).toBe(71);
  });

  it("shortens health like the game", () => {
    expect(formatHealth(950)).toBe("950");
    expect(formatHealth(7500)).toBe("7.5k");
    expect(formatHealth(112_000)).toBe("112k");
    expect(formatHealth(3_400_000)).toBe("3.4M");
    expect(formatHealth(1_200_000_000)).toBe("1.2B");
    expect(formatHealth(9_960)).toBe("10k");
  });

  it("converts the old Extra Tier Health", () => {
    expect(healthPerTierFromExtraTierHealth(150)).toBe(0);
    expect(healthPerTierFromExtraTierHealth(225)).toBe(10);
  });

  it("rounds halfway values to even, like C#", () => {
    expect(roundHalfEven(2.5)).toBe(2);
    expect(roundHalfEven(3.5)).toBe(4);
    expect(roundHalfEven(-2.5)).toBe(-2);
  });
});
