// The Studio's row icons (#133): small line drawings of our own, in the text colour, so each row reads like the Boss
// Creator's (an icon, the name, the control and Reset). No art from the game's files (#96).

const SVG = "http://www.w3.org/2000/svg";

/** The drawings, as SVG path data on a 20 x 20 grid, drawn with a 2 px line */
const PATHS = {
  boss: "M3 15h14l1-9-4.5 3.5L10 4 6.5 9.5 2 6z M3 17.5h14",
  speed: "M3 5l5 5-5 5 M10 5l5 5-5 5",
  health: "M10 17s-7-4.3-7-9a3.8 3.8 0 0 1 7-2 3.8 3.8 0 0 1 7 2c0 4.7-7 9-7 9z",
  paragon: "M10 2.5l2.3 4.8 5.2.7-3.8 3.6.9 5.2L10 14.3 5.4 16.8l.9-5.2L2.5 8l5.2-.7z",
  tiers: "M4 16h12 M5.5 12h9 M7 8h6 M8.5 4h3",
  round: "M5 18V3 M5 4h10l-2.5 3.5L15 11H5",
  event: "M3 6h14v11H3z M3 9.5h14 M7 3.5v4 M13 3.5v4",
  skull: "M10 2.5c-4 0-6.5 2.7-6.5 6.2 0 2.2 1 3.6 2.5 4.4V16h8v-2.9c1.5-.8 2.5-2.2 2.5-4.4 0-3.5-2.5-6.2-6.5-6.2z M7.5 9.5h.01 M12.5 9.5h.01 M9 16v1.5 M11 16v1.5",
  name: "M3 3h7l7 7-7 7-7-7z M7 7h.01",
  map: "M10 18s-5.5-5.2-5.5-9.5a5.5 5.5 0 0 1 11 0C15.5 12.8 10 18 10 18z M10 10.5a2 2 0 1 0 0-4 2 2 0 0 0 0 4",
  difficulty: "M3 15a7 7 0 0 1 14 0 M10 15l3.5-5",
  mode: "M3 3h6v6H3z M11 3h6v6h-6z M3 11h6v6H3z M11 11h6v6h-6z",
  cash: "M10 17.5a7.5 7.5 0 1 0 0-15 7.5 7.5 0 0 0 0 15 M12.5 7.5c-.5-1-1.4-1.5-2.5-1.5-1.5 0-2.5.8-2.5 2s1 1.6 2.5 2 2.5.8 2.5 2-1 2-2.5 2c-1.2 0-2.1-.6-2.6-1.6 M10 4.5V6 M10 14v1.5",
  regrow: "M16 10a6 6 0 1 1-2-4.5 M16 3v3.5h-3.5",
  camo: "M2 10s3-5.5 8-5.5S18 10 18 10s-3 5.5-8 5.5S2 10 2 10z M10 12.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5",
  fortified: "M10 2.5l6.5 2.5v5c0 4-3 6.5-6.5 7.5C6.5 16.5 3.5 14 3.5 10V5z",
  tower: "M5 17.5h10 M6.5 17.5l1-10h5l1 10 M6 7.5V3.5h2v2h4v-2h2v4z",
  rule: "M10 17.5a7.5 7.5 0 1 0 0-15 7.5 7.5 0 0 0 0 15 M4.7 4.7l10.6 10.6",
  path: "M10 17V4 M5 9l5-5 5 5",
  hero: "M10 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6 M4 17.5c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5",
} as const;

export type IconName = keyof typeof PATHS;

/** The icon each row has, by the row's name; a row without one gets an empty space, so the names still line up */
const BY_LABEL: Record<string, IconName> = {
  Mode: "mode",
  Boss: "boss",
  "Boss Speed": "speed",
  "Boss Health": "health",
  "Max Paragons": "paragon",
  "Boss Tiers": "tiers",
  "Speed per Tier": "speed",
  "Health per Tier": "health",
  "Tier Preview": "tiers",
  "First Boss Round": "round",
  "Rounds Between Tiers": "round",
  "Spawn Rounds": "round",
  "Boss Event Rounds": "event",
  Skulls: "skull",
  "Skull Count": "skull",
  "Skulls at Boss HP %": "skull",
  Elite: "boss",
  Speed: "speed",
  Health: "health",
  "Challenge name": "name",
  Map: "map",
  Difficulty: "difficulty",
  "Starting cash": "cash",
  Lives: "health",
  "Max lives": "health",
  "Start round": "round",
  "Bloon speed": "speed",
  "MOAB speed": "speed",
  "Regrow rate": "regrow",
  "Bloon health": "health",
  "MOAB health": "health",
  "All Camo": "camo",
  "All Regrow": "regrow",
  "All Fortified": "fortified",
  "Max towers": "tower",
  Limit: "tower",
  Hero: "hero",
  "Path 1": "path",
  "Path 2": "path",
  "Path 3": "path",
  "Paragon locked": "paragon",
};

/** The icon for a row's name; the Challenge Editor's "No …" rules share one */
export const iconNameFor = (label: string): IconName | undefined => BY_LABEL[label] ?? (label.startsWith("No ") ? "rule" : undefined);

/** An icon as an SVG, hidden from screen readers: the row's name says what it is */
export function icon(name: IconName | undefined): SVGSVGElement {
  const svg = document.createElementNS(SVG, "svg");
  svg.setAttribute("viewBox", "0 0 20 20");
  svg.setAttribute("class", "studio-icon");
  svg.setAttribute("aria-hidden", "true");
  svg.setAttribute("focusable", "false");
  if (name) {
    const path = document.createElementNS(SVG, "path");
    path.setAttribute("d", PATHS[name]);
    svg.append(path);
  }
  return svg;
}
