// What every page of the site shares: the fonts and the game version in the footer.
// The fonts are part of the site, so they load nothing from other servers (only the visit counter in the HTML does).
// The site is in English: Latin is enough.
import "@fontsource/luckiest-guy/latin-400.css";
import "@fontsource/nunito/latin-400.css";
import "@fontsource/nunito/latin-700.css";
import "@fontsource/nunito/latin-800.css";
import { GAME } from "./data/types";

const version = document.getElementById("game-version");
if (version) version.textContent = `Boss data from BTD6 ${GAME.gameVersion}`;
