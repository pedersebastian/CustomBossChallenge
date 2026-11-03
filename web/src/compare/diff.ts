// Compares two setups (issue #35): every option side by side, which ones differ, and a short verdict where it's clear

import { GAME } from "../data/types";
import type { Challenge } from "../code/decode";
import { effectiveSpawnRounds, isMixed, tierBossName, type BossOptions } from "../code/options";
import { formatDecimals } from "../math/dotnet";
import { formatPercent } from "../math/skulls";
import { clampTiers, endRound } from "../math/tiers";
import { skulls, tierCards, tierSkulls, type TierCard } from "../preview/compute";

export interface Setup {
  options: BossOptions;
  challenge: Challenge;
}

export const GROUPS = ["Boss", "Tiers", "Game", "Bloons", "Rules", "Towers"] as const;
export type Group = (typeof GROUPS)[number];

/** One option of both setups, as the website shows it */
export interface Row {
  group: Group;
  label: string;
  a: string;
  b: string;
  same: boolean;
}

const percent = (multiplier: number): string => `${formatDecimals(multiplier * 100, 0)} %`;
const perTier = (value: number): string => (value === 0 ? "Off" : `${value > 0 ? "+" : ""}${value} %`);
const startValue = (value: number): string => (value < 0 ? "Default" : value.toLocaleString("en"));
const yesNo = (value: boolean): string => (value ? "Yes" : "No");
const paragons = (value: number): string => (value < 0 ? "Unlimited" : value === 0 ? "None" : String(value));
const towerLimit = (value: number | undefined): string =>
  value === undefined ? "Default" : value === 0 ? "Not allowed" : value < 0 ? "No limit" : `Max ${value}`;

function skullText(options: BossOptions): string {
  const mode = options.SkullMode === "Vanilla" ? "Boss's own" : options.SkullMode === "EvenlySpaced" ? "Evenly spaced" : "Custom";
  // With a boss per tier, each tier's boss has its own number of skulls
  if (isMixed(options) && options.SkullMode === "Vanilla") {
    const counts = Array.from({ length: clampTiers(options.Tiers) }, (_, i) => tierSkulls(options, i + 1)?.length ?? "?");
    return `${mode}, per tier: ${counts.join(", ")}`;
  }
  const percents = skulls(options).percents ?? [];
  return `${mode}: ${percents.length === 0 ? "none" : percents.map(formatPercent).join(", ")}`;
}

/** Every option of both setups, in the order the Boss Creator and Challenge Editor show them */
export function compareRows(a: Setup, b: Setup): Row[] {
  const rows: Row[] = [];
  const add = (group: Group, label: string, read: (setup: Setup) => string): void => {
    const [left, right] = [read(a), read(b)];
    rows.push({ group, label, a: left, b: right, same: left === right });
  };

  add("Boss", "Name", (s) => s.challenge.name || "(the player's own)");
  add("Boss", "Boss", (s) => s.options.Boss);
  add("Boss", "Elite", (s) => yesNo(s.options.Elite));
  add("Boss", "Boss Speed", (s) => percent(s.challenge.bossSpeed));
  add("Boss", "Boss Health", (s) => percent(s.challenge.bossHealth));
  add("Boss", "Skulls", (s) => skullText(s.options));

  add("Tiers", "Tiers", (s) => String(s.options.Tiers));
  add("Tiers", "Speed per Tier", (s) => perTier(s.options.SpeedPerTier));
  add("Tiers", "Health per Tier", (s) => perTier(s.options.HealthPerTier));
  add("Tiers", "Spawn rounds", (s) => effectiveSpawnRounds(s.options).join(", "));
  // With Advanced Mode on either side, each tier's boss
  if (isMixed(a.options) || isMixed(b.options)) {
    add("Tiers", "Advanced Mode", (s) => yesNo(isMixed(s.options)));
    for (let tier = 1; tier <= Math.max(a.options.Tiers, b.options.Tiers); tier++) {
      add("Tiers", `Tier ${tier} boss`, (s) => (tier <= s.options.Tiers ? tierBossName(s.options, tier) : "—"));
    }
  }
  add("Tiers", "Game ends", (s) => `Round ${endRound(effectiveSpawnRounds(s.options))}`);

  add("Game", "Map", (s) => GAME.maps[s.challenge.map] ?? s.challenge.map);
  add("Game", "Difficulty", (s) => GAME.difficulties[s.challenge.difficulty] ?? s.challenge.difficulty);
  add("Game", "Mode", (s) => GAME.modes[s.challenge.mode] ?? s.challenge.mode);
  add("Game", "Starting cash", (s) => startValue(s.challenge.cash));
  add("Game", "Lives", (s) => startValue(s.challenge.lives));
  add("Game", "Max lives", (s) => startValue(s.challenge.maxLives));
  add("Game", "Start round", (s) => startValue(s.challenge.round));

  add("Bloons", "Bloon speed", (s) => percent(s.challenge.bloonSpeed));
  add("Bloons", "MOAB speed", (s) => percent(s.challenge.moabSpeed));
  add("Bloons", "Bloon health", (s) => percent(s.challenge.bloonHealth));
  add("Bloons", "MOAB health", (s) => percent(s.challenge.moabHealth));
  add("Bloons", "All Camo", (s) => yesNo(s.challenge.allCamo));
  add("Bloons", "All Regrow", (s) => yesNo(s.challenge.allRegen));
  add("Bloons", "All Fortified", (s) => yesNo(s.challenge.allFortified));

  add("Rules", "Max Paragons", (s) => paragons(s.challenge.maxParagons));
  add("Rules", "Max towers", (s) => (s.challenge.maxTowers < 0 ? "Default" : String(s.challenge.maxTowers)));
  add("Rules", "No Monkey Knowledge", (s) => yesNo(s.challenge.disableMK));
  add("Rules", "No Selling", (s) => yesNo(s.challenge.disableSelling));
  add("Rules", "No Powers", (s) => yesNo(s.challenge.disablePowers));

  // Only towers that one of the two changes; in the order of the game's tower list
  const changed = new Set([...Object.keys(a.challenge.towers), ...Object.keys(b.challenge.towers)]);
  const order = Object.keys(GAME.towers);
  const towers = [...changed].sort((x, y) => (order.indexOf(x) + 1 || Infinity) - (order.indexOf(y) + 1 || Infinity));
  for (const id of towers) add("Towers", GAME.towers[id] ?? id, (s) => towerLimit(s.challenge.towers[id]));

  return rows;
}

