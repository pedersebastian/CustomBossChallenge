// The boss's health and speed per tier of two setups in one chart. A and B differ in more than colour (issue #18): A is
// a solid line with round points, B a dashed line with square ones, and the values are also there as a table.

import { h } from "../dom";
import { formatDecimals } from "../math/dotnet";
import { formatHealth } from "../math/tiers";
import type { TierCard } from "../preview/compute";
import type { tierPairs } from "./diff";

type Pairs = ReturnType<typeof tierPairs>;
type Side = "a" | "b";

const SVG = "http://www.w3.org/2000/svg";

function s<K extends keyof SVGElementTagNameMap>(tag: K, attributes: Record<string, string | number>, ...children: (Node | string)[]): SVGElementTagNameMap[K] {
  const element = document.createElementNS(SVG, tag);
  for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, String(value));
  element.append(...children);
  return element;
}

const WIDTH = 420;
const HEIGHT = 230;
const LEFT = 56;
const RIGHT = 18;
const TOP = 16;
const BOTTOM = 38;

interface Axis {
  /** From a value to a height in the chart */
  y: (value: number) => number;
  ticks: { value: number; label: string }[];
}

/** Powers of ten, since health grows by much more than speed: a 10 tier setup can go from 20k to billions */
function logAxis(values: number[]): Axis {
  let low = Math.floor(Math.log10(Math.min(...values)));
  let high = Math.ceil(Math.log10(Math.max(...values)));
  if (high === low) [low, high] = [low - 0.5, high + 0.5];
  const step = high - low > 6 ? 2 : 1;
  const ticks = [];
  for (let power = Math.ceil(low); power <= high; power += step) ticks.push({ value: 10 ** power, label: formatHealth(10 ** power) });
  return { y: (value) => TOP + (1 - (Math.log10(value) - low) / (high - low)) * (HEIGHT - TOP - BOTTOM), ticks };
}

/** The smallest round step (1, 2, 2.5 or 5 times a power of ten, at least 0.25) that needs at most 6 of them up to max */
export function roundStep(max: number): number {
  for (let power = 10 ** Math.floor(Math.log10(Math.max(max, 1e-9) / 6)); ; power *= 10) {
    const step = [1, 2, 2.5, 5].map((factor) => factor * power).find((candidate) => candidate >= 0.25 && max / candidate <= 6);
    if (step !== undefined) return step;
  }
}

/** From 0 up, in a round step; at most 7 ticks however fast a setup gets (#113) */
export function linearAxis(values: number[]): Axis {
  const max = Math.max(...values, 1) * 1.1;
  const step = roundStep(max);
  const high = Math.ceil(max / step) * step;
  const ticks = [];
  for (let value = 0; value <= high + 1e-9; value += step) ticks.push({ value, label: `×${formatDecimals(value, 2)}` });
  return { y: (value) => TOP + (1 - value / high) * (HEIGHT - TOP - BOTTOM), ticks };
}

const POINT = 5;

function marker(side: Side, x: number, y: number, title: string): SVGElement {
  const label = s("title", {}, title);
  return side === "a"
    ? s("circle", { cx: x, cy: y, r: POINT, class: "point a" }, label)
    : s("rect", { x: x - POINT, y: y - POINT, width: POINT * 2, height: POINT * 2, class: "point b" }, label);
}

