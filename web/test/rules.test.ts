// The Challenge Editor's rules in the Studio (#97): every value is held to ChallengeLimits' limits, only fields a code
// may set are written, and the website's reader keeps what the Studio writes

import { describe, expect, it } from "vitest";
import { decode } from "../src/code/decode";
import { encode } from "../src/code/encode";
import { GAME, type Json, type JsonObject } from "../src/data/types";
import { float } from "../src/math/dotnet";
import { MAP_GROUPS, OTHER_MAPS, RULES, cleanText, mapChoices, percentText, resetRule, ruleInput, ruleValue, setRule, type Rule } from "../src/studio/rules";
import { newSetup } from "../src/studio/state";

const rule = (id: string): Rule => {
  const found = RULES.find((candidate) => candidate.path.join(".") === id);
  if (!found) throw new Error(`No rule ${id}`);
  return found;
};

const set = (id: string, input: string | boolean) => setRule(newSetup(), rule(id), ruleInput(rule(id), input));

describe("the rule table", () => {
  it("only has fields a code may set (CodeFormat.ChallengeFields, #108)", () => {
    for (const { path } of RULES) {
      const field = path.reduce<Json | undefined>((fields, key) => (fields as JsonObject | undefined)?.[key], GAME.challengeFields);
      expect(field, path.join(".")).toBeTypeOf("string");
    }
  });

  it("never offers the end round, which follows the last tier in a boss game", () => {
    expect(RULES.some((r) => r.path.includes("endRound"))).toBe(false);
  });

  it("offers every map, difficulty and mode, and turns off the modes Play refuses", () => {
    const choices = (id: string) => (rule(id) as Extract<Rule, { kind: "choice" }>).choices;
    expect(choices("map").map((c) => c.value).sort()).toEqual(Object.keys(GAME.maps).sort());
    expect(choices("difficulty").map((c) => c.value)).toEqual(Object.keys(GAME.difficulties));
    expect(choices("mode").filter((c) => c.disabled).map((c) => c.value)).toEqual(GAME.unsupportedModes);
    expect(ruleInput(rule("map"), "NoSuchMap")).toBeNull();
    expect(set("map", "NoSuchMap").challenge.map).toBe(GAME.defaultChallenge.map);
  });
});

describe("the map list (#97)", () => {
  it("puts every map under its group from the game, in the game's own order", () => {
    const choices = mapChoices();
    expect(choices.map((c) => c.value)).toEqual(Object.keys(GAME.maps));
    expect(new Set(choices.map((c) => c.group))).toEqual(new Set(MAP_GROUPS));
    expect(choices.find((c) => c.value === "Tutorial")?.group).toBe("Beginner");
  });

  it("puts a map without a group in Other", () => {
    const difficulties = GAME.mapDifficulties;
    try {
      GAME.mapDifficulties = { ...difficulties, Tutorial: "Brand New" };
      expect(mapChoices().find((c) => c.value === "Tutorial")?.group).toBe(OTHER_MAPS);
    } finally {
      GAME.mapDifficulties = difficulties;
    }
  });
});

describe("values held to ChallengeLimits", () => {
  it.each([
    ["startRules.cash", "", -1],
    ["startRules.cash", "-50", -1],
    ["startRules.cash", "0", 0],
    ["startRules.cash", "650.4", 650],
    ["startRules.cash", "999999999999", GAME.limits.maxCash],
    ["startRules.lives", "0", 1],
    ["startRules.lives", "5000000", GAME.limits.maxLives],
    ["startRules.maxLives", "  ", -1],
    ["startRules.round", "1001", 1000],
    ["maxTowers", "3000", GAME.limits.maxTowerCount],
    ["maxTowers", "0", 0],
  ])("%s %j is kept as %d", (id, input, expected) => expect(ruleInput(rule(id), input)).toBe(expected));

  it("refuses what isn't a number", () => expect(ruleInput(rule("startRules.cash"), "lots")).toBeNull());

  it.each([
    ["37.5", 0.375],
    ["0", GAME.limits.minMultiplier],
    ["100000", GAME.limits.maxMultiplier],
    ["100", 1],
  ])("a percent of %s is the multiplier %d", (input, expected) =>
    expect(ruleInput(rule("bloonModifiers.speedMultiplier"), input)).toBe(expected),
  );

  it("shows a multiplier back as a percent", () => {
    expect(percentText(0.375)).toBe("37.5");
    expect(percentText(1)).toBe("100");
    expect(percentText(float(0.3))).toBe("30");
  });

  it("cleans the name like the mod and keeps an empty one empty", () => {
    expect(cleanText("  My\u0007 Boss  ", 60)).toBe("My Boss");
    expect(cleanText("x".repeat(80), 60)).toHaveLength(60);
    expect(cleanText("   ", 60)).toBe("");
  });
});

describe("changing the challenge", () => {
  it("makes the objects on a rule's path when a code has none", () => {
    const setup = { ...newSetup(), challenge: {} };
    const changed = setRule(setup, rule("bloonModifiers.healthMultipliers.moabs"), 2);
    expect(changed.challenge).toEqual({ bloonModifiers: { healthMultipliers: { moabs: 2 } } });
    expect(setup.challenge).toEqual({});
    expect(ruleValue(setup, rule("startRules.cash"))).toBe(-1);
  });

  it("goes back to the new challenge's value", () => {
    const changed = set("noInstaReward", false);
    expect(ruleValue(changed, rule("noInstaReward"))).toBe(false);
    expect(ruleValue(resetRule(changed, rule("noInstaReward")), rule("noInstaReward"))).toBe(true);
  });

  it("writes values the website reads back unchanged", async () => {
    const changes: [string, string | boolean][] = [
      ["name", "Hard Times"], ["map", "Infernal"], ["difficulty", "Hard"], ["mode", "HalfCash"],
      ["startRules.cash", "20000"], ["startRules.lives", "1"], ["startRules.maxLives", "5"], ["startRules.round", "12"],
      ["bloonModifiers.speedMultiplier", "150"], ["bloonModifiers.moabSpeedMultiplier", "37.5"],
      ["bloonModifiers.healthMultipliers.bloons", "300"], ["bloonModifiers.healthMultipliers.moabs", "1"],
      ["bloonModifiers.allCamo", true], ["bloonModifiers.allRegen", true], ["bloonModifiers.allFortified", true],
      ["maxTowers", "8"], ["disableMK", true], ["disableSelling", true], ["disablePowers", true], ["noContinues", true],
    ];
    const setup = changes.reduce((s, [id, input]) => setRule(s, rule(id), ruleInput(rule(id), input)), newSetup());
    const read = await decode(await encode(setup.options, setup.challenge));
    if (!read.ok) throw new Error(read.reason);
    expect(read.challenge).toMatchObject({
      name: "Hard Times", map: "Infernal", difficulty: "Hard", mode: "HalfCash", cash: 20000, lives: 1, maxLives: 5,
      round: 12, bloonSpeed: float(1.5), moabSpeed: float(0.375), bloonHealth: float(3), moabHealth: float(0.01),
      allCamo: true, allRegen: true, allFortified: true, maxTowers: 8, disableMK: true, disableSelling: true, disablePowers: true,
    });
    // Everything the Studio wrote is in the challenge the website read, as it was written
    for (const [id] of changes) expect(ruleValue({ ...setup, challenge: read.json }, rule(id)), id).toEqual(ruleValue(setup, rule(id)));
  });
});
