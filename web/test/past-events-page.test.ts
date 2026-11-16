// @vitest-environment happy-dom
// The Past Events page in a browser (#213): it lists the events newest first, filters them, keeps the search and the
// filters in the address, makes the code the mod reads, and says when the list can't be loaded

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { decode } from "@cbc/share-code";
import { h } from "../src/dom";
import { queryFromHash, safeSource, setUpPastEvents, shownName } from "../src/pastEvents/list";
import cases from "./past-event-cases.json";

const FILE = JSON.stringify({ formatVersion: 1, generated: "2026-11-16T00:00:00.000Z", license: "test", events: cases.events });

let copied: string[] = [];
const stops: (() => void)[] = [];

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
  stops.splice(0).forEach((stop) => stop());
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  document.body.replaceChildren();
});

async function page(file: string | (() => Promise<string>) = FILE) {
  const root = h("div");
  document.body.append(root);
  stops.push(await setUpPastEvents(root, typeof file === "string" ? async () => file : file));
  return root;
}

const rows = (root: HTMLElement) => [...root.querySelectorAll<HTMLElement>(".past-event")];
const ids = (root: HTMLElement) => rows(root).map((row) => row.dataset.id);

/** The address changing to hash, as the browser does it: no new page, then hashchange */
function goTo(hash: string): void {
  const oldURL = location.href;
  history.replaceState(null, "", hash === "" ? location.pathname : hash);
  window.dispatchEvent(new HashChangeEvent("hashchange", { oldURL, newURL: location.href }));
}

const button = (level: Element, text: string) => [...level.querySelectorAll<HTMLElement>("button, a")].find((b) => b.textContent === text)!;

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
    await vi.waitFor(() => expect(copied).toHaveLength(1));
    const read = await decode(copied[0]);
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.Boss).toBe("Lych");
    expect(read.options.Elite).toBe(true);
    expect(read.challenge.name).toBe("Lych47 Elite, 17 Jan 2025");
  });

  it("makes the code when the visitor comes to the event, so Copy copies in the click and the links are real", async () => {
    const root = await page();
    const lych = root.querySelector<HTMLElement>('[data-id="Lych47_m5zq59de"]')!;
    const [normal] = lych.querySelectorAll(".past-event-level");
    const studio = button(normal, "Open in Studio") as HTMLAnchorElement;
    expect(studio.hasAttribute("href")).toBe(false);

    lych.dispatchEvent(new Event("pointerenter"));
    await vi.waitFor(() => expect(studio.getAttribute("href")).toContain("studio.html#code=CBC"));
    expect((button(normal, "Details") as HTMLAnchorElement).getAttribute("href")).toContain("#code=CBC");

    // Copied in the click itself, before any await
    button(normal, "Copy code").click();
    expect(copied).toHaveLength(1);
    expect(studio.getAttribute("href")).toContain(copied[0]);
  });

  it("copies a code still being made through a ClipboardItem, inside the click", async () => {
    const items: { data: Record<string, Promise<Blob>> }[] = [];
    vi.stubGlobal("ClipboardItem", class {
      constructor(public data: Record<string, Promise<Blob>>) {}
    });
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText: async () => {}, write: async (list: { data: Record<string, Promise<Blob>> }[]) => void items.push(...list) },
      configurable: true,
    });
    const root = await page();
    const [, elite] = root.querySelector('[data-id="Lych47_m5zq59de"]')!.querySelectorAll(".past-event-level");
    button(elite, "Copy code").click();
    expect(items).toHaveLength(1);
    const read = await decode(await (await items[0].data["text/plain"]).text());
    if (!read.ok) throw new Error(read.reason);
    expect(read.options.Elite).toBe(true);
  });

  it("follows the address's filters, but a # like the skip link's keeps them", async () => {
    const root = await page();
    goTo("#boss=diamondback");
    expect(ids(root)).toEqual(["Diamondback8_mvaa404d"]);
    goTo("#top");
    expect(ids(root)).toEqual(["Diamondback8_mvaa404d"]);
    expect(root.querySelectorAll<HTMLSelectElement>(".past-events-filter")[0].value).toBe("diamondback");
    goTo("");
    expect(rows(root)).toHaveLength(cases.events.length);
  });

  it("reads filters that match no choice as every one, and a boss in any case", async () => {
    history.replaceState(null, "", "/past-events.html#boss=Lych&year=1999");
    await new Promise((resolve) => setTimeout(resolve, 20));
    const root = await page();
    const [boss, year] = root.querySelectorAll<HTMLSelectElement>(".past-events-filter");
    expect(boss.value).toBe("lych");
    expect(year.value).toBe("");
    expect(location.hash).toBe("#boss=lych");
  });

  it("allows only https links to the sources", () => {
    expect(safeSource("https://www.bloonswiki.com/x")).toBe("https://www.bloonswiki.com/x");
    expect(safeSource("javascript:alert(1)")).toBeNull();
    expect(safeSource("not a url")).toBeNull();
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
