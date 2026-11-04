// Joke "codes" (issue #61): a few exact texts that aren't share codes get a joke card instead of the error. Only
// text that fails to decode ever gets here, so a real code always shows its setup.

export interface Joke {
  title: string;
  sub: string;
  text: string;
}

const JOKES: Record<string, Joke> = {
  "cbc0:moab": {
    title: "The Red Bloon",
    sub: "Tier 1 · one red bloon · 0 % health",
    text: "Good luck.",
  },
  "cbc9:": {
    title: "A code from the future",
    sub: "Version 9 · arrives in a later update",
    text: "Your code is from the future. Please return it.",
  },
  hello: {
    title: "👋 Hi!",
    sub: "Not a share code",
    text: "That's not a share code, but thanks for stopping by.",
  },
  "cbc1:bloonarius": {
    title: "Bloonarius, but spelled out",
    sub: "Tier 1 · very literal",
    text: "Share codes come from the Boss Creator's Copy button, not from typing the boss's name. Nice try though.",
  },
};

/** The joke for exactly this text (ignoring case and surrounding spaces), or null for anything else */
export function jokeFor(text: string): Joke | null {
  return Object.prototype.hasOwnProperty.call(JOKES, text.trim().toLowerCase()) ? JOKES[text.trim().toLowerCase()] : null;
}

/** Every text that has a joke, for the tests */
export const JOKE_TEXTS = Object.keys(JOKES);
