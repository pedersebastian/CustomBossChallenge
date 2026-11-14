// The compare page's entry point (compare.html)
import "./site";
import { compareCodesFromHash } from "./code/link";
import { followCompareLinks, setUpCompare } from "./sections/compare";

const inputA = document.getElementById("code-a") as HTMLTextAreaElement;
const inputB = document.getElementById("code-b") as HTMLTextAreaElement;
const open = setUpCompare(
  inputA,
  inputB,
  document.getElementById("compare-result") as HTMLElement,
  document.getElementById("swap") as HTMLButtonElement,
);

const fromLink = compareCodesFromHash();
if (fromLink.a || fromLink.b) void open(fromLink.a, fromLink.b);

followCompareLinks(inputA, inputB, open);
