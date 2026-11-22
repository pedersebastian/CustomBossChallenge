// The difficulty rating next to a setup (#263), like the Boss Creator's row under its Tier Preview (Menus/DifficultyStars.cs)

import type { BossOptions, Challenge } from "@cbc/share-code";
import { difficultyPoints, MAX_STARS, starsOf } from "@cbc/share-code/difficulty";
import { factsOf } from "@cbc/share-code/warnings";
import { h } from "../dom";
import { MOD_TEXTS } from "../modTexts";
import { tierBases } from "./warnings";

/** The setup's score in points and its stars, 1 to 5 */
export function difficultyOf(options: BossOptions, challenge: Challenge): { points: number; stars: number } {
  const points = difficultyPoints(factsOf(options, challenge, tierBases(options)));
  return { points, stars: starsOf(points) };
}

/** What a screen reader says for the stars */
export const starsText = (stars: number): string => `${MOD_TEXTS.Difficulty}: ${stars} of ${MAX_STARS} stars, an estimate`;

let panels = 0;

/**
 * The stars, lit in gold and the rest dimmed, with an i like the Studio's that says what they are: an estimate
 * (MOD_TEXTS.DifficultyInfo)
 */
export function renderDifficulty(options: BossOptions, challenge: Challenge): HTMLElement {
  const { stars } = difficultyOf(options, challenge);
  const shown = h("span", { class: "difficulty-stars", role: "img", "aria-label": starsText(stars) });
  for (let i = 1; i <= MAX_STARS; i++) shown.append(h("span", { class: i <= stars ? "star on" : "star", "aria-hidden": "true" }, "★"));

  const panelId = `difficulty-info-${++panels}`;
  const button = h(
    "button",
    {
      class: "studio-info",
      type: "button",
      "aria-label": `About ${MOD_TEXTS.Difficulty}`,
      "aria-expanded": "false",
      "aria-controls": panelId,
      title: `About ${MOD_TEXTS.Difficulty}`,
    },
    "i",
  );
  const panel = h("p", { class: "studio-info-text difficulty-text", id: panelId }, MOD_TEXTS.DifficultyInfo);
  panel.hidden = true;
  button.addEventListener("click", () => {
    panel.hidden = !panel.hidden;
    button.setAttribute("aria-expanded", String(!panel.hidden));
  });

  return h(
    "div",
    { class: "difficulty", "data-stars": String(stars) },
    h("div", { class: "difficulty-row" }, h("span", { class: "difficulty-label", "aria-hidden": "true" }, MOD_TEXTS.Difficulty), shown, button),
    panel,
  );
}
