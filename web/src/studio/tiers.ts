// The Studio's Advanced Mode (#95): a card per tier, the bosses to put on them, and the chosen tier's own Elite, speed,
// health and skulls, like the Boss Creator's Tier Preview and tier picker (Menus/TierStrip.cs, TierBossPicker.cs).
// A boss goes on a tier by dragging it there, by picking a tier and then a boss (on a phone too), or with the arrows on
// a tier's card. Only text and our own marks: no art from the game.

import { SKULL_MODES, SKULL_MODE_NAMES, effectiveSpawnRounds, tierBossName, type SkullMode } from "@cbc/share-code";
import { announce, h } from "../dom";
import { CHOICES, FOLLOWS, MAX_HEALTH, MAX_SPEED, MIN_HEALTH, MIN_SPEED, NORMAL, RANDOM } from "@cbc/share-code/tier-bosses";
import { MAX_SKULLS } from "@cbc/share-code/skulls";
import { MAX_TIERS } from "@cbc/share-code/tiers";
import * as edit from "./state";
import type { StudioSetup } from "./state";
import { makeRows, type Control } from "./rows";

/** The step of a tier's speed and health, like Boss Speed and Boss Health */
const VALUE_STEP = 10;

/** Where a dragged boss's name goes */
const DRAG_TYPE = "text/plain";

const choiceName = (choice: string): string => (choice === RANDOM ? "Random" : choice);

export interface TierEditor {
  root: HTMLElement;
  sync: (setup: StudioSetup) => void;
  /** The tier the panel edits, 1-based */
  selected: () => number;
}

