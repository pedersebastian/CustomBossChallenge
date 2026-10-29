// Random codes from the property tests (PropertyTests/GeneratedFixtures.fs): the website must read every one exactly
// like the mod's CodeFormat does, and refuse the broken ones the same way.

import { describe, expect, it } from "vitest";
import file from "./generated-codes.json";
import { decode } from "../src/code/decode";
import { optionsJson } from "../src/code/options";
import { GAME, type Json, type JsonObject } from "../src/data/types";

/** The value at a path like "startRules.cash" */
const at = (json: JsonObject, path: string): Json | undefined =>
  path.split(".").reduce<Json | undefined>((value, key) => (value as JsonObject | undefined)?.[key], json);

interface GeneratedCodes {
  /** The challenge values by path, and towerMax: the towers whose limit differs from the default, by index */
  valid: { code: string; options: JsonObject; challenge: JsonObject & { towerMax: Record<string, number> } }[];
  invalid: { code: string; problem: string }[];
}

const generated = file as unknown as GeneratedCodes;

const towerMax = (json: JsonObject): Json[] => (json.towers as JsonObject[]).map((tower) => tower.max);

describe("generated codes", () => {
  const defaults = towerMax(GAME.defaultChallenge);

  it.each(generated.valid.map((entry, i) => [i, entry] as const))("reads code %i like the mod", async (_, entry) => {
    const result = await decode(entry.code);
    if (!result.ok) throw new Error(`Didn't load: ${result.reason}`);

    // The setups are ones the Boss Creator can make, so reading them changes nothing
    expect(optionsJson(result.options)).toEqual(entry.options);

    const { towerMax: changed, ...values } = entry.challenge;
    for (const [path, value] of Object.entries(values)) expect(at(result.json, path), path).toEqual(value);
    expect(towerMax(result.json)).toEqual(defaults.map((max, i) => changed[i] ?? max));
  });

  it.each(generated.invalid.map((entry, i) => [i, entry] as const))("refuses broken code %i like the mod", async (_, entry) => {
    const result = await decode(entry.code);
    expect(result.ok).toBe(false);
    expect(result.problem).toBe(entry.problem);
  });
});
