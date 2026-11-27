// What decode says about a code it can't read, how it finds a code in pasted text, %-escapes in links, the challenge
// fields a code may not set, and names: details the mutation tests found unchecked (#296). Same rules as
// Persistence/CodeFormat.cs in the mod.

import { describe, expect, it } from "vitest";
import { cleanName, decode, defaultOptions, extractCode, filterChallenge, readSetup, unescapeDataString } from "@cbc/share-code";
import { GAME } from "../src/data/types";

const NOT_A_CODE = "This isn't a Boss Creator code. Codes start with CBC.";

describe("the reason a code can't be read", () => {
  it.each([
    ["", "Invalid", "The code is empty."],
    ["   ", "Invalid", "The code is empty."],
    ["hello", "Invalid", NOT_A_CODE],
    ["CBC", "Invalid", NOT_A_CODE],
    // A version number needs something after it before it looks like a code
    ["CBC9", "Invalid", NOT_A_CODE],
    ["CBCx:abc", "Invalid", NOT_A_CODE],
    ["CBC9:abc", "TooNew", "This code comes from a newer version of Custom Boss Challenge."],
    ["CBC12:abc", "TooNew", "This code comes from a newer version of Custom Boss Challenge."],
    ["CBC1:!!!", "Invalid", "The code is damaged or cut short."],
    ["CBC1:abc", "Invalid", "The code is damaged or cut short."],
  ])("%j is %s: %s", async (code, problem, reason) => {
    expect(await decode(code)).toEqual({ ok: false, problem, reason });
  });
});

describe("%-escapes in a link, like Uri.UnescapeDataString", () => {
  it.each([
    ["%C3%A6", "æ"],
    ["%E2%82%AC", "€"],
    ["%F0%9F%98%80", "😀"],
    // The first and last character of each length
    ["%C2%80", "\u0080"],
    ["%DF%BF", "߿"],
    ["%E0%A0%80", "ࠀ"],
    ["%EF%BF%BF", "￿"],
    ["%F4%8F%BF%BF", "\u{10ffff}"],
    // Not characters: an overlong form, past U+10FFFF, cut short, a lone continuation byte
    ["%C0%80", "%C0%80"],
    ["%C1%BF", "%C1%BF"],
    ["%F5%80%80%80", "%F5%80%80%80"],
    ["%C3", "%C3"],
    ["%E2%82", "%E2%82"],
    ["%80", "%80"],
    ["%E2%82%ACx%C3", "€x%C3"],
  ])("%s is %j", (text, unescaped) => expect(unescapeDataString(text)).toBe(unescaped));
});

describe("a code in pasted text", () => {
  it.each([
    ["CBC10:abc", "CBC10:abc"],
    ["say CBC10:abc.", "CBC10:abc"],
    ["<<CBC1:abc>>", "CBC1:abc"],
    ["CBC1:abc。", "CBC1:abc"],
    // An emoji (a surrogate pair, not a letter) or a lone half of one before the code is fine
    ["😀CBC1:abc", "CBC1:abc"],
    ["a\udc00CBC1:abc", "CBC1:abc"],
    ["\ud835CBC1:abc", "CBC1:abc"],
  ])("%j has %j", (text, code) => expect(extractCode(text)).toBe(code));

  it.each([
    "xCBC1:abc",
    "æCBC1:abc",
    // A letter outside the BMP, 𝐀, is a surrogate pair
    "𝐀CBC1:abc",
    // A broken %-escape or a letter right after it
    "CBC1:abc%20",
    "CBC1:abcé",
  ])("%j has no code on its own, so it stays as it is", (text) => expect(extractCode(text)).toBe(text));
});

describe("what a code's challenge may set (CodeFormat.FilterChallenge)", () => {
  const filter = (diff: object) => {
    const dropped: string[] = [];
    return { kept: filterChallenge(diff as never, GAME.defaultChallenge, dropped), dropped };
  };

  it.each([
    [{ nope: 1 }, {}, ["nope"]],
    [{ $x: 1 }, {}, ["$x"]],
    [{ name: 1 }, {}, ["name"]],
    [{ maxTowers: 1.5 }, {}, ["maxTowers"]],
    [{ startRules: { cash: "x" } }, {}, ["startRules.cash"]],
    [{ startRules: { cash: null } }, {}, ["startRules.cash"]],
    [{ startRules: { nope: 1, cash: 5 } }, { startRules: { cash: 5 } }, ["startRules.nope"]],
    [{ bloonModifiers: { healthMultipliers: { nope: 1 } } }, {}, ["bloonModifiers.healthMultipliers.nope"]],
    // An empty object changes nothing, so nothing was dropped
    [{ startRules: {} }, {}, []],
    [{ towers: 7 }, {}, ["towers"]],
  ])("%j keeps %j and drops %j", (diff, kept, dropped) => expect(filter(diff)).toEqual({ kept, dropped }));

  it("keeps a tower list's entries by index or by name, and names the rest", () => {
    expect(filter({ towers: { $items: { 0: { max: 2 }, 999: { max: 1 }, x: { max: 1 }, "01": { max: 1 } } } })).toEqual({
      kept: { towers: { $items: { 0: { max: 2 } } } },
      dropped: ["towers[999]", "towers[x]", "towers[01]"],
    });
    expect(filter({ towers: [{ tower: "Quincy", max: 3 }, { tower: "Nope", max: 1 }, 5, { max: 1 }] })).toEqual({
      kept: { towers: { $items: { 1: { max: 3 } } } },
      dropped: ["towers[1]", "towers[2]", "towers[3]"],
    });
  });
});

describe("names and setups", () => {
  it("cuts a name at 60 characters, then trims the end again", () => {
    expect(cleanName("a".repeat(59) + " b" + "c".repeat(9), "F")).toBe("a".repeat(59));
    expect(cleanName("\u0001 \u0085", "F")).toBe("F");
  });

  it("keeps the Random tiers' picks when reading a setup without a code (#270)", () => {
    const options = { ...defaultOptions(), Tiers: 2, TierBosses: "Lych,Vortex", RandomTiers: "2" };
    expect(readSetup(options, {}).options.RandomTiers).toBe("2");
  });
});
