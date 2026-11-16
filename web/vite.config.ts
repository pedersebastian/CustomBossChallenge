import { defineConfig } from "vitest/config";

export default defineConfig({
  // Relative paths, so the built site works from any folder, e.g. GitHub Pages under /CustomBossChallenge/
  base: "./",
  server: { port: 5173, strictPort: true },
  // Four pages: the main page, the one that compares two setups, the one that makes them (#12) and the past boss
  // events (#213)
  build: {
    rollupOptions: {
      input: { main: "index.html", compare: "compare.html", studio: "studio.html", pastEvents: "past-events.html" },
    },
  },
  // CSS on, so a test can read style.css as text
  test: { environment: "node", css: true },
});
