// The share-code package's copy of the game data (#98) is what scripts/sync-share-code-data.mjs makes from game.json,
// which the mod's WebDataTest writes. After new game data, run `npm run sync-data`.

import { expect, it } from "vitest";
import game from "../src/data/game.json";
import copy from "../packages/share-code/src/data.json";
// @ts-expect-error a plain JS script, without types
import { shareCodeData } from "../scripts/sync-share-code-data.mjs";

it("the share-code package has the game data in game.json (else run npm run sync-data)", () => {
  expect(copy).toEqual(shareCodeData(game));
});
