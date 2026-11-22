// The menu's What's new (#264): the newest versions' Added points from CHANGELOG.md, the same news the mod shows in
// its popup after an update. The file is read when the site is built, so a release's notes show with the next deploy.

import { h } from "../dom";
import { inlineParts, latestNews, parseChangelog, UNRELEASED, type ChangelogEntry, type ChangelogPoint } from "./changelog";

/** CHANGELOG.md, put in by vite.config.ts */
declare const __CHANGELOG__: string;

export const NEWS: ChangelogEntry[] = latestNews(parseChangelog(__CHANGELOG__));

export const CHANGELOG_URL = "https://github.com/pedersebastian/CustomBossChallenge/blob/master/CHANGELOG.md";

/** Where the newest version the visitor has opened the news for is remembered, for the dot on the button */
export const SEEN_KEY = "cbc-news-seen";

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** "2026-11-22" as "22 Nov 2026", or "" */
export function formatDate(date: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date);
  if (!match) return "";
  const month = MONTHS[Number(match[2]) - 1];
  return month ? `${Number(match[3])} ${month} ${match[1]}` : "";
}

function pointText(point: ChangelogPoint): HTMLElement {
  const item = h("li");
  if (point.title) {
    item.append(h("strong", {}, point.title));
    if (point.refs) item.append(" ", h("span", { class: "news-refs" }, `(${point.refs})`));
    item.append(": ");
  }
  for (const part of inlineParts(point.text)) {
    if (part.kind === "bold") item.append(h("strong", {}, part.text));
    else if (part.kind === "code") item.append(h("code", {}, part.text));
    else if (part.kind === "link" && /^https:\/\//.test(part.href)) {
      item.append(h("a", { href: part.href, target: "_blank", rel: "noopener" }, part.text));
    } else item.append(part.text);
  }
  return item;
}

function entrySection(entry: ChangelogEntry): HTMLElement {
  const next = entry.version === UNRELEASED;
  const heading = next
    ? h("h3", {}, "Coming in the next version")
    : h("h3", {}, `Version ${entry.version}`, entry.date ? h("span", { class: "news-date" }, formatDate(entry.date)) : null);
  return h(
    "section",
    { class: next ? "news-entry next" : "news-entry" },
    heading,
    next ? h("p", { class: "news-note" }, "On this website already; in the game after the mod's next update.") : null,
    h("ul", {}, ...entry.added.map(pointText)),
  );
}

function newestRelease(entries: ChangelogEntry[]): string | undefined {
  return entries.find((entry) => entry.version !== UNRELEASED)?.version;
}

function seen(): string | null {
  try {
    return localStorage.getItem(SEEN_KEY);
  } catch {
    return null;
  }
}

function remember(version: string | undefined) {
  if (!version) return;
  try {
    localStorage.setItem(SEEN_KEY, version);
  } catch {
    // Private browsing or storage turned off: the dot just stays
  }
}

/**
 * Makes `button` open the news. A dot on the button says there is a release the visitor hasn't opened the news for.
 * Returns the dialog.
 */
export function setUpWhatsNew(button: HTMLButtonElement, entries: ChangelogEntry[] = NEWS): HTMLDialogElement {
  const close = h("button", { type: "button", class: "news-close", "aria-label": "Close" }, "×");
  const dialog = h(
    "dialog",
    { class: "whats-new", id: "whats-new-dialog", "aria-labelledby": "whats-new-title" },
    h("div", { class: "news-head" }, h("h2", { id: "whats-new-title" }, "What's new"), close),
    entries.length > 0
      ? h("div", { class: "news-list" }, ...entries.map(entrySection))
      : h("p", {}, "No news to show."),
    h(
      "p",
      { class: "news-foot" },
      "Fixes and other changes are in the ",
      h("a", { href: CHANGELOG_URL, target: "_blank", rel: "noopener" }, "full changelog"),
      ". The mod shows its news once after each update; Mod Settings can turn that off.",
    ),
  );
  document.body.append(dialog);

  const newest = newestRelease(entries);
  button.classList.toggle("has-news", newest !== undefined && seen() !== newest);
  button.setAttribute("aria-haspopup", "dialog");
  button.setAttribute("aria-controls", dialog.id);

  const hide = () => {
    if (typeof dialog.close === "function") dialog.close();
    else dialog.removeAttribute("open");
  };
  button.addEventListener("click", () => {
    if (typeof dialog.showModal === "function") dialog.showModal();
    else dialog.setAttribute("open", "");
    remember(newest);
    button.classList.remove("has-news");
  });
  close.addEventListener("click", hide);
  // A click on the dark backdrop around the box closes it too
  dialog.addEventListener("click", (event) => {
    if (event.target === dialog) hide();
  });
  dialog.addEventListener("close", () => button.focus());
  return dialog;
}
