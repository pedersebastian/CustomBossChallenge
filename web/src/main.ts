import { GAME } from "./data/types";
import { codeFromHash, setUpCodeViewer } from "./sections/codeViewer";
import { setUpExamples } from "./sections/examples";

const input = document.getElementById("code-input") as HTMLTextAreaElement;
const result = document.getElementById("code-result") as HTMLElement;
const open = setUpCodeViewer(input, result);

const fromLink = codeFromHash();
if (fromLink) {
  open(fromLink);
  document.getElementById("codes")?.scrollIntoView();
}

void setUpExamples(document.getElementById("example-list") as HTMLElement, open);

const version = document.getElementById("game-version");
if (version) version.textContent = `Boss data from BTD6 ${GAME.gameVersion}`;
