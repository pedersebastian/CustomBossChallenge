// @vitest-environment happy-dom
// The Past Events page in a browser (#213): it lists the events newest first, filters them, keeps the search and the
// filters in the address, makes the code the mod reads, and says when the list can't be loaded; a link opens it on one
// event (#271)

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ANY_EVENT, decode, newestFirst, readPastEvents, type PastEvent } from "@cbc/share-code";
import { h } from "../src/dom";
import {
  eventFromHash,
  eventLink,
  hashForQuery,
  isQueryHash,
  queryFromHash,
  queryShowing,
  ratingsOf,
  safeSource,
  setUpPastEvents,
  shownName,
  sortFromHash,
  sortPastEvents,
  unknownEventText,
} from "../src/pastEvents/list";
import { rate } from "../src/pastEvents/rating";
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

describe("the difficulty on Past events", () => {
  const read = readPastEvents(FILE);
  if (read.problem !== "None") throw new Error(read.error);
  const events = read.file.events;
  const sortSelect = (root: HTMLElement) => root.querySelector<HTMLSelectElement>('select[aria-label="Sort"]')!;

  it("shows each level's stars, as the Studio rates the setup it opens as", async () => {
    const root = await page();
    const lych = events.find((event) => event.id === "Lych47_m5zq59de")!;
    const [normal, elite] = root.querySelector('[data-id="Lych47_m5zq59de"]')!.querySelectorAll(".past-event-level");
    for (const [level, isElite] of [[normal, false], [elite, true]] as const) {
      const stars = level.querySelector(".past-event-stars")!;
      const rated = rate(lych, isElite)!;
      expect(stars.getAttribute("role")).toBe("img");
      expect(stars.getAttribute("aria-label")).toBe(`Difficulty: ${rated.stars} of 10 stars, an estimate`);
      expect(stars.querySelectorAll(".star.on")).toHaveLength(rated.stars);
      expect(stars.querySelectorAll(".star")).toHaveLength(10);
    }
    expect(rate(lych, true)!.points).toBeGreaterThan(rate(lych, false)!.points);
  });

  it("shows no stars for a level that can't be opened", async () => {
    const root = await page();
    const [normal] = root.querySelector('[data-id="MadeUpMap"]')!.querySelectorAll(".past-event-level");
    expect(normal.querySelector(".past-event-stars")).toBeNull();
  });

  it("sorts by date or by difficulty, Elite first, then normal, ties newest first and the unrated last", () => {
    const [a, b, c, d] = newestFirst(events).slice(0, 4);
    const rated = (event: PastEvent, elite: number | null, normal: number | null) => ({
      normal: normal === null ? null : { event, elite: false, points: normal, stars: 1 },
      elite: elite === null ? null : { event, elite: true, points: elite, stars: 1 },
    });
    const ratings = new Map([[a.id, rated(a, 5, 1)], [b.id, rated(b, 7, 2)], [c.id, rated(c, 5, 3)], [d.id, rated(d, null, null)]]);
    const of = (event: PastEvent) => ratings.get(event.id)!;
    const four = [d, c, b, a];
    expect(sortPastEvents(four, "newest", of)).toEqual([a, b, c, d]);
    expect(sortPastEvents(four, "oldest", of)).toEqual([d, c, b, a]);
    expect(sortPastEvents(four, "hardest", of)).toEqual([b, c, a, d]);
    expect(sortPastEvents(four, "easiest", of)).toEqual([a, c, b, d]);
    // Two alike stay newest first
    const same = new Map([[a.id, rated(a, 5, 1)], [b.id, rated(b, 5, 1)]]);
    expect(sortPastEvents([b, a], "hardest", (event) => same.get(event.id)!)).toEqual([a, b]);
  });

  it("sorts the list from the Sort menu and keeps the order in the address", async () => {
    const root = await page();
    expect(sortSelect(root).value).toBe("newest");
    sortSelect(root).value = "hardest";
    sortSelect(root).dispatchEvent(new Event("change"));
    expect(location.hash).toBe("#sort=hardest");
    expect(ids(root)).toEqual(sortPastEvents(events, "hardest", ratingsOf).map((event) => event.id));
    // The hardest has an Elite at least as hard as the next one's
    const [first, second] = ids(root).map((id) => events.find((event) => event.id === id)!);
    expect(rate(first, true)!.points).toBeGreaterThanOrEqual(rate(second, true)!.points);

    const [boss] = root.querySelectorAll<HTMLSelectElement>(".past-events-filter");
    boss.value = "lych";
    boss.dispatchEvent(new Event("change"));
    expect(location.hash).toBe("#boss=lych&sort=hardest");
    sortSelect(root).value = "newest";
    sortSelect(root).dispatchEvent(new Event("change"));
    expect(location.hash).toBe("#boss=lych");
  });

  it("opens and follows the address's order, and leaves out one it doesn't know", async () => {
    history.replaceState(null, "", "/past-events.html#sort=easiest");
    await new Promise((resolve) => setTimeout(resolve, 20));
    const root = await page();
    expect(sortSelect(root).value).toBe("easiest");
    expect(ids(root)).toEqual(sortPastEvents(events, "easiest", ratingsOf).map((event) => event.id));
    goTo("#sort=oldest");
    expect(sortSelect(root).value).toBe("oldest");
    expect(ids(root)).toEqual(newestFirst(events).reverse().map((event) => event.id));
    goTo("#year=2025&sort=sideways");
    expect(sortSelect(root).value).toBe("newest");
    expect(location.hash).toBe("#year=2025");
  });

  it("writes the order after the filters, and newest first not at all", () => {
    expect(hashForQuery({ ...ANY_EVENT, mode: "Clicks" }, "hardest")).toBe("#mode=Clicks&sort=hardest");
    expect(hashForQuery(ANY_EVENT, "newest")).toBe("");
    expect(sortFromHash("#sort=oldest")).toBe("oldest");
    expect(sortFromHash("#sort=nope")).toBe("newest");
  });
});

