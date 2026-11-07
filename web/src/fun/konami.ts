// The Konami code (issue #61): ↑ ↑ ↓ ↓ ← → ← → B A anywhere on a page lets a handful of bloons float up and pop.
// With reduced motion there are no bloons, only a short message.

import { h } from "../dom";

export const KONAMI = ["arrowup", "arrowup", "arrowdown", "arrowdown", "arrowleft", "arrowright", "arrowleft", "arrowright", "b", "a"];

/** Remembers the last keys and says when they spell the Konami code */
export class KonamiTracker {
  private keys: string[] = [];

  /** Adds a key (a KeyboardEvent.key); true when it completes the code */
  push(key: string): boolean {
    this.keys.push(key.toLowerCase());
    if (this.keys.length > KONAMI.length) this.keys.shift();
    const done = this.keys.length === KONAMI.length && this.keys.every((k, i) => k === KONAMI[i]);
    if (done) this.keys = [];
    return done;
  }
}

/** The game's bloon colours, from red up to white */
const COLOURS = ["#e8282b", "#2f7fe0", "#5ac02c", "#ffd21f", "#ff6fae", "#2b2b2b", "#f4f4f4", "#a06cd5", "#8bd3e6"];
const BLOONS = 14;
const MESSAGE_TIME = 2500;
/** Longer than the slowest bloon takes to rise and pop (0.8 s delay, 3.6 s rising, 0.25 s pop) */
export const PARTY_TIME = 6000;

let partying = false;

function message(text: string): void {
  const toast = h("div", { class: "pop-toast", role: "status" }, text);
  document.body.append(toast);
  setTimeout(() => toast.remove(), MESSAGE_TIME);
}

function party(): void {
  if (partying) return;
  partying = true;

  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    message("🎈 Pop!");
    setTimeout(() => (partying = false), MESSAGE_TIME);
    return;
  }

  const sky = h("div", { class: "bloon-party", "aria-hidden": "true" });
  let left = BLOONS;
  // Ends the party once; also when reduced motion is turned on while it runs, which hides the bloons, so their
  // animations never end (#113)
  const end = (): void => {
    if (!sky.isConnected) return;
    sky.remove();
    partying = false;
  };
  setTimeout(end, PARTY_TIME);
  for (let i = 0; i < BLOONS; i++) {
    const bloon = h("span", { class: "party-bloon" });
    bloon.style.left = `${4 + Math.random() * 88}%`;
    bloon.style.setProperty("--colour", COLOURS[i % COLOURS.length]);
    bloon.style.setProperty("--pop-at", `${10 + Math.random() * 45}vh`);
    bloon.style.setProperty("--drift", `${(Math.random() - 0.5) * 120}px`);
    bloon.style.animationDuration = `${2 + Math.random() * 1.6}s`;
    bloon.style.animationDelay = `${Math.random() * 0.8}s`;
    // Rising ends in a pop, and the pop in nothing
    bloon.addEventListener("animationend", () => {
      if (!bloon.classList.contains("popped")) {
        bloon.classList.add("popped");
        return;
      }
      bloon.remove();
      if (--left === 0) end();
    });
    sky.append(bloon);
  }
  document.body.append(sky);
  message(`🎈 ${BLOONS} pops!`);
}

/** Keys typed into a text box are text, not a code */
function typing(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
}

export function setUpKonami(): void {
  const tracker = new KonamiTracker();
  window.addEventListener("keydown", (event) => {
    if (event.ctrlKey || event.metaKey || event.altKey || typing(event.target)) return;
    if (tracker.push(event.key)) party();
  });
}
