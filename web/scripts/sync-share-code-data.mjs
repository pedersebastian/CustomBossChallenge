// Writes the part of the game data that reading and making share codes needs (web/src/data/game.json, which the
// mod's WebDataTest writes from the game) into the share-code package, so the package stands alone (#98).
// Run it with `npm run sync-data` after WebDataTest has written new game data; web/test/share-code-data.test.ts fails
// until it has.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const game = JSON.parse(readFileSync(fileURLToPath(new URL("../src/data/game.json", import.meta.url)), "utf8"));

/** The share-code data: what the package's decode and encode read, in a fixed order */
export const shareCodeData = (data) => ({
  gameVersion: data.gameVersion,
  bosses: Object.keys(data.bosses),
  defaultOptions: data.defaultOptions,
  defaultChallenge: data.defaultChallenge,
  challengeFields: data.challengeFields,
  maps: data.maps,
  difficulties: data.difficulties,
  modes: data.modes,
  unsupportedModes: data.unsupportedModes,
  limits: data.limits,
});

export const DATA_FILE = fileURLToPath(new URL("../packages/share-code/src/data.json", import.meta.url));

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  writeFileSync(DATA_FILE, JSON.stringify(shareCodeData(game), null, 2) + "\n");
  console.log(`wrote ${DATA_FILE}`);
}
