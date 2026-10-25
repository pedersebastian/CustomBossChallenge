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

/** Copies text and briefly says on the button whether it worked */
export async function copyWithFeedback(button: HTMLButtonElement, text: string, done = "Copied!"): Promise<void> {
  const label = button.textContent;
  button.textContent = (await copyText(text)) ? done : "Copy failed";
  setTimeout(() => (button.textContent = label), 1500);
}
