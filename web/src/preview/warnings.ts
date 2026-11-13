// The Boss Creator's warnings under the Tier Preview (#186), with its own texts (Menus/Texts.cs, Texts.Warning)

import type { BossOptions, Challenge } from "@cbc/share-code";
import { entryOf } from "@cbc/share-code";
import { formatHealth, VANILLA_TIERS, clampTiers } from "@cbc/share-code/tiers";
import { factsOf, setupWarnings, type TierBase, type Warning } from "@cbc/share-code/warnings";
import { MOD_TEXTS } from "../modTexts";
import { modelOf } from "./compute";

/**
 * The health and speed of the game's model each tier is made from, like TierStrip.Facts: tier 5's for the tiers above
 * it, and null for a Random tier or a boss the website doesn't know
 */
export function tierBases(options: BossOptions): (TierBase | null)[] {
  return Array.from({ length: clampTiers(options.Tiers) }, (_, i) => {
    const tier = i + 1;
    if (entryOf(options, tier).boss === "Random") return null;
    const model = modelOf(options, Math.min(tier, VANILLA_TIERS), tier);
    return model ? { health: model.health, speed: model.speed } : null;
  });
}

/** The warnings the Boss Creator shows for the setup, in its order */
export const warningsOf = (options: BossOptions, challenge: Challenge): Warning[] =>
  setupWarnings(factsOf(options, challenge, tierBases(options)));

/** A warning as the Boss Creator says it */
export function warningText(warning: Warning): string {
  const fill = (text: string, ...args: (number | string)[]): string =>
    text.replace(/\{(\d+)\}/g, (all, i: string) => (Number(i) < args.length ? String(args[Number(i)]) : all));
  switch (warning.kind) {
    case "StartRoundClose":
      return fill(MOD_TEXTS.StartRoundClose, ...warning.args);
    case "TiersClose":
      return fill(MOD_TEXTS.TiersClose, warning.args[0]);
    case "HealthCapped":
      return fill(MOD_TEXTS.HealthCap, formatHealth(warning.args[0] as number));
    case "SameTiers":
      return fill(MOD_TEXTS.SameTiers, warning.args[0]);
    case "ChimpsEventRounds":
      return warning.args[0] === "Earlier" ? MOD_TEXTS.ChimpsEventRoundsEarlier : MOD_TEXTS.ChimpsEventRoundsNewest;
    case "HardSetup":
      return MOD_TEXTS.HardSetup;
  }
}

/** The texts of the warnings the Boss Creator shows for the setup */
export const warningTexts = (options: BossOptions, challenge: Challenge): string[] => warningsOf(options, challenge).map(warningText);
