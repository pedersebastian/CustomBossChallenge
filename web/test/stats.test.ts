// @vitest-environment happy-dom
// The Stats page (#265): its numbers from a small archive made here, and the page built from boss-events.json

import { afterEach, describe, expect, it } from "vitest";
import type { PastEvent, Scoring } from "@cbc/share-code";
import { h } from "../src/dom";
import {
  countedEvents,
  eventsPerYear,
  mapsAndModes,
  mapsOf,
  scoringPerYear,
  statsPerBoss,
} from "../src/stats/compute";
import { setUpStats, sinceText, topList } from "../src/stats/page";

const DAY = 24 * 60 * 60 * 1000;
const at = (date: string): number => Date.parse(`${date}T00:00:00Z`);

let made = 0;
function event(
  boss: string,
  start: string,
  { days = 5, map = "Cubism", eliteMap = map, mode = "Standard", eliteMode = mode, normal = null, elite = null, replacedBy = [], replaces = [], id }: {
    days?: number;
    map?: string;
    eliteMap?: string;
    mode?: string;
    eliteMode?: string;
    normal?: Scoring | null;
    elite?: Scoring | null;
    replacedBy?: string[];
    replaces?: string[];
    id?: string;
  } = {},
): PastEvent {
  const name = `${boss[0].toUpperCase()}${boss.slice(1)}${++made}`;
  return {
    id: id ?? name,
    name,
    displayName: name,
    bossKey: boss,
    start: at(start),
    end: at(start) + days * DAY,
    normalScoring: normal,
    eliteScoring: elite,
    datesGuessed: false,
    standard: { map, mode, difficulty: "Hard" },
    elite: { map: eliteMap, mode: eliteMode, difficulty: "Hard" },
    replaces,
    replacedBy,
    license: null,
    sourceUrls: [],
  };
}

const EVENTS: PastEvent[] = [
  event("lych", "2022-03-01", { map: "Ravine", normal: "LeastCash", elite: "LeastTiers" }),
  event("bloonarius", "2021-07-24", { map: "Ravine" }),
  event("bloonarius", "2021-09-01", { map: "Cubism", eliteMap: "Ravine", mode: "Clicks", eliteMode: "Standard" }),
  // Pulled after a few hours and run again: counts once, as the one that replaced it
  event("vortex", "2023-03-18", { id: "Pulled", days: 0, replacedBy: ["Again"], normal: "GameTime", elite: "GameTime" }),
  event("vortex", "2023-03-20", { id: "Again", replaces: ["Pulled"], normal: "GameTime", elite: "LeastCash" }),
  event("bloonarius", "2022-01-10", { map: "Logs", normal: "GameTime", elite: "GameTime" }),
  event("lych", "2023-05-01", { map: "Logs", normal: "LeastCash", elite: "LeastCash" }),
];

const NOW = at("2023-05-03");

