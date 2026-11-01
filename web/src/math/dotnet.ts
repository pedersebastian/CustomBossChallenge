// .NET number behaviour the mod's C# relies on, so the website gives exactly the same numbers

/** C# Math.Round(value, digits): halfway values go to the even neighbour */
export function roundHalfEven(value: number, digits = 0): number {
  const factor = 10 ** digits;
  const scaled = value * factor;
  const floor = Math.floor(scaled);
  const diff = scaled - floor;
  const epsilon = 1e-9 * Math.max(1, Math.abs(scaled));
  let rounded: number;
  if (Math.abs(diff - 0.5) <= epsilon) rounded = floor % 2 === 0 ? floor : floor + 1;
  else rounded = Math.round(scaled);
  return rounded / factor;
}

/** A C# float: the value as a 32-bit float, then widened back to a double like C# does */
export const float = (value: number): number => Math.fround(value);

/** Math.Clamp */
export const clamp = (value: number, min: number, max: number): number => Math.min(Math.max(value, min), max);

/**
 * value.ToString("0.0") / ToString("0") for a positive number: .NET rounds the shortest text of the double halfway away
 * from zero, so 1.45 (really 1.4499999999999999556) gives 1.5, where toFixed rounds the exact value and gives 1.4
 */
export function toFixedNet(value: number, digits: number): string {
  const text = String(Math.abs(value));
  if (text.includes("e")) return value.toFixed(digits);
  const [whole, fraction = ""] = text.split(".");
  const kept = BigInt(whole + fraction.padEnd(digits, "0").slice(0, digits)) + (fraction.length > digits && fraction[digits] >= "5" ? 1n : 0n);
  const padded = kept.toString().padStart(digits + 1, "0");
  const result = digits === 0 ? padded : `${padded.slice(0, -digits)}.${padded.slice(-digits)}`;
  return (value < 0 && kept !== 0n ? "-" : "") + result;
}

/**
 * value.ToString("0.#") (or "0.##" with 2 digits) for positive numbers: rounded like .NET (see toFixedNet), without
 * zeros at the end
 */
export function formatDecimals(value: number, digits: number): string {
  const text = toFixedNet(value, digits);
  return text.includes(".") ? text.replace(/\.?0+$/, "") : text;
}