export function setUpTierEditor(
  change: (update: (current: StudioSetup) => StudioSetup) => void,
  current: () => StudioSetup,
  resetAll: () => void,
): TierEditor {
  let selected = 1;
  const { reset, slider, checkbox, select, text } = makeRows(change);

  /** Changes a tier and says what it has now */
  const changeTier = (tier: number, update: (s: StudioSetup) => StudioSetup): void => {
    change(update);
    announce(`Tier ${tier}: ${tierBossName(current().options, tier)}.`);
  };

  const select_ = (tier: number): void => {
    selected = tier;
    sync(current());
  };

  // ---------- The bosses to put on a tier ----------

  const chips = CHOICES.map((choice) => {
    const chip = h("button", { class: "studio-chip", type: "button", draggable: "true", "data-boss": choice }, choiceName(choice));
    chip.addEventListener("click", () => changeTier(selected, (s) => edit.pickTierBoss(s, selected, choice)));
    chip.addEventListener("dragstart", (event) => {
      event.dataTransfer?.setData(DRAG_TYPE, choice);
      if (event.dataTransfer) event.dataTransfer.effectAllowed = "copy";
    });
    return chip;
  });
  const paletteHelp = h("p", { class: "studio-help", id: "studio-palette-help" });
  const palette = h(
    "div",
    { class: "studio-palette", role: "group", "aria-label": "Bosses", "aria-describedby": "studio-palette-help" },
    ...chips,
  );

  // ---------- A card per tier ----------

  const cards = Array.from({ length: MAX_TIERS }, (_, i) => {
    const tier = i + 1;
    const card = h("button", { class: "studio-tier", type: "button", "data-tier": String(tier) });
    card.addEventListener("click", () => select_(tier));
    card.addEventListener("keydown", (event) => {
      if (event.key === "ArrowRight" || event.key === "ArrowLeft") {
        event.preventDefault();
        select_(tier);
        changeTier(tier, (s) => edit.stepTierBoss(s, tier, event.key === "ArrowRight" ? 1 : -1));
      } else if (event.key === "Delete" || event.key === "Backspace") {
        event.preventDefault();
        changeTier(tier, (s) => edit.setTierBoss(s, tier, FOLLOWS));
      }
    });
    card.addEventListener("dragover", (event) => {
      event.preventDefault();
      if (event.dataTransfer) event.dataTransfer.dropEffect = "copy";
      card.classList.add("drop-target");
    });
    card.addEventListener("dragleave", () => card.classList.remove("drop-target"));
    card.addEventListener("drop", (event) => {
      event.preventDefault();
      card.classList.remove("drop-target");
      const choice = event.dataTransfer?.getData(DRAG_TYPE) ?? "";
      if (!CHOICES.includes(choice)) return;
      select_(tier);
      changeTier(tier, (s) => edit.pickTierBoss(s, tier, choice));
    });
    return card;
  });
  const strip = h("div", { class: "studio-tiers", role: "group", "aria-label": "Tiers" }, ...cards);

  // ---------- The chosen tier's own values ----------

  const heading = h("h3", { class: "studio-tier-heading" });
  const tierElite = checkbox("Elite", (s) => edit.shownTier(s, selected).elite, (s, on) => edit.setTierElite(s, selected, on));
  const tierSpeed = slider(
    "Speed",
    { min: MIN_SPEED, max: MAX_SPEED, step: VALUE_STEP },
    (s) => edit.shownTier(s, selected).speed,
    (v) => `${v} %`,
    (s, v) => edit.setTierSpeed(s, selected, v),
    NORMAL,
  );
  const tierHealth = slider(
    "Health",
    { min: MIN_HEALTH, max: MAX_HEALTH, step: VALUE_STEP },
    (s) => edit.shownTier(s, selected).health,
    (v) => `${v} %`,
    (s, v) => edit.setTierHealth(s, selected, v),
    NORMAL,
  );
  const tierSkulls = select(
    "Skulls",
    SKULL_MODES,
    (mode) => SKULL_MODE_NAMES[mode as SkullMode],
    (s) => edit.shownTier(s, selected).skulls,
    (s, mode) => edit.setTierSkullMode(s, selected, mode as SkullMode),
    reset("Skulls", () => change((s) => edit.resetTierSkulls(s, selected))),
  );
  const tierSkullCount = slider(
    "Skull Count",
    { min: 0, max: MAX_SKULLS, step: 1 },
    (s) => edit.shownTier(s, selected).skullCount,
    String,
    (s, v) => edit.setTierSkullCount(s, selected, v),
  );
  const tierCustomSkulls = text(
    "Skulls at Boss HP %",
    "75, 50, 25",
    (s) => edit.shownTier(s, selected).customSkulls,
    (s, typed) => edit.setTierCustomSkulls(s, selected, typed),
  );
  const panelControls: Control[] = [tierElite, tierSpeed, tierHealth, tierSkulls, tierSkullCount, tierCustomSkulls];

  const follow = h("button", { class: "button small blue", type: "button" }, "Follow the setup's boss");
  follow.addEventListener("click", () => changeTier(selected, (s) => edit.setTierBoss(s, selected, FOLLOWS)));
  const resetAllButton = h("button", { class: "button small orange", type: "button" }, "Reset every tier");
  resetAllButton.addEventListener("click", resetAll);

  const panel = h(
    "div",
    { class: "studio-tier-panel", role: "group", "aria-labelledby": "studio-tier-heading" },
    heading,
    ...panelControls.map((control) => control.row),
    h("div", { class: "actions" }, follow, resetAllButton),
  );
  heading.id = "studio-tier-heading";

  const root = h(
    "div",
    { class: "studio-advanced" },
    h("p", { class: "studio-row-title" }, "Bosses per tier"),
    paletteHelp,
    palette,
    strip,
    panel,
  );

  function sync(setup: StudioSetup): void {
    const tiers = setup.options.Tiers;
    if (selected > tiers) selected = tiers;
    const rounds = effectiveSpawnRounds(setup.options);

    cards.forEach((card, i) => {
      const tier = i + 1;
      card.hidden = tier > tiers;
      const shown = edit.shownTier(setup, tier);
      const name = tierBossName(setup.options, tier);
      const values = shown.speed === NORMAL && shown.health === NORMAL ? "" : `${shown.speed} % speed, ${shown.health} % health`;
      const skulls = shown.skulls === "Vanilla" ? "" : shown.skulls === "EvenlySpaced" ? `${shown.skullCount} skulls` : "Custom skulls";
      card.replaceChildren(
        h("span", { class: "studio-tier-number" }, `T${tier}`),
        h("span", { class: "studio-tier-round" }, `R${rounds[i] ?? ""}`),
        h("span", { class: "studio-tier-boss" }, name),
        // Speed and health on one short line; the card's label says them in words
        values && h("span", { class: "studio-tier-values", title: values }, `${shown.speed}%/${shown.health}%`),
        skulls && h("span", { class: "studio-tier-values" }, skulls),
      );
      card.setAttribute("aria-pressed", String(tier === selected));
      card.setAttribute(
        "aria-label",
        `Tier ${tier}, round ${rounds[i] ?? ""}: ${name}${values ? `, ${values}` : ""}${skulls ? `, ${skulls}` : ""}`,
      );
    });

    paletteHelp.textContent = `Drag a boss onto a tier, or pick a tier and then a boss. Now picking for tier ${selected}. On a tier, the arrow keys step its boss and Delete makes it follow the setup's boss.`;
    const chosen = edit.shownTier(setup, selected).boss;
    for (const chip of chips) chip.setAttribute("aria-pressed", String(chip.dataset.boss === chosen));

    heading.textContent = `Tier ${selected}: ${tierBossName(setup.options, selected)}`;
    for (const control of panelControls) control.sync(setup);
    const mode = edit.shownTier(setup, selected).skulls;
    tierSkullCount.row.hidden = mode !== "EvenlySpaced";
    tierCustomSkulls.row.hidden = mode !== "Custom";
  }

  return { root, sync, selected: () => selected };
}
