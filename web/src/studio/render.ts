// The Studio page's editor (#12): the Boss Creator's controls, the code they make and the Tier Preview of that code.
// The preview shows the code read back the way the mod reads it, so what you see is what the mod loads.

import { decode } from "@cbc/share-code";
import { encode } from "@cbc/share-code";
import { compareLinkTo, hashFor, linkTo, replaceAddress } from "../code/link";
import { CHIMPS_MODE, SKULL_MODES, SKULL_MODE_NAMES, effectiveSpawnRounds, isMixed, type SkullMode } from "@cbc/share-code";
import { copyWithFeedback, h } from "../dom";
import { GAME } from "../data/types";
import { paragonsText, fromSlider } from "../math/paragons";
import { MAX_SKULLS } from "@cbc/share-code/skulls";
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
} from "@cbc/share-code/tiers";
import { bossTitle, releaseSetup, renderSetup, renderSkulls, renderTiers } from "../preview/render";
import type { Challenge } from "@cbc/share-code";
import * as edit from "./state";
import type { StudioSetup } from "./state";
import { makeRows, nextId, type Control } from "./rows";
import { setUpTierEditor } from "./tiers";
import { setUpBossPicker } from "./bossPicker";
import { icon } from "./icons";
import { setUpRulesSection } from "./rulesSection";
import { randomizeSetup } from "./random";
import { addInfo } from "./info";
import { MOD_TEXTS, type ModText } from "../modTexts";

/** What Reset Boss Options asks first (Texts.ResetBossOptionsQuestion, without Undo, which the Studio doesn't have) */
const RESET_BOSS_QUESTION =
  "Put every boss option back to its default? The Challenge Editor rules, like the map and starting cash, stay as they are.";
/** What Reset asks first */
const RESET_QUESTION = "Start over with a new setup? Every boss option and every Challenge Editor rule goes back to its default.";

/** The CHIMPS dropdown's entries as the Boss Creator names them (Texts.EventRoundsOff, ChimpsRoundsNewest, ...) */
const CHIMPS_EVENT_NAMES: Record<edit.ChimpsEventChoice, string> = {
  Off: MOD_TEXTS.EventRoundsOff,
  Newest: MOD_TEXTS.ChimpsRoundsNewest,
  Earlier: MOD_TEXTS.ChimpsRoundsEarlier,
  Own: MOD_TEXTS.ChimpsRoundsOwn,
};

/** How long to wait after the last change before making the code and the preview again */
export const UPDATE_DELAY = 150;

