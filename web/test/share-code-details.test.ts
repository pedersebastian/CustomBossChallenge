// Details of the share-code package that the mutation tests found unchecked (#296): broken option values in a code,
// the options' ranges, comparing JSON, and the .NET string helpers.

import { afterEach, describe, expect, it, vi } from "vitest";
import {
  UnsupportedBrowserError,
  canUseCodes,
  decode,
  deepEqual,
  defaultName,
  defaultOptions,
  diff,
  hideRandomPicks,
  optionsJson,
  pack,
  readOptions,
  sanitize,
  withoutRandomMark,
  type Json,
} from "@cbc/share-code";
import { normalizeRounds } from "@cbc/share-code/tiers";
import { FOLLOWS, parseTierList, withTier } from "@cbc/share-code/tier-bosses";
import { equalsIgnoreCase, netTrimEnd, toFixedNet } from "@cbc/share-code/dotnet";

/** decode of a code holding these options */
const read = async (o: object) => decode(await pack(JSON.stringify({ o })));

describe("a code's option values", () => {
  it.each([
    [{ Boss: "Nope" }, "Boss 'Nope' is unknown"],
    [{ Elite: null }, "Elite is null"],
    [{ Elite: 3 }, "Elite has the wrong type"],
    [{ Tiers: "5.0" }, "Tiers has the wrong type"],
  ])("%j is a broken value: %s", async (o, why) => {
    expect(await read(o)).toEqual({ ok: false, problem: "Invalid", reason: `The code has a broken value: ${why}.` });
  });

  it.each([
    [{ Boss: 1 }, "Lych"],
    [{ Boss: 6 }, "Diamondback"],
    // Past the last boss or below the first: the default boss
    [{ Boss: 7 }, "Bloonarius"],
    [{ Boss: -1 }, "Bloonarius"],
  ])("%j is %s", async (o, boss) => {
    const result = await read(o);
    expect(result.ok && result.options.Boss).toBe(boss);
  });

  it("reads a whole number written as text, spaces around it too", async () => {
    const result = await read({ Tiers: " 7 " });
    expect(result.ok && result.options.Tiers).toBe(7);
  });
});

describe("the options' ranges (BossOptions.Sanitized)", () => {
  const raw = () => readOptions(optionsJson(defaultOptions()));

  it("turns the old Extra Tier Health into Health per Tier only when that is 0", () => {
    expect(sanitize({ ...raw(), ExtraTierHealth: 225, HealthPerTier: 0 }).HealthPerTier).toBe(10);
    expect(sanitize({ ...raw(), ExtraTierHealth: 225, HealthPerTier: 20 }).HealthPerTier).toBe(20);
  });

  it("drops the Random tiers' picks unless the code's version holds them (#270)", () => {
    const options = { ...raw(), Tiers: 2, TierBosses: "Lych,Vortex", RandomTiers: "2" };
    expect(sanitize(options)).not.toHaveProperty("RandomTiers");
    expect(sanitize(options, true).RandomTiers).toBe("2");
  });

  it("leaves options without Random picks as they are", () => {
    const options = defaultOptions();
    expect(hideRandomPicks(options)).toBe(options);
  });
});

describe("JToken.DeepEquals", () => {
  it.each([
    [[1, 2], [1, 2], true],
    [[1], [1, 2], false],
    [[1, 2], [1, 3], false],
    [[1], { 0: 1 }, false],
    [{ a: [1] }, { a: [1] }, true],
    [{ a: 1, b: 2 }, { b: 2, a: 1 }, true],
    [{ a: 1 }, { b: 1 }, false],
    [{ a: 1 }, { a: 2 }, false],
  ])("%j and %j: %s", (a, b, equal) => {
    expect(deepEqual(a, b)).toBe(equal);
    expect(deepEqual(b, a)).toBe(equal);
  });
});

