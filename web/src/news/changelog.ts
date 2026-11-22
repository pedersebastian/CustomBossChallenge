// Reads CHANGELOG.md for What's new (#264), like the mod's Config/Changelog.cs reads the copy it is built with, so the
// website and the game's popup tell the same news. test/changelog-cases.json holds the cases both must read alike.

/** One point under a version's "### Added": "- **Title** (#1, #2): text" */
export interface ChangelogPoint {
  /** What is in bold at the start, or "" */
  title: string;
  /** What is in brackets after the title, like "#272, #19, for contributors", or "" */
  refs: string;
  /** The rest after the colon, in Markdown, on one line */
  text: string;
  /** A point only contributors care about, like a new workflow: players aren't shown it */
  forContributors: boolean;
}

/** A version's section */
export interface ChangelogEntry {
  /** "2.14.0", or "Unreleased" */
  version: string;
  /** "2026-11-22", or "" */
  date: string;
  added: ChangelogPoint[];
}

export const UNRELEASED = "Unreleased";

const VERSION_HEADING = /^##\s+\[([^\]]+)\](?:\s*-\s*(\S+))?/;
const KIND_HEADING = /^###\s+(.+?)\s*$/;
const LEAD = /^\*\*(.+?)\*\*\s*(?:\(([^)]*)\))?\s*:?\s*(.*)$/;

function readPoint(text: string): ChangelogPoint {
  const lead = LEAD.exec(text);
  if (!lead) return { title: "", refs: "", text: text.trim(), forContributors: false };
  const refs = (lead[2] ?? "").trim();
  return {
    title: lead[1].trim(),
    refs,
    text: lead[3].trim(),
    forContributors: refs.toLowerCase().includes("for contributors"),
  };
}

/** Every version's section, newest first as in the file, with the points under its "### Added" */
export function parseChangelog(markdown: string | null | undefined): ChangelogEntry[] {
  const entries: ChangelogEntry[] = [];
  if (!markdown) return entries;

  let version: string | null = null;
  let date = "";
  let kind: string | null = null;
  let added: ChangelogPoint[] = [];
  let point: string | null = null;
  let inComment = false;

  const endPoint = () => {
    if (point !== null && kind === "added") added.push(readPoint(point));
    point = null;
  };
  const endEntry = () => {
    endPoint();
    if (version !== null) entries.push({ version, date, added });
    added = [];
  };

  for (const raw of markdown.replace(/\r\n/g, "\n").split("\n")) {
    const line = raw.trimEnd();
    // Comments, like the one Keep a Changelog's template puts above Unreleased, are no part of any version
    if (inComment || line.trimStart().startsWith("<!--")) {
      inComment = !line.includes("-->");
      continue;
    }

    const heading = VERSION_HEADING.exec(line);
    if (heading) {
      endEntry();
      version = heading[1].trim();
      date = heading[2] ?? "";
      kind = null;
      continue;
    }
    if (line.startsWith("#")) {
      endPoint();
      const sub = KIND_HEADING.exec(line);
      kind = sub ? sub[1].toLowerCase() : null;
      continue;
    }
    if (version === null) continue;

    if (line.startsWith("- ") || line.startsWith("* ")) {
      endPoint();
      point = line.slice(2).trim();
    } else if (line.length > 0 && /\s/.test(line[0]) && point !== null) {
      point += " " + line.trim();
    } else {
      endPoint();
    }
  }
  endEntry();
  return entries;
}

/**
 * The news for the website: the newest released versions with points for players, newest first, at most `count`. The
 * points under Unreleased are already on the website but not yet in the mod, so they come first as "coming next" when
 * there are any.
 */
export function latestNews(entries: ChangelogEntry[], count = 3): ChangelogEntry[] {
  const forPlayers = entries
    .map((entry) => ({ ...entry, added: entry.added.filter((point) => !point.forContributors) }))
    .filter((entry) => entry.added.length > 0);
  const next = forPlayers.filter((entry) => entry.version === UNRELEASED);
  const released = forPlayers.filter((entry) => entry.version !== UNRELEASED).slice(0, count);
  return [...next, ...released];
}

/** A part of a point's text: plain, bold, code or a link */
export type Inline =
  | { kind: "text" | "bold" | "code"; text: string }
  | { kind: "link"; text: string; href: string };

const INLINE = /\*\*(.+?)\*\*|`([^`]*)`|\[([^\]]+)\]\(([^)\s]+)\)/g;

/** Markdown on one line as parts, so the page can build it as text and elements, never as HTML */
export function inlineParts(markdown: string): Inline[] {
  const parts: Inline[] = [];
  let at = 0;
  for (const match of markdown.matchAll(INLINE)) {
    const index = match.index ?? 0;
    if (index > at) parts.push({ kind: "text", text: markdown.slice(at, index) });
    if (match[1] !== undefined) parts.push({ kind: "bold", text: match[1] });
    else if (match[2] !== undefined) parts.push({ kind: "code", text: match[2] });
    else parts.push({ kind: "link", text: match[3], href: match[4] });
    at = index + match[0].length;
  }
  if (at < markdown.length) parts.push({ kind: "text", text: markdown.slice(at) });
  return parts;
}
