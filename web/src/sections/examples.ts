// The "Try these" section: setups worth playing, with their code ready to copy

import { decode } from "../code/decode";
import { EXAMPLES } from "../data/types";
import { copyWithFeedback, h } from "../dom";
import { renderSetup } from "../preview/render";

export async function setUpExamples(list: HTMLElement, open: (code: string) => void): Promise<void> {
  for (const example of EXAMPLES) {
    const decoded = await decode(example.code);
    if (!decoded.ok) continue;

    const copy = h("button", { class: "button small", type: "button" }, "Copy code");
    copy.addEventListener("click", () => copyWithFeedback(copy, example.code));
    const view = h("button", { class: "button small blue", type: "button" }, "See details");
    view.addEventListener("click", () => {
      open(example.code);
      document.getElementById("codes")?.scrollIntoView({ behavior: "smooth" });
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
