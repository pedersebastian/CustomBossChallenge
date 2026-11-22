// @vitest-environment happy-dom
// The bosses' portraits (#206): every boss has them, the Studio and the setup view show them, Elite when Elite is on,
// and the first letter comes back when one doesn't load

import { readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { BOSSES, decode, defaultOptions, type Challenge } from "@cbc/share-code";
import { h } from "../src/dom";
import { EXAMPLES } from "../src/data/types";
import { portrait, portraitUrl, setPortrait } from "../src/preview/portrait";
import { renderSetup, renderTiers } from "../src/preview/render";
import { setUpStudio } from "../src/studio/render";
import { newSetup } from "../src/studio/state";

const folder = join(__dirname, "..", "public", "bosses");

async function challenge(): Promise<Challenge> {
  const result = await decode(EXAMPLES[0].code);
  if (!result.ok) throw new Error(result.reason);
  return result.challenge;
}

afterEach(() => document.body.replaceChildren());

describe("the portrait files", () => {
  for (const boss of BOSSES) {
    for (const elite of [false, true]) {
      const file = portraitUrl(boss, elite).replace("./bosses/", "");
      it(`has ${file}, a small WebP`, () => {
        const bytes = readFileSync(join(folder, file));
        expect(bytes.subarray(0, 4).toString("latin1")).toBe("RIFF");
        expect(bytes.subarray(8, 12).toString("latin1")).toBe("WEBP");
        expect(statSync(join(folder, file)).size).toBeLessThan(20 * 1024);
      });
    }
  }

  it("says where they come from and who owns them", () => {
    const readme = readFileSync(join(folder, "README.md"), "utf8");
    expect(readme).toContain("Ninja Kiwi");
    expect(readme).toContain("BossPortraitsTest");
  });
});

describe("portrait()", () => {
  it("is a decoration of a fixed size that loads lazily", () => {
    const image = portrait("Lych", true, 46);
    expect(image.getAttribute("src")).toBe("./bosses/LychElite.webp");
    expect(image.getAttribute("alt")).toBe("");
    expect(image.getAttribute("width")).toBe("46");
    expect(image.getAttribute("height")).toBe("46");
    expect(image.getAttribute("loading")).toBe("lazy");
  });

  it("falls back to the boss's first letter, or to nothing", () => {
    const badge = h("div", {}, portrait("Vortex", false, 46));
    badge.querySelector("img")!.dispatchEvent(new Event("error"));
    expect(badge.querySelector("img")).toBeNull();
    expect(badge.textContent).toBe("V");

    const name = h("span", {}, portrait("Vortex", false, 24, null), "Vortex");
    name.querySelector("img")!.dispatchEvent(new Event("error"));
    expect(name.innerHTML).toBe("Vortex");
  });

  it("switches between normal and Elite, but not once it has fallen back", () => {
    const badge = h("div");
    const image = portrait("Phayze", false, 46);
    badge.append(image);
    setPortrait(image, "Phayze", true);
    expect(image.getAttribute("src")).toBe("./bosses/PhayzeElite.webp");
    image.dispatchEvent(new Event("error"));
    setPortrait(image, "Phayze", false);
    expect(badge.textContent).toBe("P");
  });
});

describe("in the setup view", () => {
  it("shows the boss's portrait, Elite when Elite is on", async () => {
    const normal = renderSetup({ ...defaultOptions(), Boss: "Dreadbloon" }, await challenge());
    expect(normal.querySelector(".boss-badge img")!.getAttribute("src")).toBe("./bosses/Dreadbloon.webp");
    const elite = renderSetup({ ...defaultOptions(), Boss: "Dreadbloon", Elite: true }, await challenge());
    expect(elite.querySelector(".boss-badge img")!.getAttribute("src")).toBe("./bosses/DreadbloonElite.webp");
  });

  it("keeps the star for Advanced Mode, with the boss's portrait on every tier's card and a die on a Random one", async () => {
    const options = { ...defaultOptions(), Tiers: 4, TierBosses: "Lych,VortexElite,Random,Phayze" };
    const setup = renderSetup(options, await challenge());
    expect(setup.querySelector(".boss-badge")!.textContent).toBe("★");
    const tiers = renderTiers(options, await challenge());
    expect([...tiers.querySelectorAll(".tier-boss img")].map((i) => i.getAttribute("src"))).toEqual([
      "./bosses/Lych.webp",
      "./bosses/VortexElite.webp",
      "./bosses/Phayze.webp",
    ]);
    expect(tiers.querySelectorAll(".tier-boss svg.dice")).toHaveLength(1);
  });
});

describe("in the Studio's boss row", () => {
  it("shows every boss's portrait, and the chosen one's Elite portrait when Elite is ticked", async () => {
    const form = h("form");
    document.body.append(form);
    const studio = setUpStudio(form, h("textarea"), h("div"), h("div"));
    await studio.load(newSetup(), null);
    const src = (boss: string) => form.querySelector(`.studio-boss[data-boss="${boss}"] img`)!.getAttribute("src");

    expect(BOSSES.map(src)).toEqual(BOSSES.map((boss) => portraitUrl(boss, false)));
    const elite = form.querySelector<HTMLInputElement>(".studio-elite input")!;
    elite.checked = true;
    elite.dispatchEvent(new Event("change"));
    expect(src("Bloonarius")).toBe("./bosses/BloonariusElite.webp");
    expect(src("Lych")).toBe("./bosses/Lych.webp");

    form.querySelector<HTMLButtonElement>('.studio-boss[data-boss="Lych"]')!.click();
    expect(src("Lych")).toBe("./bosses/LychElite.webp");
    expect(src("Bloonarius")).toBe("./bosses/Bloonarius.webp");
  });

  it("shows the portraits on Advanced Mode's bosses and tiers, and a die for Random", async () => {
    const form = h("form");
    document.body.append(form);
    const studio = setUpStudio(form, h("textarea"), h("div"), h("div"));
    await studio.load(newSetup(), null);
    [...form.querySelectorAll<HTMLButtonElement>(".studio-tab")].find((b) => b.textContent === "Advanced")!.click();
    const chip = (boss: string) => form.querySelector(`.studio-chip[data-boss="${boss}"]`)!;
    const tierSrc = (tier: number) => form.querySelector(`.studio-tier[data-tier="${tier}"] .studio-tier-boss img`)?.getAttribute("src");

    expect(chip("Phayze").querySelector("img")!.getAttribute("src")).toBe("./bosses/Phayze.webp");
    expect(chip("Random").querySelector("img")).toBeNull();
    expect(chip("Random").querySelector("svg.dice")).not.toBeNull();
    expect(tierSrc(1)).toBe("./bosses/Bloonarius.webp");

    form.querySelector<HTMLButtonElement>('.studio-tier[data-tier="2"]')!.click();
    (chip("Lych") as HTMLButtonElement).click();
    expect(tierSrc(2)).toBe("./bosses/Lych.webp");
    (chip("Random") as HTMLButtonElement).click();
    expect(tierSrc(2)).toBeUndefined();
    expect(form.querySelector('.studio-tier[data-tier="2"] .studio-tier-boss svg.dice')).not.toBeNull();
  });
});
