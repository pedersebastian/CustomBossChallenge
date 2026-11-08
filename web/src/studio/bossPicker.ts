// The Studio's boss row (#133), like the Boss Creator's: a button per boss with a tick on the chosen one, then Elite and
// Reset. Each button is our own badge (the boss's first letter in the mod's own colour for that boss, from
// Config/TierLook.cs), not a portrait from the game (#96). A radio group: one tab stop, and the arrow keys, Home and End
// pick a boss, like the Boss Creator's arrows.

import { BOSSES } from "@cbc/share-code";
import { h } from "../dom";
import { TIER_LOOK } from "../data/types";
import { icon } from "./icons";
import { breakable } from "../preview/render";
import { nextId, type Control } from "./rows";
import * as edit from "./state";
import type { StudioSetup } from "./state";

/** The colour of a boss's badge: the first of its tier colours */
export const bossColour = (boss: string): string => (TIER_LOOK.themes[boss] ?? TIER_LOOK.defaultTheme)[0];

export function setUpBossPicker(
  change: (update: (current: StudioSetup) => StudioSetup) => void,
  reset: (label: string, onReset: () => void) => HTMLButtonElement,
): Control & { buttons: HTMLButtonElement[]; elite: HTMLInputElement } {
  const labelId = nextId("boss-label");

  const buttons = BOSSES.map((boss) => {
    const button = h(
      "button",
      { class: "studio-boss", type: "button", role: "radio", "data-boss": boss, "aria-label": boss, title: boss },
      h("span", { class: "studio-boss-badge", style: `--boss: ${bossColour(boss)}`, "aria-hidden": "true" }, boss.slice(0, 1)),
      // Long names may break where their parts meet, "Blasta-popoulos", never cut off
      h("span", { class: "studio-boss-name", "aria-hidden": "true" }, breakable(boss)),
    );
    button.addEventListener("click", () => change((s) => edit.setBoss(s, boss)));
    button.addEventListener("keydown", (event) => {
      const i = BOSSES.indexOf(boss);
      const next =
        event.key === "ArrowRight" || event.key === "ArrowDown" ? (i + 1) % BOSSES.length
        : event.key === "ArrowLeft" || event.key === "ArrowUp" ? (i - 1 + BOSSES.length) % BOSSES.length
        : event.key === "Home" ? 0
        : event.key === "End" ? BOSSES.length - 1
        : -1;
      if (next < 0) return;
      event.preventDefault();
      change((s) => edit.setBoss(s, BOSSES[next]));
      buttons[next].focus();
    });
    return button;
  });

  const eliteId = nextId("elite");
  const elite = h("input", { id: eliteId, type: "checkbox" });
  elite.addEventListener("change", () => change((s) => edit.setElite(s, elite.checked)));

  const row = h(
    "div",
    { class: "studio-row studio-boss-row" },
    icon("boss"),
    h("label", { id: labelId }, "Boss"),
    h(
      "div",
      { class: "studio-control" },
      h("div", { class: "studio-bosses", role: "radiogroup", "aria-labelledby": labelId }, ...buttons),
      h("label", { class: "toggle studio-elite", for: eliteId }, elite, "Elite"),
      reset("Boss", () => change(edit.resetBoss)),
    ),
  );

  return {
    row,
    buttons,
    elite,
    sync: (s) => {
      for (const button of buttons) {
        const chosen = button.dataset.boss === s.options.Boss;
        button.setAttribute("aria-checked", String(chosen));
        // One tab stop: the chosen boss
        button.tabIndex = chosen ? 0 : -1;
        button.classList.toggle("elite", chosen && s.options.Elite);
      }
      elite.checked = s.options.Elite;
    },
  };
}
