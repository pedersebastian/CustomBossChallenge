// Port of Config/TierBossMath.cs: the boss of each tier with "Boss per Tier", a comma separated token per tier like
// "Lych,,RandomElite". An empty token follows the setup's own boss and Elite.

export const RANDOM = "Random";
const ELITE = "Elite";
const MAX_TIERS = 10;

/** The bosses in the order the Boss Creator shows them, which is the order of the game's BossType */
export const TIER_BOSSES = ["Bloonarius", "Lych", "Vortex", "Dreadbloon", "Phayze", "Blastapopoulos", "Diamondback"];
const CHOICES = [...TIER_BOSSES, RANDOM];

export interface TierBoss {
  /** A boss, Random, or null when the tier follows the setup's boss */
  boss: string | null;
  elite: boolean;
}

const FOLLOWS: TierBoss = { boss: null, elite: false };

function parseToken(token: string): TierBoss {
  const trimmed = token.trim();
  const elite = trimmed.toLowerCase().endsWith(ELITE.toLowerCase());
  const name = (elite ? trimmed.slice(0, -ELITE.length) : trimmed).toLowerCase();
  const known = CHOICES.find((choice) => choice.toLowerCase() === name);
  return known === undefined ? FOLLOWS : { boss: known, elite };
}

/** The tiers' bosses in the list, at most 10; unknown tokens follow the setup's boss */
export function parseTierBosses(list: string | null | undefined): TierBoss[] {
  if (list == null || list.trim() === "") return [];
  return list.split(",").slice(0, MAX_TIERS).map(parseToken);
}

const tokenOf = (tier: TierBoss): string => (tier.boss === null ? "" : tier.boss + (tier.elite ? ELITE : ""));

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
