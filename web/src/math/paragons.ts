// Port of Config/ParagonMath.cs: how the Challenge Editor's Max Paragons reads

import { GAME } from "../data/types";

/**
 * ParagonMath.IsUnlimited: no limit (-1), or a limit of at least every paragon the game has, which limits nothing.
 * The Boss Creator's slider shows both as its last position.
 */
export const isUnlimited = (maxParagons: number, available = GAME.limits.paragons): boolean =>
  maxParagons < 0 || maxParagons >= available;

/** ParagonMath.NoLimit: the maxParagons the game reads as no limit */
export const NO_LIMIT = -1;

/** ParagonMath.ToSlider: the slider position for maxParagons; no limit and broken values are the last position */
export const toSlider = (maxParagons: number, available = GAME.limits.paragons): number =>
  isUnlimited(maxParagons, available) ? available : maxParagons;

/** ParagonMath.FromSlider: the maxParagons for a slider position, the last one meaning no limit */
export const fromSlider = (position: number, available = GAME.limits.paragons): number =>
  position >= available ? NO_LIMIT : Math.max(position, 0);

/** Max Paragons as the setup shows it: "Unlimited", "None" or the limit */
export const paragonsText = (maxParagons: number, available = GAME.limits.paragons): string =>
  isUnlimited(maxParagons, available) ? "Unlimited" : maxParagons === 0 ? "None" : String(maxParagons);
