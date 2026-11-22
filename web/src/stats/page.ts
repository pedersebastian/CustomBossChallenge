// The Stats page (#265): the official boss events in numbers, from boss-events.json like Past Events. Every chart is
// hidden from screen readers and has its values in a table; nothing moves, so there is nothing to turn off for
// reduced motion. Each chart tells things apart by labels and shapes, not by colour alone.

import { englishDate, readPastEvents, type PastEvent } from "@cbc/share-code";
import { s } from "../compare/chart";
import { h } from "../dom";
import { MOD_TEXTS } from "../modTexts";
import { PAST_EVENTS_FILE, shownName } from "../pastEvents/list";
import {
  SCORINGS,
  countedEvents,
  eventsPerYear,
  mapsAndModes,
  scoringPerYear,
  statsPerBoss,
  type BossStats,
  type Count,
  type ScoringOrUnknown,
  type ScoringYear,
  type Usage,
} from "./compute";

/** How many maps and modes a row of the tables names */
export const TOP = 3;

export const scoringLabel = (scoring: ScoringOrUnknown): string =>
  scoring === "GameTime" ? MOD_TEXTS.ScoringTime
    : scoring === "LeastCash" ? MOD_TEXTS.ScoringLeastCash
      : scoring === "LeastTiers" ? MOD_TEXTS.ScoringLeastTiers
        : "Unknown";

/** "On now", "Ended today", "1 day" or "12 days" */
export function sinceText(boss: BossStats): string {
  if (boss.daysSince === null) return "On now";
  if (boss.daysSince === 0) return "Ended today";
  return days(boss.daysSince);
}

const days = (count: number): string => `${count} ${count === 1 ? "day" : "days"}`;

/** "Dark Castle (12), Ravine (9), Sanctuary (7)": the first TOP, by the name the game shows */
export function topList(counts: Count[], top = TOP): string {
  return counts.length === 0 ? "–" : counts.slice(0, top).map((c) => `${shownName(c.id)} (${c.count})`).join(", ");
}

function table(head: string[], rows: (string | number)[][], caption?: string): HTMLElement {
  return h(
    "div",
    { class: "table-scroll" },
    h(
      "table",
      { class: "compare-table stats-table" },
      caption ? h("caption", { class: "sr-only" }, caption) : null,
      h("thead", {}, h("tr", {}, ...head.map((text) => h("th", { scope: "col" }, text)))),
      h("tbody", {}, ...rows.map(([first, ...rest]) => h("tr", {}, h("th", { scope: "row" }, String(first)), ...rest.map((cell) => h("td", {}, String(cell)))))),
    ),
  );
}

/** A chart's box: a title, and for screen readers where its values are */
function figure(title: string, where: string, chart: SVGElement): HTMLElement {
  return h("figure", { class: "curve stats-figure" }, h("figcaption", {}, title, h("span", { class: "sr-only" }, ` (${where})`)), chart);
}

/** Room for the longest boss name, also at the bigger text phones get */
const LABEL = 158;

/** Events per boss as bars, each with its boss and number written out */
export function barChart(bosses: BossStats[]): SVGElement {
  const width = 460;
  const row = 30;
  const height = bosses.length * row + 8;
  const max = Math.max(...bosses.map((boss) => boss.events), 1);
  const room = width - LABEL - 44;
  const chart = s("svg", { viewBox: `0 0 ${width} ${height}`, class: "curve-chart stats-bars", "aria-hidden": "true" });
  bosses.forEach((boss, i) => {
    const y = 4 + i * row;
    const length = Math.max(2, (boss.events / max) * room);
    chart.append(
      s("text", { x: LABEL - 8, y: y + 19, class: "tick", "text-anchor": "end" }, boss.name),
      s("rect", { x: LABEL, y: y + 4, width: length, height: row - 10, rx: 4, class: "bar" }),
      s("text", { x: LABEL + length + 6, y: y + 19, class: "tick value" }, String(boss.events)),
    );
  });
  return chart;
}

/** Midnight UTC of 1 January of the year */
const newYear = (year: number): number => Date.UTC(year, 0, 1);

/**
 * Every event as a mark on its boss's row, from the first event to now, with a line at each new year. A mark is as
 * long as the event, but never thinner than a line.
 */