// The row is called "… per Tier", so the value is just the change, short enough for one line (#133)
const perTier = (value: number): string => `${value > 0 ? "+" : ""}${value} %`;

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

  const { reset, slider, checkbox, select, text } = makeRows(change);

  // ---------- The Boss Creator's controls, in its order ----------

  // Above the boss, like the Boss Creator's tabs: Standard, one boss for every tier, or Advanced, a boss per tier (#95)
  let memory = edit.NO_MEMORY;
  const switchMode = (on: boolean): void =>
    change((s) => {
      const switched = edit.setAdvanced(s, on, memory);
      memory = switched.memory;
      return switched.setup;
    });
  const standardTab = h("button", { class: "studio-tab", type: "button" }, "Standard");
  const advancedTab = h("button", { class: "studio-tab", type: "button" }, "Advanced");
  standardTab.addEventListener("click", () => switchMode(false));
  advancedTab.addEventListener("click", () => switchMode(true));
  const modeHelp = h("p", { class: "studio-help studio-row-help", id: nextId("mode-help") });
  const modeRow = h(
    "div",
    { class: "studio-row" },
    icon("mode"),
    h("p", { class: "studio-row-title", id: nextId("mode") }, "Mode"),
    h(
      "div",
      { class: "studio-control" },
      h("div", { class: "studio-tabs", role: "group", "aria-label": "Mode", "aria-describedby": modeHelp.id }, standardTab, advancedTab),
    ),
    modeHelp,
  );

  // The Challenge Editor's Random and Reset, and the Boss Creator's Reset Boss Options, which ask first like the game
  const randomButton = h("button", { class: "button small", type: "button" }, "Random");
  randomButton.addEventListener("click", () =>
    change((s) => {
      memory = edit.NO_MEMORY;
      return randomizeSetup(s);
    }),
  );
  const resetBossButton = h("button", { class: "button small blue", type: "button" }, "Reset Boss Options");
  resetBossButton.addEventListener("click", () => {
    if (!window.confirm(RESET_BOSS_QUESTION)) return;
    change((s) => {
      memory = edit.NO_MEMORY;
      return edit.resetBossOptions(s);
    });
  });
  const resetButton = h("button", { class: "button small orange", type: "button" }, "Reset");
  resetButton.addEventListener("click", () => {
    if (!window.confirm(RESET_QUESTION)) return;
    change(() => {
      memory = edit.NO_MEMORY;
      return edit.newSetup();
    });
  });
  const toolbar = h("div", { class: "actions studio-toolbar" }, randomButton, resetBossButton, resetButton);

  const tierEditor = setUpTierEditor(change, () => setup, () =>
    change((s) => {
      const reset = edit.resetAllTiers(s, memory);
      memory = reset.memory;
      return reset.setup;
    }),
  );

  // A button per boss with a tick, then Elite, like the Boss Creator's boss row (#133)
  const boss = setUpBossPicker(change, reset);

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
  // In CHIMPS the tick is a dropdown, like in the Boss Creator (#139)
  const chimpsEventRounds = select(
    "Boss Event Rounds",
    edit.CHIMPS_EVENT_CHOICES,
    (choice) => CHIMPS_EVENT_NAMES[choice as edit.ChimpsEventChoice],
    edit.chimpsEventChoice,
    (s, choice) => edit.setChimpsEventRounds(s, choice as edit.ChimpsEventChoice),
  );

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
    firstRound, roundsBetween, spawnRounds, eventRounds, chimpsEventRounds, skullMode, skullCount, customSkulls,
  ];
  // An i on every row, with the Boss Creator's explanation, and one after the CHIMPS dropdown for its round sets (#157)
  const infos: [Control, string, ModText][] = [
    [boss, "Boss", "BossTypeInfo"],
    [bossSpeed, "Boss Speed", "BossSpeedInfo"],
    [bossHealth, "Boss Health", "BossHealthInfo"],
    [maxParagons, "Max Paragons", "MaxParagonsInfo"],
    [tiers, "Boss Tiers", "BossTiersInfo"],
    [speedPerTier, "Speed per Tier", "SpeedPerTierInfo"],
    [healthPerTier, "Health per Tier", "HealthPerTierInfo"],
    [firstRound, "First Boss Round", "FirstBossRoundInfo"],
    [roundsBetween, "Rounds Between Tiers", "RoundsBetweenTiersInfo"],
    [spawnRounds, "Spawn Rounds", "SpawnRoundsInfo"],
    [eventRounds, "Boss Event Rounds", "BossEventRoundsInfo"],
    [chimpsEventRounds, "Boss Event Rounds", "BossEventRoundsInfo"],
    [skullMode, "Skulls", "SkullsInfo"],
    [skullCount, "Skull Count", "SkullCountInfo"],
    [customSkulls, "Skulls at Boss HP %", "SkullHealthInfo"],
  ];
  for (const [control, title, text] of infos) addInfo(control.row, title, text);
  addInfo(chimpsEventRounds.row, "the CHIMPS round sets", "ChimpsRoundsInfo", chimpsEventRounds.row.querySelector("select")!);

  // Where the Boss Creator has them (#133): the Tier Preview right after the tier rows, which in Advanced Mode is the
  // strip that opens each tier's picker, and the Skull Preview under the skull rows
  const tierPreview = h("div", { class: "studio-preview-block studio-tier-preview" });
  const skullPreview = h("div", { class: "studio-preview-block studio-skull-preview" });
  const rows = controls.map((control) => control.row);
  rows.splice(controls.indexOf(healthPerTier) + 1, 0, tierPreview, tierEditor.root);
  rows.push(skullPreview);
  // The Challenge Editor's own rules after the Boss Creator's (#97)
  const rulesSection = setUpRulesSection(change, () => setup);
  form.replaceChildren(toolbar, modeRow, ...rows, rulesSection.root);
  form.addEventListener("submit", (event) => event.preventDefault());

  /** Shows the setup in every control, and only the rows that apply (BossEditorSection.Relayout) */
  function sync(): void {
    for (const control of controls) control.sync(setup);
    rulesSection.sync(setup);
    const mixed = isMixed(setup.options);
    standardTab.setAttribute("aria-pressed", String(!mixed));
    advancedTab.setAttribute("aria-pressed", String(mixed));
    modeHelp.textContent = mixed
      ? "Advanced: every tier has a boss of its own, with its own Elite, speed, health and skulls."
      : "Standard: the same boss on every tier. Advanced gives every tier a boss of its own.";
    tierEditor.root.hidden = !mixed;
    if (mixed) tierEditor.sync(setup);
    boss.row.hidden = mixed;
    // In Advanced Mode, Boss Speed and Boss Health stay only while they are set, so they can be seen and reset (#112)
    bossSpeed.row.hidden = mixed && edit.bossSpeed(setup) === 100;
    bossHealth.row.hidden = mixed && edit.bossHealth(setup) === 100;
    skullMode.row.hidden = mixed;
    const chimps = setup.challenge.mode === CHIMPS_MODE;
    eventRounds.row.hidden = chimps;
    chimpsEventRounds.row.hidden = !chimps;
    skullCount.row.hidden = mixed || setup.options.SkullMode !== "EvenlySpaced";
    customSkulls.row.hidden = mixed || setup.options.SkullMode !== "Custom";

    // The previews follow every change, from the setup as it is now; Boss Speed and Boss Health are all the Tier
    // Preview needs from the challenge
    releaseSetup(tierPreview);
    releaseSetup(skullPreview);
    tierPreview.hidden = mixed;
    skullPreview.hidden = mixed;
    if (!mixed) {
      const summary = { bossSpeed: edit.bossSpeed(setup) / 100, bossHealth: edit.bossHealth(setup) / 100 } as Challenge;
      tierPreview.replaceChildren(renderTiers(setup.options, summary));
      skullPreview.replaceChildren(renderSkulls(setup.options));
    }
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
    replaceAddress(hashFor(made));
  }

  return {
    async load(next, from) {
      setup = next;
      original = from;
      memory = edit.NO_MEMORY;
      sync();
      unchanged = from === null ? null : await encode(next.options, next.challenge);
      await refresh();
    },
    setup: () => setup,
    code: () => code,
    flush: () => refresh(),
  };
}
