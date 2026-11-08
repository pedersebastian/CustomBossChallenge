// Port of Config/ChallengeLimits.cs's clamping: how the mod brings a challenge's numbers into range. The website reads
// codes with it (decode.ts) and the Studio writes with it (studio/rules.ts, #97), so both agree with the mod.

import { clamp, float } from "./dotnet";

/** ChallengeLimits.OrDefault: the game uses negative values for "default"; anything else is kept within the limits */
export const orDefault = (value: number, min: number, max: number): number => (value < 0 ? -1 : clamp(value, min, max));

/** ChallengeLimits.Clamp: a C# float within the limits; NaN becomes the lower limit */
export function clampMultiplier(value: number, min: number, max: number): number {
  const read = float(value);
  return Number.isNaN(read) ? float(min) : clamp(read, float(min), float(max));
}
