// Codes made by the Studio (#12) for the mod's tests: web/test/website-codes.json holds codes the website made with
// the Studio's own controls, with the options and challenge changes the mod must read from them.
// UnitTests/CodeFormatTests.cs reads them with the mod's CodeFormat, so a code made here loads in the mod unchanged.
//
// With the environment variable CBC_WEBSITE_CODES=1 this writes the file; otherwise it checks that the committed one
// still holds what the website makes now. Codes are compared by the JSON inside, since deflate may pack the same JSON
// into other bytes in another version of Node.

import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { decode, filterChallenge } from "../src/code/decode";
import { encode, normalized, payload } from "../src/code/encode";
import { optionsJson } from "../src/code/options";
import { GAME, type JsonObject } from "../src/data/types";
import * as edit from "../src/studio/state";
import { FOLLOWS } from "../src/math/tierBosses";
import type { StudioSetup } from "../src/studio/state";
import { RULES, ruleInput, setRule } from "../src/studio/rules";
import * as towers from "../src/studio/towers";

const FILE = fileURLToPath(new URL("./website-codes.json", import.meta.url));

/** A setup made by doing the changes one after another, like a player would */
const make = (...changes: ((setup: StudioSetup) => StudioSetup)[]): StudioSetup => changes.reduce((setup, change) => change(setup), edit.newSetup());

/** A Challenge Editor rule as typed or picked in the Studio */
const rule = (id: string, input: string | boolean) => (setup: StudioSetup): StudioSetup => {
  const found = RULES.find((candidate) => candidate.path.join(".") === id);
  if (!found) throw new Error(`No rule ${id}`);
  return setRule(setup, found, ruleInput(found, input));
};

/** The Advanced tab, as clicked in the Studio */
const toAdvanced = (setup: StudioSetup): StudioSetup => edit.setAdvanced(setup, true, edit.NO_MEMORY).setup;

/** An Advanced setup as pasted from a code: the Studio keeps its tier bosses */
const advanced = (tierBosses: string, tiers: number): ((setup: StudioSetup) => StudioSetup) => (setup) => ({
  ...setup,
  options: { ...setup.options, TierBosses: tierBosses, Tiers: tiers },
});

