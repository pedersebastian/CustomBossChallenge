// Test helper: a code for any payload, even one the Boss Creator can't make, packed the way the website packs codes

import { pack } from "@cbc/share-code";

/** Raw deflate in base64url after the prefix */
export const encode = (payload: object | string, prefix = "CBC2:"): Promise<string> =>
  pack(typeof payload === "string" ? payload : JSON.stringify(payload), prefix);
