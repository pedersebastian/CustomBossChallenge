// @cbc/share-code: read, check and make Custom Boss Challenge share codes (CBC1: to CBC5:) exactly like the mod does
// (#98). The mod's own code is the reference: Persistence/CodeFormat.cs, SetupCode.cs, Config/BossOptions.cs,
// ChallengeLimits.cs, TierMath.cs, SkullMath.cs and TierBossMath.cs. The test vectors in web/test are shared with the
// mod's C# tests, so both sides read and write codes the same way.

import { DATA, type JsonObject } from "./data";

// Read: a code (or a link to one, or pasted text) to the setup the mod would load
export {
  decode,
  extractCode,
  unescapeDataString,
  filterChallenge,
  apply,
  playProblem,
  cleanName,
  PREFIX,
  VERSION2_PREFIX,
  VERSION3_PREFIX,
  VERSION4_PREFIX,
  VERSION5_PREFIX,
  LINK_KEY,
  type Challenge,
  type CodeProblem,
  type DecodeResult,
} from "./decode";

// What a browser needs for codes (#181)
export { canUseCodes, UNSUPPORTED_REASON, UnsupportedBrowserError } from "./support";

// Make: a setup to a code the mod loads unchanged
export { encode, payload, prefixFor, normalized, pack, diff, deepEqual } from "./encode";

// The Boss Creator's options, read and kept in range like BossOptions
export * from "./options";

export type { Json, JsonObject, ShareCodeData } from "./data";

// The helpers, each also on its own path, e.g. "@cbc/share-code/tiers"
export * as tiers from "./math/tiers";
export * as skulls from "./math/skulls";
export * as tierBosses from "./math/tierBosses";
export * as limits from "./math/limits";
export * as warnings from "./math/warnings";
export type { TierBoss } from "./math/tierBosses";
export type { SetupFacts, SetupWarning, TierBase, Warning } from "./math/warnings";

/** Every code prefix this version reads, oldest first */
export const PREFIXES = ["CBC1:", "CBC2:", "CBC3:", "CBC4:", "CBC5:"] as const;

/** The game version the package's data was taken from */
export const gameVersion: string = DATA.gameVersion;

/** The mod's limits (Config/ChallengeLimits.cs and SetupCode) */
export const LIMITS = DATA.limits;

/** The game data the codes are made against */
export const data = DATA;

/** A copy of the challenge codes are made against: the game's new Challenge Editor challenge */
export const defaultChallenge = (): JsonObject => JSON.parse(JSON.stringify(DATA.defaultChallenge)) as JsonObject;
