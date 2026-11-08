// The website makes codes the mod would make (#12): for every code the mod made, reading it and making it again on the
// website gives the same prefix and the same JSON inside, key for key. Only the deflated bytes may differ.

import { describe, expect, it } from "vitest";
import fixtures from "../src/data/fixtures.json";
import generatedFile from "./generated-codes.json";
import { decode, filterChallenge } from "@cbc/share-code";
import { deepEqual, diff, encode, prefixFor } from "@cbc/share-code";
import { defaultOptions } from "@cbc/share-code";
import { EXAMPLES, GAME, type Json, type JsonObject } from "../src/data/types";

/** The JSON inside a code, as the mod reads it */
async function contents(code: string): Promise<JsonObject> {
  const base64 = code.slice(5).replace(/-/g, "+").replace(/_/g, "/");
  const bytes = Uint8Array.from(atob(base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=")), (c) => c.charCodeAt(0));
  const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream("deflate-raw"));
  return JSON.parse(await new Response(stream).text()) as JsonObject;
}

/** The mod's payload, without the challenge fields the mod ignores when it reads the code */
async function modPayload(code: string): Promise<JsonObject> {
  const json = await contents(code);
  const c = (json.c ?? {}) as JsonObject;
  return { o: json.o ?? {}, c: filterChallenge(c, GAME.defaultChallenge) };
}

const generated = (generatedFile as unknown as { valid: { code: string }[] }).valid.map((entry) => entry.code);
/** Made by SetupCode.Encode in the game (WebDataTest), so the keys are in the mod's own order */
const examples = EXAMPLES.map((example) => example.code);
/** Codes the Boss Creator can't make, out of range or broken on purpose: only read, never made */
const handMade = fixtures.valid.map((fixture) => fixture.code).filter((code) => !examples.includes(code));

/** Reads a code and makes it again on the website */
async function remake(code: string) {
  const read = await decode(code);
  if (!read.ok) throw new Error(`Didn't load: ${read.reason}`);
  const made = await encode(read.options, read.json);
  const again = await decode(made);
  if (!again.ok) throw new Error(`Own code didn't load: ${again.reason}`);
  return { read, made, again };
}

describe("codes the website makes", () => {
  it.each(examples.map((code, i) => [i, code] as const))("makes example %i exactly like the mod", async (_, code) => {
    const { made } = await remake(code);
    expect(made.slice(0, 5)).toBe(code.slice(0, 5));
    // Same keys in the same order, compared as parsed JSON so 1.0 from the mod equals 1 from the website
    expect(JSON.stringify(await contents(made))).toBe(JSON.stringify(await modPayload(code)));
  });

  // The property tests build their options JSON key by key, so only the values are compared, not the order
  it.each(generated.map((code, i) => [i, code] as const))("makes generated code %i with the mod's values", async (_, code) => {
    const { made } = await remake(code);
    expect(made.slice(0, 5)).toBe(code.slice(0, 5));
    expect(deepEqual(await contents(made), await modPayload(code))).toBe(true);
  });

  it.each(handMade.map((code, i) => [i, code] as const))("reads its own code of fixture %i as the same setup", async (_, code) => {
    const { read, again } = await remake(code);
    expect(again.options).toEqual(read.options);
    expect(again.json).toEqual(read.json);
    expect(again.challenge).toEqual(read.challenge);
  });

  it("covers every prefix", () => {
    expect(new Set([...examples, ...generated].map((code) => code.slice(0, 5)))).toEqual(new Set(["CBC1:", "CBC2:", "CBC3:", "CBC4:"]));
  });

  it("makes an empty payload for a new setup", async () => {
    expect(await contents(await encode(defaultOptions()))).toEqual({ o: {}, c: {} });
  });

  it("brings options into range before writing them", async () => {
    const options = { ...defaultOptions(), Tiers: 9999, SkullCount: -4 };
    const read = await decode(await encode(options));
    if (!read.ok) throw new Error(read.reason);
    expect(await contents(await encode(read.options))).toEqual(await contents(await encode(options)));
    expect(read.options.Tiers).toBeLessThan(9999);
  });
});

describe("prefixFor", () => {
  const base = defaultOptions();
  it.each([
    [{}, "CBC1:"],
    [{ SpeedPerTier: 10 }, "CBC2:"],
    [{ HealthPerTier: 10 }, "CBC2:"],
    [{ TierBosses: "Lych" }, "CBC3:"],
    [{ TierBosses: "Lych", BossEventRounds: true }, "CBC4:"],
    [{ BossEventRounds: true }, "CBC4:"],
  ])("%j gives %s", (change, prefix) => {
    expect(prefixFor({ ...base, ...change })).toBe(prefix);
  });
});

describe("diff", () => {
  it("keeps only what changed, lists by index", () => {
    const defaults: Json = { a: 1, b: { c: 2, d: 3 }, list: [{ x: 1 }, { x: 2 }] };
    const value: Json = { a: 1, b: { c: 2, d: 4 }, list: [{ x: 1 }, { x: 5 }] };
    expect(diff(value, defaults)).toEqual({ b: { d: 4 }, list: { $items: { "1": { x: 5 } } } });
  });

  it("writes a whole list whose length changed, and nothing for no change", () => {
    expect(diff({ list: [1, 2, 3] }, { list: [1, 2] })).toEqual({ list: [1, 2, 3] });
    expect(diff({ a: [1], b: null }, { a: [1], b: null })).toBeUndefined();
  });

  it("compares objects in any order, like JToken.DeepEquals", () => {
    expect(deepEqual({ a: 1, b: 2 }, { b: 2, a: 1 })).toBe(true);
    expect(deepEqual({ a: 1 }, { a: 1, b: null })).toBe(false);
    expect(deepEqual([1, 2], [2, 1])).toBe(false);
  });
});
