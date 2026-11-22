// The Stats page (#265): the official boss events in numbers, from boss-events.json like Past Events. Every chart is
// hidden from screen readers and has its values in a table; nothing moves, so there is nothing to turn off for
// reduced motion. Each chart tells things apart by labels and shapes, not by colour alone.

import { ANY_EVENT, englishDate, readPastEvents, type PastEvent } from "@cbc/share-code";
import { MAX_STARS } from "@cbc/share-code/difficulty";
import { s } from "../compare/chart";
import { h } from "../dom";
import { MOD_TEXTS } from "../modTexts";
import { PAST_EVENTS_FILE, hashForQuery, shownName } from "../pastEvents/list";
import {
  SCORINGS,
  countedEvents,
  difficultyStats,
  hardestFirst,
  starSpread,
  eventsPerYear,
  mapsAndModes,
  scoringPerYear,
  statsPerBoss,
  type BossStats,
  type Count,
  type DifficultyGroup,
  type Rated,
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

/** "Lych 47 Elite" */
export const levelName = (rated: Rated): string => `${rated.event.displayName}${rated.elite ? " Elite" : ""}`;

/** "5.8", or – with no rated event */
const average = (stars: number | null): string => (stars === null ? "–" : stars.toFixed(1));

/** "Lych 47 Elite, 8 stars" */
const hardestText = (rated: Rated | null): string => (rated ? `${levelName(rated)}, ${rated.stars} ${rated.stars === 1 ? "star" : "stars"}` : "–");

/** The event in the Past events list, found by its name */
const pastEventLink = (event: PastEvent): string => `./past-events.html${hashForQuery({ ...ANY_EVENT, text: event.name })}`;

/**
 * The average stars of each year as two lines on a scale from 0 to 10: normal solid with round points, Elite dashed
 * with square ones, like the A and B of Compare
 */
export function difficultyChart(years: DifficultyGroup[]): SVGElement {
  const width = 460;
  const height = 230;
  const left = 40;
  const right = 28;
  const top = 12;
  const bottom = 32;
  const x = (i: number): number => (years.length === 1 ? (left + width - right) / 2 : left + (i / (years.length - 1)) * (width - left - right));
  const y = (stars: number): number => top + (1 - stars / MAX_STARS) * (height - top - bottom);
  const chart = s("svg", { viewBox: `0 0 ${width} ${height}`, class: "curve-chart stats-difficulty", "aria-hidden": "true" });
  for (let stars = 0; stars <= MAX_STARS; stars += 2) {
    chart.append(
      s("line", { x1: left, x2: width - right, y1: y(stars), y2: y(stars), class: "grid" }),
      s("text", { x: left - 8, y: y(stars) + 4, class: "tick", "text-anchor": "end" }, String(stars)),
    );
  }
  years.forEach((year, i) => chart.append(s("text", { x: x(i), y: height - 10, class: "tick", "text-anchor": "middle" }, year.group)));
  for (const [side, level] of [["a", "normal"], ["b", "elite"]] as const) {
    const points = years.flatMap((year, i) => (year[level] === null ? [] : [{ x: x(i), y: y(year[level]!) }]));
    if (points.length > 1) chart.append(s("polyline", { points: points.map((p) => `${p.x},${p.y}`).join(" "), class: `series ${side}` }));
    for (const p of points) {
      chart.append(side === "a" ? s("circle", { cx: p.x, cy: p.y, r: 5, class: "point a" }) : s("rect", { x: p.x - 5, y: p.y - 5, width: 10, height: 10, class: "point b" }));
    }
  }
  return chart;
}

/** A sample of a line and its point, for the legend */
function swatch(side: "a" | "b"): SVGElement {
  return s(
    "svg",
    { viewBox: "0 0 40 16", width: 40, height: 16, class: "swatch", "aria-hidden": "true" },
    s("line", { x1: 2, x2: 38, y1: 8, y2: 8, class: `series ${side}` }),
    side === "a" ? s("circle", { cx: 20, cy: 8, r: 5, class: "point a" }) : s("rect", { x: 15, y: 3, width: 10, height: 10, class: "point b" }),
  );
}

/** The difficulty section: the stars of #263, as the Studio and the Boss Creator rate the setup each event opens as */
function difficultySection(events: readonly PastEvent[], now: number, nameOf: Map<string, string>): HTMLElement {
  const { perBoss, perYear, rated } = difficultyStats(events, now);
  const spread = starSpread(rated);
  const hardest = hardestFirst(rated).slice(0, 5);
  return h(
    "section",
    { class: "block", "aria-labelledby": "stats-difficulty" },
    h("h2", { id: "stats-difficulty" }, MOD_TEXTS.Difficulty),
    h(
      "p",
      {},
      `Each event rated from 1 to ${MAX_STARS} stars, like the Boss Creator and the Studio rate the setup it opens as. `,
      "It is an estimate from the setup's numbers, not how hard the event felt.",
    ),
    h(
      "div",
      { class: "legend" },
      h("span", {}, swatch("a"), ` ${MOD_TEXTS.PastEventNormal} (solid line, round points)`),
      h("span", {}, swatch("b"), " Elite (dashed line, square points)"),
    ),
    figure("Average stars per year", "the numbers are in the table below", difficultyChart(perYear)),
    h(
      "details",
      { class: "towers" },
      h("summary", {}, "The averages per year as a table"),
      table(
        ["Year", `${MOD_TEXTS.PastEventNormal}, average stars`, "Elite, average stars", "Hardest"],
        perYear.map((year) => [year.group, average(year.normal), average(year.elite), hardestText(year.hardest)]),
        "Average stars per year",
      ),
    ),
    h("h3", {}, "Per boss"),
    table(
      ["Boss", `${MOD_TEXTS.PastEventNormal}, average stars`, "Elite, average stars", "Hardest"],
      perBoss.map((boss) => [nameOf.get(boss.group) ?? boss.group, average(boss.normal), average(boss.elite), hardestText(boss.hardest)]),
      "Average stars per boss",
    ),
    h("h3", {}, "How many got each number of stars"),
    table(
      ["Stars", ...Array.from({ length: MAX_STARS }, (_, i) => String(i + 1))],
      [
        [MOD_TEXTS.PastEventNormal, ...spread.normal.slice(1)],
        ["Elite", ...spread.elite.slice(1)],
      ],
      "Events per number of stars",
    ),
    h("h3", {}, "The hardest events"),
    h(
      "ol",
      { class: "stats-hardest" },
      ...hardest.map((r) =>
        h(
          "li",
          {},
          h("a", { href: pastEventLink(r.event) }, levelName(r)),
          `, ${englishDate(r.event.start)}: `,
          h("strong", {}, `${r.stars} of ${MAX_STARS} stars`),
        )),
    ),
    h("p", { class: "small muted" }, "Events the Boss Creator can't open, on a map the game no longer has, aren't rated."),
  );
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
      ["Boss", "Events", "Normal", "Elite", "First", "Last", "Since the last"],
      bosses.map((boss) => [
        boss.name,
        boss.events,
        boss.normal,
        boss.elite,
        englishDate(boss.first.start),
        englishDate(boss.last.start),
        sinceText(boss),
      ]),
      "Events per boss",
    ),
    h("p", { class: "small muted" }, "An event pulled early and run again counts once. Since the last counts from the end of the boss's last event."),
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

  return [summary, perBoss, timeline, difficultySection(events, now, nameOf), scoringSection, mapsSection];
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