const SETUPS: [string, StudioSetup][] = [
  ["new setup", make()],
  ["boss and Elite", make((s) => edit.setBoss(s, "Lych"), (s) => edit.setElite(s, true))],
  ["every boss", make((s) => edit.setBoss(s, "Diamondback"))],
  ["boss speed and health", make((s) => edit.setBossSpeed(s, 250), (s) => edit.setBossHealth(s, 1500))],
  ["lowest boss speed and health", make((s) => edit.setBossSpeed(s, 10), (s) => edit.setBossHealth(s, 10))],
  ["no paragons", make((s) => edit.setParagonSlider(s, 0))],
  ["three paragons", make((s) => edit.setParagonSlider(s, 3))],
  ["one tier", make((s) => edit.setTiers(s, 1))],
  ["ten tiers, faster and tougher", make((s) => edit.setTiers(s, 10), (s) => edit.setSpeedPerTier(s, 50), (s) => edit.setHealthPerTier(s, 200))],
  ["weaker per tier", make((s) => edit.setTiers(s, 7), (s) => edit.setSpeedPerTier(s, -10), (s) => edit.setHealthPerTier(s, -10))],
  ["spread spawn rounds", make((s) => edit.spreadSpawnRounds(s, 25, 12))],
  ["latest spawn rounds", make((s) => edit.setTiers(s, 10), (s) => edit.spreadSpawnRounds(s, 200, 50))],
  ["typed spawn rounds", make((s) => edit.setSpawnRounds(s, "30, 31; 90 400"))],
  ["broken spawn rounds", make((s) => edit.setSpawnRounds(s, "x, 0, -5, 60"))],
  ["boss event rounds", make((s) => edit.setBoss(s, "Vortex"), (s) => edit.setBossEventRounds(s, true))],
  ["evenly spaced skulls", make((s) => edit.setSkullMode(s, "EvenlySpaced"), (s) => edit.setSkullCount(s, 7))],
  ["no skulls", make((s) => edit.setSkullMode(s, "EvenlySpaced"), (s) => edit.setSkullCount(s, 0))],
  ["custom skulls", make((s) => edit.setSkullMode(s, "Custom"), (s) => edit.setCustomSkulls(s, "90, 66.666, junk, 12.5"))],
  ["advanced kept", make(advanced("Bloonarius,LychElite@150/80#2,Random,Vortex@100/300#50%;20%", 4))],
  ["advanced with event rounds", make(advanced("Lych,Phayze", 2), (s) => edit.setBossEventRounds(s, true), (s) => edit.setTiers(s, 3))],
  ["advanced with boss speed", make(advanced("Dreadbloon,Blastapopoulos", 2), (s) => edit.setBossSpeed(s, 300))],
  // Advanced Mode made with the Studio's own tier controls (#95)
  ["advanced from standard", make((s) => edit.setBoss(s, "Lych"), (s) => edit.setBossSpeed(s, 150), toAdvanced)],
  [
    "advanced, every kind of tier",
    make(
      (s) => edit.setTiers(s, 6), toAdvanced, (s) => edit.pickTierBoss(s, 1, "Vortex"), (s) => edit.setTierElite(s, 1, true),
      (s) => edit.pickTierBoss(s, 2, "Random"), (s) => edit.setTierSpeed(s, 2, 320), (s) => edit.setTierHealth(s, 2, 1990),
      (s) => edit.pickTierBoss(s, 3, "Random"), (s) => edit.setTierElite(s, 3, true),
      (s) => edit.setTierSkullMode(s, 4, "EvenlySpaced"), (s) => edit.setTierSkullCount(s, 4, 7),
      (s) => edit.setTierSkullMode(s, 5, "Custom"), (s) => edit.setTierCustomSkulls(s, 5, "95, 33.3, 5"),
      (s) => edit.setTierBoss(s, 6, FOLLOWS), (s) => edit.stepTierBoss(s, 6, -1),
    ),
  ],
  [
    "advanced with event rounds and spread rounds",
    make(toAdvanced, (s) => edit.stepTierBoss(s, 1, 1), (s) => edit.setBossEventRounds(s, true), (s) => edit.spreadSpawnRounds(s, 20, 10)),
  ],
  // The Challenge Editor's rules, made with the Studio's own rule controls (#97)
  ["rules: game", make(rule("name", "  Hard Times "), rule("map", "Infernal"), rule("difficulty", "Hard"), rule("mode", "HalfCash"))],
  [
    "rules: start",
    make(rule("startRules.cash", "999999999999"), rule("startRules.lives", "0"), rule("startRules.maxLives", "250"), rule("startRules.round", "12")),
  ],
  [
    "rules: bloons",
    make(
      rule("bloonModifiers.speedMultiplier", "150"), rule("bloonModifiers.moabSpeedMultiplier", "37.5"),
      rule("bloonModifiers.regrowRateMultiplier", "0"), rule("bloonModifiers.healthMultipliers.bloons", "300"),
      rule("bloonModifiers.healthMultipliers.moabs", "100000"), rule("bloonModifiers.allCamo", true),
      rule("bloonModifiers.allRegen", true), rule("bloonModifiers.allFortified", true),
    ),
  ],
  [
    "rules: on and off",
    make(
      rule("maxTowers", "8"), rule("disableMK", true), rule("disableSelling", true), rule("disablePowers", true),
      rule("noContinues", true), rule("disableDoubleCash", true), rule("disableFastTrack", true), rule("noInstaReward", false),
    ),
  ],
  // Tower restrictions, made with the Studio's own tower controls (#128)
  [
    "towers: limits, paths and a paragon",
    make(
      (s) => towers.setTowerLimit(s, "DartMonkey", "limited", "3"), (s) => towers.setTowerLimit(s, "SuperMonkey", "notAllowed"),
      (s) => towers.setTowerLimit(s, "BananaFarm", "limited", "5000"), (s) => towers.setBlockedTiers(s, "NinjaMonkey", 1, 1),
      (s) => towers.setBlockedTiers(s, "NinjaMonkey", 2, 5), (s) => towers.setBlockedTiers(s, "NinjaMonkey", 3, 3),
      (s) => towers.setParagonLock(s, "DartMonkey", true),
    ),
  ],
  ["towers: military only, no heroes", make(...["Primary", "Magic", "Support", "Hero"].map((set) => (s: StudioSetup) => towers.setClassAllowed(s, set, false)))],
  ["towers: heroes allowed", make((s) => towers.setHeroAllowed(s, "Quincy", true), (s) => towers.setHeroAllowed(s, "ChosenPrimaryHero", true), (s) => towers.setHeroAllowed(s, "Ezili", false))],
  [
    "everything",
    make(
      (s) => edit.setBoss(s, "Phayze"), (s) => edit.setElite(s, true), (s) => edit.setBossSpeed(s, 120),
      (s) => edit.setBossHealth(s, 80), (s) => edit.setParagonSlider(s, 5), (s) => edit.setTiers(s, 8),
      (s) => edit.setSpeedPerTier(s, 5), (s) => edit.setHealthPerTier(s, 25), (s) => edit.spreadSpawnRounds(s, 35, 18),
      (s) => edit.setBossEventRounds(s, true), (s) => edit.setSkullMode(s, "Custom"), (s) => edit.setCustomSkulls(s, "80, 40"),
    ),
  ],
];

