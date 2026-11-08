// The Studio's "Towers" group (#128): the Challenge Editor's 45 towers and heroes, a collapsed list per class like the
// game's. A class's rows are made the first time it is opened. Each tower is a fieldset named after it, with its limit,
// the highest tier of each path and, for the 14 towers that have one, the paragon lock. Text only, no game art.

import { h } from "../dom";
import { NO_AUTOFILL, makeRows, nextId } from "./rows";
import type { StudioSetup } from "./state";
import {
  OTHER_TOWERS,
  PATH_TIERS,
  TOWER_SETS,
  changed,
  highestTier,
  limitMode,
  resetTower,
  resetTowers,
  setBlockedTiers,
  setClassAllowed,
  setParagonLock,
  setTowerLimit,
  towerEntries,
  type LimitMode,
  type TowerEntry,
} from "./towers";

const CLASS_NAMES: Record<string, string> = {
  Hero: "Heroes",
  Primary: "Primary",
  Military: "Military",
  Magic: "Magic",
  Support: "Support",
  [OTHER_TOWERS]: "Other",
};

const LIMITS: { value: LimitMode; label: string }[] = [
  { value: "none", label: "No limit" },
  { value: "notAllowed", label: "Not allowed" },
  { value: "limited", label: "Limited to" },
];

export interface TowersSection {
  root: HTMLElement;
  sync: (setup: StudioSetup) => void;
}

export function setUpTowersSection(
  change: (update: (current: StudioSetup) => StudioSetup) => void,
  current: () => StudioSetup,
): TowersSection {
  const { row, reset } = makeRows(change);
  /** The rows made so far, by tower */
  const syncs = new Map<string, (entry: TowerEntry) => void>();

  function towerRow(entry: TowerEntry): HTMLElement {
    const tower = entry.tower;
    const limitId = nextId("tower");
    const limit = h("select", { id: limitId }, ...LIMITS.map((choice) => h("option", { value: choice.value }, choice.label)));
    const count = h("input", {
      type: "number",
      class: "studio-text studio-number",
      min: "1",
      max: "1000",
      step: "1",
      inputmode: "numeric",
      "aria-label": `${entry.name}: how many`,
      ...NO_AUTOFILL,
    });
    limit.addEventListener("change", () => change((s) => setTowerLimit(s, tower, limit.value as LimitMode)));
    count.addEventListener("change", () => change((s) => setTowerLimit(s, tower, "limited", count.value)));
    count.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        count.blur();
      }
    });

    const rows: HTMLElement[] = [row("Limit", limit, limitId, count)];
    /** The rows that only mean something while the tower is allowed: its paths and paragon lock */
    const allowedOnly: HTMLElement[] = [];

    // Heroes have no upgrade paths or paragon
    const paths = entry.isHero
      ? []
      : [1, 2, 3].map((path) => {
          const id = nextId("path");
          const select = h(
            "select",
            { id },
            ...Array.from({ length: PATH_TIERS + 1 }, (_, i) => PATH_TIERS - i).map((tier) =>
              h("option", { value: String(tier) }, tier === 0 ? "No upgrades" : tier === PATH_TIERS ? "Tier 5 (all)" : `Up to tier ${tier}`),
            ),
          );
          select.addEventListener("change", () => change((s) => setBlockedTiers(s, tower, path, PATH_TIERS - Number(select.value))));
          const pathRow = row(`Path ${path}`, select, id);
          rows.push(pathRow);
          allowedOnly.push(pathRow);
          return select;
        });

    let paragon: HTMLInputElement | null = null;
    if (entry.hasParagon) {
      const id = nextId("paragon");
      const box = h("input", { id, type: "checkbox" });
      box.addEventListener("change", () => change((s) => setParagonLock(s, tower, box.checked)));
      const paragonRow = row("Paragon locked", box, id);
      rows.push(paragonRow);
      allowedOnly.push(paragonRow);
      paragon = box;
    }

    const fieldset = h(
      "fieldset",
      { class: "studio-tower" },
      h("legend", {}, entry.name),
      ...rows,
      h("div", { class: "studio-control" }, reset(entry.name, () => change((s) => resetTower(s, tower)))),
    );

    syncs.set(tower, (now) => {
      limit.value = limitMode(now.max);
      count.hidden = now.max <= 0;
      if (document.activeElement !== count) count.value = now.max > 0 ? String(now.max) : "";
      paths.forEach((select, i) => (select.value = String(highestTier(now.blocked[i]))));
      if (paragon) paragon.checked = now.restrictParagon;
      // A tower that isn't allowed can't be upgraded, so its paths and paragon lock are hidden; what they were set
      // to is kept, for when it is allowed again
      for (const hidden of allowedOnly) hidden.hidden = now.max === 0;
      fieldset.classList.toggle("changed", changed(now));
    });
    return fieldset;
  }

  const sets = [...TOWER_SETS, OTHER_TOWERS].filter((set) => towerEntries(current()).some((entry) => entry.set === set));
  const classes = sets.map((set) => {
    const summary = h("summary", {});
    const details = h("details", { class: "studio-tower-class", "data-set": set }, summary);
    let built = false;
    const allowAll = h("button", { class: "button small", type: "button" }, "Allow all");
    allowAll.addEventListener("click", () => change((s) => setClassAllowed(s, set, true)));
    const allowNone = h("button", { class: "button small orange", type: "button" }, "Allow none");
    allowNone.addEventListener("click", () => change((s) => setClassAllowed(s, set, false)));

    // The rows are made when the class is first opened, so the page doesn't make 45 of them up front
    details.addEventListener("toggle", () => {
      if (!details.open || built) return;
      built = true;
      const entries = towerEntries(current()).filter((entry) => entry.set === set);
      details.append(h("div", { class: "actions" }, allowAll, allowNone), ...entries.map(towerRow));
      sync(current());
    });
    return { set, summary };
  });

  const resetAll = h("button", { class: "button small blue", type: "button" }, "Reset towers");
  resetAll.addEventListener("click", () => change(resetTowers));

  const root = h(
    "fieldset",
    { class: "studio-rules-group studio-towers" },
    h("legend", {}, "Towers"),
    h(
      "p",
      { class: "studio-help" },
      "Each tower's limit, the highest tier of each path and the paragon lock. By default, a hero is allowed only when it is the player's own selected hero.",
    ),
    ...classes.map(({ summary }) => summary.parentElement as HTMLElement),
    h("div", { class: "actions" }, resetAll),
  );

  function sync(setup: StudioSetup): void {
    const entries = towerEntries(setup);
    for (const { set, summary } of classes) {
      const inSet = entries.filter((entry) => entry.set === set);
      const differ = inSet.filter(changed).length;
      summary.textContent = `${CLASS_NAMES[set] ?? set} (${inSet.length}${differ ? `, ${differ} changed` : ""})`;
    }
    for (const entry of entries) syncs.get(entry.tower)?.(entry);
  }

  return { root, sync };
}
