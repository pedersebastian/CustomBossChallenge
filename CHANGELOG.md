# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--List any changes you make to your mod in the Unreleased section. If you trigger a release in GitHub, the sections will be automatically updated for the new version.-->

## [Unreleased]

## [2.2.0] - 2026-10-17

### Added

- **Share Code**: Copy puts the whole Boss Creator setup (boss options and Challenge Editor rules) on the
  clipboard as a short text code (`CBC1:...`), and Paste loads a code someone shared after showing what it contains
  and asking before the current setup is replaced. Codes only store what differs from a new setup, so they fit in
  a chat message.
- Shared codes and the saved setup are treated as untrusted: type names in the JSON are removed before the game
  reads it, values are held to what the game can play (unknown maps, difficulties and modes fall back to the
  defaults) and the challenge name is shown as plain text.

## [2.1.0] - 2026-10-17

### Added

- **Max Paragons** slider in the Boss section: limit how many paragons can be placed, from 0 (no paragons) up to
  one less than the number of paragons in the game. All the way to the right is Unlimited, the default. Uses the
  game's own paragon limit, the same one events use.

## [2.0.2] - 2026-10-17

### Fixed

- Restarting a custom game after winning it ended the next attempt as soon as the first boss was defeated.
- Spawn rounds near round 1000 could put two tiers on the same round.
- The challenge name now also follows the boss when a setup is loaded, not only when a boss is clicked.
- Play now explains in a popup why a boss can't be played, instead of doing nothing.
- The Skull Health % field now shows the skulls as they are used (sorted, duplicates merged, invalid values
  dropped), and a list without any valid value keeps the previous skulls instead of silently using the vanilla ones.

### Changed

- New English screenshots in the README, including the info popup.
- Sturdier against game updates: a missing boss model plays the tiers that exist instead of failing, a renamed
  progress method no longer stops the other guards, and a hand edited `LastSetup.json` is brought back into range.
- More tests: progress guards, restarting, losing, Play refusing a setup and broken setup files.

## [2.0.0] - 2026-10-17

### Added

- **Boss Creator** button on the main menu, just below the event buttons on the right.
- The Boss Creator opens the game's own Challenge Editor with a new **Boss** section: boss and Elite picker, Boss Speed, Boss Health, All Fortified, Boss Tiers, Extra Tier Health, Spawn Rounds and Skulls. All of the editor's own options (map, difficulty, mode, rules, towers, sliders, modifiers) work as usual.
- **Boss tiers 1-10.** Tiers above 5 are copies of tier 5 with more health per tier; the game is won when the last chosen tier is defeated.
- **Custom spawn rounds** for every tier.
- **Play** button in the Boss Creator that starts the custom boss game directly.
- The last Boss Creator setup is remembered in `UserData/CustomBossChallenge/LastSetup.json`.
- Custom games also skip boss tier progress and badges.
- New in-game tests for the Boss Creator flow, the main menu button, extra tiers played to victory, tier math and saving the setup, plus a windowed screenshot tour.

- Info buttons on every boss option that explain what the option does.
- Translations for every language BTD6 supports, in `Localization/[Language].json`.

### Changed

- The Boss Creator button on the main menu always shows the mod's logo.
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

[unreleased]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.2.0...HEAD
[2.2.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.1.0...2.2.0
[2.1.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.0.2...2.1.0
[2.0.2]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.0.0...2.0.2
[2.0.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/35e49dabca58e55918fa44ceb775b53ed06d84f2...2.0.0