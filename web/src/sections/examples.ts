// The "Try these" section: setups worth playing, with their code ready to copy

import { decode } from "@cbc/share-code";
import { EXAMPLES } from "../data/types";
import { copyWithFeedback, focusWithoutScroll, h, scrollToElement } from "../dom";
import { renderSetup } from "../preview/render";

/**
 * Fills the list. See details opens the example in the code viewer above (open, done once it shows), scrolls to it and
 * moves the keyboard to the setup's name there, so the result isn't far above the focus (#173).
 */
export async function setUpExamples(list: HTMLElement, open: (code: string) => Promise<void>): Promise<void> {
  for (const example of EXAMPLES) {
    const decoded = await decode(example.code);
    if (!decoded.ok) continue;

    const copy = h("button", { class: "button small", type: "button" }, "Copy code");
    copy.addEventListener("click", () => copyWithFeedback(copy, example.code));
    const view = h("button", { class: "button small blue", type: "button" }, "See details");
    view.addEventListener("click", async () => {
      const section = document.getElementById("codes");
      if (section) scrollToElement(section);
      await open(example.code);
      const name = section?.querySelector<HTMLElement>(".result .setup-name");
      if (name) focusWithoutScroll(name);
    });

    // The description goes right under the setup's name, inside the card
    const setup = renderSetup(decoded.options, decoded.challenge, true);
    setup.firstElementChild?.after(h("p", { class: "example-text" }, example.description));
    setup.append(
      h("code", { class: "example-code", title: example.code }, example.code),
      h("div", { class: "actions" }, copy, view),
    );
    list.append(h("article", { class: "example" }, setup));
  }
}
