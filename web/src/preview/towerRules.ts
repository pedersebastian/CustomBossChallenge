// A tower's rules in a challenge: its limit, the upgrade tiers it may not buy and whether its Paragon is locked (#167).
// Shared by the setup view and Compare, so both say the same.

import { GAME } from "../data/types";
import type { Challenge } from "@cbc/share-code";

/** The towers with a rule of their own in either challenge, in the game's tower order */
export function towersWithRules(...challenges: Challenge[]): string[] {
  const ids = new Set(
    challenges.flatMap((challenge) => [...Object.keys(challenge.towers), ...Object.keys(challenge.blockedTiers), ...challenge.paragonLocked]),
  );
  const order = Object.keys(GAME.towers);
  return [...ids].sort((x, y) => (order.indexOf(x) + 1 || Infinity) - (order.indexOf(y) + 1 || Infinity));
}

/** The highest tier each path may reach, like players write it: [2, 0, 1] blocked is "3-5-4" */
export const upgradesText = (blocked: [number, number, number]): string => blocked.map((tiers) => 5 - tiers).join("-");

/** What the challenge says about the tower, e.g. "Max 4, upgrades up to 3-5-5, no Paragon", or "Default" */
export function towerRuleText(challenge: Challenge, id: string): string {
  const max = challenge.towers[id];
  // Not allowed at all: the upgrades don't matter then
  if (max === 0) return "Not allowed";

  const parts: string[] = [];
  if (max !== undefined) parts.push(max < 0 ? "No limit" : `Max ${max}`);
  const blocked = challenge.blockedTiers[id];
  if (blocked) parts.push(`upgrades up to ${upgradesText(blocked)}`);
  if (challenge.paragonLocked.includes(id)) parts.push("no Paragon");
  if (parts.length === 0) return "Default";
  const text = parts.join(", ");
  return text.charAt(0).toUpperCase() + text.slice(1);
}
