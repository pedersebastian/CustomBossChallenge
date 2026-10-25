// Shows a decoded setup the way the Boss Creator would: the boss, the Tier Preview, the skulls and the rules

import { GAME } from "../data/types";
import { h } from "../dom";
import { playProblem, type Challenge } from "../code/decode";
import { effectiveSpawnRounds, type BossOptions } from "../code/options";
import { formatDecimals } from "../math/dotnet";
import { formatPercent } from "../math/skulls";
import { endRound, formatHealth, maxModelHealth } from "../math/tiers";
import { skulls, tierCards } from "./compute";

/** Orange where a tier is harder than the normal game, blue where it is easier, like the in-game preview */
const tone = (factor: number): string => (factor > 1.0001 ? "harder" : factor < 0.9999 ? "easier" : "same");

/** The same as tone without colour: an arrow on screen, and words for screen readers */
const ARROW: Record<string, string> = { harder: "▲", easier: "▼", same: "" };
const SPOKEN: Record<string, [string, string]> = {
  harder: ["faster than normal", "tougher than normal"],
  easier: ["slower than normal", "weaker than normal"],
  same: ["normal speed", "normal health"],
};

/** Text only screen readers get */
const spoken = (text: string): HTMLElement => h("span", { class: "sr-only" }, text);

const percent = (multiplier: number): string => `${formatDecimals(multiplier * 100, 0)} %`;

export const bossTitle = (options: BossOptions): string => `${options.Elite ? "Elite " : ""}${options.Boss}`;

export function renderSetup(options: BossOptions, challenge: Challenge, compact = false): HTMLElement {
  const rounds = effectiveSpawnRounds(options);
  const problem = playProblem(challenge, rounds);
  const root = h("div", { class: "setup" });

  root.append(
    h(
      "div",
      { class: "setup-head" },
      h("div", { class: "boss-badge" + (options.Elite ? " elite" : ""), "aria-hidden": "true" }, options.Boss.slice(0, 1)),
      h(
        "div",
        {},
        h("h3", { class: "setup-name" }, challenge.name || bossTitle(options)),
        h(
          "p",
          { class: "setup-sub" },
          `${bossTitle(options)} · ${options.Tiers} ${options.Tiers === 1 ? "tier" : "tiers"} · ` +
            `${GAME.maps[challenge.map] ?? challenge.map} · ${GAME.difficulties[challenge.difficulty] ?? challenge.difficulty}` +
            (challenge.mode === "Standard" ? "" : ` · ${GAME.modes[challenge.mode] ?? challenge.mode}`),
        ),
      ),
    ),
  );

  if (problem) root.append(h("p", { class: "warning" }, `In the game, Play refuses this setup: ${problem}`));

  root.append(renderTiers(options, challenge));
  if (compact) return root;

  root.append(renderSkulls(options), renderRules(options, challenge, rounds));
  return root;
}

function renderTiers(options: BossOptions, challenge: Challenge): HTMLElement {
  const cards = tierCards(options, challenge);
  const strip = h("div", { class: "tiers", role: "list" });
  for (const card of cards) {
    const speed = `×${formatDecimals(card.speed, 2)}`;
    const speedTone = tone(card.speed);
    const healthTone = tone(card.healthFactor);
    strip.append(
      h(
        "div",
        { class: "tier", role: "listitem" },
        spoken(
          `Tier ${card.tier}, round ${card.round}, speed ${speed}, ${SPOKEN[speedTone][0]}, ` +
            `health ${card.health}, ${SPOKEN[healthTone][1]}`,
        ),
        h("span", { class: "tier-label", "aria-hidden": "true" }, `T${card.tier}`),
        h("span", { class: "tier-round", "aria-hidden": "true" }, `R${card.round}`),
        h("span", { class: `tier-speed ${speedTone}`, title: "Speed compared with the normal game", "aria-hidden": "true" }, speed + ARROW[speedTone]),
        h("span", { class: `tier-health ${healthTone}`, title: "Health", "aria-hidden": "true" }, card.health + ARROW[healthTone]),
      ),
    );
  }

  const wrapper = h("div", { class: "block" }, h("h4", {}, "Tier Preview"), strip);
  if (cards.some((card) => card.capped)) {
    const bossHealth = Math.fround(challenge.bossHealth);
    wrapper.append(
      h(
        "p",
        { class: "note harder" },
        `Health stops at ${formatHealth(maxModelHealth(bossHealth) * bossHealth)} per tier, the game's limit: some tiers get less than the settings ask for.`,
      ),
    );
  }
  return wrapper;
}

function renderSkulls(options: BossOptions): HTMLElement {
  const { percents, tier } = skulls(options);
  const bar = h(
    "div",
    { class: "skull-bar", role: "img", "aria-label": `Health bar with skulls at ${percents.map(formatPercent).join(", ")} of the boss's health` },
    h("div", { class: "skull-fill" }),
  );
  for (const value of percents) {
    const marker = h("div", { class: "skull" }, h("span", {}, formatPercent(value)));
    marker.style.left = `${(1 - value) * 100}%`;
    bar.append(marker);
  }
  // Whether labels fit side by side depends on the bar's width, so they are laid out again when it changes
  skullBars.observe(bar);

  const mode =
    options.SkullMode === "Vanilla" ? "the boss's own skulls" : options.SkullMode === "EvenlySpaced" ? "evenly spaced" : "custom";
  return h(
    "div",
    { class: "block" },
    h("h4", {}, `Skulls (${mode}${tier ? `, tier ${tier}` : ""})`),
    percents.length === 0 ? h("p", { class: "muted" }, "No skulls: the boss never triggers its skull effect.") : bar,
  );
}

