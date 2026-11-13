// How Boss Event Rounds read on the website (#80, #139): on or off, and in CHIMPS which CHIMPS events' rounds

import { CHIMPS_MODE, chimpsEventRoundSet, usesBossEventRounds, type BossOptions } from "@cbc/share-code";
import { MOD_TEXTS } from "../modTexts";

/** "No", "Yes", or in CHIMPS "Newest CHIMPS events", "Earlier CHIMPS events" or "Boss's own event rounds" */
export function eventRoundsText(options: BossOptions, mode: string): string {
  if (!usesBossEventRounds(options)) return "No";
  const chimps = chimpsEventRoundSet(options, mode);
  if (chimps === null) return mode === CHIMPS_MODE ? "Boss's own event rounds" : "Yes";
  return chimps === "chimpsbloonarius" ? "Earlier CHIMPS events" : "Newest CHIMPS events";
}

/**
 * The Boss Creator's warning for Boss Event Rounds in CHIMPS (SetupWarning.ChimpsEventRounds), naming what the picked
 * round set tests (#157), or null; the boss's own event rounds get none
 */
export function chimpsEventWarning(options: BossOptions, mode: string): string | null {
  const chimps = usesBossEventRounds(options) ? chimpsEventRoundSet(options, mode) : null;
  if (chimps === null) return null;
  return chimps === "chimpsbloonarius" ? MOD_TEXTS.ChimpsEventRoundsEarlier : MOD_TEXTS.ChimpsEventRoundsNewest;
}
