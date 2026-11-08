// The same cases as the mod's unit tests (UnitTests/TierMathTests.cs, UnitTests/SkullMathTests.cs)

import { describe, expect, it } from "vitest";
import { evenlySpaced, formatCustom, formatPercent, normalizeCustom, parseCustom } from "@cbc/share-code/skulls";
import { endRound, formatHealth, healthPerTierFromExtraTierHealth, spawnRounds } from "@cbc/share-code/tiers";
import { roundHalfEven } from "@cbc/share-code/dotnet";
import { FOLLOWS, normalizeTierBosses, parseTierBosses, resolveTiers, stepTier, tierEntry, tierPercents, withTier } from "@cbc/share-code/tier-bosses";
import tierBossCases from "./tier-boss-cases.json";
import skullCases from "./skull-cases.json";
import { isUnlimited, paragonsText } from "../src/math/paragons";
import { GAME } from "../src/data/types";

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

describe("rounding like .NET", () => {
  // The same cases as UnitTests/SkullMathTests.cs, from skull-cases.json
  it.each(skullCases.cases.map((c) => [c.text, c.shown]))("a custom skull of %s shows as %s", (text, shown) =>
    expect(normalizeCustom(text, "-")).toBe(shown),
  );

  // .NET rounds the shortest text of a double, so 1.45 (stored as 1.4499999…) still gives 1.5, like Phayze's tier 5
  it.each([
    [1_450_000, "1.5M"],
    [1_440_000, "1.4M"],
    [14_500, "15k"],
    [9_950, "10k"],
  ])("formatHealth(%d) = %s", (health, shown) => expect(formatHealth(health)).toBe(shown));
});

// The same cases as UnitTests/TierBossMathTests.cs
describe("tier bosses", () => {
  it.each([
    ["Lych,,RandomElite,VortexElite", "Lych,,RandomElite,VortexElite"],
    [" lych , VORTEXelite ", "Lych,VortexElite"],
    ["Lych,,,", "Lych"],
    ["Nonsense,Lych", ",Lych"],
    ["LychElite@120/80,Vortex@100/100", "LychElite@120/80,Vortex"],
    [" lych @ 125 / 5000 ", "Lych@130/2000"],
    ["Lych@0/x", "Lych@10/100"],
    ["@150/100,,", "@150/100"],
    ["Nonsense@200/200", "@200/200"],
    ["Lych@99999999999/100", "Lych"],
    ["LychElite@120/80#4", "LychElite@120/80#4"],
    [" lych # 12 ", "Lych#10"],
    ["Lych#-3", "Lych#0"],
    ["Vortex#25;50%;75;junk;50", "Vortex#75%;50%;25%"],
    ["Vortex#33.5%", "Vortex#33.5%"],
    ["Lych#junk,Lych#%", "Lych,Lych"],
    ["#5,,", "#5"],
    ["Lych#4@150/100", "Lych"],
  ])("normalizes %j to %j", (list, expected) => expect(normalizeTierBosses(list)).toBe(expected));

  it("reads each tier's speed and health", () =>
    expect(tierEntry("Lych,VortexElite@150/250", 2)).toEqual({ ...FOLLOWS, boss: "Vortex", elite: true, speed: 150, health: 250 }));

  it("reads each tier's skulls", () => {
    const even = tierEntry("Lych,Vortex#4", 2);
    expect(even).toEqual({ ...FOLLOWS, boss: "Vortex", skulls: "EvenlySpaced", skullCount: 4 });
    expect(tierPercents(even)).toEqual(f([0.8, 0.6, 0.4, 0.2]));
    const custom = tierEntry("Vortex@200/100#60%;30%", 1);
    expect(custom).toEqual({ ...FOLLOWS, boss: "Vortex", speed: 200, skulls: "Custom", customSkulls: "60, 30" });
    expect(tierPercents(custom)).toEqual(f([0.6, 0.3]));
    expect(tierPercents(tierEntry("Lych", 1))).toBeNull();
    expect(tierPercents(tierEntry("Lych#0", 1))).toEqual([]);
  });
});

// ParagonMath.IsUnlimited (Tests/OptionsTests.cs, ParagonMathTest): a limit of every paragon or more limits nothing (#113)
describe("max paragons", () => {
  it.each([
    [-1, "Unlimited"],
    [0, "None"],
    [1, "1"],
    [13, "13"],
    [14, "Unlimited"],
    [1000, "Unlimited"],
  ])("%d of 14 paragons shows as %s", (max, shown) => expect(paragonsText(max, 14)).toBe(shown));

  it("knows how many paragons the game has", () => expect(isUnlimited(GAME.limits.paragons)).toBe(true));
});

describe("changing a tier's boss like the Boss Creator", () => {
  // The same cases as UnitTests/TierBossMathTests.cs, from tier-boss-cases.json (#95)
  const tierOf = (token: string) => parseTierBosses(token)[0] ?? FOLLOWS;
  const tokenOf = (token: string) => (token === "" ? "" : withTier(null, 1, tierOf(token)));

  it.each(tierBossCases.with.map((c) => [c.list, c.tier, c.boss, c.expected] as const))("With(%j, %i, %j) = %j", (list, tier, boss, expected) =>
    expect(withTier(list, tier, tierOf(boss))).toBe(expected),
  );

  it.each(tierBossCases.step.map((c) => [c.tier, c.setupBoss, c.setupElite, c.direction, c.expected] as const))(
    "Step(%j, %s, %s, %i) = %j",
    (tier, boss, elite, direction, expected) => expect(withTier(null, 1, stepTier(tierOf(tier), boss, elite, direction))).toBe(tokenOf(expected)),
  );

  it.each(tierBossCases.resolve.map((c) => [c.list, c.boss, c.elite, c.tiers, c.pick, c.standard, c.expected] as const))(
    "Resolve(%j, %s, %s, %i, pick %i, %j) = %j",
    (list, boss, elite, tiers, pick, standard, expected) =>
      expect(resolveTiers(list, boss, elite, tiers, () => pick, tierOf(standard))).toBe(expected),
  );
});