interface WebsiteCode {
  name: string;
  code: string;
  /** The options the mod reads from the code: all of them, as SetupCode.OptionsJson writes them */
  options: JsonObject;
  /** The challenge changes the mod reads from the code; CodeFormat.FilterChallenge must keep all of them */
  challenge: JsonObject;
}

async function contents(code: string): Promise<JsonObject> {
  const base64 = code.slice(5).replace(/-/g, "+").replace(/_/g, "/");
  const bytes = Uint8Array.from(atob(base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=")), (c) => c.charCodeAt(0));
  const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream("deflate-raw"));
  return JSON.parse(await new Response(stream).text()) as JsonObject;
}

async function made(): Promise<WebsiteCode[]> {
  return Promise.all(
    SETUPS.map(async ([name, setup]) => {
      const options = normalized(setup.options);
      return {
        name,
        code: await encode(setup.options, setup.challenge),
        options: optionsJson(options),
        challenge: payload(options, setup.challenge).c as JsonObject,
      };
    }),
  );
}

describe("codes the website makes for the mod", () => {
  it("are in web/test/website-codes.json", async () => {
    const codes = await made();
    if (process.env.CBC_WEBSITE_CODES === "1") {
      const file = {
        comment:
          "Codes the website's Studio made (#12), with the options and challenge changes the mod must read from them. Made by web/test/website-codes.test.ts (CBC_WEBSITE_CODES=1 npm test), read by UnitTests/CodeFormatTests.cs.",
        codes,
      };
      writeFileSync(FILE, JSON.stringify(file, null, 2) + "\n");
    }

    if (!existsSync(FILE)) throw new Error("web/test/website-codes.json is missing: run CBC_WEBSITE_CODES=1 npm test");
    const committed = (JSON.parse(readFileSync(FILE, "utf8")) as { codes: WebsiteCode[] }).codes;
    expect(committed.map((entry) => entry.name)).toEqual(codes.map((entry) => entry.name));
    for (const [i, entry] of committed.entries()) {
      expect(entry.code.slice(0, 5), entry.name).toBe(codes[i].code.slice(0, 5));
      expect(await contents(entry.code), entry.name).toEqual(await contents(codes[i].code));
      expect(entry.options, entry.name).toEqual(codes[i].options);
      expect(entry.challenge, entry.name).toEqual(codes[i].challenge);
    }
  });

  it("cover every prefix", async () => {
    expect(new Set((await made()).map((entry) => entry.code.slice(0, 5)))).toEqual(new Set(["CBC1:", "CBC2:", "CBC3:", "CBC4:"]));
  });

  it.each(SETUPS)("%s is read back on the website as made", async (_, setup) => {
    const read = await decode(await encode(setup.options, setup.challenge));
    if (!read.ok) throw new Error(read.reason);
    expect(optionsJson(read.options)).toEqual(optionsJson(normalized(setup.options)));
    // Nothing the code holds is dropped when it is read
    const c = payload(normalized(setup.options), setup.challenge).c as JsonObject;
    const dropped: string[] = [];
    expect(filterChallenge(c, GAME.defaultChallenge, dropped)).toEqual(c);
    expect(dropped).toEqual([]);
  });
});
