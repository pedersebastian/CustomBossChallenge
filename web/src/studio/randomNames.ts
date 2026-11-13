// Port of Config/RandomNames.cs: fun names that describe a setup the Random button made, like "The Unkillable Snail"
// for a slow boss with lots of health. The rules are checked in order, so the most striking trait wins; a setup
// without one gets no name here. web/test/random-name-cases.json is shared with UnitTests/RandomNamesTests.cs.

import { TIER_BOSSES } from "@cbc/share-code/tier-bosses";

/** What the names look at; speed and health are in percent, 100 is the normal game (NameTraits) */
export interface NameTraits {
  /** The boss as the name shows it, e.g. "Lych" or "Elite Lych"; empty when the tiers have different bosses */
  boss: string;
  /** Whether every tier is Elite */
  elite: boolean;
  tiers: number;
  bossSpeed: number;
  bossHealth: number;
  speedPerTier: number;
  healthPerTier: number;
  /** Whether no tier has a single skull */
  noSkulls: boolean;
  allFortified: boolean;
  /** In Advanced Mode: the boss of every tier when they are all the same one (without Elite), or null */
  sameBoss: string | null;
}

export const NORMAL_TRAITS: NameTraits = {
  boss: "",
  elite: false,
  tiers: 5,
  bossSpeed: 100,
  bossHealth: 100,
  speedPerTier: 0,
  healthPerTier: 0,
  noSkulls: false,
  allFortified: false,
  sameBoss: null,
};

/** The game's challenge name field holds 40 characters */
export const MAX_LENGTH = 40;
export const HIGH = 250;
export const LOW = 60;
export const MANY_TIERS = 8;
export const FAST_PER_TIER = 20;
export const TOUGH_PER_TIER = 30;

/** When a rule applies, and the names it picks from; {0} is the boss and {1} the same boss of every tier */
const RULES: [(t: NameTraits) => boolean, string[]][] = [
  [(t) => t.sameBoss !== null && t.tiers >= 3, ["{1}, {1}, {1}...", "Nothing but {1}", "{1} on Repeat"]],
  [(t) => t.bossSpeed <= LOW && t.bossHealth >= HIGH, ["The Unkillable Snail", "Slow but Unstoppable"]],
  [(t) => t.bossSpeed >= HIGH && t.bossHealth <= LOW, ["Fast and Fragile", "Paper Rocket"]],
  [(t) => t.bossSpeed >= HIGH && t.tiers === 1, ["Blink and It's Gone", "Gone in a Flash"]],
  [(t) => t.tiers >= MANY_TIERS && t.healthPerTier >= TOUGH_PER_TIER, ["The Long Haul", "It Just Keeps Going"]],
  [(t) => t.allFortified && t.elite, ["Armoured and Angry", "Elite and Armoured"]],
  [(t) => t.noSkulls, ["No Skulls Allowed", "No Skulls Attached"]],
  [(t) => t.bossSpeed >= HIGH, ["Turbo {0}", "{0} in a Hurry"]],
  [(t) => t.bossHealth >= HIGH, ["Heavyweight {0}", "{0} the Unbreakable"]],
  [(t) => t.bossSpeed <= LOW, ["Sleepy {0}", "{0} in Slow Motion"]],
  [(t) => t.bossHealth <= LOW, ["Paper {0}", "Fragile {0}"]],
  [(t) => t.tiers === 1, ["One and Done", "Just the One"]],
  [(t) => t.tiers >= MANY_TIERS, ["Tier After Tier", "The Long Road"]],
  [(t) => t.speedPerTier >= FAST_PER_TIER, ["Faster Every Time", "Picking Up Speed"]],
  [(t) => t.healthPerTier >= TOUGH_PER_TIER, ["Tougher Every Time", "Growing Pains"]],
  [(t) => t.allFortified, ["Hard Shell", "Fortified Everything"]],
];

const fill = (name: string, boss: string, sameBoss: string): string => name.replaceAll("{0}", boss).replaceAll("{1}", sameBoss);

/** The names of the first rule that applies and has a name for the setup (RandomNames.NamesFor) */
export function namesFor(traits: NameTraits): string[] {
  for (const [applies, names] of RULES) {
    if (!applies(traits)) continue;
    const filled = names
      .filter((name) => !name.includes("{0}") || traits.boss.length > 0)
      .map((name) => fill(name, traits.boss, traits.sameBoss ?? ""))
      // e.g. "Blastapopoulos, Blastapopoulos, Blastapopoulos...", which the name field would cut off
      .filter((name) => name.length <= MAX_LENGTH);
    if (filled.length > 0) return filled;
  }
  return [];
}

/** A name that describes the setup, picked by random (0 to 1), or null when nothing about it stands out */
export function randomName(traits: NameTraits, random: () => number): string | null {
  const names = namesFor(traits);
  return names.length > 0 ? names[Math.floor(random() * names.length)] : null;
}

/** Every name the rules can make, with every boss (RandomNames.AllNames) */
export const ALL_NAMES: ReadonlySet<string> = new Set(
  RULES.flatMap(([, names]) => names)
    .flatMap((name) =>
      name.includes("{0}")
        ? [...TIER_BOSSES, ...TIER_BOSSES.map((boss) => `Elite ${boss}`)].map((boss) => fill(name, boss, ""))
        : name.includes("{1}")
          ? TIER_BOSSES.map((boss) => fill(name, "", boss))
          : [name],
    )
    .filter((name) => name.length <= MAX_LENGTH),
);
