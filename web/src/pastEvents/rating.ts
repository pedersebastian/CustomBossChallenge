// The difficulty of a past event's normal or Elite level (#263), as the Studio and the Boss Creator rate the setup
// Past Events opens it as. Used by Past events (its stars and Hardest first) and by the Stats page (#265).

import { pastEventSetup, readSetup, type PastEvent } from "@cbc/share-code";
import { difficultyOf } from "../preview/difficulty";

export interface Rated {
  event: PastEvent;
  elite: boolean;
  points: number;
  stars: number;
}

/**
 * The difficulty of the normal or the Elite event, from the setup Past Events opens it as, or null when it can't be
 * opened (a map the game doesn't have, a boss this version doesn't know). readSetup gives the setup its code would
 * read back as without making the code, so a browser without CompressionStream rates it too.
 */
export function rate(event: PastEvent, elite: boolean): Rated | null {
  const { setup, json } = pastEventSetup(event, elite);
  if (json === null) return null;
  const { options, challenge } = readSetup(setup.options, json);
  return { event, elite, ...difficultyOf(options, challenge) };
}
