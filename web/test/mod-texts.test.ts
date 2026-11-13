// The website shows the mod's own texts (#157): each is the English text of Localization/English.json, or where it
// talks about something only the game has, its first sentences

import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { MOD_TEXTS } from "../src/modTexts";

const english = JSON.parse(readFileSync(new URL("../../Localization/English.json", import.meta.url), "utf8")) as Record<string, string>;

/** The texts the website shortens to their first sentences, since the rest is about the game's keyboard */
const SHORTENED = new Set(["BossTypeInfo"]);

describe("the mod's texts on the website", () => {
  for (const [name, text] of Object.entries(MOD_TEXTS)) {
    it(`${name} is the mod's`, () => {
      const mod = english[`CustomBossChallenge-${name}`];
      expect(mod, `no ${name} in English.json`).toBeDefined();
      if (SHORTENED.has(name)) {
        expect(mod.startsWith(text)).toBe(true);
        expect(text.endsWith(".")).toBe(true);
      } else expect(text).toBe(mod);
    });
  }

  it("no longer ranks the CHIMPS round sets by difficulty (#157)", () => {
    for (const name of ["ChimpsRoundsNewest", "ChimpsRoundsEarlier"] as const) expect(MOD_TEXTS[name]).not.toMatch(/hard/i);
  });
});
