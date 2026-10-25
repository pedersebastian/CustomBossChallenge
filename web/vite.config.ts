import { defineConfig } from "vitest/config";

export default defineConfig({
  // Relative paths, so the built site works from any folder, e.g. GitHub Pages under /CustomBossChallenge/
  base: "./",
  server: { port: 5173, strictPort: true },
  test: { environment: "node" },
});
