// @vitest-environment happy-dom
// The page code of the main page and Compare in a browser (#173): the code viewer, Compare, the examples and the
// links in the address bar

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { h } from "../src/dom";
import { EXAMPLES } from "../src/data/types";
import { JOKE_TEXTS } from "../src/fun/jokes";
import { compareCodesFromHash, codeFromHash } from "../src/code/link";
import { setUpCodeViewer } from "../src/sections/codeViewer";
import { followCompareLinks, setUpCompare } from "../src/sections/compare";
import { setUpExamples } from "../src/sections/examples";

const [first, second] = EXAMPLES.map((example) => example.code);
const scrolled: ScrollIntoViewOptions[] = [];
let reducedMotion = false;
const stops: (() => void)[] = [];

beforeEach(async () => {
  history.replaceState(null, "", "/");
  // Unlike browsers, happy-dom sends hashchange for replaceState too, a moment later: let that pass before a test's
  // own listeners are added
  await new Promise((resolve) => setTimeout(resolve, 20));
  scrolled.length = 0;
  reducedMotion = false;
  Element.prototype.scrollIntoView = function (options?: boolean | ScrollIntoViewOptions) {
    scrolled.push(typeof options === "object" ? options : {});
  };
  window.matchMedia = ((query: string) => ({ matches: reducedMotion && query.includes("reduce"), media: query })) as typeof window.matchMedia;
});

afterEach(() => {
  stops.splice(0).forEach((stop) => stop());
  vi.restoreAllMocks();
  document.body.replaceChildren();
});

/** history.replaceState throwing like Safari after 100 calls in 30 seconds */
const refuseAddressChanges = () =>
  vi.spyOn(history, "replaceState").mockImplementation(() => {
    throw new DOMException("Attempt to use history.replaceState() more than 100 times per 30 seconds", "SecurityError");
  });

function viewer() {
  const input = h("textarea");
  const result = h("div", { class: "result" });
  const section = h("section", { id: "codes" }, input, result);
  document.body.append(section);
  return { input, result, open: setUpCodeViewer(input, result) };
}

describe("the code viewer", () => {
  it("shows a code and links to it in the address bar", async () => {
    const { result, open } = viewer();
    await open(first);
    expect(result.querySelector(".setup-name")).not.toBeNull();
    expect(codeFromHash()).toBe(first);
  });

  it("shows why a code can't be read and takes the old code out of the address", async () => {
    const { result, open } = viewer();
    await open(first);
    await open("CBC2:not a code");
    expect(result.querySelector(".error")).not.toBeNull();
    expect(location.hash).toBe("");
  });

  it("shows a joke for a joke text", async () => {
    const { result, open } = viewer();
    await open(JOKE_TEXTS[0]);
    expect(result.querySelector(".setup.joke")).not.toBeNull();
    expect(location.hash).toBe("");
  });

  it("shows the newest code when an older one is still being read", async () => {
    const { result, open } = viewer();
    const older = open(first);
    const newer = open(second);
    await Promise.all([older, newer]);
    expect(codeFromHash()).toBe(second);
    expect(result.querySelectorAll(".setup-name")).toHaveLength(1);
  });

  it("still shows the code when the browser refuses to change the address", async () => {
    refuseAddressChanges();
    const { result, open } = viewer();
    await open(first);
    expect(result.querySelector(".setup-name")).not.toBeNull();
  });
});

function compare() {
  const inputA = h("textarea");
  const inputB = h("textarea");
  const result = h("div");
  const swap = h("button");
  document.body.append(inputA, inputB, swap, result);
  const open = setUpCompare(inputA, inputB, result, swap);
  stops.push(followCompareLinks(inputA, inputB, open));
  return { inputA, inputB, result, open };
}

/** The address changing to hash, as the browser does it: no new page, then hashchange */
function goTo(hash: string): Promise<void> {
  const oldURL = location.href;
  history.replaceState(null, "", hash === "" ? location.pathname : hash);
  window.dispatchEvent(new HashChangeEvent("hashchange", { oldURL, newURL: location.href }));
  // Lets the comparison it may open finish
  return new Promise((resolve) => setTimeout(resolve, 50));
}

describe("Compare", () => {
  it("compares two codes and links to both", async () => {
    const { result, open } = compare();
    await open(first, second);
    expect(result.textContent).toContain("Side by side");
    expect(compareCodesFromHash()).toEqual({ a: first, b: second });
  });

  it("leaves the boxes alone when the skip link changes the address", async () => {
    const { inputA, inputB } = compare();
    inputA.value = "CBC2:still typ";
    inputB.value = "half a code";
    await goTo("#top");
    expect(inputA.value).toBe("CBC2:still typ");
    expect(inputB.value).toBe("half a code");
  });

  it("opens a comparison pasted into the address bar, and Back from it empties the boxes", async () => {
    const { inputA, inputB, result } = compare();
    await goTo(`#a=${first}&b=${second}`);
    expect([inputA.value, inputB.value]).toEqual([first, second]);
    expect(result.textContent).toContain("Side by side");

    await goTo("");
    expect([inputA.value, inputB.value]).toEqual(["", ""]);
  });

  it("still shows the comparison when the browser refuses to change the address", async () => {
    refuseAddressChanges();
    const { result, open } = compare();
    await open(first, second);
    expect(result.textContent).toContain("Side by side");
  });
});

describe("the examples", () => {
  async function examples() {
    const { result, open } = viewer();
    const list = h("div");
    document.body.append(list);
    await setUpExamples(list, open);
    return { list, result };
  }

  async function seeDetails(list: HTMLElement): Promise<void> {
    const button = [...list.querySelectorAll("button")].find((b) => b.textContent === "See details");
    if (!button) throw new Error("No See details button");
    button.click();
    await new Promise((resolve) => setTimeout(resolve, 50));
  }

  it("shows every example with its code", async () => {
    const { list } = await examples();
    expect(list.querySelectorAll("article.example")).toHaveLength(EXAMPLES.length);
  });

  it("See details opens the example and moves the keyboard to its name", async () => {
    const { list, result } = await examples();
    await seeDetails(list);
    expect(codeFromHash()).toBe(first);
    const name = result.querySelector(".setup-name");
    expect(name).not.toBeNull();
    expect(document.activeElement).toBe(name);
    expect(scrolled).toEqual([{ behavior: "smooth" }]);
  });

  it("See details doesn't animate when the visitor asked for less motion", async () => {
    reducedMotion = true;
    const { list } = await examples();
    await seeDetails(list);
    expect(scrolled).toEqual([{ behavior: "auto" }]);
  });
});
