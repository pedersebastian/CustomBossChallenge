# Boss events archive

Every official Boss Bloon Event of Bloons TD 6 since July 2021, one file per event: `<id>.json`, named by Ninja
Kiwi's event id. Used for the Past Events list in the Boss Creator (#143). Files are only ever added, never changed by
the archiver.

## Format (`formatVersion` 1)

```jsonc
{
  "formatVersion": 1,
  "id": "Vortex45_mqa0rl1r",          // Ninja Kiwi's event id
  "name": "Vortex45",
  "bossType": "vortex",               // bloonarius, lych, vortex, dreadbloon, phayze, blastapopoulos, diamondback
  "start": 1781316000000,             // Unix time in milliseconds, like the API
  "end": 1781751600000,
  "normalScoringType": "GameTime",    // GameTime, LeastCash or LeastTiers; missing when not known
  "eliteScoringType": "LeastTiers",
  "source": "jxu-api",                // where the rules come from, see below
  "sourceUrls": ["https://..."],
  "license": null,                    // "CC BY-NC-SA 4.0" when anything in the file comes from Blooncyclopedia
  "lossy": [],                        // what this file doesn't know, see below
  "standard": { ... },                // the rules, in the shape of the API's metadata
  "elite": { ... }
}
```

`standard` and `elite` have the shape of the body of Ninja Kiwi's
`https://data.ninjakiwi.com/btd6/bosses/<id>/metadata/standard` (and `/elite`): `map`, `mode`, `difficulty`,
`startingCash`, `lives`, `maxLives`, `startRound`, `endRound`, `roundSets`, `maxTowers`, `maxParagons`, the
`disable...` flags, `_bloonModifiers` (with `bossSpeedMultiplier` and `healthMultipliers.boss`) and `_towers`.
Files from the API keep every field the API gave, also the ones the mod doesn't use (`mapURL`, play counts).

Things to know when reading them:

- **Towers that aren't listed in `_towers` are not allowed.** The API also lists some of those with `max: 0`; the
  older sources only list the towers that are allowed. A tower added to the game after the event isn't listed.
- `max: -1` means no limit. `maxLives` and `maxTowers` are `-1` (the game's default) in the older sources where the
  API writes `5000` and `9999`.
- `roundSets` always starts with `"default"`, like the API: `["default", "vortex"]` is the Vortex boss rounds.
  Event-only sets like `chimpsbloonarius` aren't in the game files (#139).
- Odd values (`null`s, multipliers up to ×25, `0` as scoring type) are kept as they were. The mod sanitizes them like
  a share code (`ChallengeLimits.Sanitize`).
- `mode` uses the game's ids (`Clicks` is CHIMPS, `DoubleMoabHealth` is Double HP MOABs), `map` the game's map ids.

### `source`

From best to worst. A file is only replaced by a better source, and never when it came from the API.

| `source` | What it is | Events |
|---|---|---|
| `api` | Saved by the archiver (#144) from Ninja Kiwi's Open Data API | from Nov 2026 |
| `jxu-api` | The API's metadata as archived every week by [jxu/btd6-events](https://github.com/jxu/btd6-events) | Nov 2024 – Nov 2026 |
| `nk-dcm` | The game's own challenge models, which Ninja Kiwi gave jxu/btd6-events (`BossDataDecrypted`), dated with the wiki | Dec 2021 – 2024 |
| `bloonswiki` | The rules on [Blooncyclopedia](https://www.bloonswiki.com/Event_history:Boss_Bloon_Event)'s event history pages | 2021 – 2025 |

Where the sources overlap (2022–2025), the wiki's rules were checked against Ninja Kiwi's: they match, towers and
blocked upgrade paths included, apart from the `maxLives`/`maxTowers` defaults above.

### `lossy`

| Value | Meaning |
|---|---|
| `scoringType` | The scoring type isn't known (the oldest events were ranked on time) |
| `maxParagons` | The source has no paragon limit |
| `id` | Not Ninja Kiwi's id: the event is missing from the wiki, so the file is named after the event |
| `dates` | `start`/`end` are guessed: the one free week between the boss's previous and next event |

## Making the files

`tools/boss-events/backfill.mjs` made the files from before the archiver; see
[CONTRIBUTING.md](../../CONTRIBUTING.md#boss-events-archive).

## Credits and licence

- Rules and events © Ninja Kiwi, from [Ninja Kiwi's Open Data](https://data.ninjakiwi.com/).
- [jxu/btd6-events](https://github.com/jxu/btd6-events), which archived the API every week and holds the boss data
  Ninja Kiwi gave its author. Used with the author's permission
  ([jxu/btd6-events#3](https://github.com/jxu/btd6-events/issues/3)); a link is the credit they asked for.
- [Blooncyclopedia](https://www.bloonswiki.com/), whose content is under
  [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). Files with `"license": "CC BY-NC-SA 4.0"`
  use its content (rules, dates or scoring) and are shared under the same licence, with the pages they come from in
  `sourceUrls`. The code that made them is under the repository's MIT licence.
