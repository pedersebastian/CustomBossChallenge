// Details of the share-code package that the mutation tests found unchecked (#296): broken option values in a code,
// the options' ranges, comparing JSON, and the .NET string helpers.

import { describe, expect, it } from "vitest";
import { decode, deepEqual, defaultOptions, hideRandomPicks, optionsJson, pack, readOptions, sanitize } from "@cbc/share-code";
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
