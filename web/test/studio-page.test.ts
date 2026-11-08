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
  // The Standard rows, not the chosen tier's own in Advanced Mode
  const row = [...form.querySelectorAll<HTMLElement>(":scope > .studio-row")].find((r) => r.querySelector("label")?.textContent === label);
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
    expect(form.querySelector<HTMLElement>(".studio-advanced")?.hidden).toBe(false);
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

  const tab = (name: string): HTMLButtonElement =>
    [...form.querySelectorAll<HTMLButtonElement>(".studio-tab")].find((b) => b.textContent === name)!;
  const card = (tier: number): HTMLElement => form.querySelector<HTMLElement>(`.studio-tier[data-tier="${tier}"]`)!;
  const chip = (boss: string): HTMLElement => form.querySelector<HTMLElement>(`.studio-chip[data-boss="${boss}"]`)!;
  const tierBosses = async (): Promise<string | null | undefined> => {
    await studio.flush();
    const read = await decode(studio.code());
    if (!read.ok) throw new Error(read.reason);
    return read.options.TierBosses;
  };

  it("switches to Advanced and back with the mode tabs (#95)", async () => {
    await studio.load(newSetup(), null);
    const advanced = form.querySelector<HTMLElement>(".studio-advanced")!;
    expect(advanced.hidden).toBe(true);
    expect(tab("Standard").getAttribute("aria-pressed")).toBe("true");
    tab("Advanced").click();
    expect(advanced.hidden).toBe(false);
    expect(tab("Advanced").getAttribute("aria-pressed")).toBe("true");
    expect(rowOf("Boss").hidden).toBe(true);
    expect(studio.code().startsWith("CBC")).toBe(true);
    await studio.flush();
    expect(studio.code().startsWith("CBC3:")).toBe(true);
    expect([...form.querySelectorAll(".studio-tier")].filter((c) => !(c as HTMLElement).hidden)).toHaveLength(5);
    tab("Standard").click();
    expect(advanced.hidden).toBe(true);
    expect(await tierBosses()).toBeNull();
  });

  it("puts a dropped boss on a tier", async () => {
    await studio.load(newSetup(), null);
    tab("Advanced").click();
    const data = new Map<string, string>();
    const transfer = { setData: (type: string, value: string) => data.set(type, value), getData: (type: string) => data.get(type) ?? "" };
    const drag = (type: string, target: HTMLElement) => {
      const event = new Event(type, { bubbles: true, cancelable: true });
      Object.defineProperty(event, "dataTransfer", { value: transfer });
      target.dispatchEvent(event);
    };
    drag("dragstart", chip("Phayze"));
    drag("dragover", card(2));
    expect(card(2).classList.contains("drop-target")).toBe(true);
    drag("drop", card(2));
    expect(card(2).classList.contains("drop-target")).toBe(false);
    expect((await tierBosses())?.split(",")[1]).toBe("Phayze");
    expect(card(2).getAttribute("aria-pressed")).toBe("true");
  });

  it("gives a tapped tier the boss tapped next, on a phone too", async () => {
    await studio.load(newSetup(), null);
    tab("Advanced").click();
    card(4).click();
    chip("Random").click();
    expect((await tierBosses())?.split(",")[3]).toBe("Random");
    expect(chip("Random").getAttribute("aria-pressed")).toBe("true");
  });

  it("steps a tier's boss with the arrows and lets Delete make it follow the setup", async () => {
    await studio.load(newSetup(), null);
    tab("Advanced").click();
    card(1).dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true }));
    expect((await tierBosses())?.split(",")[0]).toBe("Lych");
    card(1).dispatchEvent(new KeyboardEvent("keydown", { key: "Delete", bubbles: true }));
    expect((await tierBosses())?.split(",")[0]).toBe("");
    expect(card(1).getAttribute("aria-label")).toContain("Bloonarius");
  });

  it("edits the chosen tier's own values in its panel", async () => {
    await studio.load(newSetup(), null);
    tab("Advanced").click();
    card(3).click();
    const panel = form.querySelector<HTMLElement>(".studio-tier-panel")!;
    expect(panel.querySelector("h3")?.textContent).toBe("Tier 3: Bloonarius");
    const speed = [...panel.querySelectorAll<HTMLElement>(".studio-row")].find((r) => r.querySelector("label")?.textContent === "Speed")!;
    const input = speed.querySelector("input")!;
    input.value = "250";
    input.dispatchEvent(new Event("input"));
    expect((await tierBosses())?.split(",")[2]).toBe("Bloonarius@250/100");
    expect(card(3).querySelector(".studio-tier-values")?.textContent).toBe("250%/100%");
  });

  const ruleControl = (label: string): HTMLInputElement & HTMLSelectElement => {
    const found = [...form.querySelectorAll<HTMLElement>(".studio-rules .studio-row")].find((r) => r.querySelector("label")?.textContent === label);
    if (!found) throw new Error(`No rule ${label}`);
    return found.querySelector("input, select") as HTMLInputElement & HTMLSelectElement;
  };
  const readChallenge = async () => {
    await studio.flush();
    const read = await decode(studio.code());
    if (!read.ok) throw new Error(read.reason);
    return read.challenge;
  };

  it("edits the Challenge Editor's rules into the code (#97)", async () => {
    await studio.load(newSetup(), null);
    const map = ruleControl("Map");
    map.value = "Infernal";
    map.dispatchEvent(new Event("change"));
    const cash = ruleControl("Starting cash");
    cash.value = "20000";
    cash.dispatchEvent(new Event("change"));
    expect(await readChallenge()).toMatchObject({ map: "Infernal", cash: 20000 });

    // Empty is the game's default again, and the field shows it as empty
    cash.value = "";
    cash.dispatchEvent(new Event("change"));
    expect((await readChallenge()).cash).toBe(-1);
    expect(cash.value).toBe("");
    expect(cash.placeholder).toBe("Default");
  });

  it("shows a clamped value back and turns off the modes Play refuses", async () => {
    await studio.load(newSetup(), null);
    const lives = ruleControl("Lives");
    lives.value = "0";
    lives.dispatchEvent(new Event("change"));
    expect(lives.value).toBe("1");
    const disabled = [...ruleControl("Mode").querySelectorAll("option")].filter((o) => o.disabled).map((o) => o.value);
    expect(disabled).toEqual(["Deflation", "Apopalypse"]);
  });

  it("groups the maps by the game's difficulty and keeps a map it doesn't know", async () => {
    await studio.load(newSetup(), null);
    const map = ruleControl("Map");
    expect([...map.querySelectorAll("optgroup")].map((g) => g.label)).toEqual(["Beginner", "Intermediate", "Advanced", "Expert"]);

    const setup = newSetup();
    await studio.load({ ...setup, challenge: { ...setup.challenge, map: "BrandNewMap" } }, null);
    expect(map.value).toBe("BrandNewMap");
    expect(map.selectedOptions[0].textContent).toBe("BrandNewMap (not known to the website yet)");
    await studio.flush();
    const read = await decode(studio.code());
    if (!read.ok) throw new Error(read.reason);
    expect(read.json.map).toBe("BrandNewMap");
  });

  it("builds a tower class when it opens, and its controls change the code (#128)", async () => {
    await studio.load(newSetup(), null);
    const primary = form.querySelector<HTMLDetailsElement>('.studio-tower-class[data-set="Primary"]')!;
    expect(primary.querySelectorAll(".studio-tower")).toHaveLength(0);
    primary.open = true;
    primary.dispatchEvent(new Event("toggle"));
    const dart = [...primary.querySelectorAll<HTMLElement>(".studio-tower")].find((f) => f.querySelector("legend")?.textContent === "Dart Monkey")!;
    expect(primary.querySelectorAll(".studio-tower")).toHaveLength(7);

    const limit = dart.querySelector("select")!;
    const count = dart.querySelector<HTMLInputElement>('input[type="number"]')!;
    expect(count.hidden).toBe(true);
    limit.value = "limited";
    limit.dispatchEvent(new Event("change"));
    expect(count.hidden).toBe(false);
    count.value = "4";
    count.dispatchEvent(new Event("change"));
    const path3 = dart.querySelectorAll("select")[3];
    path3.value = "2";
    path3.dispatchEvent(new Event("change"));

    expect((await readChallenge()).towers).toMatchObject({ DartMonkey: 4 });
    const read = await decode(studio.code());
    if (!read.ok) throw new Error(read.reason);
    const towers = read.json.towers as { tower: string; path3NumBlockedTiers: number }[];
    expect(towers.find((t) => t.tower === "DartMonkey")?.path3NumBlockedTiers).toBe(3);
    expect(dart.classList.contains("changed")).toBe(true);
    expect(primary.querySelector("summary")?.textContent).toBe("Primary (7, 1 changed)");
  });

  it("labels every tower control, and gives heroes no paths or paragon", async () => {
    await studio.load(newSetup(), null);
    for (const details of form.querySelectorAll<HTMLDetailsElement>(".studio-tower-class")) {
      details.open = true;
      details.dispatchEvent(new Event("toggle"));
    }
    expect(form.querySelectorAll(".studio-tower")).toHaveLength(45);
    for (const input of form.querySelectorAll(".studio-tower input, .studio-tower select")) {
      const named = input.id !== "" && form.querySelector(`label[for="${input.id}"]`) !== null;
      expect(named || input.hasAttribute("aria-label"), input.outerHTML).toBe(true);
    }
    const quincy = [...form.querySelectorAll<HTMLElement>(".studio-tower")].find((f) => f.querySelector("legend")?.textContent === "Quincy")!;
    expect(quincy.querySelectorAll("select")).toHaveLength(1);
    expect(quincy.querySelector('input[type="checkbox"]')).toBeNull();
  });

  it("hides a tower's paths and paragon lock while it isn't allowed, and keeps them", async () => {
    await studio.load(newSetup(), null);
    const primary = form.querySelector<HTMLDetailsElement>('.studio-tower-class[data-set="Primary"]')!;
    primary.open = true;
    primary.dispatchEvent(new Event("toggle"));
    const dart = [...primary.querySelectorAll<HTMLElement>(".studio-tower")].find((f) => f.querySelector("legend")?.textContent === "Dart Monkey")!;
    const rows = [...dart.querySelectorAll<HTMLElement>(".studio-row")];
    const path1 = rows[1].querySelector("select")!;
    path1.value = "2";
    path1.dispatchEvent(new Event("change"));

    const limit = dart.querySelector("select")!;
    limit.value = "notAllowed";
    limit.dispatchEvent(new Event("change"));
    expect(rows.slice(1).every((row) => row.hidden)).toBe(true);
    expect(rows[0].hidden).toBe(false);

    limit.value = "none";
    limit.dispatchEvent(new Event("change"));
    expect(rows.some((row) => row.hidden)).toBe(false);
    expect(path1.value).toBe("2");
  });
});
