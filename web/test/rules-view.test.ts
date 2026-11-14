// @vitest-environment happy-dom
// The setup view shows every rule a code can hold (#167), from the fixture made in the game with all of them

import { describe, expect, it } from "vitest";
import fixtures from "../src/data/fixtures.json";
import { decode } from "@cbc/share-code";
import { renderSetup } from "../src/preview/render";

describe("the setup view", () => {
  it("shows every rule of a code that sets them all", async () => {
    const fixture = fixtures.valid.find((entry) => (entry.challenge as { noContinues?: boolean }).noContinues);
    if (!fixture) throw new Error("No fixture with every rule; write fixtures.json again (CONTRIBUTING.md)");
    const read = await decode(fixture.code);
    if (!read.ok) throw new Error(read.reason);

    const text = renderSetup(read.options, read.challenge).textContent ?? "";
    for (const shown of [
      "Regrow rate250 %",
      "Ability cooldown50 %",
      "Removable cost10000 %",
      "Least cash5,000",
      "Least tiers12",
      "No Continues",
      "No Double Cash",
      "No Fast Track",
      "Insta Monkey reward",
      "Dart Monkey: Upgrades up to 3-5-0, no Paragon",
      "Bomb Shooter: Not allowed",
    ]) {
      expect(text, shown).toContain(shown);
    }
  });
});
