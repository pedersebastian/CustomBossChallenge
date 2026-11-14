// @vitest-environment happy-dom
// A browser without CompressionStream and DecompressionStream (Safari before 16.4, Firefox before 113, some in-app
// browsers) says so on every page, instead of calling every code damaged or leaving the Studio empty (#181)

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { UNSUPPORTED_REASON, UnsupportedBrowserError, canUseCodes, decode, encode, pack } from "@cbc/share-code";
import { h } from "../src/dom";
import { EXAMPLES } from "../src/data/types";
import { setUpCodeViewer } from "../src/sections/codeViewer";
import { setUpCompare } from "../src/sections/compare";
import { setUpStudio } from "../src/studio/render";
import { newSetup } from "../src/studio/state";

const [first, second] = EXAMPLES.map((example) => example.code);
const rejections: unknown[] = [];
const onRejection = (reason: unknown) => rejections.push(reason);

beforeEach(() => {
  history.replaceState(null, "", "/");
  rejections.length = 0;
  process.on("unhandledRejection", onRejection);
  vi.stubGlobal("CompressionStream", undefined);
  vi.stubGlobal("DecompressionStream", undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
  process.off("unhandledRejection", onRejection);
  document.body.replaceChildren();
});

/** Lets promises nobody waits for settle, so an unhandled rejection would show up */
const settle = () => new Promise((resolve) => setTimeout(resolve, 50));

describe("share-code without the streams", () => {
  it("knows the browser can't use codes", () => {
    expect(canUseCodes()).toBe(false);
  });

  it("decode says the browser can't read codes, not that the code is damaged", async () => {
    expect(await decode(first)).toEqual({ ok: false, problem: "Unsupported", reason: UNSUPPORTED_REASON });
  });

  it("decode still says when text isn't a code at all", async () => {
    const read = await decode("hello");
    expect(read.ok === false && read.problem).toBe("Invalid");
  });

  it("encode and pack throw an UnsupportedBrowserError", async () => {
    await expect(encode(newSetup().options)).rejects.toBeInstanceOf(UnsupportedBrowserError);
    await expect(pack("{}")).rejects.toBeInstanceOf(UnsupportedBrowserError);
  });
});

describe("the pages without the streams", () => {
  it("the code viewer shows the browser message", async () => {
    const input = h("textarea");
    const result = h("div");
    document.body.append(input, result);
    await setUpCodeViewer(input, result)(first);
    expect(result.textContent).toBe(UNSUPPORTED_REASON);
  });

  it("Compare shows the browser message once", async () => {
    const [inputA, inputB, result, swap] = [h("textarea"), h("textarea"), h("div"), h("button")];
    document.body.append(inputA, inputB, swap, result);
    await setUpCompare(inputA, inputB, result, swap)(first, second);
    expect(result.textContent).toBe(UNSUPPORTED_REASON);
  });

  it("the Studio shows the browser message without an unhandled rejection", async () => {
    const [form, codeBox, actions, preview] = [h("form"), h("textarea"), h("div"), h("div")];
    document.body.append(form, codeBox, actions, preview);
    const studio = setUpStudio(form, codeBox, actions, preview);

    await studio.load(newSetup(), null);
    // A change refreshes with void, as the controls do
    form.querySelector<HTMLInputElement>("input[type=range]")?.dispatchEvent(new Event("input", { bubbles: true }));
    await settle();

    expect(preview.textContent).toBe(UNSUPPORTED_REASON);
    expect(codeBox.value).toBe("");
    expect([...actions.querySelectorAll("button")].every((button) => button.disabled)).toBe(true);
    expect(rejections).toEqual([]);
  });
});
