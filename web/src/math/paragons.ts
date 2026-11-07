// Port of Config/ParagonMath.cs: how the Challenge Editor's Max Paragons reads

import { GAME } from "../data/types";

/**
 * ParagonMath.IsUnlimited: no limit (-1), or a limit of at least every paragon the game has, which limits nothing.
 * The Boss Creator's slider shows both as its last position.
 */
export const isUnlimited = (maxParagons: number, available = GAME.limits.paragons): boolean =>
  maxParagons < 0 || maxParagons >= available;

/** Max Paragons as the setup shows it: "Unlimited", "None" or the limit */
export const paragonsText = (maxParagons: number, available = GAME.limits.paragons): string =>
  isUnlimited(maxParagons, available) ? "Unlimited" : maxParagons === 0 ? "None" : String(maxParagons);
