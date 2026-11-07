// The Studio page's editor (#12): the Boss Creator's controls, the code they make and the Tier Preview of that code.
// The preview shows the code read back the way the mod reads it, so what you see is what the mod loads.

import { decode } from "../code/decode";
import { encode } from "../code/encode";
import { compareLinkTo, hashFor, linkTo } from "../code/link";
import { BOSSES, SKULL_MODES, SKULL_MODE_NAMES, effectiveSpawnRounds, isMixed, type SkullMode } from "../code/options";
import { copyWithFeedback, h } from "../dom";
import { GAME } from "../data/types";
import { paragonsText, fromSlider } from "../math/paragons";
import { MAX_SKULLS } from "../math/skulls";
import {
  BOSS_HEALTH_SLIDER,
  BOSS_SPEED_SLIDER,
  MAX_FIRST_SPAWN_ROUND,
  MAX_HEALTH_PER_TIER,
  MAX_ROUNDS_BETWEEN_TIERS,
  MAX_SPEED_PER_TIER,
  MAX_TIERS,
  MIN_HEALTH_PER_TIER,
  MIN_SPEED_PER_TIER,
  MIN_TIERS,
  PER_TIER_STEP,
  formatRounds,
} from "../math/tiers";
import { bossTitle, releaseSetup, renderSetup } from "../preview/render";
import * as edit from "./state";
import type { StudioSetup } from "./state";

/** How long to wait after the last change before making the code and the preview again */
export const UPDATE_DELAY = 150;

/** A control and how it shows the setup */
interface Control {
  row: HTMLElement;
  sync: (setup: StudioSetup) => void;
}

let ids = 0;
const nextId = (name: string): string => `studio-${name}-${++ids}`;

const perTier = (value: number): string => `${value > 0 ? "+" : ""}${value} % per tier`;

export interface Studio {
  /** Starts editing a setup; code is the code it came from, for Compare, or null for a new setup */
  load: (setup: StudioSetup, code: string | null) => Promise<void>;
  /** The setup being edited */
  setup: () => StudioSetup;
  /** The code shown, once it has been made */
  code: () => string;
  /** Makes the code and the preview now, without waiting */
  flush: () => Promise<void>;
}