export function timelineChart(bosses: BossStats[], events: PastEvent[], now: number): SVGElement {
  const width = 760;
  const row = 28;
  const top = 6;
  const height = top + bosses.length * row + 26;
  const first = Date.UTC(new Date(events[0].start).getUTCFullYear(), new Date(events[0].start).getUTCMonth(), 1);
  const last = Math.max(now, ...events.map((event) => event.end));
  const room = width - LABEL - 12;
  const x = (time: number): number => LABEL + ((time - first) / (last - first)) * room;

  const chart = s("svg", { viewBox: `0 0 ${width} ${height}`, class: "curve-chart stats-timeline", "aria-hidden": "true" });
  const bottom = top + bosses.length * row;
  for (let year = new Date(first).getUTCFullYear() + 1; newYear(year) <= last; year++) {
    const at = x(newYear(year));
    chart.append(
      s("line", { x1: at, x2: at, y1: top, y2: bottom, class: "grid" }),
      s("text", { x: at, y: bottom + 18, class: "tick", "text-anchor": "middle" }, String(year)),
    );
  }
  bosses.forEach((boss, i) => {
    const y = top + i * row;
    chart.append(
      s("line", { x1: LABEL, x2: width - 12, y1: y + row / 2, y2: y + row / 2, class: "grid" }),
      s("text", { x: LABEL - 8, y: y + 19, class: "tick", "text-anchor": "end" }, boss.name),
    );
    for (const event of events.filter((e) => e.bossKey === boss.key)) {
      const start = x(event.start);
      chart.append(
        s(
          "rect",
          { x: start, y: y + 5, width: Math.max(3, x(event.end) - start), height: row - 10, rx: 1.5, class: "bar" },
          s("title", {}, `${event.displayName}, ${englishDate(event.start)}`),
        ),
      );
    }
  });
  return chart;
}

/**
 * One level's scoring types per year as a grid: a row per type, a column per year, and a circle as large as the
 * number of events, which is also written in it
 */
export function scoringChart(rows: ScoringYear[], level: "normal" | "elite"): SVGElement {
  const label = 96;
  const column = 58;
  const row = 40;
  const width = label + rows.length * column + 8;
  const height = SCORINGS.length * row + 30;
  const max = Math.max(1, ...rows.flatMap((year) => SCORINGS.map((scoring) => year[level][scoring])));
  const chart = s("svg", { viewBox: `0 0 ${width} ${height}`, class: "curve-chart stats-scoring", "aria-hidden": "true" });
  SCORINGS.forEach((scoring, i) => {
    chart.append(s("text", { x: label - 8, y: i * row + row / 2 + 5, class: "tick", "text-anchor": "end" }, scoringLabel(scoring)));
  });
  rows.forEach((year, j) => {
    const cx = label + j * column + column / 2;
    chart.append(s("text", { x: cx, y: SCORINGS.length * row + 20, class: "tick", "text-anchor": "middle" }, String(year.year)));
    SCORINGS.forEach((scoring, i) => {
      const count = year[level][scoring];
      const cy = i * row + row / 2;
      if (count === 0) {
        chart.append(s("text", { x: cx, y: cy + 5, class: "tick", "text-anchor": "middle" }, "–"));
        return;
      }
      // The area grows with the number, from a circle big enough for two digits
      const r = 9 + 9 * Math.sqrt(count / max);
      chart.append(
        s("circle", { cx, cy, r, class: "bubble" }),
        s("text", { x: cx, y: cy + 5, class: "tick bubble-count", "text-anchor": "middle" }, String(count)),
      );
    });
  });
  return chart;
}

function usageTable(rows: Usage[], name: (group: string) => string, first: string): HTMLElement {
  return table(
    [first, "Events", "Maps used most", "Modes used most"],
    rows.map((row) => [name(row.group), row.events, topList(row.maps), topList(row.modes)]),
  );
}

