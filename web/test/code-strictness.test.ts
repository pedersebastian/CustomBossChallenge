// Codes that only one of the mod and the website used to read (#174): strict JSON, Base64 and deflate, and text in
// options trimmed and compared like C#. The cases are shared with the mod (UnitTests/CodeFormatTests.cs).

import { describe, expect, it } from "vitest";
import vectors from "./code-strictness.json";
import { decode, effectiveSpawnRounds, pack } from "@cbc/share-code";
import { netTrim, toLowerAscii } from "@cbc/share-code/dotnet";
import { parseCustom } from "@cbc/share-code/skulls";
import { parseTierBosses } from "@cbc/share-code/tier-bosses";

interface Vector {
  name: string;
  json: string;
  ok: boolean;
  codePrefix?: string;
  codeSuffix?: string;
  codeInsert?: string;
  codeCut?: number;
  tiers?: number;
  spawnRounds?: number[];
  customSkulls?: number[];
  tierBosses?: [string | null, boolean][];
}

const PREFIX_LENGTH = 5;
const round4 = (value: number) => Math.round(value * 10000) / 10000;

describe("codes the mod and the website read the same way", () => {
  for (const vector of vectors.cases as Vector[]) {
    it(vector.name, async () => {
      let code = await pack(vector.json, "CBC5:");
      if (vector.codeCut !== undefined) code = code.slice(0, -vector.codeCut);
      if (vector.codeInsert !== undefined) code = code.slice(0, PREFIX_LENGTH + 8) + vector.codeInsert + code.slice(PREFIX_LENGTH + 8);
      code = (vector.codePrefix ?? "") + code + (vector.codeSuffix ?? "");

      const result = await decode(code);
      expect(result.ok, result.ok ? "read" : result.reason).toBe(vector.ok);
      if (!result.ok) return;

      if (vector.tiers !== undefined) expect(result.options.Tiers).toBe(vector.tiers);
      if (vector.spawnRounds !== undefined) expect(effectiveSpawnRounds(result.options)).toEqual(vector.spawnRounds);
      if (vector.customSkulls !== undefined) expect(parseCustom(result.options.CustomSkulls)?.map(round4)).toEqual(vector.customSkulls);
      if (vector.tierBosses !== undefined) {
        expect(parseTierBosses(result.options.TierBosses).map((tier) => [tier.boss, tier.elite])).toEqual(vector.tierBosses);
      }
    });
  }
});

describe("netTrim", () => {
  it("strips what char.IsWhiteSpace calls white space", () => {
    expect(netTrim("\u0085   x \t　")).toBe("x");
  });

  it("keeps the byte order mark and U+180E, which JavaScript's trim strips or C# keeps", () => {
    expect(netTrim("﻿x﻿")).toBe("﻿x﻿");
    expect(netTrim("᠎x")).toBe("᠎x");
  });
});

describe("toLowerAscii", () => {
  it("folds only A-Z", () => {
    expect(toLowerAscii("LYCH K İ É")).toBe("lych K İ É");
  });
});
