// The bosses' portraits (#206), the same ones the Boss Creator shows. They are Ninja Kiwi's art, taken from the game
// by the in-game test BossPortraitsTest into public/bosses/ (see its README), and credited in every page's footer.
// They are decoration: the boss's name is always written next to them, so they have no text of their own. If one
// doesn't load, the boss's first letter is shown instead, like the badges before #206.

/** Where a boss's portrait is: public/bosses/<Boss>.webp, or <Boss>Elite.webp */
export const portraitUrl = (boss: string, elite: boolean): string => `./bosses/${boss}${elite ? "Elite" : ""}.webp`;

/**
 * A boss's portrait, `size` pixels wide and high. If it can't be loaded it is replaced with `fallback`: by default
 * the boss's first letter, or nothing with `null`.
 */
export function portrait(boss: string, elite: boolean, size: number, fallback: string | null = boss.slice(0, 1)): HTMLImageElement {
  const image = document.createElement("img");
  image.className = "portrait";
  image.alt = "";
  image.width = size;
  image.height = size;
  image.loading = "lazy";
  image.decoding = "async";
  image.draggable = false;
  image.src = portraitUrl(boss, elite);
  image.addEventListener(
    "error",
    () => {
      if (fallback === null) image.remove();
      else image.replaceWith(fallback);
    },
    { once: true },
  );
  return image;
}

/**
 * A die, our own drawing, in place of a portrait for Random: a boss not known until the game picks it. Decoration
 * too, as "Random" is written next to it.
 */
export function dice(size: number): SVGSVGElement {
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 20 20");
  svg.setAttribute("class", "portrait dice");
  svg.setAttribute("width", String(size));
  svg.setAttribute("height", String(size));
  svg.setAttribute("aria-hidden", "true");
  svg.setAttribute("focusable", "false");
  const body = document.createElementNS("http://www.w3.org/2000/svg", "rect");
  for (const [name, value] of Object.entries({ x: "2.5", y: "2.5", width: "15", height: "15", rx: "3.5", class: "dice-body" }))
    body.setAttribute(name, value);
  svg.append(body);
  for (const [x, y] of [[6.5, 6.5], [13.5, 6.5], [10, 10], [6.5, 13.5], [13.5, 13.5]]) {
    const pip = document.createElementNS("http://www.w3.org/2000/svg", "circle");
    pip.setAttribute("cx", String(x));
    pip.setAttribute("cy", String(y));
    pip.setAttribute("r", "1.5");
    pip.setAttribute("class", "dice-pip");
    svg.append(pip);
  }
  return svg;
}

/** Shows a boss's normal or Elite portrait in an image from portrait(), unless it has already fallen back */
export function setPortrait(image: HTMLImageElement, boss: string, elite: boolean): void {
  const src = portraitUrl(boss, elite);
  if (image.parentNode && image.getAttribute("src") !== src) image.src = src;
}
