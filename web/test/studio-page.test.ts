// @vitest-environment happy-dom
// The Studio page (#12) in a browser: it starts from a code, keeps what it doesn't edit, follows the controls and copies
// the code it shows

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { decode } from "../src/code/decode";
import { h } from "../src/dom";
import { EXAMPLES } from "../src/data/types";
import { setUpStudio, type Studio } from "../src/studio/render";
import { newSetup } from "../src/studio/state";

let studio: Studio;
let form: HTMLFormElement;
let codeBox: HTMLTextAreaElement;
let actions: HTMLElement;
let preview: HTMLElement;
const writeText = vi.fn().mockResolvedValue(undefined);

beforeEach(() => {
  Object.defineProperty(window, "isSecureContext", { value: true, configurable: true });
  Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
  form = h("form");
  codeBox = h("textarea");
  actions = h("div");
  preview = h("div");
  document.body.append(form, codeBox, actions, preview);
  studio = setUpStudio(form, codeBox, actions, preview);
});

afterEach(() => {
  writeText.mockClear();
  document.body.replaceChildren();
});

const rowOf = (label: string): HTMLElement => {
  const row = [...form.querySelectorAll<HTMLElement>(".studio-row")].find((r) => r.querySelector("label")?.textContent === label);
  if (!row) throw new Error(`No row ${label}`);
  return row;
};

const button = (text: string): HTMLElement => {
  const found = [...actions.querySelectorAll<HTMLElement>(".button")].find((b) => b.textContent === text);
  if (!found) throw new Error(`No button ${text}`);
  return found;
};

const gauntlet = EXAMPLES.find((example) => example.code.startsWith("CBC3:"))!.code;

async function openCode(code: string): Promise<void> {
  const read = await decode(code);
  if (!read.ok) throw new Error(read.reason);
  await studio.load({ options: read.options, challenge: read.json }, code);
}

describe("the Studio page", () => {
  it("gives every control a label", async () => {
    await studio.load(newSetup(), null);
    for (const input of form.querySelectorAll("input, select")) {
      const named = input.id !== "" && form.querySelector(`label[for="${input.id}"]`) !== null;
      expect(named || input.closest("label") !== null, input.outerHTML).toBe(true);
    }
  });

  it("starts from a code and keeps its Advanced tier bosses, name and rules", async () => {
    await openCode(gauntlet);
    const read = await decode(studio.code());
    const original = await decode(gauntlet);
    if (!read.ok || !original.ok) throw new Error("Didn't load");
    expect(studio.code().startsWith("CBC3:")).toBe(true);
    expect(read.options).toEqual(original.options);
    expect(read.challenge).toEqual(original.challenge);
    expect(rowOf("Boss").hidden).toBe(true);
    expect(rowOf("Skulls").hidden).toBe(true);
    expect(form.querySelector(".note")?.hasAttribute("hidden")).toBe(false);
    // Nothing changed yet, so there is nothing to compare
    expect(button("Compare with the original").hidden).toBe(true);
  });

  it("follows a slider, and offers to compare once something changed", async () => {
    await openCode(gauntlet);
    const tiers = rowOf("Boss Tiers").querySelector("input")!;
    tiers.value = "9";
    tiers.dispatchEvent(new Event("input"));
    await studio.flush();
    const read = await decode(studio.code());
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.Tiers).toBe(9);
    expect(codeBox.value).toBe(studio.code());
    expect(button("Compare with the original").hidden).toBe(false);
    expect(preview.querySelector(".setup-sub")?.textContent).toContain("9 tiers");
  });

  it("shows only the skull rows the skull mode uses", async () => {
    await studio.load(newSetup(), null);
    const mode = rowOf("Skulls").querySelector("select")!;
    expect(rowOf("Skull Count").hidden).toBe(true);
    mode.value = "EvenlySpaced";
    mode.dispatchEvent(new Event("change"));
    expect(rowOf("Skull Count").hidden).toBe(false);
    expect(rowOf("Skulls at Boss HP %").hidden).toBe(true);
    mode.value = "Custom";
    mode.dispatchEvent(new Event("change"));
    expect(rowOf("Skulls at Boss HP %").hidden).toBe(false);
  });

  it("copies the code it shows", async () => {
    await studio.load(newSetup(), null);
    button("Copy code").click();
    await vi.waitFor(() => expect(writeText).toHaveBeenCalledWith(studio.code()));
    expect(studio.code().startsWith("CBC1:")).toBe(true);
  });
});
