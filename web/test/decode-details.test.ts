// What decode says about a code it can't read, how it finds a code in pasted text, %-escapes in links, the challenge
// fields a code may not set, and names: details the mutation tests found unchecked (#296). Same rules as
// Persistence/CodeFormat.cs in the mod.

import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { apply, cleanName, decode, defaultOptions, extractCode, filterChallenge, pack, playProblem, readSetup, unescapeDataString } from "@cbc/share-code";
import type { Json, JsonObject } from "@cbc/share-code";
import { GAME } from "../src/data/types";

interface FilterCase {
  name: string;
  c: JsonObject;
  kept: JsonObject;
  dropped: string[];
  defaults?: JsonObject;
}

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

// The cases are shared with the mod (UnitTests/ChallengeFilterTests.cs, #302). Read with JSON.parse like a code, not
// imported: an import would make "__proto__" the prototype rather than a key.
const filterCases = JSON.parse(readFileSync(new URL("./challenge-filter-cases.json", import.meta.url), "utf8")) as { cases: FilterCase[] };

describe("what a code's challenge may set, like the mod (challenge-filter-cases.json)", () => {
  for (const entry of filterCases.cases) {
    it(entry.name, () => {
      const dropped: string[] = [];
      const kept = filterChallenge(entry.c, (entry.defaults ?? GAME.defaultChallenge) as JsonObject, dropped);
      expect({ kept, dropped }).toEqual({ kept: entry.kept, dropped: entry.dropped });
    });
  }

  it("keeps a change to the last tower, and drops one past it", () => {
    const count = (GAME.defaultChallenge.towers as unknown[]).length;
    const dropped: string[] = [];
    const kept = filterChallenge({ towers: { $items: { [count - 1]: { max: 2 }, [count]: { max: 2 } } } }, GAME.defaultChallenge, dropped);
    expect({ kept, dropped }).toEqual({ kept: { towers: { $items: { [count - 1]: { max: 2 } } } }, dropped: [`towers[${count}]`] });
  });

  it("can't set the challenge's prototype", () => {
    const kept = filterChallenge(JSON.parse('{"__proto__": {"map": "Logs"}, "map": "Tutorial"}'), GAME.defaultChallenge);
    expect(Object.getPrototypeOf(kept)).toBe(Object.prototype);
    expect(kept).toEqual({ map: "Tutorial" });
  });
});

describe("putting a diff back on the defaults (SetupCode.Apply)", () => {
  it.each([
    ["a list item", [1, 2, 3], { $items: { 1: 9 } }, [1, 9, 3]],
    ["an item in a list in an object", { a: [1, { b: 2 }] }, { a: { $items: { 1: { b: 3 } } } }, { a: [1, { b: 3 }] }],
    ["an index past the end", [1, 2], { $items: { 2: 9 } }, [1, 2]],
    ["a null item", [1, 2], { $items: { 0: null } }, [1, 2]],
    // Only a diff of exactly $items, onto a list, changes items
    ["$items next to another key", [1], { $items: { 0: 5 }, x: 1 }, { $items: { 0: 5 }, x: 1 }],
    ["$items that isn't an object", [1], { $items: 5 }, { $items: 5 }],
    ["$items onto an object", { a: 1 }, { $items: { 0: 2 } }, { a: 1, $items: { 0: 2 } }],
    ["an object onto a number", 5, { a: 1 }, { a: 1 }],
    ["a null property", { a: 1, b: 2 }, { a: null, b: 3 }, { a: 1, b: 3 }],
    ["a property that was null", { a: null }, { a: { b: 1 } }, { a: { b: 1 } }],
    ["a new property", { a: 1 }, { b: [1] }, { a: 1, b: [1] }],
  ])("%s", (_, target, change, expected) => {
    expect(apply(target as Json, change as Json)).toEqual(expected);
  });

  it.each(["x", "1x", "x1", "-1", ""])("refuses the list index %j", (index) => {
    expect(() => apply([1, 2], { $items: { [index]: 5 } })).toThrow("Bad list index");
  });

  it("never sets the prototype", () => {
    const result = apply({}, JSON.parse('{"__proto__": {"polluted": true}}')) as JsonObject;
    expect(Object.getPrototypeOf(result)).toBe(Object.prototype);
    expect(result.polluted).toBeUndefined();
  });

  it("copies what it puts in, so changing the result leaves the diff alone", () => {
    const change = { a: { b: [1] } };
    const result = apply({}, change) as { a: { b: number[] } };
    result.a.b.push(2);
    expect(change).toEqual({ a: { b: [1] } });
  });
});

describe("what decode says about a broken code (#302)", () => {
  it("doesn't call a code from a newer version what only has a digit where the version goes", async () => {
    expect(await decode("abc9:xyz")).toEqual({ ok: false, problem: "Invalid", reason: NOT_A_CODE });
  });

  it.each([
    ["nesting too deep", `{"o":{},"c":{"x":${"[".repeat(65)}${"]".repeat(65)}}}`, "The code is damaged or cut short."],
    ["a list, not an object", "[1]", "The code is damaged."],
    ["a number, not an object", "5", "The code is damaged."],
    ["null", "null", "The code is damaged."],
  ])("%s", async (_, json, reason) => {
    expect(await decode(await pack(json, "CBC5:"))).toEqual({ ok: false, problem: "Invalid", reason });
  });

  it("reads null or nothing as an empty code", async () => {
    expect(extractCode(null)).toBe("");
    expect(extractCode(undefined)).toBe("");
    expect(await decode(null)).toEqual({ ok: false, problem: "Invalid", reason: "The code is empty." });
  });

  it("reads text that unpacks in many parts without breaking a character between them", async () => {
    // Several hundred kB of two-byte characters: the decompressed text comes in parts, split anywhere in the bytes
    const name = "æ€".repeat(100_000);
    const result = await decode(await pack(JSON.stringify({ o: {}, c: { name } }), "CBC5:"));
    expect(result.ok && (result.json.name as string) === name).toBe(true);
  });
});

describe("a code with an emoji or a lone surrogate before it", () => {
  it.each([
    // Half a pair and a letter: the letter stands right before the code
    ["\ud835aCBC1:abc", "\ud835aCBC1:abc"],
    ["\ud835ﬀCBC1:abc", "\ud835ﬀCBC1:abc"],
  ])("%j has %j", (text, code) => expect(extractCode(text)).toBe(code));
});

describe("why Play would refuse a setup", () => {
  const challenge = async () => {
    const result = await decode(await pack('{"o":{},"c":{}}', "CBC5:"));
    if (!result.ok) throw new Error(result.reason);
    return result.challenge;
  };

  it.each(GAME.unsupportedModes)("refuses the %s game mode", async (mode) => {
    expect(playProblem({ ...(await challenge()), mode }, [40])).toBe(
      `The ${GAME.modes[mode] ?? mode} game mode can't be used for boss games.`,
    );
  });

  it("refuses a start on the first boss tier's round, and plays one round before it", async () => {
    expect(playProblem({ ...(await challenge()), round: 40 }, [40, 60])).toBe(
      "The game starts on round 40, but the first boss tier comes on round 40 and would be skipped.",
    );
    expect(playProblem({ ...(await challenge()), round: 39 }, [40, 60])).toBeNull();
  });
});
