// The Past Events page (#213): every official boss event, newest first, with a search and filters for the boss, the
// year and the mode, like the Boss Creator's Past Events window (#146). Each event opens in the Studio, normal or
// Elite, or is copied as a code or a link. The search and the filters are in the address after #, so a filtered list
// can be shared.

import {
  canUseCodes,
  data,
  filterChallenge,
  hasRules,
  mapPastEvent,
  matchesPastEvent,
  newestFirst,
  pastEventBoss,
  pastEventCode,
  pastEventModes,
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
import { announce, copyWithFeedback, h } from "../dom";
import { MOD_TEXTS, fillText } from "../modTexts";
import { NO_AUTOFILL } from "../studio/rows";

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

export function hashForQuery(query: PastEventQuery): string {
  const params = new URLSearchParams();
  if (query.text) params.set("q", query.text);
  if (query.boss) params.set("boss", query.boss);
  if (query.year) params.set("year", String(query.year));
  if (query.mode) params.set("mode", query.mode);
  const text = params.toString();
  return text ? `#${text}` : "";
}

/** The page's address with this query, for the address bar */
const addressFor = (query: PastEventQuery): string => location.pathname + location.search + hashForQuery(query);

/** The codes made so far, by event and level, so a second click doesn't make it again */
const codes = new Map<string, Promise<string | null>>();
const codeOf = (event: PastEvent, elite: boolean): Promise<string | null> => {
  const key = `${event.id}/${elite}`;
  let code = codes.get(key);
  if (!code) {
    code = pastEventCode(event, elite).then((result) => result.code);
    codes.set(key, code);
  }
  return code;
};

/** What the Boss Creator leaves out of the normal or Elite event, also what a code can't hold */
function leftOut(event: PastEvent, elite: boolean): string[] {
  const setup = mapPastEvent(event, elite);
  if (setup.problem !== "None") return [];
  const dropped: string[] = [];
  filterChallenge(setup.challenge, data.defaultChallenge, dropped);
  return [...setup.unsupported, ...dropped];
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

/** The buttons of the normal or the Elite event, or why it can't be opened */
function level(event: PastEvent, elite: boolean, codesWork: boolean): HTMLElement | null {
  const rules = rulesOf(event, elite);
  if (!rules) return null;
  const label = elite ? "Elite" : MOD_TEXTS.PastEventNormal;
  const setup = mapPastEvent(event, elite);
  const row = h("div", { class: "past-event-level" + (elite ? " elite" : "") }, h("span", { class: "past-event-level-name" }, label));

  if (setup.problem === "MissingMap") {
    row.append(h("span", { class: "muted" }, fillText(MOD_TEXTS.PastEventMissingMap, setup.map ?? "?")));
    return row;
  }

  const name = `${event.displayName}${elite ? " Elite" : ""}`;
  const button = (text: string, cls: string, aria: string) =>
    h("button", { class: `button small ${cls}`, type: "button", "aria-label": `${text}: ${aria}`, disabled: codesWork ? undefined : "" }, text);
  const studio = button("Open in Studio", "", name);
  const copyCode = button("Copy code", "blue", name);
  const copyLink = button("Copy link", "blue", name);
  const view = button("Details", "orange", name);

  studio.addEventListener("click", async () => {
    const code = await codeOf(event, elite);
    if (code) location.href = studioLinkTo(code);
  });
  view.addEventListener("click", async () => {
    const code = await codeOf(event, elite);
    if (code) location.href = linkTo(code, new URL("./", location.href).href);
  });
  copyCode.addEventListener("click", async () => {
    const code = await codeOf(event, elite);
    if (code) await copyWithFeedback(copyCode, code);
  });
  copyLink.addEventListener("click", async () => {
    const code = await codeOf(event, elite);
    if (code) await copyWithFeedback(copyLink, linkTo(code, new URL("./", location.href).href), "Link copied!");
  });

  row.append(h("span", { class: "actions past-event-actions" }, studio, copyCode, copyLink, view));
  const missing = leftOut(event, elite);
  if (missing.length > 0) row.append(h("p", { class: "note past-event-note" }, fillText(MOD_TEXTS.PastEventUnsupported, missing.join(", "))));
  return row;
}

function renderEvent(event: PastEvent, all: Map<string, PastEvent>, codesWork: boolean): HTMLElement {
  const boss = pastEventBoss(event.bossKey);
  const replacedBy = event.replacedBy.map((id) => all.get(id)?.displayName ?? id);
  const levels = [level(event, false, codesWork), level(event, true, codesWork)].filter((part): part is HTMLElement => part !== null);
  const source = event.license && event.sourceUrls[0]
    ? h("a", { class: "past-event-source", href: event.sourceUrls[0], rel: "noopener" }, `Source (${event.license})`)
    : null;

  return h(
    "article",
    { class: "past-event" + (hasRules(event) ? "" : " no-rules"), "data-id": event.id },
    h("div", { class: "boss-badge past-event-badge", "aria-hidden": "true" }, (boss ?? "?").slice(0, 1)),
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
      ...levels,
      hasRules(event) ? null : h("p", { class: "muted" }, MOD_TEXTS.PastEventNoRules),
      source,
    ),
  );
}

function select(label: string, choices: [string, string][], value: string | null): HTMLSelectElement {
  const element = h("select", { class: "past-events-filter", "aria-label": label });
  for (const [id, text] of choices) element.append(h("option", { value: id }, text));
  element.value = value ?? "";
  return element;
}

/**
 * Fills the page: loads the events (load gives the file's text; tests give their own) and shows them with the
 * address's search and filters
 */
export async function setUpPastEvents(
  root: HTMLElement,
  load: () => Promise<string> = async () => {
    const response = await fetch(`./${PAST_EVENTS_FILE}`);
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return response.text();
  },
): Promise<void> {
  const status = h("p", { class: "muted", role: "status" }, MOD_TEXTS.PastEventsLoading);
  root.replaceChildren(status);

  let text: string;
  try {
    text = await load();
  } catch {
    status.textContent = `${MOD_TEXTS.PastEventsNone} Try again later.`;
    status.className = "error";
    return;
  }
  const read = readPastEvents(text);
  if (read.problem !== "None") {
    status.textContent = read.problem === "TooNew"
      ? "The list of events is from a newer version of the website. Reload the page to get it."
      : `${MOD_TEXTS.PastEventsNone} Try again later.`;
    status.className = read.problem === "TooNew" ? "warning" : "error";
    return;
  }

  const events = newestFirst(read.file.events);
  const byId = new Map(events.map((event) => [event.id, event]));
  const codesWork = canUseCodes();
  const searchTexts = new Map(events.map((event) => [event.id, searchText(event, shownName)]));
  let query = queryFromHash();

  const search = h("input", {
    class: "past-events-search",
    type: "search",
    placeholder: MOD_TEXTS.PastEventsSearch,
    "aria-label": "Search the events",
    ...NO_AUTOFILL,
  });
  search.value = query.text;
  const bosses = data.bosses.filter((boss) => events.some((event) => event.bossKey === boss.toLowerCase()));
  const boss = select("Boss", [["", MOD_TEXTS.AnyBoss], ...bosses.map((b): [string, string] => [b.toLowerCase(), b])], query.boss);
  const year = select("Year", [["", MOD_TEXTS.AnyYear], ...pastEventYears(events).map((y): [string, string] => [String(y), String(y)])],
    query.year ? String(query.year) : null);
  const mode = select("Mode", [["", MOD_TEXTS.AnyMode], ...pastEventModes(events).map((m): [string, string] => [m, shownName(m)])], query.mode);
  const count = h("p", { class: "past-events-count", "aria-live": "polite" });
  const list = h("div", { class: "past-events-list" });
  const empty = h("p", { class: "muted past-events-empty" }, MOD_TEXTS.PastEventsNoMatch);

  const rendered = new Map<string, HTMLElement>();
  const rowOf = (event: PastEvent): HTMLElement => {
    let row = rendered.get(event.id);
    if (!row) {
      row = renderEvent(event, byId, codesWork);
      rendered.set(event.id, row);
    }
    return row;
  };

  function show(): void {
    const shown = events.filter((event) => matchesPastEvent(event, query, () => searchTexts.get(event.id) ?? ""));
    list.replaceChildren(...shown.map(rowOf));
    count.textContent = fillText(MOD_TEXTS.PastEventsCount, shown.length, events.length);
    empty.hidden = shown.length > 0;
  }

  function update(change: Partial<PastEventQuery>): void {
    query = { ...query, ...change };
    replaceAddress(addressFor(query));
    show();
  }

  search.addEventListener("input", () => update({ text: search.value }));
  boss.addEventListener("change", () => update({ boss: boss.value || null }));
  year.addEventListener("change", () => update({ year: year.value ? Number(year.value) : null }));
  mode.addEventListener("change", () => update({ mode: mode.value || null }));

  // A link with other filters, or Back, shows those
  window.addEventListener("hashchange", () => {
    query = queryFromHash();
    search.value = query.text;
    boss.value = query.boss ?? "";
    year.value = query.year ? String(query.year) : "";
    mode.value = query.mode ?? "";
    show();
  });

  root.replaceChildren(h("div", { class: "past-events-filters" }, search, boss, year, mode, count));
  // Without CompressionStream no code can be made; the list still shows the events
  if (!codesWork) root.append(h("p", { class: "warning" }, UNSUPPORTED_REASON));
  root.append(list, empty);
  show();
  announce(fillText(MOD_TEXTS.PastEventsCount, list.childElementCount, events.length));
}
