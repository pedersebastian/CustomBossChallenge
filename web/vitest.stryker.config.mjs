// The test settings Stryker uses (#268, see stryker.config.mjs): vite.config.ts, plus an alias for every export of
// @cbc/share-code. node_modules/@cbc/share-code links to the real web/packages/share-code, outside Stryker's sandbox,
// so without the alias the tests would import the package as it is rather than Stryker's changed copy, and no change
// to the package could ever make a test fail. The alias points at the package files next to this file, in the sandbox.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { mergeConfig } from "vitest/config";
import base from "./vite.config.ts";

const packageFolder = new URL("./packages/share-code/", import.meta.url);
const { exports } = JSON.parse(readFileSync(new URL("package.json", packageFolder), "utf8"));

// Whole names only (^...$), so "@cbc/share-code" doesn't also catch "@cbc/share-code/tiers"
const alias = Object.entries(exports)
  .map(([subpath, file]) => ({
    find: new RegExp(`^@cbc/share-code${subpath.slice(1).replace(/[/.-]/g, "\\$&")}$`),
    replacement: fileURLToPath(new URL(file, packageFolder)),
  }));

export default mergeConfig(base, { resolve: { alias } });
