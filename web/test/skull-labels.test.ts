// @vitest-environment happy-dom
// The Skull Preview's labels (#297): like the game's, a percentage too close to the one before it on a line goes on the
// next line down. That happens when a skull bar's size is known, which the test page never reports, so a stand-in
// ResizeObserver hands the bars over and each label says where it is.

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { decode, defaultOptions, type BossOptions } from "@cbc/share-code";
import { releaseSetup, renderSetup, renderSkulls } from "../src/preview/render";
import { EXAMPLES } from "../src/data/types";

// render.ts makes its one observer when it loads, so the stand-in must be in place before the imports above run.
// Loading render.ts again in each test (vi.resetModules) would do it too, but then Stryker counts these tests as
// covering all code that runs when a module loads, and tries those mutants against these three tests only (#268).
const { FakeObserver } = vi.hoisted(() => {
  class FakeObserver {
    static last: FakeObserver;
    readonly watched = new Set<Element>();
    constructor(readonly callback: ResizeObserverCallback) {
      FakeObserver.last = this;
    }
    observe(target: Element): void {
      this.watched.add(target);
    }
    unobserve(target: Element): void {
      this.watched.delete(target);
    }
    disconnect(): void {
      this.watched.clear();
    }
    /** As if every bar watched had changed size */
    resize(): void {
      this.callback([...this.watched].map((target) => ({ target }) as ResizeObserverEntry), this as unknown as ResizeObserver);
    }
  }
  vi.stubGlobal("ResizeObserver", FakeObserver);
  return { FakeObserver };
});

/** Where each label is, from its text: [left, right] in px */
const RECTS: Record<string, [number, number]> = {
  "90%": [100, 140],
  // Starts just before the gap after the first label ends, so it goes a line down
  "89%": [137, 150],
  "88%": [120, 160],
  // Exactly the gap after the first label on the top line
  "50%": [146, 186],
  "49%": [156, 196],
};

beforeEach(() => {
  // The bars of the test before are gone from the page
  FakeObserver.last.watched.clear();
  vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(function (this: HTMLElement) {
    const [left, right] = RECTS[this.textContent ?? ""] ?? [0, 0];
    return { left, right, top: 0, bottom: 0, width: right - left, height: 0, x: left, y: 0, toJSON: () => ({}) } as DOMRect;
  });
});

afterEach(() => {
  vi.restoreAllMocks();
  document.body.replaceChildren();
});

const custom = (skulls: string): BossOptions => ({ ...defaultOptions(), Boss: "Lych", SkullMode: "Custom", CustomSkulls: skulls });

describe("the Skull Preview's labels", () => {
  it("go one line down when they would come too close to the one before", () => {
    const bar = renderSkulls(custom("90, 89, 88, 50, 49")).querySelector<HTMLElement>(".skull-bar")!;
    expect(FakeObserver.last.watched.has(bar)).toBe(true);

    FakeObserver.last.resize();
    const labels = [...bar.querySelectorAll<HTMLElement>(".skull span")];
    expect(labels.map((label) => [label.textContent, label.style.top])).toEqual([
      ["90%", "48px"],
      ["89%", "72px"],
      ["88%", "96px"],
      ["50%", "48px"],
      ["49%", "72px"],
    ]);
    // Room under the bar for the three lines
    expect(bar.style.marginBottom).toBe("100px");
  });

  it("keep one line when they don't overlap", () => {
    const bar = renderSkulls(custom("90, 50")).querySelector<HTMLElement>(".skull-bar")!;
    FakeObserver.last.resize();
    expect([...bar.querySelectorAll<HTMLElement>(".skull span")].map((label) => label.style.top)).toEqual(["48px", "48px"]);
    expect(bar.style.marginBottom).toBe("52px");
  });

  it("are no longer laid out once the setup is taken away", async () => {
    const read = await decode(EXAMPLES.find((entry) => entry.name === "Boss Gauntlet")!.code);
    if (!read.ok) throw new Error(read.reason);

    const setup = renderSetup(read.options, read.challenge);
    const first = setup.querySelector(".skull-bar")!;
    expect(FakeObserver.last.watched.has(first)).toBe(true);
    // A bar that is swapped for another tier's is let go
    setup.querySelectorAll(".tier")[1].dispatchEvent(new Event("mouseenter"));
    const second = setup.querySelector(".skull-bar")!;
    expect(FakeObserver.last.watched.has(first)).toBe(false);
    expect(FakeObserver.last.watched.has(second)).toBe(true);

    releaseSetup(setup);
    expect(FakeObserver.last.watched.has(second)).toBe(false);
  });
});
