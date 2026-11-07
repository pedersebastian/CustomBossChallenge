// What every page of the site shares: the fonts and the game version in the footer.
// The fonts are part of the site, so they load nothing from other servers (only the visit counter in the HTML does).
// The site is in English: Latin is enough.
import "@fontsource/luckiest-guy/latin-400.css";
import "@fontsource/nunito/latin-400.css";
import "@fontsource/nunito/latin-700.css";
import "@fontsource/nunito/latin-800.css";
import { GAME } from "./data/types";
import { setUpKonami } from "./fun/konami";
import { keepsSetup } from "./code/link";

setUpKonami();

// A link to a part of the page (the menu's #codes) would replace the #code=… or #a=…&b=… the address holds, so the
// address would no longer point at the setup shown. Such links scroll there and leave the address as it is (#113).
document.addEventListener("click", (event) => {
  const link = event.target instanceof Element ? event.target.closest<HTMLAnchorElement>('a[href^="#"]') : null;
  if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey) return;
  if (!keepsSetup(location.hash)) return;
  const target = document.getElementById(decodeURIComponent(link.hash.slice(1)));
  if (!target) return;
  event.preventDefault();
  target.scrollIntoView({ behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth" });
  // Where the keyboard goes next, as following the link would have done
  if (!target.hasAttribute("tabindex")) target.setAttribute("tabindex", "-1");
  target.focus({ preventScroll: true });
});

const version = document.getElementById("game-version");
if (version) version.textContent = `Boss data from BTD6 ${GAME.gameVersion}`;
