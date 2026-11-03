// Port of Config/TierBossMath.cs: the boss of each tier in Advanced Mode, a comma separated token per tier like
// "Lych,,RandomElite,VortexElite@120/80#4". An empty token follows the setup's own boss and Elite, "@speed/health"
// gives the tier its own speed and health in percent, and "#" its own skulls: a count of evenly spaced skulls ("#4") or
// their health percentages ("#75%;50%;25%").

import { DEFAULT_COUNT, DEFAULT_CUSTOM, MAX_SKULLS, evenlySpaced, formatCustom, formatPercent, parseCustom } from "./skulls";

export const RANDOM = "Random";
const ELITE = "Elite";
const MAX_TIERS = 10;
const VALUES_MARK = "@";
const SKULLS_MARK = "#";

/** A tier's speed and health in percent, with the range and step of Boss Speed and Boss Health */
export const NORMAL = 100;
export const MIN_SPEED = 10;
export const MAX_SPEED = 1000;
export const MIN_HEALTH = 10;
export const MAX_HEALTH = 2000;
const VALUE_STEP = 10;

/** The bosses in the order the Boss Creator shows them, which is the order of the game's BossType */
export const TIER_BOSSES = ["Bloonarius", "Lych", "Vortex", "Dreadbloon", "Phayze", "Blastapopoulos", "Diamondback"];
const CHOICES = [...TIER_BOSSES, RANDOM];

export interface TierBoss {
  /** A boss, Random, or null when the tier follows the setup's boss */
  boss: string | null;
  elite: boolean;
  /** The tier's own speed and health in percent, in place of Boss Speed and Boss Health */
  speed: number;
  health: number;
  /** The tier's own skulls in place of the Skulls option: the mode, the count for EvenlySpaced, the list for Custom */
  skulls: "Vanilla" | "EvenlySpaced" | "Custom";
  skullCount: number;
  customSkulls: string;
}

export const FOLLOWS: TierBoss = {
  boss: null,
  elite: false,
  speed: NORMAL,
  health: NORMAL,
  skulls: "Vanilla",
  skullCount: DEFAULT_COUNT,
  customSkulls: DEFAULT_CUSTOM,
};

/** A tier's skull thresholds (0-1, from full health down), or null to keep the boss's own (TierBoss.SkullPercents) */
export function tierPercents(tier: TierBoss): number[] | null {
  if (tier.skulls === "EvenlySpaced") return evenlySpaced(tier.skullCount);
  if (tier.skulls === "Custom") return parseCustom(tier.customSkulls);
  return null;
}

/** A whole number that fits a .NET int, or null, like int.TryParse */
function parseInt32(text: string): number | null {
  const trimmed = text.trim();
  if (!/^[+-]?\d+$/.test(trimmed)) return null;
  const value = Number(trimmed);
  return Math.abs(value) > 2147483647 ? null : value;
}

/** A tier's own skulls after "#": a whole number is that many evenly spaced skulls, a list with "%" their percentages */
function parseSkulls(text: string): Pick<TierBoss, "skulls" | "skullCount" | "customSkulls"> {
  const trimmed = text.trim();
  if (trimmed.includes("%")) {
    const percents = parseCustom(trimmed);
    return percents ? { ...FOLLOWS, skulls: "Custom", customSkulls: formatCustom(percents) } : FOLLOWS;
  }
  const count = parseInt32(trimmed);
  return count === null ? FOLLOWS : { ...FOLLOWS, skulls: "EvenlySpaced", skullCount: Math.min(Math.max(count, 0), MAX_SKULLS) };
}

/** A tier's own skulls as they follow "#", or "" for the boss's own (TierBossMath.FormatSkulls) */
function formatSkulls(tier: TierBoss): string {
  if (tier.skulls === "EvenlySpaced") return SKULLS_MARK + Math.min(Math.max(tier.skullCount, 0), MAX_SKULLS);
  const percents = tier.skulls === "Custom" ? parseCustom(tier.customSkulls) : null;
  return percents ? SKULLS_MARK + percents.map(formatPercent).join(";") : "";
}

/** A speed or health held to its range, on a multiple of 10, like TierBossMath.ClampValue */
const clampValue = (value: number, min: number, max: number): number =>
  Math.min(Math.max(Math.round(value / VALUE_STEP) * VALUE_STEP, min), max);

/** Like .NET's int.TryParse: whole numbers that fit an int, junk is the normal 100 */
function parseValue(text: string, min: number, max: number): number {
  const value = parseInt32(text);
  return value === null ? NORMAL : clampValue(value, min, max);
}

function parseToken(token: string): TierBoss {
  let trimmed = token.trim();
  let skulls: Pick<TierBoss, "skulls" | "skullCount" | "customSkulls"> = FOLLOWS;
  const skullsMark = trimmed.indexOf(SKULLS_MARK);
  if (skullsMark >= 0) {
    skulls = parseSkulls(trimmed.slice(skullsMark + 1));
    trimmed = trimmed.slice(0, skullsMark).trim();
  }
  let speed = NORMAL;
  let health = NORMAL;
  const mark = trimmed.indexOf(VALUES_MARK);
  if (mark >= 0) {
    const values = trimmed.slice(mark + 1).split("/");
    speed = parseValue(values[0], MIN_SPEED, MAX_SPEED);
    health = parseValue(values.length > 1 ? values[1] : "", MIN_HEALTH, MAX_HEALTH);
    trimmed = trimmed.slice(0, mark).trim();
  }
  const elite = trimmed.toLowerCase().endsWith(ELITE.toLowerCase());
  const name = (elite ? trimmed.slice(0, -ELITE.length) : trimmed).toLowerCase();
  const known = CHOICES.find((choice) => choice.toLowerCase() === name);
  return { ...FOLLOWS, ...skulls, boss: known ?? null, elite: known !== undefined && elite, speed, health };
}

/** The tiers' bosses in the list, at most 10; unknown tokens follow the setup's boss */
export function parseTierBosses(list: string | null | undefined): TierBoss[] {
  if (list == null || list.trim() === "") return [];
  return list.split(",").slice(0, MAX_TIERS).map(parseToken);
}

const tokenOf = (tier: TierBoss): string =>
  (tier.boss === null ? "" : tier.boss + (tier.elite ? ELITE : "")) +
  (tier.speed === NORMAL && tier.health === NORMAL ? "" : `${VALUES_MARK}${tier.speed}/${tier.health}`) +
  formatSkulls(tier);

/** The list without the tiers at the end that follow the setup's boss */
export function formatTierBosses(tiers: TierBoss[]): string {
  const tokens = tiers.slice(0, MAX_TIERS).map(tokenOf);
  while (tokens.length > 0 && tokens[tokens.length - 1] === "") tokens.pop();
  return tokens.join(",");
}

/** The list in its canonical form; null (a single boss for every tier) stays null */
export const normalizeTierBosses = (list: string | null | undefined): string | null =>
  list == null ? null : formatTierBosses(parseTierBosses(list));

/** What a 1-based tier was set to */
export function tierEntry(list: string | null | undefined, tier: number): TierBoss {
  const tiers = parseTierBosses(list);
  return tier >= 1 && tier <= tiers.length ? tiers[tier - 1] : FOLLOWS;
}
