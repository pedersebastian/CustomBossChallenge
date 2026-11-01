import "./site";
import { extractCode } from "./code/decode";
import { codeFromHash } from "./code/link";
import { setUpCodeViewer } from "./sections/codeViewer";
import { setUpExamples } from "./sections/examples";

const input = document.getElementById("code-input") as HTMLTextAreaElement;
const result = document.getElementById("code-result") as HTMLElement;
const open = setUpCodeViewer(input, result);

const fromLink = codeFromHash();
if (fromLink) {
  open(fromLink);
  document.getElementById("codes")?.scrollIntoView();
}

// Another link pasted into the address bar of an open tab, or Back to an earlier code. The code viewer's own changes
// to the address (replaceState) don't come here.
window.addEventListener("hashchange", () => {
  const code = codeFromHash();
  if (code && code !== extractCode(input.value)) {
    open(code);
    document.getElementById("codes")?.scrollIntoView();
  }
});

void setUpExamples(document.getElementById("example-list") as HTMLElement, open);
