// The Past Events page (#213): every official boss event, newest first, with a search and filters for the boss, the
// year and the mode, like the Boss Creator's Past Events window (#146). Each event opens in the Studio, normal or
// Elite, or is copied as a code or a link. Each level shows its difficulty stars (#263), and the list can be sorted by them.
// The search, the filters and the order are in the address after #, so a filtered list can be shared.

import {
  canUseCodes,
  data,
  encode,
  hasRules,
  matchesPastEvent,
  newestFirst,
  pastEventBoss,
  pastEventModes,
  pastEventSetup,
  pastEventYears,
  readPastEvents,
  rulesOf,
  searchText,
  englishDate,
  UNSUPPORTED_REASON,
  type PastEvent,
  type PastEventQuery,
} from "@cbc/share-code";
import { linkTo, replaceAddress, studioLinkTo } from "../code/link";
import { GAME } from "../data/types";
import { NO_AUTOFILL, announce, copyWithFeedback, h } from "../dom";
import { MAX_STARS } from "@cbc/share-code/difficulty";
import { MOD_TEXTS, fillText } from "../modTexts";
import { starsText } from "../preview/difficulty";
import { portrait } from "../preview/portrait";
import { rate, type Rated } from "./rating";

/** How long after the last change screen readers hear the count */
export const COUNT_DELAY = 600;

/** Where the website publishes the events; pages.yml builds it next to the pages */
export const PAST_EVENTS_FILE = "boss-events.json";

/** The name the visitor sees of a map, mode or difficulty id; CHIMPS for Clicks, as the game shows it */
export function shownName(id: string): string {
  if (id === "Clicks") return "CHIMPS";
  return GAME.maps[id] ?? GAME.modes[id] ?? GAME.difficulties[id] ?? id;
}

const scoringName = (scoring: string | null): string =>
  scoring === "GameTime" ? MOD_TEXTS.ScoringTime
    : scoring === "LeastCash" ? MOD_TEXTS.ScoringLeastCash
      : scoring === "LeastTiers" ? MOD_TEXTS.ScoringLeastTiers
        : "?";

/** "17 Jan 2025 - 22 Jan 2025", with ~ in front when the archive only knows the dates roughly */
export function eventDates(event: PastEvent): string {
  const start = englishDate(event.start);
  const end = englishDate(event.end);
  return `${event.datesGuessed ? "~" : ""}${start === end ? start : `${start} - ${end}`}`;
}

const QUERY_KEYS = ["q", "boss", "year", "mode", "sort"];

/** The orders the list can be in; newest first is the default and isn't written in the address */
export const SORTS = ["newest", "oldest", "hardest", "easiest"] as const;
export type PastEventSort = (typeof SORTS)[number];

const SORT_NAMES: Record<PastEventSort, string> = {
  newest: "Newest first",
  oldest: "Oldest first",
  hardest: "Hardest first",
  easiest: "Easiest first",
};

/** The sort in the address as written, or null without one */
const sortParam = (hash: string): string | null => new URLSearchParams(hash.startsWith("#") ? hash.slice(1) : "").get("sort");

/** The order in the address: #sort=hardest; newest first for none or one this version doesn't know */
export function sortFromHash(hash = location.hash): PastEventSort {
  const sort = sortParam(hash);
  return SORTS.find((known) => known === sort) ?? "newest";
}

/** The two levels' ratings of an event, null for one that can't be opened */
export interface EventRatings {
  normal: Rated | null;
  elite: Rated | null;
}

export const ratingsOf = (event: PastEvent): EventRatings => ({ normal: rate(event, false), elite: rate(event, true) });

/**
 * The events in the order: by date, or by difficulty. Difficulty compares the Elite level first, then the normal
 * level; events that can't be rated come last, and ties go newest first.
 */
