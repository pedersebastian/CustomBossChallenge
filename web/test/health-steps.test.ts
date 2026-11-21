// The Boss Health sliders' steps (#250), the same as UnitTests/HealthStepsTests.cs

import { describe, expect, it } from "vitest";
import { LAST_POSITION, MAX_HEALTH, MIN_HEALTH, healthAt, healthPosition, snapHealth } from "@cbc/share-code/health-steps";

describe("Boss Health steps", () => {
  it("go from 5 % to 5000 %", () => {
    expect([MIN_HEALTH, MAX_HEALTH, LAST_POSITION]).toEqual([5, 5000, 549]);
    expect(healthAt(0)).toBe(5);
    expect(healthAt(LAST_POSITION)).toBe(5000);
    expect(healthAt(-3)).toBe(5);
    expect(healthAt(9999)).toBe(5000);
  });

  it("are 5 up to 500 % and 10 above", () => {
    for (let position = 1; position <= LAST_POSITION; position++) {
      const before = healthAt(position - 1);
      expect(healthAt(position) - before).toBe(before < 500 ? 5 : 10);
      expect(healthPosition(healthAt(position))).toBe(position);
    }
  });

  it.each([
    [-50, 5],
    [0, 5],
    [7, 5],
    [7.5, 10],
    [123, 125],
    [500, 500],
    [504, 500],
    [505, 510],
    [1335, 1340],
    [1337, 1340],
    [4999, 5000],
    [99999, 5000],
    [Number.NaN, 5],
  ])("snap %d to %d", (percent, expected) => expect(snapHealth(percent)).toBe(expected));
});
