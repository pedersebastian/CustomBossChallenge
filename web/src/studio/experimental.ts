// Gildebloon, the mod's own boss (#25), is hidden in the mod until it is finished, and so it is in the Studio: the
// boss row and the tier bosses offer it only on the dev server (npm run dev) or with ?experimental=gildebloon in the
// address. Codes with it open and preview everywhere; Random never picks it.

import { BOSSES, MOD_BOSSES } from "@cbc/share-code";
import { CHOICES, RANDOM } from "@cbc/share-code/tier-bosses";

/** The address switch that shows Gildebloon in the Studio */
export const EXPERIMENTAL_PARAM = "experimental";
export const EXPERIMENTAL_VALUE = "gildebloon";

/** Gildebloon's badge colour: it has no tier colours in the mod's TierLook */
export const GILDEBLOON_COLOUR = "#e8b923";

/** Whether the Studio offers Gildebloon: on the dev server (not in the tests), or with ?experimental=gildebloon */
export function experimentalOn(
  search: string = typeof location === "undefined" ? "" : location.search,
  dev: boolean = import.meta.env.DEV && import.meta.env.MODE !== "test",
): boolean {
  if (dev) return true;
  const values = new URLSearchParams(search).getAll(EXPERIMENTAL_PARAM).flatMap((value) => value.split(","));
  return values.some((value) => value.trim().toLowerCase() === EXPERIMENTAL_VALUE);
}

/** The bosses of the Studio's boss row */
export const studioBosses = (on: boolean = experimentalOn()): readonly string[] => (on ? [...BOSSES, ...MOD_BOSSES] : BOSSES);

/** The bosses to put on a tier, Random last */
export const studioChoices = (on: boolean = experimentalOn()): readonly string[] =>
  on ? [...CHOICES.filter((choice) => choice !== RANDOM), ...MOD_BOSSES, RANDOM] : CHOICES;
