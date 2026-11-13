// @vitest-environment happy-dom
// What the page does in a browser (#113): the copy buttons, what screen readers are told, links within the page, the
// skull block of Advanced Mode, the Konami party and how large a code may unpack to

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { decode } from "@cbc/share-code";
import { keepsSetup } from "../src/code/link";
import { FEEDBACK_TIME, announce, copyWithFeedback, h } from "../src/dom";
import { GAME } from "../src/data/types";
import { PARTY_TIME, KONAMI } from "../src/fun/konami";
import { linearAxis } from "../src/compare/chart";
import { encode } from "./encode";

const live = (): string => document.querySelector('[aria-live="polite"]')?.textContent ?? "";

beforeEach(() => {
  vi.useFakeTimers();
  Object.defineProperty(window, "isSecureContext", { value: true, configurable: true });
  Object.defineProperty(navigator, "clipboard", { value: { writeText: vi.fn().mockResolvedValue(undefined) }, configurable: true });
});

afterEach(() => {
  vi.useRealTimers();
  document.body.replaceChildren();
});

describe("copy buttons", () => {
  it("say Copied! and go back to their label", async () => {
    const button = h("button", {}, "Copy code");
    await copyWithFeedback(button, "CBC2:abc");
    expect(button.textContent).toBe("Copied!");
    vi.advanceTimersByTime(FEEDBACK_TIME);
    expect(button.textContent).toBe("Copy code");
  });

  it("go back to their label after two clicks close together", async () => {
    const button = h("button", {}, "Copy link");
    await copyWithFeedback(button, "a", "Link copied!");
    vi.advanceTimersByTime(FEEDBACK_TIME / 2);
    await copyWithFeedback(button, "a", "Link copied!");
    vi.advanceTimersByTime(FEEDBACK_TIME * 2);
    expect(button.textContent).toBe("Copy link");
  });

  it("tell screen readers whether copying worked", async () => {
    await copyWithFeedback(h("button", {}, "Copy code"), "CBC2:abc");
    vi.advanceTimersByTime(100);
    expect(live()).toBe("Copied!");

    (navigator.clipboard.writeText as ReturnType<typeof vi.fn>).mockRejectedValue(new Error("no"));
    document.execCommand = () => false;
    await copyWithFeedback(h("button", {}, "Copy code"), "CBC2:abc");
    vi.advanceTimersByTime(100);
    expect(live()).toBe("Copy failed");
  });

  it("read the same message out again", () => {
    announce("Copied!");
    vi.advanceTimersByTime(100);
    announce("Copied!");
    expect(live()).toBe("");
    vi.advanceTimersByTime(100);
    expect(live()).toBe("Copied!");
    expect(document.querySelectorAll('[aria-live="polite"]')).toHaveLength(1);
  });
});

describe("links within the page", () => {
  it("know which addresses point at a setup", () => {
    expect(keepsSetup("#code=CBC2:abc")).toBe(true);
    expect(keepsSetup("#CBC4:abc")).toBe(true);
    expect(keepsSetup("#a=CBC2:abc&b=CBC2:def")).toBe(true);
    expect(keepsSetup("#b=CBC2:def")).toBe(true);
    expect(keepsSetup("#codes")).toBe(false);
    expect(keepsSetup("")).toBe(false);
  });

  it("keep the code in the address and move to the part of the page", async () => {
    document.body.append(h("nav", {}, h("a", { href: "#codes" }, "Read a code")), h("section", { id: "codes" }, "Codes"));
    history.replaceState(null, "", "#code=CBC2:abc");
    await import("../src/site");

    const link = document.querySelector<HTMLAnchorElement>('a[href="#codes"]')!;
    const click = new MouseEvent("click", { bubbles: true, cancelable: true, button: 0 });
    link.dispatchEvent(click);

    expect(click.defaultPrevented).toBe(true);
    expect(location.hash).toBe("#code=CBC2:abc");
    expect(document.activeElement?.id).toBe("codes");
  });

  it("are followed as usual when the address has no setup", async () => {
    document.body.append(h("a", { href: "#codes" }, "Read a code"), h("section", { id: "codes" }, "Codes"));
    history.replaceState(null, "", "#install");
    await import("../src/site");

    const click = new MouseEvent("click", { bubbles: true, cancelable: true, button: 0 });
    document.querySelector("a")!.dispatchEvent(click);
    expect(click.defaultPrevented).toBe(false);
  });
});

