// The website must read every code exactly like the mod does. The fixtures come from the game (Tests/WebDataTest.cs).

import { describe, expect, it } from "vitest";
import fixtures from "../src/data/fixtures.json";
import { decode, playProblem } from "../src/code/decode";
import { effectiveSpawnRounds, optionsJson } from "../src/code/options";
import { roundHalfEven } from "../src/math/dotnet";
import { formatPercent } from "../src/math/skulls";
import { endRound } from "../src/math/tiers";
import { skulls, tierCards } from "../src/preview/compute";
import { EXAMPLES } from "../src/data/types";

const round6 = (value: number) => roundHalfEven(value, 6);

describe("codes the mod reads", () => {
  for (const fixture of fixtures.valid) {
    it(`reads ${fixture.code.slice(0, 40)}…`, async () => {
      const result = await decode(fixture.code);
      if (!result.ok) throw new Error(`Didn't load: ${result.reason}`);

      expect(optionsJson(result.options)).toEqual(fixture.options);

      const challenge = { ...result.challenge };
      for (const key of ["bossSpeed", "bossHealth", "bloonSpeed", "moabSpeed", "bloonHealth", "moabHealth"] as const) {
        challenge[key] = round6(challenge[key]);
      }
      expect(challenge).toEqual(fixture.challenge);

      const rounds = effectiveSpawnRounds(result.options);
      expect(rounds).toEqual(fixture.spawnRounds);
      expect(endRound(rounds)).toBe(fixture.endRound);

      const cards = tierCards(result.options, result.challenge).map((card) => ({
        round: card.round,
        speed: round6(card.speed),
        healthFactor: round6(card.healthFactor),
        health: card.health,
        capped: card.capped,
      }));
      expect(cards).toEqual(fixture.tiers);

      expect(skulls(result.options).percents.map(formatPercent)).toEqual(fixture.skulls);
      expect(playProblem(result.challenge, rounds) === null ? null : "yes").toBe(fixture.playProblem);
    });
  }
});

describe("codes the mod refuses", () => {
  for (const fixture of fixtures.invalid) {
    it(`handles ${fixture.why}`, async () => {
      const result = await decode(fixture.code);
      expect(result.ok).toBe(fixture.loads);
      expect(result.problem).toBe(fixture.problem);
    });
  }

  it("never throws on junk", async () => {
    for (const junk of [null, undefined, "CBC2:", "CBC1:____", "CBC1:" + "-".repeat(100), "CBC12", "  CBC1:AA  "]) {
      await expect(decode(junk)).resolves.toHaveProperty("ok");
    }
  });
});

describe("examples", () => {
  it("are all playable codes", async () => {
    expect(EXAMPLES.length).toBeGreaterThanOrEqual(5);
    for (const example of EXAMPLES) {
      const result = await decode(example.code);
      if (!result.ok) throw new Error(`${example.name}: ${result.reason}`);
      expect(result.challenge.name).toBe(example.name);
      expect(playProblem(result.challenge, effectiveSpawnRounds(result.options))).toBeNull();
    }
  });
});
