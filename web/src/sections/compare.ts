// The compare page (issue #35): paste two codes, see what differs. A link with #a=…&b=… opens both straight away.

import { decode, extractCode, type DecodeResult } from "@cbc/share-code";
import { compareHashFor, compareLinkTo } from "../code/link";
import { compareRows, GROUPS, tierPairs, verdict, type Setup } from "../compare/diff";
import { renderCurves } from "../compare/chart";
import { copyWithFeedback, h } from "../dom";
import { bossTitle, releaseSetup, renderSetup } from "../preview/render";

/** How long to wait after the last key press before reading the codes */
const TYPING_DELAY = 150;

const nameOf = (setup: Setup): string => setup.challenge.name || bossTitle(setup.options);

function renderDifferences(a: Setup, b: Setup): HTMLElement {
  const rows = compareRows(a, b);
  const changed = rows.filter((row) => !row.same).length;

  const table = h(
    "table",
    { class: "compare-table diff" },
    h("thead", {}, h("tr", {}, h("th", { scope: "col" }, "Option"), h("th", { scope: "col" }, "A"), h("th", { scope: "col" }, "B"))),
  );
  for (const group of GROUPS) {
    const inGroup = rows.filter((row) => row.group === group);
    if (inGroup.length === 0) continue;
    const body = h(
      "tbody",
      { class: inGroup.every((row) => row.same) ? "same" : undefined },
      h("tr", { class: "group" }, h("th", { scope: "colgroup", colspan: "3" }, group)),
    );
    for (const row of inGroup) {
      body.append(
        h(
          "tr",
          { class: row.same ? "same" : "changed" },
          // "≠" marks a difference without colour; screen readers hear it in words
          h("th", { scope: "row" }, row.same ? null : h("span", { class: "differs", "aria-hidden": "true" }, "≠ "), row.label, row.same ? h("span", { class: "sr-only" }, " (the same)") : null),
          h("td", {}, row.a),
          h("td", {}, row.b),
        ),
      );
    }
    table.append(body);
  }

  const toggle = h("input", { type: "checkbox", id: "show-same" });
  toggle.addEventListener("change", () => table.classList.toggle("show-same", toggle.checked));

  return h(
    "div",
    { class: "block" },
    h("h2", {}, changed === 0 ? "No differences" : `${changed} ${changed === 1 ? "difference" : "differences"}`),
    h("label", { class: "toggle" }, toggle, ` Show the ${rows.length - changed} options that are the same`),
    h("div", { class: "table-scroll" }, table),
  );
}

function renderComparison(a: Setup, b: Setup, codes: { a: string; b: string }): Node[] {
  const lines = verdict(a, b);
  const same = compareRows(a, b).every((row) => row.same);

  const summary = h(
    "div",
    { class: "block verdict" },
    h("h2", {}, "In short"),
    same
      ? h("p", {}, "The two setups are the same.")
      : lines.length
        ? h("ul", {}, ...lines.map((line) => h("li", {}, line)))
        : h("p", {}, "The boss has the same tiers, health and speed in both. The other differences are below."),
  );

  const side = (label: string, setup: Setup): HTMLElement =>
    h("div", { class: "compare-side" }, h("span", { class: "side-label", "aria-hidden": "true" }, label), h("span", { class: "sr-only" }, `Setup ${label}`), renderSetup(setup.options, setup.challenge, true));

  const copyLink = h("button", { class: "button small blue", type: "button" }, "Copy link");
  copyLink.addEventListener("click", () => copyWithFeedback(copyLink, compareLinkTo(codes.a, codes.b), "Link copied!"));

  return [
    summary,
    renderDifferences(a, b),
    renderCurves(tierPairs(a, b), { a: nameOf(a), b: nameOf(b) }),
    h("div", { class: "block" }, h("h2", {}, "Side by side"), h("div", { class: "compare-sides" }, side("A", a), side("B", b))),
    h("div", { class: "actions" }, copyLink),
  ];
}

export function setUpCompare(inputA: HTMLTextAreaElement, inputB: HTMLTextAreaElement, result: HTMLElement, swap: HTMLButtonElement): (a: string, b: string) => void {
  let latest = 0;
  let typing: number | undefined;
  // What screen readers hear when the result changes: one line, not the whole comparison
  const status = h("p", { class: "sr-only", role: "status" });
  result.before(status);

  const replace = (...nodes: Node[]): void => {
    releaseSetup(result);
    result.replaceChildren(...nodes);
  };

  async function show(): Promise<void> {
    const request = ++latest;
    const codes = { a: extractCode(inputA.value), b: extractCode(inputB.value) };
    const read = async (code: string): Promise<DecodeResult | null> => (code === "" ? null : decode(code));
    const [a, b] = await Promise.all([read(codes.a), read(codes.b)]);
    // Slower, older codes must not replace the newest ones
    if (request !== latest) return;

    // The address bar only links to codes that can be read
    history.replaceState(null, "", location.pathname + location.search + compareHashFor(a?.ok ? codes.a : "", b?.ok ? codes.b : ""));

    const problems = ([["A", a], ["B", b]] as const).flatMap(([label, decoded]) =>
      decoded && !decoded.ok ? [h("p", { class: decoded.problem === "TooNew" ? "warning" : "error" }, `${label}: ${decoded.reason}`)] : [],
    );
    if (problems.length) {
      status.textContent = problems.map((problem) => problem.textContent).join(" ");
      return replace(...problems);
    }
    if (!a?.ok || !b?.ok) {
      const hint = a || b ? `Paste a code for ${a ? "B" : "A"} as well to compare the two.` : "";
      status.textContent = hint;
      return replace(...(hint ? [h("p", { class: "muted" }, hint)] : []));
    }

    const changed = compareRows(a, b).filter((row) => !row.same).length;
    status.textContent =
      changed === 0 ? "No differences: the two setups are the same." : `${changed} ${changed === 1 ? "difference" : "differences"} between A and B.`;
    replace(...renderComparison(a, b, codes));
  }

  const later = (): void => {
    clearTimeout(typing);
    typing = window.setTimeout(() => void show(), TYPING_DELAY);
  };
  inputA.addEventListener("input", later);
  inputB.addEventListener("input", later);

  swap.addEventListener("click", () => {
    [inputA.value, inputB.value] = [inputB.value, inputA.value];
    clearTimeout(typing);
    void show();
  });

  return (a: string, b: string) => {
    clearTimeout(typing);
    inputA.value = a;
    inputB.value = b;
    void show();
  };
}
