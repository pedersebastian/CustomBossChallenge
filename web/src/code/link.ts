// Links straight to a setup on the website (issue #16): #code=CBC2:… after the page's address.

import { LINK_KEY, extractCode } from "./decode";

/** The code in a link's #, or null when the # is something else (e.g. #codes, the menu's link to this section) */
export function codeFromHash(hash = location.hash): string | null {
  if (!hash.startsWith("#")) return null;
  const code = extractCode(hash);
  return code.startsWith("CBC") ? code : null;
}

/** A code in a link: as it is when it's a plain code, %-encoded otherwise */
const linkCode = (code: string): string => (/^CBC\d+:[\w-]*$/.test(code) ? code : encodeURIComponent(code));

/** The part after # in a link to a code. Codes only use letters, digits, - and _ after CBCn:, so they go in as they are. */
export function hashFor(code: string): string {
  return `#${LINK_KEY}${linkCode(code)}`;
}

export function linkTo(code: string, page = `${location.origin}${location.pathname}`): string {
  return page + hashFor(code);
}

/** Whether the address's # links to a setup (#code=…, #CBC2:…) or a comparison (#a=…&b=…), which a link within the page must keep */
export function keepsSetup(hash = location.hash): boolean {
  if (codeFromHash(hash) !== null) return true;
  const { a, b } = compareCodesFromHash(hash);
  return a !== "" || b !== "";
}

/** The page that compares two setups (issue #35), next to the main page */
export const COMPARE_PAGE = "compare.html";

/** The part after # in a link to a comparison: #a=CBC2:…&b=CBC2:…, either one can be left out */
export function compareHashFor(a: string, b: string): string {
  const parts = [a && `a=${linkCode(a)}`, b && `b=${linkCode(b)}`].filter(Boolean);
  return parts.length ? `#${parts.join("&")}` : "";
}

/** The two codes in a comparison link's #, "" for one that isn't there */
export function compareCodesFromHash(hash = location.hash): { a: string; b: string } {
  const codes = { a: "", b: "" };
  if (!hash.startsWith("#")) return codes;
  for (const part of hash.slice(1).split("&")) {
    const match = /^([ab])=(.*)$/.exec(part);
    if (!match) continue;
    const code = extractCode(`#${match[2]}`);
    if (code.startsWith("CBC")) codes[match[1] as "a" | "b"] = code;
  }
  return codes;
}

/** The page that makes and edits setups (#12), next to the main page */
export const STUDIO_PAGE = "studio.html";

/** A link that opens a code on the Studio page to edit it, from any page of the site */
export function studioLinkTo(code: string, page = new URL(STUDIO_PAGE, location.href).href): string {
  return page + hashFor(code);
}

/** A link to the comparison page, from any page of the site */
export function compareLinkTo(a: string, b: string, page = new URL(COMPARE_PAGE, location.href).href): string {
  return page + compareHashFor(a, b);
}
