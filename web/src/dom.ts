// A tiny element builder. Text always goes in as text, never as HTML, so a code's name can't add markup.

type Child = Node | string | number | null | undefined | false;

export function h<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  props: { class?: string; title?: string; [attribute: string]: string | undefined } = {},
  ...children: Child[]
): HTMLElementTagNameMap[K] {
  const element = document.createElement(tag);
  for (const [name, value] of Object.entries(props)) {
    if (value === undefined) continue;
    if (name === "class") element.className = value;
    else element.setAttribute(name, value);
  }
  for (const child of children) {
    if (child === null || child === undefined || child === false) continue;
    element.append(child instanceof Node ? child : String(child));
  }
  return element;
}

/**
 * Copies text to the clipboard. The Clipboard API only works on https and localhost, so on a plain http address (a
 * phone on the same Wi-Fi) the text is copied the old way, through a hidden text box.
 */
export async function copyText(text: string): Promise<boolean> {
  if (window.isSecureContext && navigator.clipboard) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // Falls back below, e.g. when the browser didn't allow it
    }
  }

  const box = h("textarea", { readonly: "", "aria-hidden": "true", class: "copy-box" });
  box.value = text;
  document.body.append(box);
  box.select();
  box.setSelectionRange(0, text.length);
  try {
    return document.execCommand("copy");
  } catch {
    return false;
  } finally {
    box.remove();
  }
}

/** The page's one live region: what it says, screen readers read out without moving the focus */
let liveRegion: HTMLElement | null = null;

/** Tells screen readers something that changed on the page, e.g. that copying worked (#113) */
export function announce(message: string): void {
  if (!liveRegion || !liveRegion.isConnected) {
    liveRegion = h("p", { class: "sr-only", role: "status", "aria-live": "polite" });
    document.body.append(liveRegion);
  }
  // Emptied first, so the same message twice in a row is read out again
  liveRegion.textContent = "";
  const region = liveRegion;
  setTimeout(() => (region.textContent = message), 50);
}

/** How long a button says whether copying worked */
export const FEEDBACK_TIME = 1500;

/** Each button's own label and the timer that puts it back, so clicks close together can't leave it on "Copied!" */
const feedback = new WeakMap<HTMLButtonElement, { label: string; timer?: ReturnType<typeof setTimeout> }>();

/** Copies text, briefly says on the button whether it worked, and tells screen readers too */
export async function copyWithFeedback(button: HTMLButtonElement, text: string, done = "Copied!"): Promise<void> {
  let state = feedback.get(button);
  if (!state) {
    state = { label: button.textContent ?? "" };
    feedback.set(button, state);
  }
  const message = (await copyText(text)) ? done : "Copy failed";
  button.textContent = message;
  announce(message);
  clearTimeout(state.timer);
  const label = state.label;
  state.timer = setTimeout(() => (button.textContent = label), FEEDBACK_TIME);
}
