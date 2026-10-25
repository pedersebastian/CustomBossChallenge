// Port of Config/SkullMath.cs and SkullStrip.Format (Menus/SkullStrip.cs)

import { clamp, float, formatDecimals, roundHalfEven } from "./dotnet";

export const MAX_SKULLS = 10;

/** Evenly spaced thresholds, e.g. 3 -> [0.75, 0.5, 0.25]; 0 skulls gives an empty list */
export function evenlySpaced(count: number): number[] {
  count = clamp(count, 0, MAX_SKULLS);
  return Array.from({ length: count }, (_, i) => float(roundHalfEven(1 - (i + 1) / (count + 1), 4)));
}

/** A float as float.TryParse with NumberStyles.Float reads it, or NaN */
function parseFloat32(text: string): number {
  return /^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/.test(text) ? float(Number(text)) : NaN;
}

/**
 * Parses a list like "75, 50,25" into descending thresholds [0.75, 0.5, 0.25]. Values outside 1-99, duplicates and junk
 * are ignored. Returns null when nothing valid remains.
 */
export function parseCustom(text: string | null | undefined): number[] | null {
  if (text == null || text.trim() === "") return null;

  const values: number[] = [];
  for (const part of text.split(/[,; ]/)) {
    if (part === "") continue;
    const value = parseFloat32(part.trim().replace(/%+$/, ""));
    if (Number.isNaN(value) || value < 1 || value > 99) continue;
    const threshold = float(roundHalfEven(value / 100, 4));
    if (!values.includes(threshold)) values.push(threshold);
  }

  values.sort((a, b) => b - a);
  const result = values.slice(0, MAX_SKULLS);
  return result.length === 0 ? null : result;
}

/** Thresholds as the player writes them, e.g. [0.5, 0.335] -> "50, 33.5" */
export const formatCustom = (thresholds: number[]): string =>
  thresholds.map((t) => formatDecimals(roundHalfEven(t * 100, 2), 2)).join(", ");

/** The list as it is understood, or fallback when nothing in it is valid */
export function normalizeCustom(text: string | null | undefined, fallback: string): string {
  const values = parseCustom(text);
  return values ? formatCustom(values) : fallback;
}

/** A skull's health percentage, e.g. 0.3333 -> "33.33%" */
export const formatPercent = (percent: number): string => formatDecimals(roundHalfEven(percent * 100, 2), 2) + "%";
