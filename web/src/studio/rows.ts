// The rows of the Studio's controls (#12, #95): a label, the control, its value and a Reset, each showing the setup
// it is given. Built with h(), so every control has a label and works with the keyboard like any form field.

import { NO_AUTOFILL, h } from "../dom";
import type { StudioSetup } from "./state";
import { icon, iconNameFor } from "./icons";

/** A control and how it shows the setup */
export interface Control {
  row: HTMLElement;
  sync: (setup: StudioSetup) => void;
}

/** A slider whose position is the number of a step: the last position, the value at one, and the nearest one */
export interface SliderSteps {
  readonly last: number;
  readonly valueAt: (position: number) => number;
  readonly positionOf: (value: number) => number;
}

let ids = 0;
export const nextId = (name: string): string => `studio-${name}-${++ids}`;

/** The row builders, each calling change with how the setup changes */
export function makeRows(change: (update: (current: StudioSetup) => StudioSetup) => void) {
  /** A row like the Boss Creator's (#133): its icon, its name, then the control, its value and Reset */
  function row(label: string, control: HTMLElement, id: string, ...extra: Node[]): HTMLElement {
    return h(
      "div",
      { class: "studio-row" },
      icon(iconNameFor(label)),
      h("label", { for: id }, label),
      h("div", { class: "studio-control" }, control, ...extra),
    );
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
    steps?: SliderSteps,
  ): Control & { input: HTMLInputElement } {
    const id = nextId("slider");
    // With steps of different sizes (Boss Health, #250) the position is the number of the step
    const bounds = steps ? { min: 0, max: steps.last, step: 1 } : range;
    const input = h("input", { id, type: "range", min: String(bounds.min), max: String(bounds.max), step: String(bounds.step) });
    const output = h("output", { for: id, class: "studio-value" });
    input.addEventListener("input", () => change((s) => set(s, steps ? steps.valueAt(Number(input.value)) : Number(input.value))));
    const extra = defaultValue === undefined ? [] : [reset(label, () => change((s) => set(s, defaultValue)))];
    return {
      input,
      row: row(label, input, id, output, ...extra),
      sync: (s) => {
        const shown = value(s);
        input.value = String(steps ? steps.positionOf(shown) : shown);
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
