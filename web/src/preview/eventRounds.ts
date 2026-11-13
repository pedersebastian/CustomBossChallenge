// How Boss Event Rounds read on the website (#80, #139): on or off, and in CHIMPS which CHIMPS events' rounds

import { chimpsEventRoundSet, usesBossEventRounds, type BossOptions } from "@cbc/share-code";

/** "No", "Yes", or in CHIMPS "Newest CHIMPS events" or "Earlier CHIMPS events" */
export function eventRoundsText(options: BossOptions, mode: string): string {
  if (!usesBossEventRounds(options)) return "No";
  const chimps = chimpsEventRoundSet(options, mode);
  if (chimps === null) return "Yes";
  return chimps === "chimpsbloonarius" ? "Earlier CHIMPS events" : "Newest CHIMPS events";
}

/** The Boss Creator's warning for Boss Event Rounds in CHIMPS (SetupWarning.ChimpsEventRounds), or null */
export const chimpsEventWarning = (options: BossOptions, mode: string): string | null =>
  usesBossEventRounds(options) && chimpsEventRoundSet(options, mode) !== null
    ? "Boss Event Rounds in CHIMPS play the rounds of the CHIMPS boss events, which are much harder than the normal rounds."
    : null;