function chart(pairs: Pairs, title: string, value: (card: TierCard) => number | null, axisFor: (values: number[]) => Axis, text: (card: TierCard) => string): HTMLElement {
  const values = pairs.flatMap((pair) => [pair.a, pair.b]).flatMap((card) => (card ? [value(card)] : [])).filter((v): v is number => v !== null && v > 0);
  // The chart itself is hidden from screen readers, so they are told where the values are
  const figure = h("figure", { class: "curve" }, h("figcaption", {}, title, h("span", { class: "sr-only" }, " (the values are in the table below)")));
  if (values.length === 0) return figure;

  const axis = axisFor(values);
  const count = pairs.length;
  const x = (tier: number): number => (count === 1 ? (LEFT + WIDTH - RIGHT) / 2 : LEFT + ((tier - 1) / (count - 1)) * (WIDTH - LEFT - RIGHT));

  const svg = s("svg", { viewBox: `0 0 ${WIDTH} ${HEIGHT}`, class: "curve-chart", "aria-hidden": "true" });
  for (const tick of axis.ticks) {
    const y = axis.y(tick.value);
    svg.append(
      s("line", { x1: LEFT, x2: WIDTH - RIGHT, y1: y, y2: y, class: "grid" }),
      s("text", { x: LEFT - 8, y: y + 4, class: "tick", "text-anchor": "end" }, tick.label),
    );
  }
  for (const pair of pairs) {
    svg.append(s("text", { x: x(pair.tier), y: HEIGHT - BOTTOM + 22, class: "tick", "text-anchor": "middle" }, `T${pair.tier}`));
  }

  // A first and wider, so B's dashes stay visible on top of it where the two are the same
  for (const side of ["a", "b"] as const) {
    const points = pairs.flatMap((pair) => {
      const card = pair[side];
      const v = card ? value(card) : null;
      return card && v !== null && v > 0 ? [{ card, x: x(pair.tier), y: axis.y(v) }] : [];
    });
    if (points.length > 1) svg.append(s("polyline", { points: points.map((p) => `${p.x},${p.y}`).join(" "), class: `series ${side}` }));
    for (const point of points) svg.append(marker(side, point.x, point.y, `${side.toUpperCase()}, tier ${point.card.tier}: ${text(point.card)}`));
  }

  figure.append(svg);
  return figure;
}

/** A sample of a series' line and point, for the legend */
function swatch(side: Side): SVGElement {
  return s(
    "svg",
    { viewBox: "0 0 40 16", width: 40, height: 16, class: "swatch", "aria-hidden": "true" },
    s("line", { x1: 2, x2: 38, y1: 8, y2: 8, class: `series ${side}` }),
    side === "a" ? s("circle", { cx: 20, cy: 8, r: POINT, class: "point a" }) : s("rect", { x: 15, y: 3, width: 10, height: 10, class: "point b" }),
  );
}

export function renderCurves(pairs: Pairs, names: { a: string; b: string }): HTMLElement {
  const health = (card: TierCard): string => (card.capped ? `${card.health} (the game's limit)` : card.health);
  const speed = (card: TierCard): string => `×${formatDecimals(card.speed, 2)}`;

  const legend = h(
    "div",
    { class: "legend" },
    h("span", {}, swatch("a"), h("strong", {}, "A"), ` ${names.a} (solid line, round points)`),
    h("span", {}, swatch("b"), h("strong", {}, "B"), ` ${names.b} (dashed line, square points)`),
  );

  const cell = (card: TierCard | null, read: (card: TierCard) => string): string => (card ? read(card) : "–");
  const table = h(
    "table",
    { class: "compare-table" },
    h("thead", {}, h("tr", {}, h("th", { scope: "col" }, "Tier"), h("th", { scope: "col" }, "A round"), h("th", { scope: "col" }, "B round"), h("th", { scope: "col" }, "A health"), h("th", { scope: "col" }, "B health"), h("th", { scope: "col" }, "A speed"), h("th", { scope: "col" }, "B speed"))),
    h(
      "tbody",
      {},
      ...pairs.map((pair) =>
        h(
          "tr",
          {},
          h("th", { scope: "row" }, `T${pair.tier}`),
          h("td", {}, cell(pair.a, (c) => `R${c.round}`)),
          h("td", {}, cell(pair.b, (c) => `R${c.round}`)),
          h("td", {}, cell(pair.a, health)),
          h("td", {}, cell(pair.b, health)),
          h("td", {}, cell(pair.a, speed)),
          h("td", {}, cell(pair.b, speed)),
        ),
      ),
    ),
  );

  return h(
    "div",
    { class: "block" },
    h("h2", {}, "Tier curves"),
    legend,
    h(
      "div",
      { class: "curves" },
      chart(pairs, "Boss health per tier", (card) => card.healthValue, logAxis, health),
      chart(pairs, "Boss speed per tier, compared with the normal game", (card) => card.speed, linearAxis, speed),
    ),
    h("details", { class: "towers" }, h("summary", {}, "The values as a table"), h("div", { class: "table-scroll" }, table)),
  );
}