export function sortPastEvents(events: readonly PastEvent[], sort: PastEventSort, ratings: (event: PastEvent) => EventRatings): PastEvent[] {
  const newest = newestFirst(events);
  if (sort === "newest") return newest;
  if (sort === "oldest") return newest.reverse();
  const sign = sort === "hardest" ? -1 : 1;
  const points = new Map(newest.map((event) => {
    const { normal, elite } = ratings(event);
    return [event.id, [elite?.points ?? null, normal?.points ?? null]] as const;
  }));
  const compare = (a: number | null, b: number | null): number =>
    a === null ? (b === null ? 0 : 1) : b === null ? -1 : sign * (a - b);
  // The sort is stable, so equals stay newest first
  return newest.sort((a, b) => {
    const [eliteA, normalA] = points.get(a.id)!;
    const [eliteB, normalB] = points.get(b.id)!;
    return compare(eliteA, eliteB) || compare(normalA, normalB);
  });
}

/**
 * Whether the address's # holds a search: empty (every event) or with q, boss, year, mode or sort. Another # like the skip
 * link's #top isn't one, and leaves the search as it is.
 */
export function isQueryHash(hash = location.hash): boolean {
  if (hash === "" || hash === "#") return true;
  const params = new URLSearchParams(hash.startsWith("#") ? hash.slice(1) : hash);
  return QUERY_KEYS.some((key) => params.has(key));
}

/** The search and the filters in the address: #q=lych&boss=lych&year=2025&mode=Clicks */
export function queryFromHash(hash = location.hash): PastEventQuery {
  const params = new URLSearchParams(hash.startsWith("#") ? hash.slice(1) : "");
  const year = Number(params.get("year"));
  return {
    text: params.get("q") ?? "",
    boss: params.get("boss") || null,
    year: Number.isInteger(year) && year > 0 ? year : null,
    mode: params.get("mode") || null,
  };
}

export function hashForQuery(query: PastEventQuery, sort: PastEventSort = "newest"): string {
  const params = new URLSearchParams();
  if (query.text) params.set("q", query.text);
  if (query.boss) params.set("boss", query.boss);
  if (query.year) params.set("year", String(query.year));
  if (query.mode) params.set("mode", query.mode);
  if (sort !== "newest") params.set("sort", sort);
  const text = params.toString();
  return text ? `#${text}` : "";
}

/** The page's address with this query and order, for the address bar */
const addressFor = (query: PastEventQuery, sort: PastEventSort): string =>
  location.pathname + location.search + hashForQuery(query, sort);

/** The level's stars, lit in gold and the rest dimmed, like the Studio's */
export function renderStars(stars: number): HTMLElement {
  const text = starsText(stars);
  const shown = h("span", { class: "difficulty-stars past-event-stars", role: "img", "aria-label": text, title: text });
  for (let i = 1; i <= MAX_STARS; i++) shown.append(h("span", { class: i <= stars ? "star on" : "star", "aria-hidden": "true" }, "★"));
  return shown;
}

/** Only https links to an event's sources: the file comes from the website, but a link must never run script */
export function safeSource(url: string | undefined): string | null {
  if (!url) return null;
  try {
    return new URL(url).protocol === "https:" ? url : null;
  } catch {
    return null;
  }
}

/** The details line: map, modes and difficulty, Elite's map where it differs, and how both were ranked */
function details(event: PastEvent): string {
  const parts: string[] = [];
  const rules = event.standard ?? event.elite;
  if (rules) {
    const map = shownName(String(rules.map ?? ""));
    const eliteMap = event.standard && event.elite && event.elite.map !== rules.map ? shownName(String(event.elite.map)) : null;
    parts.push(eliteMap ? `${map} / Elite: ${eliteMap}` : map);
    const modes = [...new Set([event.standard, event.elite].map((r) => (r && typeof r.mode === "string" ? r.mode : null)))]
      .filter((mode): mode is string => mode !== null)
      .map(shownName);
    parts.push(modes.join(" / "));
    parts.push(shownName(String(rules.difficulty ?? "")));
  }
  if (event.normalScoring || event.eliteScoring) {
    parts.push(fillText(MOD_TEXTS.PastEventRanked, scoringName(event.normalScoring), scoringName(event.eliteScoring)));
  }
  return parts.filter((part) => part.length > 0).join(" | ");
}

