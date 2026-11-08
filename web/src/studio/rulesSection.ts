// The Studio's "Challenge rules" section (#97): a control per rule in rules.ts, in four groups like the Challenge
// Editor's. Text and number fields are read when they are left or Enter is pressed, and show the value as it is kept.

import { effectiveSpawnRounds } from "../code/options";
import { h } from "../dom";
import { endRound } from "../math/tiers";
import { makeRows, nextId } from "./rows";
import { RULES, percentText, resetRule, ruleId, ruleInput, ruleValue, setRule, type Rule, type RuleGroup } from "./rules";
import type { StudioSetup } from "./state";

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

export function setUpRulesSection(change: (update: (current: StudioSetup) => StudioSetup) => void): RulesSection {
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
    const id = nextId(ruleId(rule).replace(/\./g, "-"));
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
        const input = h(
          "select",
          { id },
          ...rule.choices.map((choice) => h("option", { value: choice.value, disabled: choice.disabled ? "" : undefined }, choice.label)),
        );
        input.addEventListener("change", () => set(input.value));
        syncs.push((s) => (input.value = String(ruleValue(s, rule))));
        return row(rule.label, input, id, resetButton());
      }
      case "text": {
        const input = h("input", { id, type: "text", class: "studio-text", maxlength: String(rule.maxLength), placeholder: "Your own name in the game" });
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
    h("p", { class: "studio-help" }, "The Challenge Editor's own settings. Tower limits are still kept as they are in the code."),
    ...groups,
  );

  return { root, sync: (setup) => syncs.forEach((sync) => sync(setup)) };
}