/** The rows that differ */
export const differences = (a: Setup, b: Setup): Row[] => compareRows(a, b).filter((row) => !row.same);

/** Both setups' tier cards, tier by tier; a setup with fewer tiers has null there */
export function tierPairs(a: Setup, b: Setup): { tier: number; a: TierCard | null; b: TierCard | null }[] {
  const left = tierCards(a.options, a.challenge);
  const right = tierCards(b.options, b.challenge);
  return Array.from({ length: Math.max(left.length, right.length) }, (_, i) => ({ tier: i + 1, a: left[i] ?? null, b: right[i] ?? null }));
}

/** Changes smaller than this (half a percent) count as the same */
const CLOSE = 0.005;

const times = (ratio: number): string => formatDecimals(ratio, ratio < 10 ? 1 : 0);

/**
 * How B's value compares with A's, e.g. "B has 40 % more boss health" or, past double, "A has 3.5 times the boss
 * health". Null when the two are about the same.
 */
function compareValue(a: number, b: number, words: { more: string; less: string; times: string }): string | null {
  if (!(a > 0 && b > 0)) return null;
  const ratio = b / a;
  if (Math.abs(ratio - 1) < CLOSE) return null;
  if (ratio >= 2) return `B ${words.times.replace("#", times(ratio))}`;
  if (ratio <= 0.5) return `A ${words.times.replace("#", times(1 / ratio))}`;
  const change = formatDecimals(Math.abs(ratio - 1) * 100, 0);
  return `B ${(ratio > 1 ? words.more : words.less).replace("#", change)}`;
}

/**
 * Short sentences about B compared with A, only for what can be measured: the boss, the number of tiers, and the
 * boss's health and speed at the last tier both have. It doesn't rate how hard a setup is overall (issue #26).
 */
export function verdict(a: Setup, b: Setup): string[] {
  const lines: string[] = [];
  // First, since the health and speed below then compare two different bosses
  const boss = (setup: Setup): string =>
    isMixed(setup.options)
      ? Array.from({ length: setup.options.Tiers }, (_, i) => tierBossName(setup.options, i + 1)).join(", ")
      : `${setup.options.Elite ? "Elite " : ""}${setup.options.Boss}`;
  if (boss(a) !== boss(b)) lines.push(`Different bosses: A is ${boss(a)}, B is ${boss(b)}.`);

  const tiersA = a.options.Tiers;
  const tiersB = b.options.Tiers;
  if (tiersA !== tiersB) lines.push(`B has ${tiersB} ${tiersB === 1 ? "tier" : "tiers"}, A has ${tiersA}.`);

  const last = tierPairs(a, b)[Math.min(tiersA, tiersB) - 1];
  if (!last?.a || !last.b) return lines;
  const at = `at tier ${last.tier}`;

  if (last.a.healthValue !== null && last.b.healthValue !== null) {
    const health = compareValue(last.a.healthValue, last.b.healthValue, {
      more: "has # % more boss health",
      less: "has # % less boss health",
      times: "has # times the boss health",
    });
    if (health) lines.push(`${health} ${at} (A ${last.a.health}, B ${last.b.health}).`);
  }

  // The boss's real speed, not the one compared with the normal game: different bosses move at different speeds
  if (last.a.speedValue === null || last.b.speedValue === null) return lines;
  const speed = compareValue(last.a.speedValue, last.b.speedValue, {
    more: "'s boss is # % faster",
    less: "'s boss is # % slower",
    times: "'s boss is # times as fast",
  });
  if (speed) lines.push(`${speed.replace(" '", "'")} ${at}.`);
  return lines;
}
