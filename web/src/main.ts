// The fonts are part of the site, so they load nothing from other servers (only the visit counter in index.html does).
// The site is in English: Latin is enough.
import "@fontsource/luckiest-guy/latin-400.css";
import "@fontsource/nunito/latin-400.css";
import "@fontsource/nunito/latin-700.css";
import "@fontsource/nunito/latin-800.css";
import { extractCode } from "./code/decode";
import { codeFromHash } from "./code/link";
import { GAME } from "./data/types";
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

const version = document.getElementById("game-version");
if (version) version.textContent = `Boss data from BTD6 ${GAME.gameVersion}`;
