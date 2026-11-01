// The "Read a share code" section: paste a code, see the setup. A link with #code=… opens a code straight away.

import { decode, extractCode } from "../code/decode";
import { codeFromHash, compareLinkTo, hashFor, linkTo } from "../code/link";
import { copyWithFeedback, h } from "../dom";
import { bossTitle, releaseSetup, renderSetup } from "../preview/render";

/** Takes the code out of the address bar, keeping the page where it is */
function clearHash(): void {
  if (codeFromHash() !== null) history.replaceState(null, "", location.pathname + location.search);
}

/** How long to wait after the last key press before reading the code */
const TYPING_DELAY = 150;

export function setUpCodeViewer(input: HTMLTextAreaElement, result: HTMLElement): (code: string) => void {
  let latest = 0;
  let typing: number | undefined;
  // What screen readers hear when the result changes: one line, not the whole setup
  const status = h("p", { class: "sr-only", role: "status" });
  result.before(status);

  const replace = (...nodes: Node[]): void => {
    releaseSetup(result);
    result.replaceChildren(...nodes);
  };

  async function show(code: string): Promise<void> {
    const request = ++latest;
    if (code.trim() === "") {
      status.textContent = "";
      replace();
      clearHash();
      return;
    }

    const decoded = await decode(code);
    // A slower, older code must not replace the newest one
    if (request !== latest) return;

    // The address bar only ever links to the setup that is shown
    if (!decoded.ok) {
      status.textContent = decoded.reason;
      replace(h("p", { class: decoded.problem === "TooNew" ? "warning" : "error" }, decoded.reason));
      clearHash();
      return;
    }

    const trimmed = extractCode(code);
    const copyCode = h("button", { class: "button small", type: "button" }, "Copy code");
    copyCode.addEventListener("click", () => copyWithFeedback(copyCode, trimmed));
    const copyLink = h("button", { class: "button small blue", type: "button" }, "Copy link");
    copyLink.addEventListener("click", () => copyWithFeedback(copyLink, linkTo(trimmed), "Link copied!"));

    // Opens the compare page with this code as A
    const compare = h("a", { class: "button small orange", href: compareLinkTo(trimmed, "") }, "Compare");

    const { options, challenge } = decoded;
    status.textContent = `Showing ${challenge.name || bossTitle(options)}: ${bossTitle(options)}, ${options.Tiers} ${options.Tiers === 1 ? "tier" : "tiers"}.`;
    replace(renderSetup(decoded.options, decoded.challenge), h("div", { class: "actions" }, copyCode, copyLink, compare));
    history.replaceState(null, "", hashFor(trimmed));
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
