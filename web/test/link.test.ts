// Links straight to a setup (issue #16): the code in a link or pasted text, and links made from codes. The cases are
// shared with the mod's Paste (UnitTests/CodeFormatTests.cs).

import { describe, expect, it } from "vitest";
import cases from "./link-cases.json";
import generated from "./generated-codes.json";
import examples from "../src/data/examples.json";
import { decode, extractCode } from "../src/code/decode";
import { codeFromHash, hashFor, linkTo } from "../src/code/link";

const PAGE = "https://pedersebastian.github.io/CustomBossChallenge/";

const codes: string[] = [
  ...generated.valid.map((entry) => entry.code),
  ...(examples as unknown as { code: string }[]).map((example) => example.code),
];

describe("extractCode", () => {
  it.each(cases.cases.map((c) => [c.text, c.code] as const))("finds the code in %j", (text, code) =>
    expect(extractCode(text)).toBe(code),
  );
});

describe("codeFromHash", () => {
  it.each(["", "#", "#codes", "#examples", "#code=", "#code=hello"])("ignores %j", (hash) => expect(codeFromHash(hash)).toBeNull());

  it("reads the issue's short form and old %-encoded links", () => {
    expect(codeFromHash("#CBC2:abc")).toBe("CBC2:abc");
    expect(codeFromHash("#code=CBC2%3Aabc")).toBe("CBC2:abc");
  });
});

describe("links", () => {
  it("has codes to test", () => expect(codes.length).toBeGreaterThan(10));

  it.each(codes.map((code, i) => [i, code] as const))("code %i survives a link", async (_, code) => {
    const link = linkTo(code, PAGE);
    // Readable: the code is in the link as it is, not %-encoded
    expect(link).toBe(`${PAGE}#code=${code}`);
    expect(codeFromHash(new URL(link).hash)).toBe(code);
    expect((await decode(link)).ok).toBe(true);
  });

  it("stays short enough for chat apps", () => {
    const longest = Math.max(...codes.map((code) => linkTo(code, PAGE).length));
    expect(longest).toBeLessThan(2000);
  });

  it("encodes anything that isn't a plain code", () => expect(hashFor("CBC2:a b#c")).toBe("#code=CBC2%3Aa%20b%23c"));
});