describe("a link to one past event (#271)", () => {
  const LYCH = "Lych47_m5zq59de";
  const read = readPastEvents(FILE);
  if (read.problem !== "None") throw new Error(read.error);
  const lych = read.file.events.find((event) => event.id === LYCH)!;

  async function openAt(hash: string) {
    history.replaceState(null, "", `/past-events.html${hash}`);
    await new Promise((resolve) => setTimeout(resolve, 20));
    return page();
  }
  const scrolled: { id: string | undefined; behavior: ScrollBehavior | undefined }[] = [];
  beforeEach(() => {
    scrolled.length = 0;
    vi.spyOn(Element.prototype, "scrollIntoView").mockImplementation(function (this: Element, options?: boolean | ScrollIntoViewOptions) {
      scrolled.push({ id: (this as HTMLElement).dataset.id, behavior: typeof options === "object" ? options.behavior : undefined });
    });
  });
  const unknownNote = (root: HTMLElement) => root.querySelector<HTMLElement>(".past-events-unknown")!;

  it("reads and writes the event in the address, first and next to the other keys", () => {
    expect(eventFromHash(`#event=${LYCH}&sort=hardest`)).toBe(LYCH);
    expect(eventFromHash("#event=")).toBeNull();
    expect(eventFromHash("#boss=lych")).toBeNull();
    expect(isQueryHash(`#event=${LYCH}`)).toBe(true);
    expect(hashForQuery({ ...ANY_EVENT, boss: "lych" }, "hardest", LYCH)).toBe(`#event=${LYCH}&boss=lych&sort=hardest`);
    expect(hashForQuery(ANY_EVENT, "newest", "a b&c")).toBe("#event=a+b%26c");
    expect(eventFromHash(hashForQuery(ANY_EVENT, "newest", "a b&c"))).toBe("a b&c");
    expect(eventLink(LYCH, "https://example.org/cbc/past-events.html?x=1#boss=lych")).toBe(
      `https://example.org/cbc/past-events.html?x=1#event=${LYCH}`,
    );
  });

  it("takes out only the filters that would hide the event", () => {
    const search = () => "lych47 tinkerton";
    expect(queryShowing(lych, { text: "tinker", boss: "lych", year: 2025, mode: "Clicks" }, search))
      .toEqual({ text: "tinker", boss: "lych", year: 2025, mode: null });
    expect(queryShowing(lych, { text: "nothing", boss: "bloonarius", year: 2021, mode: null }, search)).toEqual(ANY_EVENT);
  });

  it("opens on the event: shown, scrolled to, marked and with the keyboard on it", async () => {
    const root = await openAt(`#event=${LYCH}`);
    const row = root.querySelector<HTMLElement>(`[data-id="${LYCH}"]`)!;
    expect(rows(root)).toHaveLength(cases.events.length);
    expect(row.classList.contains("linked")).toBe(true);
    expect(root.querySelectorAll(".linked")).toHaveLength(1);
    expect(document.activeElement).toBe(row);
    // Opened from a link it jumps, like a link to an anchor
    expect(scrolled).toEqual([{ id: LYCH, behavior: "instant" }]);
    expect(location.hash).toBe(`#event=${LYCH}`);
    expect(unknownNote(root).hidden).toBe(true);
  });

  it("scrolls smoothly to a link followed on the open page, but not under reduced motion", async () => {
    await page();
    goTo(`#event=${LYCH}`);
    expect(scrolled).toEqual([{ id: LYCH, behavior: "smooth" }]);
    expect(document.querySelector(".past-events-list")!.classList.contains("measuring")).toBe(true);
    await vi.waitFor(() => expect(document.querySelector(".past-events-list")!.classList.contains("measuring")).toBe(false));

    scrolled.length = 0;
    vi.stubGlobal("matchMedia", (query: string) => ({ matches: query.includes("reduce") }));
    goTo("#event=Diamondback8_mvaa404d");
    expect(scrolled).toEqual([{ id: "Diamondback8_mvaa404d", behavior: "auto" }]);
    expect(document.activeElement).toBe(document.querySelector('[data-id="Diamondback8_mvaa404d"]'));
  });

  it("keeps the other keys, but clears the filters that would hide the event", async () => {
    const root = await openAt(`#mode=Clicks&event=${LYCH}&boss=lych&sort=hardest`);
    expect(location.hash).toBe(`#event=${LYCH}&boss=lych&sort=hardest`);
    const [boss, , mode] = root.querySelectorAll<HTMLSelectElement>(".past-events-filter");
    expect(boss.value).toBe("lych");
    expect(mode.value).toBe("");
    expect(root.querySelector<HTMLSelectElement>('select[aria-label="Sort"]')!.value).toBe("hardest");
    expect(root.querySelector(`[data-id="${LYCH}"]`)!.classList.contains("linked")).toBe(true);
  });

  it("finds the id in any case and writes it as the archive has it", async () => {
    const root = await openAt(`#event=${LYCH.toUpperCase()}`);
    expect(root.querySelector(`[data-id="${LYCH}"]`)!.classList.contains("linked")).toBe(true);
    expect(location.hash).toBe(`#event=${LYCH}`);
  });

  it("shows the whole list and a note for an id it doesn't know, and takes it out of the address", async () => {
    const root = await openAt("#event=NoSuchEvent99");
    expect(rows(root)).toHaveLength(cases.events.length);
    expect(unknownNote(root).hidden).toBe(false);
    expect(unknownNote(root).textContent).toBe(unknownEventText("NoSuchEvent99"));
    expect(unknownNote(root).textContent).toContain('"NoSuchEvent99"');
    expect(root.querySelector(".linked")).toBeNull();
    expect(scrolled).toEqual([]);
    expect(location.hash).toBe("");
    expect(unknownEventText("x".repeat(500)).length).toBeLessThan(250);

    // Another link to a known event takes the note away
    goTo(`#event=${LYCH}`);
    expect(unknownNote(root).hidden).toBe(true);
    expect(root.querySelector(".linked")!.getAttribute("data-id")).toBe(LYCH);
  });

  it("follows a link to an event while open, and lets go of it when the visitor changes the list", async () => {
    const root = await page();
    expect(root.querySelector(".linked")).toBeNull();
    goTo("#event=Diamondback8_mvaa404d&boss=lych");
    expect(root.querySelector(".linked")!.getAttribute("data-id")).toBe("Diamondback8_mvaa404d");
    expect(location.hash).toBe("#event=Diamondback8_mvaa404d");

    const [boss] = root.querySelectorAll<HTMLSelectElement>(".past-events-filter");
    boss.value = "lych";
    boss.dispatchEvent(new Event("change"));
    expect(location.hash).toBe("#boss=lych");
    expect(root.querySelector(".linked")).toBeNull();
  });

  it("has a Copy event link on every event, codes or not, that copies the link to it", async () => {
    vi.stubGlobal("CompressionStream", undefined);
    const root = await page();
    for (const row of rows(root)) {
      const copy = row.querySelector<HTMLButtonElement>(".past-event-link")!;
      expect(copy.textContent).toBe("Copy event link");
      expect(copy.disabled).toBe(false);
    }
    const copy = root.querySelector(`[data-id="${LYCH}"]`)!.querySelector<HTMLButtonElement>(".past-event-link")!;
    expect(copy.getAttribute("aria-label")).toBe("Copy event link: Lych47");
    copy.click();
    await vi.waitFor(() => expect(copied).toHaveLength(1));
    expect(copied[0]).toBe(eventLink(LYCH));
    expect(new URL(copied[0]).hash).toBe(`#event=${LYCH}`);
  });
});
