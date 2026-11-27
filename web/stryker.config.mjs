// Mutation testing (#268): Stryker changes the share-code package and the preview math a little at a time and checks
// that a test fails each time. A change no test notices ("a surviving mutant") is code the tests run but don't check.
// Run with: npm run mutation (see CONTRIBUTING.md). Slow, so it isn't part of npm test or the checks on every PR.
/** @type {import('@stryker-mutator/api/core').PartialStrykerOptions} */
export default {
  testRunner: "vitest",
  // vite.config.ts with the share-code package read from the sandbox, see that file
  vitest: { configFile: "vitest.stryker.config.mjs" },
  mutate: ["packages/share-code/src/**/*.ts", "src/preview/**/*.ts"],
  reporters: ["html", "clear-text", "progress", "json"],
  htmlReporter: { fileName: "reports/mutation/index.html" },
  jsonReporter: { fileName: "reports/mutation/mutation.json" },
  coverageAnalysis: "perTest",
  // Changes to code that runs once, when a module loads (tables, constants), must run every test: about a quarter of
  // the mutants but more than half the time. Left out, a run takes well under half as long.
  ignoreStatic: true,
  thresholds: { high: 80, low: 60, break: null },
  tempDirName: ".stryker-tmp",
  // Stryker rewrites tsconfig.json in its sandbox with the typescript package's JS API, which TypeScript 7 (the native
  // compiler) doesn't have. Vitest doesn't read tsconfig.json for the tests, so point Stryker at a file that isn't there.
  tsconfigFile: "no-tsconfig-for-stryker.json",
};
