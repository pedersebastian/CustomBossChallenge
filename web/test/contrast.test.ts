// Text colours against the backgrounds they are shown on: at least 4.5:1, WCAG AA (#113)

import { describe, expect, it } from "vitest";
import css from "../src/style.css?raw";
import { TIER_LOOK } from "../src/data/types";

/** A colour token from :root, e.g. --panel-inner */
function token(name: string): string {
  const match = new RegExp(`${name}:\\s*(#[0-9a-fA-F]{6})`).exec(css);
  if (!match) throw new Error(`${name} not found in style.css`);
  return match[1];
}

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

export function contrast(a: string, b: string): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (light + 0.05) / (dark + 0.05);
}

describe("contrast", () => {
  it.each([
    // Text on the panels and on a setup
    ["--text", "--panel"],
    ["--text", "--panel-inner"],
    ["--muted", "--panel-inner"],
    // The health limit note on a setup
    ["--harder-text", "--panel-inner"],
    // The tier cards' arrows
    ["--harder", "--card"],
    ["--easier", "--card"],
    // The Studio's lists, open (#97): the choices, the picked one and the group names
    ["--text", "--card"],
    ["--outline", "--gold"],
    ["--gold", "--card"],
    ["--muted", "--card"],
  ])("%s on %s is at least 4.5:1", (text, background) => {
    expect(contrast(token(text), token(background))).toBeGreaterThanOrEqual(4.5);
  });

  // The Studio like the Boss Creator (#133): the group headers' text, the tick and the slider knob
  it.each([
    ["--text", "--sea"],
    ["--outline", "--green"],
  ])("%s on %s is at least 4.5:1", (text, background) => {
    expect(contrast(token(text), token(background))).toBeGreaterThanOrEqual(4.5);
  });

  // Parts of controls need 3:1 (WCAG 1.4.11): the slider knob against its track, and the checked box against the card
  it.each([
    ["--gold", "--sea"],
    ["--green", "--card"],
  ])("%s against %s is at least 3:1", (part, background) => {
    expect(contrast(token(part), token(background))).toBeGreaterThanOrEqual(3);
  });

  // Each boss button's letter is dark on the boss's own colour
  it.each(Object.entries(TIER_LOOK.themes).map(([boss, colours]) => [boss, colours[0]]))("%s's badge letter is readable", (_, colour) => {
    expect(contrast(token("--outline"), colour)).toBeGreaterThanOrEqual(4.5);
  });

  it("the health note uses the lighter colour", () => expect(css).toMatch(/\.note\.harder\s*\{\s*color:\s*var\(--harder-text\)/));
});
