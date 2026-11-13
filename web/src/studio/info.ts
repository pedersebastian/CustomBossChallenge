// The Studio's i buttons (#157): like the i on the Boss Creator's rows, each explains its row in more detail, and a
// second i after a control explains its choices, like what each CHIMPS round set tests. The texts are the mod's own
// (../modTexts).

import { h } from "../dom";
import { MOD_TEXTS, type ModText } from "../modTexts";
import { nextId } from "./rows";

/**
 * Adds an i button that shows or hides the text under the whole row. Without after, the button goes right after the
 * row's name; with it, right after that element, for an i that explains a control's choices.
 */
export function addInfo(row: HTMLElement, title: string, text: ModText, after?: Element): HTMLButtonElement {
  const panelId = nextId("info");
  const button = h(
    "button",
    { class: "studio-info", type: "button", "aria-label": `About ${title}`, "aria-expanded": "false", "aria-controls": panelId, title: `About ${title}` },
    "i",
  );
  const panel = h("div", { class: "studio-info-text studio-row-help", id: panelId }, ...MOD_TEXTS[text].split("\n\n").map((part) => h("p", {}, part)));
  panel.hidden = true;
  button.addEventListener("click", () => {
    panel.hidden = !panel.hidden;
    button.setAttribute("aria-expanded", String(!panel.hidden));
  });

  if (after) after.after(button);
  else {
    // The name and its i share the name's place in the row's grid
    const name = row.querySelector(":scope > label, :scope > .studio-row-title");
    if (name) {
      const wrapper = h("div", { class: "studio-row-name" });
      name.before(wrapper);
      wrapper.append(name, button);
    } else row.append(button);
  }
  row.append(panel);
  return button;
}
