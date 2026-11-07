// The Studio page's entry point (studio.html, #12): make a setup, or change one from a code, and copy its code
import "./site";
import { decode, extractCode } from "./code/decode";
import { codeFromHash } from "./code/link";
import { newSetup } from "./studio/state";
import { setUpStudio } from "./studio/render";

const byId = <T extends HTMLElement>(id: string): T => document.getElementById(id) as T;

const studio = setUpStudio(
  byId<HTMLFormElement>("studio-controls"),
  byId<HTMLTextAreaElement>("studio-code"),
  byId("studio-actions"),
  byId("studio-preview"),
);
const startFrom = byId<HTMLDetailsElement>("start-from");
const startCode = byId<HTMLTextAreaElement>("start-code");
const problem = byId<HTMLParagraphElement>("start-problem");

/** Starts from a code; a code that can't be read says why and leaves the setup as it is */
async function open(code: string): Promise<boolean> {
  const read = await decode(code);
  if (!read.ok) {
    problem.textContent = read.reason;
    problem.hidden = false;
    startFrom.open = true;
    startCode.value = code;
    return false;
  }
  problem.hidden = true;
  studio.load({ options: read.options, challenge: read.json }, extractCode(code));
  return true;
}

byId("start-load").addEventListener("click", () => void open(startCode.value));
byId("start-new").addEventListener("click", () => {
  problem.hidden = true;
  startCode.value = "";
  studio.load(newSetup(), null);
});

const fromLink = codeFromHash();
if (fromLink) {
  void open(fromLink).then((ok) => {
    if (!ok) studio.load(newSetup(), null);
  });
} else {
  studio.load(newSetup(), null);
}

// Another link pasted into the address bar of an open tab, or Back. The page's own changes to the address
// (replaceState) don't come here.
window.addEventListener("hashchange", () => {
  const code = codeFromHash();
  if (code && code !== studio.code()) void open(code);
});