/** The page's sections from the events, as of now */
export function renderStats(events: readonly PastEvent[], generated: string, now: number): HTMLElement[] {
  const counted = countedEvents(events);
  if (counted.length === 0) return [h("p", { class: "muted" }, MOD_TEXTS.PastEventsNone)];
  const bosses = statsPerBoss(events, now);
  const nameOf = new Map(bosses.map((boss) => [boss.key, boss.name]));
  const usage = mapsAndModes(events, now);
  const scoring = scoringPerYear(events);
  const perYear = eventsPerYear(events, now);
  const lastEvent = counted[counted.length - 1];

  const summary = h(
    "p",
    { class: "stats-summary" },
    `${counted.length} events from ${englishDate(counted[0].start)} to ${englishDate(lastEvent.start)}, `,
    `with ${bosses.length} bosses. The archive was last updated ${englishDate(Date.parse(generated))}.`,
  );

  const perBoss = h(
    "section",
    { class: "block", "aria-labelledby": "stats-bosses" },
    h("h2", { id: "stats-bosses" }, "Events per boss"),
    figure("Events per boss", "the numbers are in the table below", barChart(bosses)),
    table(
      ["Boss", "Events", "Normal", "Elite", "First", "Last", "Since the last", "Longest wait"],
      bosses.map((boss) => [
        boss.name,
        boss.events,
        boss.normal,
        boss.elite,
        englishDate(boss.first.start),
        englishDate(boss.last.start),
        sinceText(boss),
        boss.longestGap ? `${days(boss.longestGap.days)}, ${englishDate(boss.longestGap.from.end)} - ${englishDate(boss.longestGap.to.start)}` : "–",
      ]),
      "Events per boss",
    ),
    h("p", { class: "small muted" }, "An event pulled early and run again counts once. Since the last and the longest wait count from the end of an event to the start of the next."),
  );

  const timeline = h(
    "section",
    { class: "block", "aria-labelledby": "stats-timeline" },
    h("h2", { id: "stats-timeline" }, "Timeline"),
    h("div", { class: "table-scroll" }, figure(`Every event since ${englishDate(counted[0].start)}`, "the table below counts them per year", timelineChart(bosses, counted, now))),
    h(
      "details",
      { class: "towers" },
      h("summary", {}, "Events per year as a table"),
      table(["Boss", ...perYear.years.map(String)], perYear.rows.map((row) => [row.name, ...row.counts]), "Events per boss and year"),
    ),
  );

  const scoringSection = h(
    "section",
    { class: "block", "aria-labelledby": "stats-scoring" },
    h("h2", { id: "stats-scoring" }, "Scoring over time"),
    h("p", {}, "How the events were ranked each year. The oldest events have no scoring type in the archive: they were ranked on time."),
    h(
      "div",
      { class: "curves" },
      figure("Normal", "the numbers are in the table below", scoringChart(scoring, "normal")),
      figure("Elite", "the numbers are in the table below", scoringChart(scoring, "elite")),
    ),
    h(
      "details",
      { class: "towers" },
      h("summary", {}, "The scoring types as a table"),
      table(
        ["Year", ...SCORINGS.map((scoringType) => `Normal: ${scoringLabel(scoringType)}`), ...SCORINGS.map((scoringType) => `Elite: ${scoringLabel(scoringType)}`)],
        scoring.map((year) => [year.year, ...SCORINGS.map((t) => year.normal[t]), ...SCORINGS.map((t) => year.elite[t])]),
        "Scoring types per year",
      ),
    ),
  );

  const mapsSection = h(
    "section",
    { class: "block", "aria-labelledby": "stats-maps" },
    h("h2", { id: "stats-maps" }, "Maps and modes"),
    h(
      "p",
      {},
      h("strong", {}, "All events: "),
      `${topList(usage.all.maps, 5)}. `,
      h("strong", {}, "Modes: "),
      `${topList(usage.all.modes, 5)}.`,
    ),
    h("h3", {}, "Per boss"),
    usageTable(usage.perBoss, (key) => nameOf.get(key) ?? key, "Boss"),
    h("h3", {}, "Per year"),
    usageTable(usage.perYear, (year) => year, "Year"),
    h("p", { class: "small muted" }, "An event counts once for each map and mode of its normal and Elite level."),
  );

  return [summary, perBoss, timeline, scoringSection, mapsSection];
}

/** Fills the page: loads the events (load gives the file's text; tests give their own) and shows the stats */
export async function setUpStats(
  root: HTMLElement,
  load: () => Promise<string> = async () => {
    const response = await fetch(`./${PAST_EVENTS_FILE}`);
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return response.text();
  },
  now: () => number = Date.now,
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
  root.replaceChildren(...renderStats(read.file.events, read.file.generated, now()));
}
