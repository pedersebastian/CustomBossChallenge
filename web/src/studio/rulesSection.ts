// The Studio's "Challenge rules" section (#97): a control per rule in rules.ts, in four groups like the Challenge
// Editor's. Text and number fields are read when they are left or Enter is pressed, and show the value as it is kept.

import { effectiveSpawnRounds } from "@cbc/share-code";
import { h } from "../dom";
import { endRound } from "@cbc/share-code/tiers";
import { NO_AUTOFILL, makeRows, nextId } from "./rows";
import { MAP_GROUPS, OTHER_MAPS, RULES, percentText, resetRule, ruleInput, ruleValue, setRule, type Choice, type Rule, type RuleGroup } from "./rules";
import type { StudioSetup } from "./state";
import { setUpTowersSection } from "./towersSection";

const GROUPS: { group: RuleGroup; title: string }[] = [
  { group: "Game", title: "Game" },
  { group: "Start", title: "Start" },
  { group: "Bloons", title: "Bloons" },
  { group: "Rules", title: "Rules" },
];

export interface RulesSection {
  root: HTMLElement;
  sync: (setup: StudioSetup) => void;
}

export function setUpRulesSection(
  change: (update: (current: StudioSetup) => StudioSetup) => void,
  current: () => StudioSetup,
): RulesSection {
  const { row, reset } = makeRows(change);
  const syncs: ((setup: StudioSetup) => void)[] = [];

  /** Reads a field when it is left, or on Enter */
  const readOnLeave = (input: HTMLInputElement, read: () => void): void => {
    input.addEventListener("change", read);
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        input.blur();
      }
    });
  };

  function control(rule: Rule): HTMLElement {
    // A neutral id: "name" in it would be one more hint to a password manager
    const id = nextId("rule");
    const set = (value: string | boolean) => change((s) => setRule(s, rule, ruleInput(rule, value)));
    const resetButton = () => reset(rule.label, () => change((s) => resetRule(s, rule)));

    switch (rule.kind) {
      case "toggle": {
        const input = h("input", { id, type: "checkbox" });
        input.addEventListener("change", () => set(input.checked));
        syncs.push((s) => (input.checked = ruleValue(s, rule) === true));
        return row(rule.label, input, id);
      }
      case "choice": {
        const option = (choice: Choice) => h("option", { value: choice.value, disabled: choice.disabled ? "" : undefined }, choice.label);
        // Choices with a group, like the maps, are shown under it, easiest group first and the rest after
        const order = (group: string | undefined): number => {
          const index = MAP_GROUPS.indexOf(group ?? "");
          return index < 0 ? MAP_GROUPS.length : index;
        };
        const groups = [...new Set(rule.choices.map((choice) => choice.group))].sort((a, b) => order(a) - order(b));
        const input = h(
          "select",
          { id },
          ...(groups.every((group) => group === undefined)
            ? rule.choices.map(option)
            : groups.map((group) => h("optgroup", { label: group ?? OTHER_MAPS }, ...rule.choices.filter((c) => c.group === group).map(option)))),
        );
        // A value the website's data doesn't know yet, e.g. a map from a newer game, is kept and shown as it is
        const unknown = h("option", { hidden: "" });
        input.prepend(unknown);
        input.addEventListener("change", () => set(input.value));
        syncs.push((s) => {
          const value = String(ruleValue(s, rule));
          const known = rule.choices.some((choice) => choice.value === value);
          unknown.hidden = known;
          unknown.value = known ? "" : value;
          unknown.textContent = known ? "" : `${value} (not known to the website yet)`;
          input.value = value;
        });
        return row(rule.label, input, id, resetButton());
      }
      case "text": {
        const input = h("input", {
          id,
          type: "text",
          class: "studio-text",
          maxlength: String(rule.maxLength),
          placeholder: "Your own name in the game",
          spellcheck: "false",
          ...NO_AUTOFILL,
        });
        readOnLeave(input, () => set(input.value));
        syncs.push((s) => {
          if (document.activeElement !== input) input.value = String(ruleValue(s, rule) ?? "");
        });
        return row(rule.label, input, id, resetButton());
      }
      case "count": {
        const input = h("input", {
          id,
          type: "number",
          class: "studio-text studio-number",
          min: String(rule.min),
          max: String(rule.max),
          step: "1",
          inputmode: "numeric",
          placeholder: "Default",
          ...NO_AUTOFILL,
        });
        readOnLeave(input, () => set(input.value));
        syncs.push((s) => {
          if (document.activeElement === input) return;
          const value = Number(ruleValue(s, rule));
          input.value = value < 0 ? "" : String(value);
        });
        return row(rule.label, input, id, h("span", { class: "studio-value", title: "Empty for the game's default" }, `${rule.min}–${rule.max.toLocaleString("en")}`), resetButton());
      }
      case "percent": {
        const input = h("input", {
          id,
          type: "number",
          class: "studio-text studio-number",
          min: String(rule.min * 100),
          max: String(rule.max * 100),
          step: "any",
          inputmode: "decimal",
          ...NO_AUTOFILL,
        });
        readOnLeave(input, () => set(input.value));
        syncs.push((s) => {
          if (document.activeElement !== input) input.value = percentText(Number(ruleValue(s, rule)));
        });
        return row(rule.label, input, id, h("span", { class: "studio-value" }, "%"), resetButton());
      }
    }
  }

  // In a boss game the end round always follows the last tier (ConfigApplier.Prepare), so it is shown, not edited
  const end = h("p", { class: "studio-help" });
  syncs.push((s) => (end.textContent = `The game ends on round ${endRound(effectiveSpawnRounds(s.options))}, 20 rounds after the last tier.`));

  // The towers and heroes, after the other rules (#128)
  const towers = setUpTowersSection(change, current);

  const groups = GROUPS.map(({ group, title }) =>
    h(
      "fieldset",
      { class: "studio-rules-group" },
      h("legend", {}, title),
      ...RULES.filter((rule) => rule.group === group).map(control),
      group === "Start" ? end : null,
    ),
  );

  const root = h(
    "details",
    { class: "studio-rules", open: "" },
    h("summary", {}, "Challenge rules"),
    h("p", { class: "studio-help" }, "The Challenge Editor's own settings."),
    ...groups,
    towers.root,
  );

  return {
    root,
    sync: (setup) => {
      syncs.forEach((sync) => sync(setup));
      towers.sync(setup);
    },
  };
}
