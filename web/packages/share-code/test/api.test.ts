// The package's public API (#98): read, change and make codes of every version, the README's examples as written, and
// nothing from the website (no DOM, no page address)

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import * as sdk from "../src/index";
import { PREFIXES, decode, defaultChallenge, defaultOptions, encode, extractCode, tierBosses, tiers } from "../src/index";

const README = readFileSync(fileURLToPath(new URL("../README.md", import.meta.url)), "utf8");
const SOURCES = ["index", "decode", "encode", "options", "data", "math/tiers", "math/skulls", "math/tierBosses", "math/limits", "math/dotnet"].map(
  (name) => readFileSync(fileURLToPath(new URL(`../src/${name}.ts`, import.meta.url)), "utf8"),
);

describe("the public API", () => {
  it("reads, changes and makes a code of every version", async () => {
    const setups = [
      { ...defaultOptions(), Boss: "Lych" },
      { ...defaultOptions(), HealthPerTier: 20 },
      { ...defaultOptions(), Tiers: 3, TierBosses: tierBosses.formatTierBosses(tierBosses.parseTierBosses("Vortex,RandomElite,Lych@150/80#4")) },
      { ...defaultOptions(), BossEventRounds: true },
    ];
    const made = await Promise.all(setups.map((options) => encode(options)));
    expect(made.map((code) => code.slice(0, 5))).toEqual([...PREFIXES]);
    for (const [i, code] of made.entries()) {
      const read = await decode(code);
      if (!read.ok) throw new Error(read.reason);
      expect(read.options).toMatchObject(setups[i]);
    }
  });

  it("keeps a challenge's rules when a code is made again from its JSON", async () => {
    const json = defaultChallenge();
    json.map = "Infernal";
    (json.startRules as Record<string, number>).cash = 20000;
    const read = await decode(await encode(defaultOptions(), json));
    if (!read.ok) throw new Error(read.reason);
    expect(read.challenge).toMatchObject({ map: "Infernal", cash: 20000 });
  });

  it("reads a code in a link, and says why it can't read one", async () => {
    const code = await encode({ ...defaultOptions(), Tiers: 2 });
    expect(extractCode(`https://pedersebastian.github.io/CustomBossChallenge/#code=${code}`)).toBe(code);
    expect(await decode("CBC9:abc")).toMatchObject({ ok: false, problem: "TooNew" });
    expect(await decode("hello")).toMatchObject({ ok: false, problem: "Invalid" });
  });

  it("has the helpers as namespaces too", () => {
    expect(tiers.endRound(tiers.spawnRounds(5, null))).toBe(140);
    expect(sdk.skulls.evenlySpaced(4)).toHaveLength(4);
    expect(sdk.limits.orDefault(-5, 0, 10)).toBe(-1);
  });
});

describe("the README", () => {
  // Each ```ts example runs as written: its imports from the package become the package itself
  const examples = [...README.matchAll(/```ts\n([\s\S]*?)```/g)].map((match) => match[1]);

  it("has examples", () => expect(examples.length).toBeGreaterThanOrEqual(4));

  it.each(examples.map((example, i) => [i, example] as const))("example %i runs", async (_, example) => {
    const body = example.replace(/import \{([^}]*)\} from "@cbc\/share-code";/g, "const {$1} = sdk;");
    const logs: unknown[][] = [];
    const run = new Function("sdk", "console", `return (async () => {${body}})();`) as (sdk: unknown, console: unknown) => Promise<void>;
    await run(sdk, { log: (...args: unknown[]) => logs.push(args) });
    expect(logs.flat()).not.toContain(undefined);
  });
});

it("uses nothing from the website: no page, no DOM", () => {
  for (const source of SOURCES) {
    expect(source).not.toMatch(/\b(document|window|location)\./);
    expect(source).not.toMatch(/from "\.\.\/\.\.\/(src|test)/);
  }
});
