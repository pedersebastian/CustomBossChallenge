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
  "license": null,                    // "CC BY-NC-SA 4.0" or "CC BY-SA 3.0" when the file uses a wiki's content
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
| `dates` | `start`/`end` are guessed (the one free week between the boss's previous and next event) or only approximate |

### Events dated by hand

Three events are in Ninja Kiwi's boss data but not on Blooncyclopedia (#152). They were dated from the old Bloons wiki
on Fandom, which lists the events per game version, and from the `#bosses` channel of the BTD6 Index Discord server.
The table is `knownEvents` in `tools/boss-events/records.mjs`.

| File | What is known |
|---|---|
| `Vortex17_lfbrv7rb.json` | Week 87a, 18 Mar 2023. Pulled after about four hours and replaced by Vortex17EX (week 87b). Fandom has its id. |
| `Lych23.json` | Week 62, 24–29 Sep 2022, the week the free week gave. The id isn't known. |
| `Bloonarius40.json` | Week 99, Least Cash. Pulled early for exploits, about 8–10 Jun 2023, so the times are approximate. The id isn't known. |

`BossDataDecrypted/Phayze_2.json` has the same rules as `Phayze2`: a copy, not another event.

An event's id ends with the time Ninja Kiwi made it, in base 36 milliseconds (`lfbrv7rb` is 16 Mar 2023). An id made
after the event's start (Vortex17EX, Dreadbloon32New) belongs to an event that replaced another one.

## Making the files

New events are saved every day by `.github/workflows/boss-events.yml`, which runs `tools/boss-events/archive.mjs`. It
reads Ninja Kiwi's list of events (`https://data.ninjakiwi.com/btd6/bosses`) and, for every id the archive doesn't
have yet, the `standard` and `elite` metadata. A file has the list entry's fields that don't change while the event
runs (not the scores or the links) and the metadata's `body` as the API gave it, with `source: "api"`.

- A file is never changed or deleted, by the archiver or the backfill.
- Only answers with `success: true` are saved. An event whose metadata fails is tried again on the next run, not
  saved half. An id that never gets metadata is tried every day: Ninja Kiwi sometimes puts an event up again under a
  new id, and then the other id has the rules.
- The API only lists the last ~7 events, so a run that fails is caught up by the next ones. A failing run sends the
  normal Actions e-mail.
- An event is saved as soon as it is listed, while it runs, so the play counts in its metadata are from that day.

`tools/boss-events/backfill.mjs` made the files from before the archiver; see
[CONTRIBUTING.md](../../CONTRIBUTING.md#boss-events-archive).

## `boss-events.json`

All files in one, published with the website at
<https://pedersebastian.github.io/CustomBossChallenge/boss-events.json> and built again on every deploy (it isn't
committed). The Boss Creator's Past Events list (#146) reads it.

```jsonc
{
  "formatVersion": 1,                     // of this file; a reader skips a newer version it doesn't know
  "generated": "2026-11-12T06:17:42.000Z", // when it was built
  "license": "...",                       // which events are under CC BY-NC-SA 4.0, see below
  "events": [ ... ]                       // every file above, as it is, oldest first (by start, then id)
}
```

Each event keeps its own `formatVersion`, `source`, `sourceUrls`, `license` and `lossy`. Events with
`"license": "CC BY-NC-SA 4.0"` use Blooncyclopedia's content and events with `"license": "CC BY-SA 3.0"` use the
Fandom wiki's, and are shared under that licence; the file's `license` field says so too.

## Credits and licence

- Rules and events © Ninja Kiwi, from [Ninja Kiwi's Open Data](https://data.ninjakiwi.com/).
- [jxu/btd6-events](https://github.com/jxu/btd6-events), which archived the API every week and holds the boss data
  Ninja Kiwi gave its author. Used with the author's permission
  ([jxu/btd6-events#3](https://github.com/jxu/btd6-events/issues/3)); a link is the credit they asked for.
- [Blooncyclopedia](https://www.bloonswiki.com/), whose content is under
  [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). Files with `"license": "CC BY-NC-SA 4.0"`
  use its content (rules, dates or scoring) and are shared under the same licence, with the pages they come from in
  `sourceUrls`.
- The [Bloons wiki on Fandom](https://bloons.fandom.com/), whose content is under
  [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Files with `"license": "CC BY-SA 3.0"` use its
  dates and ids, and are shared under the same licence, with the pages in `sourceUrls`.
- The code that made the files is under the repository's MIT licence.
