// .NET number behaviour the mod's C# relies on, so the website gives exactly the same numbers

/**
 * C# Math.Round(value, digits): like .NET, the value is scaled by 10^digits, the scaled double is rounded exactly, with
 * only a true .5 going to the even neighbour, and scaled back. No tolerance: a skull of 2.125 % is 0.02125, and
 * 0.02125 * 10000 is 212.50000000000003, which .NET rounds up to 2.13 %; a tolerance that called it halfway gave 2.12 %
 * (#113).
 */
export function roundHalfEven(value: number, digits = 0): number {
  const factor = 10 ** digits;
  const scaled = value * factor;
  const floor = Math.floor(scaled);
  const diff = scaled - floor;
  const rounded = diff === 0.5 ? (floor % 2 === 0 ? floor : floor + 1) : Math.round(scaled);
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

/**
 * C# char.IsWhiteSpace. JavaScript's trim and \s use another set: they also strip U+FEFF and leave U+0085, so a code
 * or option with one of those read differently in the mod and on the website (#174).
 */
export function isWhiteSpace(c: string): boolean {
  const code = c.charCodeAt(0);
  return (
    (code >= 0x09 && code <= 0x0d) ||
    code === 0x20 ||
    code === 0x85 ||
    code === 0xa0 ||
    code === 0x1680 ||
    (code >= 0x2000 && code <= 0x200a) ||
    code === 0x2028 ||
    code === 0x2029 ||
    code === 0x202f ||
    code === 0x205f ||
    code === 0x3000
  );
}

/** C# string.Trim() */
export function netTrim(text: string): string {
  let start = 0;
  let end = text.length;
  while (start < end && isWhiteSpace(text[start])) start++;
  while (end > start && isWhiteSpace(text[end - 1])) end--;
  return text.slice(start, end);
}

/** C# string.TrimEnd() */
export function netTrimEnd(text: string): string {
  let end = text.length;
  while (end > 0 && isWhiteSpace(text[end - 1])) end--;
  return text.slice(0, end);
}

/** C# string.IsNullOrWhiteSpace */
export const isNullOrWhiteSpace = (text: string | null | undefined): boolean => text == null || netTrim(text) === "";

/**
 * The text with only A-Z made lower case. Every name the mod compares with StringComparison.OrdinalIgnoreCase is
 * ASCII, and against ASCII that only matches ASCII letters: toLowerCase would also turn the Kelvin sign K into k.
 */
export const toLowerAscii = (text: string): string => text.replace(/[A-Z]/g, (c) => String.fromCharCode(c.charCodeAt(0) + 32));

/** string.Equals(a, b, StringComparison.OrdinalIgnoreCase) where one of them is ASCII */
export const equalsIgnoreCase = (a: string, b: string): boolean => toLowerAscii(a) === toLowerAscii(b);

