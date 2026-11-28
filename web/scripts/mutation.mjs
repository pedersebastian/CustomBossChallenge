// Runs Stryker (#268, see stryker.config.mjs) with `npm run mutation`. Stryker copies web/ into
// .stryker-tmp/sandbox-<random>/ and runs the tests there, but some tests and vite.config.ts read files above web/
// (../CHANGELOG.md, ../../Localization/, ../../data/). From a sandbox those paths land in .stryker-tmp/, so this copies
// them there first. Stryker only deletes its own sandbox folder, so the copies stay until the next run replaces them.
// Copies rather than links: Windows needs extra rights for a file link, and nothing can delete the real files by way
// of a link.

import { cpSync, mkdirSync, rmSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repo = new URL("../../", import.meta.url);
const temp = new URL("../.stryker-tmp/", import.meta.url);
mkdirSync(temp, { recursive: true });
for (const item of ["CHANGELOG.md", "Localization", "data"]) {
  const target = fileURLToPath(new URL(item, temp));
  rmSync(target, { recursive: true, force: true });
  cpSync(fileURLToPath(new URL(item, repo)), target, { recursive: true });
}

const result = spawnSync("npx", ["stryker", "run", ...process.argv.slice(2)], {
  cwd: fileURLToPath(new URL("../", import.meta.url)),
  stdio: "inherit",
  shell: process.platform === "win32",
});
process.exit(result.status ?? 1);
