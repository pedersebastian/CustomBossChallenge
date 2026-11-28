// A code's options with values of every JSON type are read like the mod reads them (#113), from option-cases.json,
// which UnitTests/CodeFormatTests.cs reads too

import { describe, expect, it } from "vitest";
import { apply } from "@cbc/share-code";
import { chimpsEventRoundSet, defaultOptions, optionsJson, readOptions, sanitize, tierSkullMode, typedOptions } from "@cbc/share-code";
import type { Json, JsonObject } from "../src/data/types";
import cases from "./option-cases.json";

/** The options as the website reads them: the ones that differ from the defaults, or "error" */
function read(diff: JsonObject): Record<string, Json> | "error" {
  let options: JsonObject;
  try {
    const json = optionsJson(defaultOptions());
    apply(json, typedOptions(diff));
    options = optionsJson(sanitize(readOptions(json)));
  } catch {
    return "error";
  }
  const defaults = optionsJson(defaultOptions());
  const changed: Record<string, Json> = {};
  for (const [key, value] of Object.entries(options)) {
    if (!(key in defaults) || JSON.stringify(value) !== JSON.stringify(defaults[key])) changed[key] = value;
  }
  for (const key of Object.keys(defaults)) if (!(key in options)) changed[key] = null;
  return changed;
}

describe("options of every JSON type, like the mod", () => {
  it.each(cases.cases.map((c) => [JSON.stringify(c.o), c.o, c.options]))("%s", (_, o, expected) =>
    expect(read(o as unknown as JsonObject)).toEqual(expected),
  );
});

describe("a tier's skulls and the CHIMPS rounds", () => {
  it("uses a tier's own skulls in Advanced Mode, else the setup's", () => {
    const options = { ...defaultOptions(), Tiers: 2, SkullMode: "Custom" as const, TierBosses: "Lych#3,Vortex" };
    expect(tierSkullMode(options, 1)).toBe("EvenlySpaced");
    expect(tierSkullMode(options, 2)).toBe("Custom");
    expect(tierSkullMode({ ...defaultOptions(), SkullMode: "EvenlySpaced" }, 1)).toBe("EvenlySpaced");
  });

  it("plays the CHIMPS events' round set that ChimpsRounds picks (BossOptions.EventRoundSet)", () => {
    expect(chimpsEventRoundSet({ ...defaultOptions(), ChimpsRounds: "Earlier" }, "Clicks")).toBe("chimpsbloonarius");
    expect(chimpsEventRoundSet(defaultOptions(), "Clicks")).toBe("chimpsboss");
    expect(chimpsEventRoundSet({ ...defaultOptions(), ChimpsRounds: "Own" }, "Clicks")).toBeNull();
    expect(chimpsEventRoundSet(defaultOptions(), "Standard")).toBeNull();
  });
});