export function setUpStudio(form: HTMLFormElement, codeBox: HTMLTextAreaElement, actions: HTMLElement, preview: HTMLElement): Studio {
  let setup = edit.newSetup();
  let original: string | null = null;
  // The code the website makes of the setup as it was loaded: the mod's own code has other bytes for the same setup
  let unchanged: string | null = null;
  let code = "";
  let pending: number | undefined;
  let latest = 0;
  // One line for screen readers once the code has changed, not the whole setup
  const status = h("p", { class: "sr-only", role: "status" });
  preview.before(status);

  const change = (update: (current: StudioSetup) => StudioSetup): void => {
    setup = update(setup);
    sync();
    clearTimeout(pending);
    pending = window.setTimeout(() => void refresh(), UPDATE_DELAY);
  };

  // ---------- Rows ----------

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
    const input = h("input", { id, type: "text", class: "studio-text", spellcheck: "false", placeholder });
    input.addEventListener("change", () => change((s) => set(s, input.value)));
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        input.blur();
      }
    });
    return { row: row(label, input, id, ...extra), sync: (s) => (document.activeElement === input ? undefined : (input.value = value(s))) };
  }

  // ---------- The Boss Creator's controls, in its order ----------

  const advancedNote = h(
    "p",
    { class: "note" },
    "Advanced Mode: every tier has its own boss, speed, health and skulls. They are kept as they are in the code; for now they are changed in the game's Boss Creator.",
  );

  const elite = h("input", { id: nextId("elite"), type: "checkbox" });
  elite.addEventListener("change", () => change((s) => edit.setElite(s, elite.checked)));
  const boss = select(
    "Boss",
    BOSSES,
    (name) => name,
    (s) => s.options.Boss,
    edit.setBoss,
    h("label", { class: "toggle studio-elite" }, elite, "Elite"),
    reset("Boss", () => change(edit.resetBoss)),
  );

  const bossSpeed = slider("Boss Speed", BOSS_SPEED_SLIDER, edit.bossSpeed, (v) => `${v} %`, edit.setBossSpeed, 100);
  const bossHealth = slider("Boss Health", BOSS_HEALTH_SLIDER, edit.bossHealth, (v) => `${v} %`, edit.setBossHealth, 100);
  const paragons = GAME.limits.paragons;
  const maxParagons = slider(
    "Max Paragons",
    { min: 0, max: paragons, step: 1 },
    edit.paragonSlider,
    (v) => paragonsText(fromSlider(v)),
    edit.setParagonSlider,
    paragons,
  );

  const tiers = slider("Boss Tiers", { min: MIN_TIERS, max: MAX_TIERS, step: 1 }, (s) => s.options.Tiers, String, edit.setTiers, 5);
  const speedPerTier = slider(
    "Speed per Tier",
    { min: MIN_SPEED_PER_TIER, max: MAX_SPEED_PER_TIER, step: PER_TIER_STEP },
    (s) => s.options.SpeedPerTier,
    perTier,
    edit.setSpeedPerTier,
    0,
  );
  const healthPerTier = slider(
    "Health per Tier",
    { min: MIN_HEALTH_PER_TIER, max: MAX_HEALTH_PER_TIER, step: PER_TIER_STEP },
    (s) => s.options.HealthPerTier,
    perTier,
    edit.setHealthPerTier,
    0,
  );

  // The two sliders spread the rounds evenly; each keeps the other's value
  const firstRound = slider(
    "First Boss Round",
    { min: 1, max: MAX_FIRST_SPAWN_ROUND, step: 1 },
    (s) => edit.spawnSliders(s, Number(roundsBetween?.input.value) || undefined).first,
    String,
    (s, first) => edit.spreadSpawnRounds(s, first, Number(roundsBetween.input.value)),
  );
  const roundsBetween = slider(
    "Rounds Between Tiers",
    { min: 1, max: MAX_ROUNDS_BETWEEN_TIERS, step: 1 },
    (s) => edit.spawnSliders(s, Number(roundsBetween?.input.value) || undefined).gap,
    String,
    (s, gap) => edit.spreadSpawnRounds(s, Number(firstRound.input.value), gap),
  );
  const spawnRounds = text(
    "Spawn Rounds",
    "40, 60, 80, ...",
    (s) => formatRounds(effectiveSpawnRounds(s.options)),
    (s, typed) => (typed === formatRounds(effectiveSpawnRounds(s.options)) ? s : edit.setSpawnRounds(s, typed)),
    reset("Spawn Rounds", () => change(edit.resetSpawnRounds)),
  );
  const eventRounds = checkbox("Boss Event Rounds", (s) => s.options.BossEventRounds, edit.setBossEventRounds);

  const skullMode = select(
    "Skulls",
    SKULL_MODES,
    (mode) => SKULL_MODE_NAMES[mode as SkullMode],
    (s) => s.options.SkullMode,
    (s, mode) => edit.setSkullMode(s, mode as SkullMode),
    reset("Skulls", () => change(edit.resetSkulls)),
  );
  const skullCount = slider("Skull Count", { min: 0, max: MAX_SKULLS, step: 1 }, (s) => s.options.SkullCount, String, edit.setSkullCount);
  const customSkulls = text("Skulls at Boss HP %", "75, 50, 25", (s) => s.options.CustomSkulls, edit.setCustomSkulls);

  const controls: Control[] = [
    boss, bossSpeed, bossHealth, maxParagons, tiers, speedPerTier, healthPerTier,
    firstRound, roundsBetween, spawnRounds, eventRounds, skullMode, skullCount, customSkulls,
  ];
  form.replaceChildren(advancedNote, ...controls.map((control) => control.row));
  form.addEventListener("submit", (event) => event.preventDefault());

  /** Shows the setup in every control, and only the rows that apply (BossEditorSection.Relayout) */
  function sync(): void {
    for (const control of controls) control.sync(setup);
    elite.checked = setup.options.Elite;
    const mixed = isMixed(setup.options);
    advancedNote.hidden = !mixed;
    boss.row.hidden = mixed;
    // In Advanced Mode, Boss Speed and Boss Health stay only while they are set, so they can be seen and reset (#112)
    bossSpeed.row.hidden = mixed && edit.bossSpeed(setup) === 100;
    bossHealth.row.hidden = mixed && edit.bossHealth(setup) === 100;
    skullMode.row.hidden = mixed;
    skullCount.row.hidden = mixed || setup.options.SkullMode !== "EvenlySpaced";
    customSkulls.row.hidden = mixed || setup.options.SkullMode !== "Custom";
  }

  // ---------- Code and preview ----------

  const copyCode = h("button", { class: "button small", type: "button" }, "Copy code");
  copyCode.addEventListener("click", () => copyWithFeedback(copyCode, code));
  const copyLink = h("button", { class: "button small blue", type: "button" }, "Copy link");
  copyLink.addEventListener("click", () => copyWithFeedback(copyLink, linkTo(code, new URL("./", location.href).href), "Link copied!"));
  const compare = h("a", { class: "button small orange" }, "Compare with the original");
  actions.replaceChildren(copyCode, copyLink, compare);

  async function refresh(): Promise<void> {
    clearTimeout(pending);
    pending = undefined;
    const request = ++latest;
    const made = await encode(setup.options, setup.challenge);
    const read = await decode(made);
    // An older change finishing late must not replace a newer one
    if (request !== latest) return;

    code = made;
    codeBox.value = made;
    releaseSetup(preview);
    if (read.ok) {
      preview.replaceChildren(renderSetup(read.options, read.challenge));
      status.textContent = `Code updated: ${bossTitle(read.options)}, ${read.options.Tiers} ${read.options.Tiers === 1 ? "tier" : "tiers"}.`;
    } else {
      // Never expected: the website reads every code it makes
      preview.replaceChildren(h("p", { class: "error" }, read.reason));
      status.textContent = read.reason;
    }
    compare.hidden = original === null || made === unchanged;
    if (original !== null) compare.setAttribute("href", compareLinkTo(original, made));
    history.replaceState(null, "", hashFor(made));
  }

  return {
    async load(next, from) {
      setup = next;
      original = from;
      sync();
      unchanged = from === null ? null : await encode(next.options, next.challenge);
      await refresh();
    },
    setup: () => setup,
    code: () => code,
    flush: () => refresh(),
  };
}
