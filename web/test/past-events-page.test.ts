// @vitest-environment happy-dom
// The Past Events page in a browser (#213): it lists the events newest first, filters them, keeps the search and the
// filters in the address, makes the code the mod reads, and says when the list can't be loaded

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { decode } from "@cbc/share-code";
import { h } from "../src/dom";
import { queryFromHash, setUpPastEvents, shownName } from "../src/pastEvents/list";
import cases from "./past-event-cases.json";

const FILE = JSON.stringify({ formatVersion: 1, generated: "2026-11-16T00:00:00.000Z", license: "test", events: cases.events });

let copied: string[] = [];

beforeEach(async () => {
  history.replaceState(null, "", "/past-events.html");
  await new Promise((resolve) => setTimeout(resolve, 20));
  copied = [];
  Object.defineProperty(window, "isSecureContext", { value: true, configurable: true });
  Object.defineProperty(navigator, "clipboard", {
    value: { writeText: async (text: string) => void copied.push(text) },
    configurable: true,
  });
});

afterEach(() => {
  vi.restoreAllMocks();
  document.body.replaceChildren();
});

async function page(file: string | (() => Promise<string>) = FILE) {
  const root = h("div");
  document.body.append(root);
  await setUpPastEvents(root, typeof file === "string" ? async () => file : file);
  return root;
}

const rows = (root: HTMLElement) => [...root.querySelectorAll<HTMLElement>(".past-event")];
const ids = (root: HTMLElement) => rows(root).map((row) => row.dataset.id);
const settle = () => new Promise((resolve) => setTimeout(resolve, 50));

describe("the Past Events page", () => {
  it("lists every event newest first, with its dates, details and count", async () => {
    const root = await page();
    const events = cases.events as { id: string; start: number }[];
    expect(rows(root)).toHaveLength(events.length);
    const starts = ids(root).map((id) => events.find((event) => event.id === id)!.start);
    expect(starts).toEqual([...starts].sort((a, b) => b - a));
    expect(root.querySelector(".past-events-count")!.textContent).toBe(`${events.length} of ${events.length}`);

    const lych = root.querySelector<HTMLElement>('[data-id="Lych47_m5zq59de"]')!;
    expect(lych.querySelector(".past-event-dates")!.textContent).toBe("17 Jan 2025 - 23 Jan 2025");
    expect(lych.querySelector(".past-event-details")!.textContent).toBe(
      "Tinkerton | Standard | Medium | Ranked: Least Cash, Elite: Least Tiers",
    );
    expect(lych.querySelectorAll(".past-event-level")).toHaveLength(2);
  });

  it("filters by search, boss, year and mode, and keeps them in the address", async () => {
    const root = await page();
    const search = root.querySelector<HTMLInputElement>(".past-events-search")!;
    search.value = "four circles";
    search.dispatchEvent(new Event("input"));
    expect(ids(root)).toEqual(["Diamondback8_mvaa404d"]);
    expect(queryFromHash().text).toBe("four circles");

    search.value = "";
    search.dispatchEvent(new Event("input"));
    const [boss, , mode] = root.querySelectorAll<HTMLSelectElement>(".past-events-filter");
    mode.value = "Clicks";
    mode.dispatchEvent(new Event("change"));
    expect(ids(root).sort()).toEqual(["Bloonarius55_m41kgdwk", "Diamondback8_mvaa404d"]);
    boss.value = "diamondback";
    boss.dispatchEvent(new Event("change"));
    expect(ids(root)).toEqual(["Diamondback8_mvaa404d"]);
    expect(location.hash).toBe("#boss=diamondback&mode=Clicks");

    search.value = "no such event";
    search.dispatchEvent(new Event("input"));
    expect(rows(root)).toHaveLength(0);
    expect(root.querySelector<HTMLElement>(".past-events-empty")!.hidden).toBe(false);
  });

  it("opens with the address's filters", async () => {
    history.replaceState(null, "", "/past-events.html#year=2021&q=bloonarius");
    await new Promise((resolve) => setTimeout(resolve, 20));
    const root = await page();
    expect(ids(root)).toEqual(["krmwspoc"]);
    expect(root.querySelector<HTMLInputElement>(".past-events-search")!.value).toBe("bloonarius");
    expect(root.querySelectorAll<HTMLSelectElement>(".past-events-filter")[1].value).toBe("2021");
  });

  it("copies the code the mod reads, normal or Elite", async () => {
    const root = await page();
    const lych = root.querySelector<HTMLElement>('[data-id="Lych47_m5zq59de"]')!;
    const [, elite] = lych.querySelectorAll<HTMLElement>(".past-event-level");
    const copy = [...elite.querySelectorAll("button")].find((button) => button.textContent === "Copy code")!;
    copy.click();
    await settle();
    expect(copied).toHaveLength(1);
    const read = await decode(copied[0]);
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.Boss).toBe("Lych");
    expect(read.options.Elite).toBe(true);
    expect(read.challenge.name).toBe("Lych47 Elite, 17 Jan 2025");
  });

  it("says which rules the Boss Creator leaves out, and why an event can't be opened", async () => {
    const root = await page();
    const unsupported = root.querySelector<HTMLElement>('[data-id="MadeUpUnsupported"]')!;
    expect(unsupported.querySelector(".past-event-note")!.textContent).toContain("tower FutureMonkey, mode Deflation");

    const noRules = root.querySelector<HTMLElement>('[data-id="MadeUpNoRules"]')!;
    const levels = noRules.querySelectorAll(".past-event-level");
    expect(levels).toHaveLength(1);
    expect(levels[0].querySelector(".past-event-level-name")!.textContent).toBe("Elite");

    const missingMap = root.querySelector<HTMLElement>('[data-id="MadeUpMap"]')!;
    const [normal] = missingMap.querySelectorAll(".past-event-level");
    expect(normal.textContent).toContain("This event's map, RemovedMap, isn't in this version of the game");
    expect(normal.querySelector("button")).toBeNull();
  });

  it("links the sources of wiki content with their licence", async () => {
    const root = await page();
    const lych = root.querySelector<HTMLElement>('[data-id="Lych47_m5zq59de"]')!;
    const source = lych.querySelector<HTMLAnchorElement>(".past-event-source")!;
    expect(source.textContent).toBe("Source (CC BY-NC-SA 4.0)");
    expect(source.href).toContain("bloonswiki.com");
  });

  it("says when the events can't be loaded or are from a newer version", async () => {
    const failed = await page(async () => {
      throw new Error("offline");
    });
    expect(failed.querySelector(".error")!.textContent).toContain("No events could be loaded.");

    const newer = await page(JSON.stringify({ formatVersion: 2, generated: "2026-11-16T00:00:00Z", events: [] }));
    expect(newer.querySelector(".warning")!.textContent).toContain("newer version");
  });

  it("shows CHIMPS for Clicks and the maps' names", () => {
    expect(shownName("Clicks")).toBe("CHIMPS");
    expect(shownName("Tutorial")).toBe("Monkey Meadow");
    expect(shownName("Medium")).toBe("Medium");
  });
});
