// What a browser needs to read and make codes: CompressionStream and DecompressionStream (deflate-raw). Safari before
// 16.4, Firefox before 113 and some in-app browsers don't have them, and without a check of its own every code looked
// damaged there (#181).

/** Whether this browser can read and make codes */
export const canUseCodes = (): boolean =>
  typeof globalThis.CompressionStream === "function" && typeof globalThis.DecompressionStream === "function";

/** What a page says in a browser that can't read codes */
export const UNSUPPORTED_REASON =
  "This browser can't read setup codes. Try a newer one: Chrome, Edge or Firefox 113 or later, or Safari 16.4 or later.";

/** Thrown by encode and pack in a browser that can't make codes */
export class UnsupportedBrowserError extends Error {
  constructor() {
    super(UNSUPPORTED_REASON);
    this.name = "UnsupportedBrowserError";
  }
}
