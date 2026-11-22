// @vitest-environment happy-dom
// What's new (#264): CHANGELOG.md read like the mod reads it (the cases in changelog-cases.json, which
// UnitTests/ChangelogTests.cs reads too), the news the menu shows, and the button and box on every page

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { inlineParts, latestNews, parseChangelog, UNRELEASED, type ChangelogEntry } from "../src/news/changelog";
import { formatDate, NEWS, SEEN_KEY, setUpWhatsNew } from "../src/news/whatsNew";

interface Case {
  name: string;
  markdown: string;
  entries: ChangelogEntry[];
}

const changelog = readFileSync(join(__dirname, "..", "..", "CHANGELOG.md"), "utf8");
const cases = (JSON.parse(readFileSync(join(__dirname, "changelog-cases.json"), "utf8")) as { cases: Case[] }).cases;

describe("reading CHANGELOG.md like the mod", () => {
  it("has shared cases", () => {
    expect(cases.length).toBeGreaterThanOrEqual(5);
  });

  it.each(cases.map((c) => [c.name, c] as const))("%s", (_, c) => {
    expect(parseChangelog(c.markdown)).toEqual(c.entries);
  });

  it("reads the real changelog, newest first", () => {
    const entries = parseChangelog(changelog);
    expect(entries[0].version).toBe(UNRELEASED);
    const released = entries.filter((entry) => entry.version !== UNRELEASED);
    expect(released.length).toBeGreaterThanOrEqual(10);
    for (const entry of released) expect(entry.version).toMatch(/^\d+\.\d+\.\d+$/);
    const numbers = released.map((entry) => entry.version.split(".").map(Number));
    for (let i = 1; i < numbers.length; i++) {
      const [a, b] = [numbers[i - 1], numbers[i]];
      expect(a[0] * 1e6 + a[1] * 1e3 + a[2]).toBeGreaterThan(b[0] * 1e6 + b[1] * 1e3 + b[2]);
    }
    expect(entries.find((entry) => entry.version === "2.14.0")?.added).toContainEqual(
      expect.objectContaining({ title: "Past events with Insta Monkeys off or only some powers", refs: "#212" }),
    );
  });
});

const sample = parseChangelog(
  "## [Unreleased]\n### Added\n- **Next** (#9): soon.\n- **Tools** (for contributors): t.\n" +
    "## [2.15.0] - 2026-12-01\n### Added\n- **Fifteen**: f.\n" +
    "## [2.14.1] - 2026-11-25\n### Fixed\n- **Fix**: x.\n" +
    "## [2.14.0] - 2026-11-22\n### Added\n- **Only for contributors** (for contributors): c.\n" +
    "## [2.13.0] - 2026-11-17\n### Added\n- **Thirteen**: b.\n" +
    "## [2.12.0] - 2026-11-10\n### Added\n- **Twelve**: c.\n" +
    "## [2.11.0] - 2026-11-01\n### Added\n- **Eleven**: d.\n",
);

describe("the news", () => {
  it("are what's coming and the newest releases with points for players", () => {
    const news = latestNews(sample);
    expect(news.map((entry) => entry.version)).toEqual([UNRELEASED, "2.15.0", "2.13.0", "2.12.0"]);
    expect(news[0].added.map((point) => point.title)).toEqual(["Next"]);
  });

  it("leave out Unreleased without points", () => {
    expect(latestNews(sample.slice(1), 2).map((entry) => entry.version)).toEqual(["2.15.0", "2.13.0"]);
  });

  it("of the site come from the real changelog, without points for contributors", () => {
    expect(NEWS.filter((entry) => entry.version !== UNRELEASED)).toHaveLength(3);
    for (const entry of NEWS) {
      expect(entry.added.length).toBeGreaterThan(0);
      for (const point of entry.added) expect(point.forContributors).toBe(false);
    }
  });

  it("read bold, code and links", () => {
    expect(inlineParts("**Bold** and `<code>` and a [link](https://x.y).")).toEqual([
      { kind: "bold", text: "Bold" },
      { kind: "text", text: " and " },
      { kind: "code", text: "<code>" },
      { kind: "text", text: " and a " },
      { kind: "link", text: "link", href: "https://x.y" },
      { kind: "text", text: "." },
    ]);
    expect(inlineParts("plain")).toEqual([{ kind: "text", text: "plain" }]);
  });

  it("show dates the way the site writes them", () => {
    expect(formatDate("2026-11-22")).toBe("22 Nov 2026");
    expect(formatDate("2026-01-05")).toBe("5 Jan 2026");
    expect(formatDate("")).toBe("");
    expect(formatDate("2026-13-01")).toBe("");
  });
});

describe("the What's new box", () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => document.body.replaceChildren());

  function open(entries = latestNews(sample)) {
    const button = document.createElement("button");
    document.body.append(button);
    const dialog = setUpWhatsNew(button, entries);
    return { button, dialog };
  }

  it("opens with every version's points and closes again", () => {
    const { button, dialog } = open();
    expect(dialog.open).toBe(false);
    button.click();
    expect(dialog.open).toBe(true);
    const headings = [...dialog.querySelectorAll("h3")].map((heading) => heading.textContent);
    expect(headings).toEqual([
      "Coming in the next version",
      "Version 2.15.01 Dec 2026",
      "Version 2.13.017 Nov 2026",
      "Version 2.12.010 Nov 2026",
    ]);
    expect([...dialog.querySelectorAll("li strong")].map((title) => title.textContent)).toEqual(["Next", "Fifteen", "Thirteen", "Twelve"]);
    expect(dialog.querySelector("li")?.textContent).toBe("Next (#9): soon.");
    dialog.querySelector<HTMLButtonElement>(".news-close")!.click();
    expect(dialog.open).toBe(false);
  });

  it("keeps text from the changelog as text", () => {
    const { button, dialog } = open(parseChangelog("## [2.0.0] - 2026-01-01\n### Added\n- **<img src=x>**: `<b>` and [a](javascript:void).\n"));
    button.click();
    expect(dialog.querySelector("img")).toBeNull();
    expect(dialog.querySelector("li b")).toBeNull();
    expect(dialog.querySelector("li a")).toBeNull();
    expect(dialog.querySelector("li")?.textContent).toBe("<img src=x>: <b> and a.");
  });

  it("marks the button until the newest release's news are opened", () => {
    const first = open();
    expect(first.button.classList.contains("has-news")).toBe(true);
    first.button.click();
    expect(first.button.classList.contains("has-news")).toBe(false);
    expect(localStorage.getItem(SEEN_KEY)).toBe("2.15.0");

    document.body.replaceChildren();
    expect(open().button.classList.contains("has-news")).toBe(false);
    // A newer release marks it again
    document.body.replaceChildren();
    const newer = parseChangelog("## [2.16.0] - 2026-12-10\n### Added\n- **Sixteen**: s.\n");
    expect(open(newer).button.classList.contains("has-news")).toBe(true);
  });
});

describe("every page", () => {
  it.each(["index.html", "compare.html", "studio.html", "past-events.html"])("%s has What's new in its menu", (page) => {
    const html = readFileSync(join(__dirname, "..", page), "utf8");
    const menu = /<div class="nav-links">([\s\S]*?)<\/div>/.exec(html)?.[1] ?? "";
    expect(menu).toContain('<button type="button" class="nav-news" id="whats-new">What\'s new</button>');
  });
});