/**
 * The buttons of the normal or the Elite event, or why it can't be opened. The code is made ahead, when the mouse,
 * the keyboard or a finger comes to the event (prepare): a copy then starts in the click itself, which Safari needs,
 * and Open in Studio and Details are real links, so a middle-click opens them in a new tab.
 */
function level(
  event: PastEvent,
  elite: boolean,
  codesWork: boolean,
  rated: Rated | null,
): { row: HTMLElement; prepare: () => void } | null {
  const rules = rulesOf(event, elite);
  if (!rules) return null;
  const label = elite ? "Elite" : MOD_TEXTS.PastEventNormal;
  const { setup, json } = pastEventSetup(event, elite);
  const row = h(
    "div",
    { class: "past-event-level" + (elite ? " elite" : "") },
    h("span", { class: "past-event-level-name" }, label),
    rated ? renderStars(rated.stars) : null,
  );

  if (setup.problem === "MissingMap") {
    row.append(h("span", { class: "muted" }, fillText(MOD_TEXTS.PastEventMissingMap, setup.map ?? "?")));
    return { row, prepare: () => {} };
  }

  const name = `${event.displayName}${elite ? " Elite" : ""}`;
  const button = (text: string, cls: string) =>
    h("button", { class: `button small ${cls}`, type: "button", "aria-label": `${text}: ${name}`, disabled: codesWork ? undefined : "" }, text);
  // Without CompressionStream there is no code to link to: the links are turned off like the buttons
  const link = (text: string, cls: string) =>
    h("a", { class: `button small ${cls}`, "aria-label": `${text}: ${name}`, "aria-disabled": codesWork ? undefined : "true" }, text);
  const studio = link("Open in Studio", "");
  const copyCode = button("Copy code", "blue");
  const copyLink = button("Copy link", "blue");
  const view = link("Details", "orange");
  const detailsOf = (code: string) => linkTo(code, new URL("./", location.href).href);

  let made: Promise<string> | null = null;
  let code: string | null = null;
  const prepare = (): Promise<string> => {
    if (!made) {
      made = json && codesWork ? encode(setup.options, json) : Promise.reject(new Error(UNSUPPORTED_REASON));
      made.then(
        (done) => {
          code = done;
          studio.href = studioLinkTo(done);
          view.href = detailsOf(done);
        },
        () => {},
      );
    }
    return made;
  };
  /** The text to copy: at once when the code is made, else a promise the copy waits for */
  const textOf = (to: (done: string) => string): string | Promise<string> => (code ? to(code) : prepare().then(to));

  // A click before the code was made waits for it, then goes there
  const follow = (anchor: HTMLAnchorElement, to: (done: string) => string) =>
    anchor.addEventListener("click", (e) => {
      if (anchor.hasAttribute("href")) return;
      e.preventDefault();
      if (codesWork) prepare().then((done) => (location.href = to(done)), () => {});
    });
  follow(studio, (done) => studioLinkTo(done));
  follow(view, detailsOf);
  copyCode.addEventListener("click", () => void copyWithFeedback(copyCode, textOf((done) => done)));
  copyLink.addEventListener("click", () => void copyWithFeedback(copyLink, textOf(detailsOf), "Link copied!"));

  row.append(h("span", { class: "actions past-event-actions" }, studio, copyCode, copyLink, view));
  if (setup.unsupported.length > 0) {
    row.append(h("p", { class: "note past-event-note" }, fillText(MOD_TEXTS.PastEventUnsupported, setup.unsupported.join(", "))));
  }
  return { row, prepare: () => void prepare().catch(() => {}) };
}

