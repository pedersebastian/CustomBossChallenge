import { defineConfig } from "vitest/config";

export default defineConfig({
  // Relative paths, so the built site works from any folder, e.g. GitHub Pages under /CustomBossChallenge/
  base: "./",
  server: { port: 5173, strictPort: true },
  // Three pages: the main page, the one that compares two setups and the one that makes them (#12)
  build: { rollupOptions: { input: { main: "index.html", compare: "compare.html", studio: "studio.html" } } },
  // CSS on, so a test can read style.css as text
  test: { environment: "node", css: true },
});
