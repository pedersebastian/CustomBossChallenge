// Links straight to a setup on the website (issue #16): #code=CBC2:… after the page's address.

import { LINK_KEY, extractCode } from "./decode";

/** The code in a link's #, or null when the # is something else (e.g. #codes, the menu's link to this section) */
export function codeFromHash(hash = location.hash): string | null {
  if (!hash.startsWith("#")) return null;
  const code = extractCode(hash);
  return code.startsWith("CBC") ? code : null;
}

/** The part after # in a link to a code. Codes only use letters, digits, - and _ after CBCn:, so they go in as they are. */
export function hashFor(code: string): string {
  return `#${LINK_KEY}${/^CBC\d+:[\w-]*$/.test(code) ? code : encodeURIComponent(code)}`;
}

export function linkTo(code: string, page = `${location.origin}${location.pathname}`): string {
  return page + hashFor(code);
}