function renderEvent(event: PastEvent, all: Map<string, PastEvent>, codesWork: boolean, ratings: EventRatings): HTMLElement {
  const boss = pastEventBoss(event.bossKey);
  const replacedBy = event.replacedBy.map((id) => all.get(id)?.displayName ?? id);
  const levels = [level(event, false, codesWork, ratings.normal), level(event, true, codesWork, ratings.elite)].filter((part) => part !== null);
  const sourceUrl = safeSource(event.sourceUrls[0]);
  const source = event.license && sourceUrl ? h("a", { class: "past-event-source", href: sourceUrl }, `Source (${event.license})`) : null;

  const article = h(
    "article",
    { class: "past-event" + (hasRules(event) ? "" : " no-rules"), "data-id": event.id },
    h("div", { class: "boss-badge past-event-badge", "aria-hidden": "true" }, boss ? portrait(boss, false, 46) : "?"),
    h(
      "div",
      { class: "past-event-body" },
      h(
        "h2",
        { class: "past-event-name" },
        event.displayName,
        " ",
        h("span", { class: "past-event-dates" }, eventDates(event)),
      ),
      h("p", { class: "past-event-details" }, details(event)),
      replacedBy.length > 0 ? h("p", { class: "past-event-replaced" }, fillText(MOD_TEXTS.PastEventReplacedBy, replacedBy.join(", "))) : null,
      ...levels.map((part) => part.row),
      hasRules(event) ? null : h("p", { class: "muted" }, MOD_TEXTS.PastEventNoRules),
      source,
    ),
  );
  // The codes are made when the visitor comes to the event, before any click
  if (codesWork && levels.length > 0) {
    let prepared = false;
    const prepare = () => {
      if (prepared) return;
      prepared = true;
      levels.forEach((part) => part.prepare());
    };
    for (const type of ["pointerenter", "focusin", "touchstart"]) article.addEventListener(type, prepare, { passive: true });
  }
  return article;
}

function select(label: string, choices: [string, string][], value: string | null): HTMLSelectElement {
  const element = h("select", { class: "past-events-filter", "aria-label": label });
  for (const [id, text] of choices) element.append(h("option", { value: id }, text));
  element.value = value ?? "";
  return element;
}

/**
 * Fills the page: loads the events (load gives the file's text; tests give their own) and shows them with the
 * address's search and filters. Gives what stops it following the address.
 */
