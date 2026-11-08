// The rows of the Studio's controls (#12, #95): a label, the control, its value and a Reset, each showing the setup
// it is given. Built with h(), so every control has a label and works with the keyboard like any form field.

import { h } from "../dom";
import type { StudioSetup } from "./state";

/** A control and how it shows the setup */
export interface Control {
  row: HTMLElement;
  sync: (setup: StudioSetup) => void;
}

/**
 * Keeps password managers out of the Studio's fields: "Challenge name" looks like a name to them, and they offered
 * to fill in the player's own (Dashlane reads data-form-type, 1Password, LastPass and Bitwarden their own markers)
 */
export const NO_AUTOFILL = {
  autocomplete: "off",
  "data-form-type": "other",
  "data-1p-ignore": "true",
  "data-lpignore": "true",
  "data-bwignore": "true",
} as const;

let ids = 0;
export const nextId = (name: string): string => `studio-${name}-${++ids}`;

/** The row builders, each calling change with how the setup changes */
export function makeRows(change: (update: (current: StudioSetup) => StudioSetup) => void) {
  function row(label: string, control: HTMLElement, id: string, ...extra: Node[]): HTMLElement {
    return h("div", { class: "studio-row" }, h("label", { for: id }, label), h("div", { class: "studio-control" }, control, ...extra));
  }

  function reset(label: string, onReset: () => void): HTMLButtonElement {
    const button = h("button", { class: "button small blue", type: "button", "aria-label": `Reset ${label}` }, "Reset");
    button.addEventListener("click", onReset);
    return button;
  }

  function slider(
    label: string,
    range: { min: number; max: number; step: number },
    value: (s: StudioSetup) => number,
    text: (value: number) => string,
    set: (s: StudioSetup, value: number) => StudioSetup,
    defaultValue?: number,
  ): Control & { input: HTMLInputElement } {
    const id = nextId("slider");
    const input = h("input", { id, type: "range", min: String(range.min), max: String(range.max), step: String(range.step) });
    const output = h("output", { for: id, class: "studio-value" });
    input.addEventListener("input", () => change((s) => set(s, Number(input.value))));
    const extra = defaultValue === undefined ? [] : [reset(label, () => change((s) => set(s, defaultValue)))];
    return {
      input,
      row: row(label, input, id, output, ...extra),
      sync: (s) => {
        const shown = value(s);
        input.value = String(shown);
        output.textContent = text(shown);
        input.setAttribute("aria-valuetext", text(shown));
      },
    };
  }

  function checkbox(label: string, value: (s: StudioSetup) => boolean, set: (s: StudioSetup, on: boolean) => StudioSetup): Control {
    const id = nextId("check");
    const input = h("input", { id, type: "checkbox" });
    input.addEventListener("change", () => change((s) => set(s, input.checked)));
    return { row: row(label, input, id), sync: (s) => (input.checked = value(s)) };
  }

  function select(
    label: string,
    choices: readonly string[],
    name: (choice: string) => string,
    value: (s: StudioSetup) => string,
    set: (s: StudioSetup, choice: string) => StudioSetup,
    ...extra: Node[]
  ): Control {
    const id = nextId("select");
    const input = h("select", { id }, ...choices.map((choice) => h("option", { value: choice }, name(choice))));
    input.addEventListener("change", () => change((s) => set(s, input.value)));
    return { row: row(label, input, id, ...extra), sync: (s) => (input.value = value(s)) };
  }

  /** A text field that is read when it is left or Enter is pressed, like the game's */
  function text(label: string, placeholder: string, value: (s: StudioSetup) => string, set: (s: StudioSetup, text: string) => StudioSetup, ...extra: Node[]): Control {
    const id = nextId("text");
    const input = h("input", { id, type: "text", class: "studio-text", spellcheck: "false", placeholder, ...NO_AUTOFILL });
    input.addEventListener("change", () => change((s) => set(s, input.value)));
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        input.blur();
      }
    });
    return { row: row(label, input, id, ...extra), sync: (s) => (document.activeElement === input ? undefined : (input.value = value(s))) };
  }

  return { row, reset, slider, checkbox, select, text };
}
