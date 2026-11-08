# @cbc/share-code

Read, check and make share codes for the [Custom Boss Challenge](https://github.com/pedersebastian/CustomBossChallenge)
BTD6 mod: the `CBC1:` to `CBC4:` codes the Boss Creator copies and pastes. A code read here gives the same setup the
mod loads, and a code made here loads in the mod unchanged.

The [website](https://pedersebastian.github.io/CustomBossChallenge/) uses this package for every code it reads and makes.

## Install

The package lives in the mod's repository, in `web/packages/share-code`, and isn't on npm yet. The website uses it as
an npm workspace: `"@cbc/share-code": "*"` in `web/package.json`. It is TypeScript source with no dependencies, so a
bundler like Vite, or Vitest, reads it as it is.

It needs only standard web APIs: `CompressionStream` and `DecompressionStream`, `atob` and `btoa`, and `TextDecoder`.
Every current browser has them, and so does Node 18 and later.

## Read a code

```ts
import { decode } from "@cbc/share-code";

const result = await decode("https://pedersebastian.github.io/CustomBossChallenge/#code=CBC1:RYwxCsJAEEWvIr9ewiaCxbQpUqXSC6xmJAuTmeDEKuzd3SBo-d9_vB0G2nHL_HJQWwIex76LmY425ef3qMTcryvzNL5ly6tUDjo3MWDmJNv8xz8dFJu21KSmhUEYJLmf-qRqigCvMVAXu0spHw");
if (result.ok) {
  console.log(result.options.Boss, result.options.Tiers); // the Boss Creator's options
  console.log(result.challenge.map, result.challenge.cash); // the challenge, in the ranges the mod keeps
} else {
  console.log(result.problem, result.reason); // "Invalid" or "TooNew", and why
}
```

`decode` takes a code, a link to the website with `#code=…`, or pasted text with a code in a link, like the mod's
Paste button. It never throws: anything it can't read comes back as `ok: false`. `result.json` is the whole
challenge as the game's JSON, with the code's changes on top of the defaults.

## Make a code

```ts
import { decode, defaultOptions, encode } from "@cbc/share-code";

const options = { ...defaultOptions(), Boss: "Lych", Elite: true, Tiers: 7, HealthPerTier: 20 };
const code = await encode(options); // "CBC2:…"

const read = await decode(code);
console.log(read.ok && read.options.Tiers); // 7
```

`encode(options, challenge?)` brings the options into the Boss Creator's ranges first (`normalized`), writes only what
differs from a new setup, and picks the oldest prefix that holds it (`prefixFor`): `CBC2:` for speed or health per
tier, `CBC3:` for a boss per tier, `CBC4:` for Boss Event Rounds. Pass the game's JSON of a challenge, for example
`result.json` from `decode`, to keep its rules; without one, the code uses the game's new challenge.

## Change a code

```ts
import { decode, encode } from "@cbc/share-code";

const result = await decode("CBC2:q1bKV7KqVnLKLy5WslLyqUzOUNJRcs3JLElVsiopKk3VUQrJTC0Cyhka6Ch5pCbmlGQEpBaBxJSsjA1qdZSSQdrzEnOB6pV8E4sSSzLy8xSg5hSnpqYoWRkZGJnV1gIA");
if (result.ok) {
  const json = structuredClone(result.json);
  json.map = "Infernal";
  const code = await encode({ ...result.options, Tiers: 5 }, json);
}
```

## Check a setup

```ts
import { decode, effectiveSpawnRounds, playProblem, tiers } from "@cbc/share-code";

const result = await decode("CBC4:q1bKV7KqVnLKLy5WslJyKUpNTEnKyc_PU9JRCi5ILM8Lyi_NSwFJGRnoKBiZ6igYA2ljIG1iAFQC0uZalppXAlNWUlSaWqujlAwyMy8xNxWoMbggNTWlqDRPAcXwYqCgkpWRgZFZbS0A");
if (result.ok) {
  const rounds = effectiveSpawnRounds(result.options); // the round each tier spawns on
  console.log(tiers.endRound(rounds)); // the round the game ends on
  console.log(playProblem(result.challenge, rounds)); // why Play would refuse it, or null
}
```

## What else is here

- **Options:** `defaultOptions`, `readOptions`, `typedOptions` and `sanitize` read and keep the Boss Creator's options
  like the mod's `BossOptions`. There are also `bossOf`, `tierBossName`, `isMixed`, `skullPercents` and more for a
  setup's bosses and skulls.
- **Challenge:** `filterChallenge` keeps only what a code may set (the mod's `CodeFormat.ChallengeFields`).
  `cleanName` cleans a name like the mod does.
- **Helpers:** `tiers` (spawn rounds, health and speed per tier), `skulls`, `tierBosses` (the per-tier tokens like
  `"LychElite@120/80#4"`) and `limits` (the mod's clamping). Each is also on its own path, e.g.
  `@cbc/share-code/tiers`.
- **Data:** `data` holds the game data the codes are made against (`gameVersion`, the default challenge, the maps,
  modes and limits), and `PREFIXES`, `LIMITS` and `defaultChallenge()` give parts of it.

## Same as the mod

- **The mod is the reference.** Each part is a port of the mod's C#: `Persistence/CodeFormat.cs` and `SetupCode.cs`,
  and `Config/BossOptions.cs`, `ChallengeLimits.cs`, `TierMath.cs`, `SkullMath.cs` and `TierBossMath.cs`. The comments
  name the C# each function follows.
- **Same JSON, not the same bytes.** The mod packs a code with .NET's `DeflateStream` and this package with the
  browser's `CompressionStream`. The JSON inside is the same, key for key, and the mod reads any raw deflate.
- **Shared test vectors.** The website's tests (`web/test`) and the mod's C# tests (`UnitTests/`, `PropertyTests/`)
  read the same files:
  - codes made in the game, with what the mod reads from them
  - 200 random codes from the property tests
  - codes this package made, which the mod's `CodeFormat` must load unchanged
  - cases for links, skulls, options and tier bosses

  CI runs both sides.
- **Game data.** `src/data.json` is a copy of part of `web/src/data/game.json`, which the mod's `WebDataTest` writes
  from the game. After a game update, run `npm run sync-data` in `web/`. A test fails while the copy is out of date.