describe(".NET string helpers", () => {
  it.each([
    ["a \t  ", "a"],
    ["  ", ""],
    [" a ", " a"],
    // A zero-width space isn't white space to .NET
    ["a​", "a​"],
  ])("TrimEnd(%j) is %j", (text, trimmed) => expect(netTrimEnd(text)).toBe(trimmed));

  it("compares names ignoring ASCII case", () => {
    expect(equalsIgnoreCase("LYCH", "lych")).toBe(true);
    expect(equalsIgnoreCase("Lych", "Vortex")).toBe(false);
  });

  it("drops the minus sign of a negative number that rounds to 0", () => {
    expect(toFixedNet(-0.04, 1)).toBe("0.0");
    expect(toFixedNet(-1.25, 1)).toBe("-1.3");
  });
});

// More details the mutation tests found unchecked (#302)
describe("why a code's option value is broken", () => {
  it.each([
    [{ Tiers: 2147483648 }, "Tiers has the wrong type"],
    [{ Tiers: -2147483649 }, "Tiers has the wrong type"],
    [{ Tiers: "abc" }, "Tiers has the wrong type"],
    [{ Boss: true }, "Boss has the wrong type"],
    [{ Boss: 1.5 }, "Boss has the wrong type"],
  ])("%j: %s", async (o, why) => {
    expect(await read(o)).toEqual({ ok: false, problem: "Invalid", reason: `The code has a broken value: ${why}.` });
  });
});

describe("comparing and diffing JSON, more closely", () => {
  it.each([
    [{ length: 0 }, [], false],
    [{}, 5, false],
    [{}, null, false],
  ])("%j and %j: %s", (a, b, equal) => {
    expect(deepEqual(a as Json, b as Json)).toBe(equal);
    expect(deepEqual(b as Json, a as Json)).toBe(equal);
  });

  it.each([
    [{}, 5, {}],
    [{ a: 1 }, 5, { a: 1 }],
    [null, { a: 1 }, null],
    [[1], { a: 1 }, [1]],
  ])("%j against %j differs as %j", (value, defaults, expected) => {
    expect(diff(value as Json, defaults as Json)).toEqual(expected);
  });
});

describe("browsers without one of the streams", () => {
  afterEach(() => vi.unstubAllGlobals());

  it.each(["CompressionStream", "DecompressionStream"])("can't use codes without %s", (name) => {
    vi.stubGlobal(name, undefined);
    expect(canUseCodes()).toBe(false);
  });

  it("names its error", () => expect(new UnsupportedBrowserError().name).toBe("UnsupportedBrowserError"));
});

describe("tier lists and rounds, more closely", () => {
  it.each([
    ["10,2", [10, 2]],
    ["0, -1, 3, 11", [3]],
  ])("the tier list %j is %j", (list, tiers) => expect(parseTierList(list)).toEqual(tiers));

  it("changes no tier past the tenth, of no list either", () => {
    expect(withTier(null, 0, { ...FOLLOWS, boss: "Lych" })).toBe("");
    expect(withTier(null, 11, { ...FOLLOWS, boss: "Lych" })).toBe("");
  });

  it("keeps the first ten spawn rounds", () => {
    expect(normalizeRounds("41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52")).toBe("41, 42, 43, 44, 45, 46, 47, 48, 49, 50");
  });
});

describe("the options' names and Random marks", () => {
  it("names a setup after its boss, Elite or not, or a gauntlet of bosses", () => {
    expect(defaultName({ ...defaultOptions(), Boss: "Lych" })).toBe("Custom Lych");
    expect(defaultName({ ...defaultOptions(), Boss: "Lych", Elite: true })).toBe("Custom Elite Lych");
    expect(defaultName({ ...defaultOptions(), Tiers: 2, TierBosses: "Lych,Vortex" })).toBe("Custom Boss Gauntlet");
  });

  it("leaves options without that Random mark as they are", () => {
    const options = defaultOptions();
    expect(withoutRandomMark(options, 1)).toBe(options);
  });
});