export async function setUpPastEvents(
  root: HTMLElement,
  load: () => Promise<string> = async () => {
    const response = await fetch(`./${PAST_EVENTS_FILE}`);
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return response.text();
  },
): Promise<() => void> {
  const status = h("p", { class: "muted", role: "status" }, MOD_TEXTS.PastEventsLoading);
  root.replaceChildren(status);

  let text: string;
  try {
    text = await load();
  } catch {
    status.textContent = `${MOD_TEXTS.PastEventsNone} Try again later.`;
    status.className = "error";
    return () => {};
  }
  const read = readPastEvents(text);
  if (read.problem !== "None") {
    status.textContent = read.problem === "TooNew"
      ? "The list of events is from a newer version of the website. Reload the page to get it."
      : `${MOD_TEXTS.PastEventsNone} Try again later.`;
    status.className = read.problem === "TooNew" ? "warning" : "error";
    return () => {};
  }

  const events = newestFirst(read.file.events);
  const byId = new Map(events.map((event) => [event.id, event]));
  const codesWork = canUseCodes();
  const searchTexts = new Map(events.map((event) => [event.id, searchText(event, shownName)]));
  const bosses = data.bosses.filter((boss) => events.some((event) => event.bossKey === boss.toLowerCase()));
  const years = pastEventYears(events);
  const modes = pastEventModes(events);

  /** The address's query with only what the filters offer: #boss=Lych is lych, #year=1999 is every year */
  const known = (read: PastEventQuery): PastEventQuery => ({
    text: read.text,
    boss: bosses.find((b) => b.toLowerCase() === read.boss?.toLowerCase())?.toLowerCase() ?? null,
    year: read.year !== null && years.includes(read.year) ? read.year : null,
    mode: modes.find((m) => m.toLowerCase() === read.mode?.toLowerCase()) ?? null,
  });
  const fromAddress = (): PastEventQuery => {
    const read = queryFromHash();
    const shown = known(read);
    // A sort this version doesn't know is taken out of the address too
    const written = sortParam(location.hash);
    const sortShown = sortFromHash();
    if (hashForQuery(shown) !== hashForQuery(read) || (written !== null && written !== sortShown)) {
      replaceAddress(addressFor(shown, sortShown));
    }
    return shown;
  };
  let query = fromAddress();
  let sort = sortFromHash();

  // Rated once, when an event is first shown or sorted on
  const ratings = new Map<string, EventRatings>();
  const ratingOf = (event: PastEvent): EventRatings => {
    let rated = ratings.get(event.id);
    if (!rated) {
      rated = ratingsOf(event);
      ratings.set(event.id, rated);
    }
    return rated;
  };

  const search = h("input", {
    class: "past-events-search",
    type: "search",
    placeholder: MOD_TEXTS.PastEventsSearch,
    "aria-label": "Search the events",
    ...NO_AUTOFILL,
  });
  search.value = query.text;
  const boss = select("Boss", [["", MOD_TEXTS.AnyBoss], ...bosses.map((b): [string, string] => [b.toLowerCase(), b])], query.boss);
  const year = select("Year", [["", MOD_TEXTS.AnyYear], ...years.map((y): [string, string] => [String(y), String(y)])],
    query.year ? String(query.year) : null);
  const mode = select("Mode", [["", MOD_TEXTS.AnyMode], ...modes.map((m): [string, string] => [m, shownName(m)])], query.mode);
  const order = select("Sort", SORTS.map((id): [string, string] => [id, SORT_NAMES[id]]), sort);
  const count = h("p", { class: "past-events-count" });
  const list = h("div", { class: "past-events-list" });
  const empty = h("p", { class: "muted past-events-empty" }, MOD_TEXTS.PastEventsNoMatch);

  const rendered = new Map<string, HTMLElement>();
  const rowOf = (event: PastEvent): HTMLElement => {
    let row = rendered.get(event.id);
    if (!row) {
      row = renderEvent(event, byId, codesWork, ratingOf(event));
      rendered.set(event.id, row);
    }
    return row;
  };

  // Screen readers hear the count once the typing stops, not after every letter
  let countTimer: ReturnType<typeof setTimeout> | undefined;
  function show(): void {
    const matching = events.filter((event) => matchesPastEvent(event, query, () => searchTexts.get(event.id) ?? ""));
    const shown = sortPastEvents(matching, sort, ratingOf);
    list.replaceChildren(...shown.map(rowOf));
    count.textContent = fillText(MOD_TEXTS.PastEventsCount, shown.length, events.length);
    empty.hidden = shown.length > 0;
    clearTimeout(countTimer);
    countTimer = setTimeout(() => announce(count.textContent ?? ""), COUNT_DELAY);
  }

  function update(change: Partial<PastEventQuery>): void {
    query = { ...query, ...change };
    replaceAddress(addressFor(query, sort));
    show();
  }

  search.addEventListener("input", () => update({ text: search.value }));
  boss.addEventListener("change", () => update({ boss: boss.value || null }));
  year.addEventListener("change", () => update({ year: year.value ? Number(year.value) : null }));
  mode.addEventListener("change", () => update({ mode: mode.value || null }));
  order.addEventListener("change", () => {
    sort = SORTS.find((known) => known === order.value) ?? "newest";
    update({});
  });

  // A link with other filters, or Back, shows those; another # like the skip link's leaves them
  const follow = (): void => {
    if (!isQueryHash()) return;
    query = fromAddress();
    sort = sortFromHash();
    order.value = sort;
    search.value = query.text;
    boss.value = query.boss ?? "";
    year.value = query.year ? String(query.year) : "";
    mode.value = query.mode ?? "";
    show();
  };
  window.addEventListener("hashchange", follow);

  root.replaceChildren(h("div", { class: "past-events-filters" }, search, boss, year, mode, order, count));
  // Without CompressionStream no code can be made; the list still shows the events
  if (!codesWork) root.append(h("p", { class: "warning" }, UNSUPPORTED_REASON));
  root.append(list, empty);
  show();
  return () => {
    window.removeEventListener("hashchange", follow);
    clearTimeout(countTimer);
  };
}