describe("the Konami party", () => {
  it("ends even when its bloons never finish rising", async () => {
    window.matchMedia = ((query: string) => ({ matches: false, media: query })) as typeof window.matchMedia;
    const { setUpKonami } = await import("../src/fun/konami");
    setUpKonami();
    const type = (): void => KONAMI.forEach((key) => window.dispatchEvent(new KeyboardEvent("keydown", { key })));

    type();
    expect(document.querySelector(".bloon-party")).not.toBeNull();
    // Reduced motion turned on: the bloons are hidden and their animations never end
    vi.advanceTimersByTime(PARTY_TIME);
    expect(document.querySelector(".bloon-party")).toBeNull();

    type();
    expect(document.querySelector(".bloon-party")).not.toBeNull();
  });
});

describe("the warnings under the Tier Preview (#186)", () => {
  const tierPreview = (root: HTMLElement): HTMLElement =>
    [...root.querySelectorAll<HTMLElement>(".block")].find((block) => block.querySelector("h4")?.textContent === "Tier Preview")!;

  it("say what the Boss Creator says, in its order", async () => {
    const { renderSetup } = await import("../src/preview/render");
    const { MOD_TEXTS } = await import("../src/modTexts");
    const { defaultChallenge, defaultOptions, encode: make } = await import("@cbc/share-code");
    const json = defaultChallenge();
    (json.startRules as Record<string, number>).round = 36;
    const read = await decode(await make({ ...defaultOptions(), SpawnRounds: "40, 60, 80, 81, 100" }, json));
    if (!read.ok) throw new Error(read.reason);

    const notes = [...tierPreview(renderSetup(read.options, read.challenge)).querySelectorAll(".note")].map((note) => note.textContent);
    expect(notes).toEqual([
      MOD_TEXTS.StartRoundClose.replace("{0}", "36").replace("{1}", "4").replace("{2}", "40"),
      MOD_TEXTS.TiersClose.replace("{0}", "80-81"),
    ]);
  });

  it("keep the game's health limit at a Boss Health below 100 %, like the mod", async () => {
    const { renderTiers } = await import("../src/preview/render");
    const { defaultOptions } = await import("@cbc/share-code");
    const { formatHealth } = await import("@cbc/share-code/tiers");
    const options = { ...defaultOptions(), Elite: true, Tiers: 10, HealthPerTier: 200 };
    const challenge = { bossSpeed: 1, bossHealth: 0.5 } as Parameters<typeof renderTiers>[1];

    const notes = [...renderTiers(options, challenge).querySelectorAll(".note")].map((note) => note.textContent ?? "");
    expect(notes).toHaveLength(1);
    expect(notes[0]).toContain(`Health stops at ${formatHealth(2e9)} per tier`);
  });
});

describe("the speed axis", () => {
  it.each([1, 3, 10, 45, 100, 1000, 10000])("has at most 7 ticks up to ×%d", (max) => {
    const ticks = linearAxis([max]).ticks;
    expect(ticks.length).toBeLessThanOrEqual(7);
    expect(ticks[ticks.length - 1].value).toBeGreaterThanOrEqual(max);
  });
});

describe("how large a code may unpack to", () => {
  // The real limit, not the fake timers': decoding streams
  beforeEach(() => vi.useRealTimers());

  it("refuses a small code that unpacks to more than the limit", async () => {
    const code = await encode({ o: {}, c: { name: "a".repeat(GAME.limits.maxJsonLength + 1) } });
    expect(code.length).toBeLessThan(GAME.limits.maxCodeLength);
    const result = await decode(code);
    expect(result.ok).toBe(false);
    expect(result.problem).toBe("Invalid");
  });

  it("reads a code that unpacks to just under it", async () => {
    const payload = JSON.stringify({ o: {}, c: { name: "" } });
    const code = await encode(payload.replace('""', `"${"a".repeat(GAME.limits.maxJsonLength - payload.length)}"`));
    const result = await decode(code);
    expect(result.ok).toBe(true);
  });

  it("refuses a code longer than the limit without unpacking it", async () => {
    const result = await decode("CBC2:" + "a".repeat(GAME.limits.maxCodeLength));
    expect(result.ok).toBe(false);
    expect(result.ok ? "" : result.reason).toBe("The code is too long.");
  });
});
