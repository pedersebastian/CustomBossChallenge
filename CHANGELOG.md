# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--List any changes you make to your mod in the Unreleased section. If you trigger a release in GitHub, the sections will be automatically updated for the new version.-->

## [Unreleased]

### Added

- **Boss Creator** button on the main menu, just below the event buttons on the right.
- The Boss Creator opens the game's own Challenge Editor with a new **Boss** section: boss and Elite picker, Boss Speed, Boss Health, All Fortified, Boss Tiers, Extra Tier Health, Spawn Rounds and Skulls. All of the editor's own options (map, difficulty, mode, rules, towers, sliders, modifiers) work as usual.
- **Boss tiers 1-10.** Tiers above 5 are copies of tier 5 with more health per tier; the game is won when the last chosen tier is defeated.
- **Custom spawn rounds** for every tier.
- **Play** button in the Boss Creator that starts the custom boss game directly.
- The last Boss Creator setup is remembered in `UserData/CustomBossChallenge/LastSetup.json`.
- Custom games also skip boss tier progress and badges.
- New in-game tests for the Boss Creator flow, the main menu button, extra tiers played to victory, tier math and saving the setup, plus a windowed screenshot tour.

### Changed

- Bloon sliders and starting rules now come from the Challenge Editor instead of the mod's own settings.
- Game modes without boss support (Deflation, Apopalypse) can't be played as a boss game.

### Fixed

- Bosses spawned one round late (on round 41 instead of 40): the game counts rounds from 0 internally.
- Games ended after the first boss tier: the difficulty's normal last round (60 on medium) ended the game before later tiers. The end round now leaves room for the last tier, and custom games are only won once the last chosen tier is defeated.
- The editor's reset button didn't reset the boss options.
- Returning from a game opened the plain Challenge Editor without the boss options instead of the Boss Creator.

### Removed

- **Breaking:** the Mod Helper Mod Settings and the "Customize" button on the Boss Challenge screen. Normal Boss Challenges are no longer changed at all. Old settings are not carried over; `Btd6ModHelper/Mod Settings/CustomBossChallenge.json` can be deleted.

## [1.0.0]

### Added

- "Customize" button on the Boss Challenge screen that opens the mod's settings and turns green while custom rules are enabled (the same settings are also available in the Mod Helper Mod Settings menu).
- Master toggle "Enable Custom Boss Challenge" (off by default).
- Boss modifiers: Boss Speed % (10-1000) and Boss Health % (10-2000), using the game's own challenge multipliers.
- Bloon modifiers: Bloon Speed, MOAB Speed, Ceramic Health, MOAB Health and Regrow Rate.
- Skull options: Vanilla, Evenly Spaced (0-10 skulls) or Custom health thresholds (e.g. `80,60,40,20`), for every boss and Elite tier, including Lych's ethereal thresholds.
- Start rules: Starting Cash, Starting Lives, Max Lives, Starting Round, Ending Round, and toggles for No Monkey Knowledge, No Selling, No Powers, No Continues, All Camo, All Regrow and All Fortified.
- Safety guards: only Boss Challenges are affected; custom games are always unranked, never saved and never update Boss Challenge records. The weekly Boss Event, co-op and all other modes are untouched.
- In-game automated tests (Mod Helper `ModTest`) covering config application, skull math, all boss/Elite skulls, ranked/co-op guards, the Customize button and a game test comparing a vanilla and a custom Bloonarius challenge.
