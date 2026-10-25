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
 * value.ToString("0.#") (or "0.##" with 2 digits) for positive numbers: .NET rounds the exact value halfway away from
 * zero, which is what toFixed does too, and leaves out zeros at the end
 */
export function formatDecimals(value: number, digits: number): string {
  const text = value.toFixed(digits);
  return text.includes(".") ? text.replace(/\.?0+$/, "") : text;
}
