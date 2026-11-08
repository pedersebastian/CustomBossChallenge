// The spawn-round timeline (#94): when each tier arrives, from the first round to the game's end, so the whole challenge
// can be seen at a glance. The bar is a picture only; the same rounds are in a list for screen readers and as text.

import type { Challenge } from "@cbc/share-code";
import { effectiveSpawnRounds, tierBossName, usesBossEventRounds, type BossOptions } from "@cbc/share-code";
import { h } from "../dom";
import { EVENT_ROUNDS, endRound } from "@cbc/share-code/tiers";

export interface TimelineTier {
  tier: number;
  round: number;
  /** "Elite Lych", "Random", ... */
  boss: string;
  /** With Boss Event Rounds, the event's round this tier plays; null when it has none or the option is off */
  eventRound: number | null;
}

export interface Timeline {
  /** The round the game starts on, or null when the difficulty decides */
  start: number | null;
  /** The round the game ends on: the last tier always gets 20 rounds */
  end: number;
  tiers: TimelineTier[];
}

export function timeline(options: BossOptions, challenge: Challenge): Timeline {
  const rounds = effectiveSpawnRounds(options);
  const events = usesBossEventRounds(options);
  return {
    start: challenge.round >= 0 ? challenge.round : null,
    end: endRound(rounds),
    tiers: rounds.map((round, i) => ({
      tier: i + 1,
      round,
      boss: tierBossName(options, i + 1),
      eventRound: events ? (EVENT_ROUNDS[i] ?? null) : null,
    })),
  };
}

/** Where a round sits on the bar, from 0 to 1 */
export const position = (round: number, first: number, last: number): number =>
  last <= first ? 0 : Math.min(Math.max((round - first) / (last - first), 0), 1);

/**
 * The row of each label, left to right: a label goes on the first row where it starts at least `width` (a share of the
 * bar) after the label before it on that row, so rounds close together don't cover each other
 */
export function labelRows(positions: number[], width: number): number[] {
  const ends: number[] = [];
  return positions.map((left) => {
    let row = ends.findIndex((end) => left >= end);
    if (row < 0) row = ends.length;
    ends[row] = left + width;
    return row;
  });
}

/** How much of the bar a label takes; a label is two short lines like "T3" over "R80" */
const LABEL_WIDTH = 0.13;
/** The height of one row of labels, in px */
const ROW_HEIGHT = 40;

/** What screen readers and the list say about a tier */
export function tierLine(tier: TimelineTier): string {
  const event = tier.eventRound === null ? "" : ` (plays the event's round ${tier.eventRound})`;
  return `Tier ${tier.tier}: round ${tier.round}, ${tier.boss}${event}`;
}

export function renderTimeline(options: BossOptions, challenge: Challenge): HTMLElement {
  const line = timeline(options, challenge);
  const first = line.start ?? 1;
  const marks = [
    ...line.tiers.map((tier) => ({ at: position(tier.round, first, line.end), top: `T${tier.tier}`, bottom: `R${tier.round}`, kind: "tier" })),
    { at: 1, top: "End", bottom: `R${line.end}`, kind: "end" },
  ];
  if (line.start !== null) marks.unshift({ at: 0, top: "Start", bottom: `R${line.start}`, kind: "start" });
  const rows = labelRows(marks.map((mark) => mark.at), LABEL_WIDTH);

  const bar = h("div", { class: "timeline-bar", "aria-hidden": "true" });
  marks.forEach((mark, i) => {
    const marker = h(
      "div",
      { class: `timeline-mark ${mark.kind}` },
      h("span", { class: "timeline-label" }, h("strong", {}, mark.top), h("span", {}, mark.bottom)),
    );
    marker.style.left = `${mark.at * 100}%`;
    marker.style.setProperty("--row", String(rows[i]));
    bar.append(marker);
  });
  bar.style.marginBottom = `${(Math.max(...rows) + 1) * ROW_HEIGHT}px`;

  const events = line.tiers.some((tier) => tier.eventRound !== null);
  return h(
    "div",
    { class: "block timeline" },
    h("h4", {}, "Timeline"),
    bar,
    h(
      "ol",
      { class: "timeline-list" },
      ...(line.start === null ? [] : [h("li", {}, `The game starts on round ${line.start}`)]),
      ...line.tiers.map((tier) => h("li", {}, tierLine(tier))),
      h("li", {}, `The game ends on round ${line.end}`),
    ),
    events && line.tiers.length > EVENT_ROUNDS.length
      ? h("p", { class: "muted" }, `Tiers above ${EVENT_ROUNDS.length} have no event round of their own.`)
      : null,
  );
}