describe("the numbers", () => {
  it("counts an event pulled and run again once, and sorts the rest oldest first", () => {
    const counted = countedEvents(EVENTS);
    expect(counted).toHaveLength(6);
    expect(counted.map((e) => e.id)).not.toContain("Pulled");
    expect(counted.map((e) => e.start)).toEqual([...counted.map((e) => e.start)].sort((a, b) => a - b));
  });

  it("gives each boss its events, normal and Elite, the most first", () => {
    const bosses = statsPerBoss(EVENTS, NOW);
    expect(bosses.map((b) => [b.name, b.events, b.normal, b.elite])).toEqual([
      ["Bloonarius", 3, 3, 3],
      ["Lych", 2, 2, 2],
      ["Vortex", 1, 1, 1],
    ]);
  });

  it("counts the days since a boss's last event, and knows one that is on", () => {
    const bosses = new Map(statsPerBoss(EVENTS, NOW).map((b) => [b.key, b]));
    // Bloonarius's last ended 15 Jan 2022: 473 days before 3 May 2023
    expect(bosses.get("bloonarius")!.daysSince).toBe(473);
    expect(bosses.get("vortex")!.daysSince).toBe(39);
    // Lych 1 May to 6 May 2023 is on at 3 May
    expect(bosses.get("lych")!.daysSince).toBeNull();
    expect(sinceText(bosses.get("lych")!)).toBe("On now");
    expect(sinceText(bosses.get("vortex")!)).toBe("39 days");
    expect(sinceText(statsPerBoss(EVENTS, at("2023-03-25") + 3600_000).find((b) => b.key === "vortex")!)).toBe("Ended today");
  });

  it("finds the longest wait between two events of a boss, from the end of one to the start of the next", () => {
    const bosses = new Map(statsPerBoss(EVENTS, NOW).map((b) => [b.key, b]));
    // Bloonarius: 29 Jul - 1 Sep 2021 is 34 days, 6 Sep 2021 - 10 Jan 2022 is 126
    const gap = bosses.get("bloonarius")!.longestGap!;
    expect(gap.days).toBe(126);
    expect([gap.from.start, gap.to.start]).toEqual([at("2021-09-01"), at("2022-01-10")]);
    expect(bosses.get("lych")!.longestGap!.days).toBe(421);
    // One event, no wait
    expect(bosses.get("vortex")!.longestGap).toBeNull();
  });

  it("counts an event's map once, or once for each of two maps", () => {
    expect(mapsOf(EVENTS[1])).toEqual(["Ravine"]);
    expect(mapsOf(EVENTS[2])).toEqual(["Cubism", "Ravine"]);
  });

  it("ranks the maps and modes for every event, per boss and per year", () => {
    const usage = mapsAndModes(EVENTS, NOW);
    expect(usage.all.events).toBe(6);
    expect(usage.all.maps).toEqual([
      { id: "Ravine", count: 3 },
      { id: "Logs", count: 2 },
      { id: "Cubism", count: 2 },
    ].sort((a, b) => b.count - a.count || (a.id < b.id ? -1 : 1)));
    expect(usage.all.modes).toEqual([{ id: "Standard", count: 6 }, { id: "Clicks", count: 1 }]);
    expect(usage.perBoss.map((u) => [u.group, u.events])).toEqual([["bloonarius", 3], ["lych", 2], ["vortex", 1]]);
    expect(usage.perYear.map((u) => [u.group, u.events])).toEqual([["2023", 2], ["2022", 2], ["2021", 2]]);
    expect(usage.perYear[2].maps).toEqual([{ id: "Ravine", count: 2 }, { id: "Cubism", count: 1 }]);
  });

  it("shows the most used by the names the game shows", () => {
    expect(topList([{ id: "Clicks", count: 3 }, { id: "Cubism", count: 2 }])).toBe("CHIMPS (3), Cubism (2)");
    expect(topList([])).toBe("–");
  });

  it("counts the scoring types of each year, normal and Elite, with the unknown ones", () => {
    const years = scoringPerYear(EVENTS);
    expect(years.map((y) => y.year)).toEqual([2021, 2022, 2023]);
    expect(years[0].normal).toEqual({ GameTime: 0, LeastCash: 0, LeastTiers: 0, Unknown: 2 });
    expect(years[1].normal).toEqual({ GameTime: 1, LeastCash: 1, LeastTiers: 0, Unknown: 0 });
    expect(years[1].elite).toEqual({ GameTime: 1, LeastCash: 0, LeastTiers: 1, Unknown: 0 });
    // The pulled Vortex isn't counted: its replacement's Elite was Least Cash
    expect(years[2].elite).toEqual({ GameTime: 0, LeastCash: 2, LeastTiers: 0, Unknown: 0 });
  });

  it("counts each boss's events per year for the timeline's table", () => {
    expect(eventsPerYear(EVENTS, NOW)).toEqual({
      years: [2021, 2022, 2023],
      rows: [
        { key: "bloonarius", name: "Bloonarius", counts: [2, 1, 0] },
        { key: "lych", name: "Lych", counts: [0, 1, 1] },
        { key: "vortex", name: "Vortex", counts: [0, 0, 1] },
      ],
    });
  });

  it("follows the archive: a new event changes the numbers", () => {
    const more = [...EVENTS, event("vortex", "2023-04-20", { normal: "LeastTiers", elite: "LeastTiers" })];
    const vortex = statsPerBoss(more, NOW).find((b) => b.key === "vortex")!;
    expect(vortex.events).toBe(2);
    expect(vortex.daysSince).toBe(8);
    expect(scoringPerYear(more)[2].normal.LeastTiers).toBe(1);
  });
});

const file = (events: PastEvent[]) =>
  JSON.stringify({
    formatVersion: 1,
    generated: "2023-05-02T00:00:00.000Z",
    license: "test",
    events: events.map((e) => ({
      formatVersion: 1,
      id: e.id,
      name: e.name,
      displayName: e.displayName,
      bossType: e.bossKey,
      start: e.start,
      end: e.end,
      normalScoringType: e.normalScoring ?? undefined,
      eliteScoringType: e.eliteScoring ?? undefined,
      standard: e.standard,
      elite: e.elite,
      replaces: e.replaces,
      replacedBy: e.replacedBy,
    })),
  });

async function page(load: () => Promise<string>) {
  const root = h("div");
  document.body.append(root);
  await setUpStats(root, load, () => NOW);
  return root;
}

afterEach(() => document.body.replaceChildren());

describe("the Stats page", () => {
  it("shows the charts with their tables, from the file", async () => {
    const root = await page(async () => file(EVENTS));
    expect(root.querySelector(".stats-summary")!.textContent).toContain("6 events from 24 Jul 2021 to 1 May 2023, with 3 bosses");
    // Every chart is hidden from screen readers and has a table
    const charts = root.querySelectorAll("svg.curve-chart");
    expect(charts).toHaveLength(4);
    charts.forEach((chart) => expect(chart.getAttribute("aria-hidden")).toBe("true"));
    expect(root.querySelectorAll("table")).toHaveLength(5);
    const bossRows = [...root.querySelectorAll("#stats-bosses ~ .table-scroll tbody tr")].map((row) =>
      [...row.children].map((cell) => cell.textContent));
    expect(bossRows[0]).toEqual(["Bloonarius", "3", "3", "3", "24 Jul 2021", "10 Jan 2022", "473 days", "126 days, 6 Sep 2021 - 10 Jan 2022"]);
    expect(bossRows[1][6]).toBe("On now");
    // One mark per counted event on the timeline
    expect(root.querySelectorAll(".stats-timeline rect")).toHaveLength(6);
  });

  it("says when the events can't be loaded", async () => {
    const root = await page(async () => {
      throw new Error("offline");
    });
    expect(root.querySelector(".error")!.textContent).toContain("Try again later");
  });

  it("asks for a reload when the file is from a newer website", async () => {
    const root = await page(async () => JSON.stringify({ formatVersion: 99, generated: "2023-05-02T00:00:00.000Z", events: [] }));
    expect(root.querySelector(".warning")!.textContent).toContain("Reload");
  });
});
