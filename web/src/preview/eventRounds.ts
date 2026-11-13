// How Boss Event Rounds read on the website (#80, #139): on or off, and in CHIMPS which CHIMPS events' rounds

import { chimpsEventRoundSet, usesBossEventRounds, type BossOptions } from "@cbc/share-code";
import { MOD_TEXTS } from "../modTexts";

/** "No", "Yes", or in CHIMPS "Newest CHIMPS events" or "Earlier CHIMPS events" */
export function eventRoundsText(options: BossOptions, mode: string): string {
  if (!usesBossEventRounds(options)) return "No";
  const chimps = chimpsEventRoundSet(options, mode);
  if (chimps === null) return "Yes";
  return chimps === "chimpsbloonarius" ? "Earlier CHIMPS events" : "Newest CHIMPS events";
}

/**
 * The Boss Creator's warning for Boss Event Rounds in CHIMPS (SetupWarning.ChimpsEventRounds), naming what the picked
 * round set tests (#157), or null
 */
export function chimpsEventWarning(options: BossOptions, mode: string): string | null {
  const chimps = usesBossEventRounds(options) ? chimpsEventRoundSet(options, mode) : null;
  if (chimps === null) return null;
  return chimps === "chimpsbloonarius" ? MOD_TEXTS.ChimpsEventRoundsEarlier : MOD_TEXTS.ChimpsEventRoundsNewest;
}