/** One observer for every skull bar; releaseSetup stops watching the bars of a setup that is taken away */
const skullBars = new ResizeObserver((entries) => {
  for (const entry of entries) layOutSkullLabels(entry.target as HTMLElement);
});

/** Call before a rendered setup is removed, so its skull bar can be freed */
export function releaseSetup(root: Element): void {
  for (const bar of root.querySelectorAll(".skull-bar")) skullBars.unobserve(bar);
}

const LABEL_TOP = 48;
const LABEL_LINE = 24;
const LABEL_GAP = 6;
const BAR_MARGIN_BOTTOM = 52;

/**
 * Like the game's Skull Preview: a percentage too close to the one before it on a line goes on the next line down, so
 * no two labels overlap however narrow the bar is
 */
function layOutSkullLabels(bar: HTMLElement): void {
  const labels = [...bar.querySelectorAll<HTMLElement>(".skull span")];
  const lineEnds: number[] = [];
  for (const label of labels) {
    label.style.top = `${LABEL_TOP}px`;
    const { left, right } = label.getBoundingClientRect();
    let line = lineEnds.findIndex((end) => left >= end + LABEL_GAP);
    if (line < 0) line = lineEnds.length;
    lineEnds[line] = right;
    label.style.top = `${LABEL_TOP + line * LABEL_LINE}px`;
  }
  bar.style.marginBottom = `${BAR_MARGIN_BOTTOM + Math.max(lineEnds.length - 1, 0) * LABEL_LINE}px`;
}

function renderRules(options: BossOptions, challenge: Challenge, rounds: number[]): HTMLElement {
  const rows: [string, string][] = [];
  const startValue = (value: number, unit = ""): string => (value < 0 ? "Default" : `${value.toLocaleString("en")}${unit}`);

  rows.push(["Spawn rounds", rounds.join(", ")]);
  rows.push(["Game ends", `Round ${endRound(rounds)}`]);
  rows.push(["Boss Speed", percent(challenge.bossSpeed)]);
  rows.push(["Boss Health", percent(challenge.bossHealth)]);
  if (options.SpeedPerTier !== 0) rows.push(["Speed per Tier", `${options.SpeedPerTier > 0 ? "+" : ""}${options.SpeedPerTier} %`]);
  if (options.HealthPerTier !== 0) rows.push(["Health per Tier", `${options.HealthPerTier > 0 ? "+" : ""}${options.HealthPerTier} %`]);
  rows.push(["Max Paragons", challenge.maxParagons < 0 ? "Unlimited" : challenge.maxParagons === 0 ? "None" : String(challenge.maxParagons)]);
  rows.push(["Starting cash", startValue(challenge.cash)]);
  rows.push(["Lives", startValue(challenge.lives)]);
  if (challenge.maxLives >= 0) rows.push(["Max lives", startValue(challenge.maxLives)]);
  if (challenge.round >= 0) rows.push(["Start round", String(challenge.round)]);
  if (challenge.maxTowers >= 0) rows.push(["Max towers", String(challenge.maxTowers)]);
  for (const [label, value] of [
    ["Bloon speed", challenge.bloonSpeed],
    ["MOAB speed", challenge.moabSpeed],
    ["Bloon health", challenge.bloonHealth],
    ["MOAB health", challenge.moabHealth],
  ] as const) {
    if (Math.abs(value - 1) > 0.0001) rows.push([label, percent(value)]);
  }

  const modifiers = [
    challenge.allCamo && "All Camo",
    challenge.allRegen && "All Regrow",
    challenge.allFortified && "All Fortified",
    challenge.disableMK && "No Monkey Knowledge",
    challenge.disableSelling && "No Selling",
    challenge.disablePowers && "No Powers",
  ].filter((x): x is string => Boolean(x));

  const towers = Object.entries(challenge.towers).map(([id, max]) => {
    const name = GAME.towers[id] ?? id;
    return max === 0 ? `${name}: not allowed` : max < 0 ? `${name}: no limit` : `${name}: max ${max}`;
  });

  const list = h("dl", { class: "rules" });
  for (const [label, value] of rows) list.append(h("div", { class: "rule" }, h("dt", {}, label), h("dd", {}, value)));

  return h(
    "div",
    { class: "block" },
    h("h4", {}, "Rules"),
    list,
    modifiers.length ? h("div", { class: "chips" }, ...modifiers.map((m) => h("span", { class: "chip" }, m))) : null,
    towers.length ? h("details", { class: "towers" }, h("summary", {}, `Tower limits (${towers.length})`), h("ul", {}, ...towers.map((t) => h("li", {}, t)))) : null,
  );
}
