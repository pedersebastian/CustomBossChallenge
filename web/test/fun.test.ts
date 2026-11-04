// The easter eggs (issue #61) never change what a real code does

import { describe, expect, it } from "vitest";
import generated from "./generated-codes.json";
import { decode } from "../src/code/decode";
import { JOKE_TEXTS, jokeFor } from "../src/fun/jokes";
import { KONAMI, KonamiTracker } from "../src/fun/konami";

describe("jokeFor", () => {
  it("knows the issue's joke codes, whatever the case and spaces", () => {
    expect(jokeFor("CBC0:MOAB")?.title).toBe("The Red Bloon");
    expect(jokeFor("  cbc0:moab\n")?.title).toBe("The Red Bloon");
    expect(jokeFor("CBC9:")).not.toBeNull();
    expect(jokeFor("Hello")).not.toBeNull();
  });

  it.each(["CBC0:MOA", "CBC0:MOABS", "CBC9:x", "hell", "", "constructor", "__proto__"])("has no joke for %j", (text) =>
    expect(jokeFor(text)).toBeNull(),
  );

  it.each(JOKE_TEXTS)("only covers text that isn't a code: %j", async (text) => expect((await decode(text)).ok).toBe(false));

  it("has no joke for any generated code, valid or broken", () => {
    for (const { code } of [...generated.valid, ...generated.invalid]) expect(jokeFor(code)).toBeNull();
  });
});

describe("KonamiTracker", () => {
  const press = (tracker: KonamiTracker, keys: string[]): boolean[] => keys.map((key) => tracker.push(key));

  it("fires on the last key of the code, ignoring case", () => {
    const keys = KONAMI.map((key) => (key.length === 1 ? key.toUpperCase() : key.replace("arrow", "Arrow")));
    expect(press(new KonamiTracker(), keys)).toEqual([...Array(KONAMI.length - 1).fill(false), true]);
  });

  it("finds the code after other keys, and after an extra ↑", () => {
    const tracker = new KonamiTracker();
    expect(press(tracker, ["x", "ArrowUp", ...KONAMI]).at(-1)).toBe(true);
  });

  it("does not fire on a wrong key and starts over after firing", () => {
    const tracker = new KonamiTracker();
    expect(press(tracker, [...KONAMI.slice(0, -1), "b"]).some(Boolean)).toBe(false);
    expect(press(tracker, KONAMI).at(-1)).toBe(true);
    expect(tracker.push("a")).toBe(false);
  });
});
