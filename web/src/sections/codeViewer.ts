// The "Read a share code" section: paste a code, see the setup. A link with #code=… opens a code straight away.

import { decode } from "../code/decode";
import { copyWithFeedback, h } from "../dom";
import { releaseSetup, renderSetup } from "../preview/render";

const HASH_KEY = "code=";

export function codeFromHash(hash = location.hash): string | null {
  const text = hash.replace(/^#/, "");
  return text.startsWith(HASH_KEY) ? decodeURIComponent(text.slice(HASH_KEY.length)) : null;
}

export function linkTo(code: string): string {
  return `${location.origin}${location.pathname}#${HASH_KEY}${encodeURIComponent(code)}`;
}

/** How long to wait after the last key press before reading the code */
const TYPING_DELAY = 150;

export function setUpCodeViewer(input: HTMLTextAreaElement, result: HTMLElement): (code: string) => void {
  let latest = 0;
  let typing: number | undefined;

  const replace = (...nodes: Node[]): void => {
    releaseSetup(result);
    result.replaceChildren(...nodes);
  };

  async function show(code: string): Promise<void> {
    const request = ++latest;
    if (code.trim() === "") {
      replace();
      return;
    }

    const decoded = await decode(code);
    // A slower, older code must not replace the newest one
    if (request !== latest) return;

    if (!decoded.ok) {
      replace(h("p", { class: decoded.problem === "TooNew" ? "warning" : "error" }, decoded.reason));
      return;
    }

    const trimmed = code.trim();
    const copyCode = h("button", { class: "button small", type: "button" }, "Copy code");
    copyCode.addEventListener("click", () => copyWithFeedback(copyCode, trimmed));
    const copyLink = h("button", { class: "button small blue", type: "button" }, "Copy link");
    copyLink.addEventListener("click", () => copyWithFeedback(copyLink, linkTo(trimmed), "Link copied!"));

    replace(renderSetup(decoded.options, decoded.challenge), h("div", { class: "actions" }, copyCode, copyLink));
    history.replaceState(null, "", `#${HASH_KEY}${encodeURIComponent(trimmed)}`);
  }

  input.addEventListener("input", () => {
    clearTimeout(typing);
    typing = window.setTimeout(() => void show(input.value), TYPING_DELAY);
  });

  return (code: string) => {
    clearTimeout(typing);
    input.value = code;
    void show(code);
  };
}
