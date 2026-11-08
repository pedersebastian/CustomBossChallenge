// The compare page's entry point (compare.html)
import "./site";
import { compareCodesFromHash } from "./code/link";
import { extractCode } from "@cbc/share-code";
import { setUpCompare } from "./sections/compare";

const inputA = document.getElementById("code-a") as HTMLTextAreaElement;
const inputB = document.getElementById("code-b") as HTMLTextAreaElement;
const open = setUpCompare(
  inputA,
  inputB,
  document.getElementById("compare-result") as HTMLElement,
  document.getElementById("swap") as HTMLButtonElement,
);

const fromLink = compareCodesFromHash();
if (fromLink.a || fromLink.b) open(fromLink.a, fromLink.b);

// Another link pasted into the address bar of an open tab, or Back. The page's own changes to the address
// (replaceState) don't come here.
window.addEventListener("hashchange", () => {
  const codes = compareCodesFromHash();
  if (codes.a !== extractCode(inputA.value) || codes.b !== extractCode(inputB.value)) open(codes.a, codes.b);
});
