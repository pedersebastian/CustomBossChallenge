# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--List any changes you make to your mod in the Unreleased section. If you trigger a release in GitHub, the sections will be automatically updated for the new version.-->

## [Unreleased]

### Added

- **Keyboard shortcuts** in the Boss Creator: Ctrl+C and Ctrl+V copy and paste a share code, Ctrl+S saves the setup
  and Ctrl+Enter plays (Cmd on a Mac). They do nothing while a text field, a popup or one of the editor's panels is
  open, so copy and paste in the fields work as before.
- **Keyboard navigation** in the Boss section: Tab and Shift+Tab (or the up and down arrows) move a yellow frame
  through every boss, Elite, the sliders, the text fields, the lists and the buttons, up to Play, and scroll it into
  view. Left and right change what has the frame, and Enter or Space press it. In a text field, Enter applies the value
  and moves on, and Esc cancels the edit instead of leaving the Boss Creator. A mouse click or Esc takes the frame
  away.
- **Example setups** in the Boss Creator: the Saved Setups list now ends with the examples from the website (Marathon
  Lych, Glass Cannon and more), marked in gold as examples and translated. Load one, change it and save it as your
  own. Examples can't be deleted and don't count towards the 50 saved setups.
- A warning in the log when another mod patches one of the methods that keep custom games away from records,
  achievements, saves or Monkey Money, so a bug report shows it.

### Changed

- With no saved setups, the Saved Setups list starts on the first example instead of saying "No saved setups".

### Fixed

- An unfinished game that can't be continued, usually after a BTD6 update, no longer disappears without a word:
  Boss Creator says so once and offers **Load Setup** to play its setup again from the start.
- An unusable `Continue.json` is moved to `Continue.broken.json` after the first try, instead of being read again,
  with a warning in the log, on every click on Boss Creator.
- After loading an example setup, its name now gives way to the default name ("Custom Vortex") once Random or a
  boss button changes the boss, like Reset already did.
- With a mod that changes every Boss Challenge before it starts, such as Custom Boss Modifiers, a Boss Creator game
  is now played with the Boss Creator's rules instead of that mod's settings. The game's own Boss Challenges still
  get the other mod's settings.

## [2.7.0] - 2026-10-29

### Added

- **Continue after closing the game**: a custom game is kept after every round in the mod's own file
  (`UserData/CustomBossChallenge/Continue.json`), so closing the game or a crash no longer loses a long run. Boss
  Creator on the main menu offers to continue it on the round it was left, with its defeated tiers and free retries.
  It is replaced when the game is won or restarted or a new custom game is played; the player's own saves stay untouched.

## [2.6.0] - 2026-10-25

### Added

- **First Boss Round** and **Rounds Between Tiers** sliders above Spawn Rounds fill in evenly spaced rounds without
  typing. They show the first round and spacing of whatever Spawn Rounds holds, and typed rounds still work as before.
- **Free retries** on the defeat screen: Retry Last Round and Retry From Round (the round the current tier arrived
  on), unlimited and without Monkey Money, after leaking or running out of time. The player's own saves are untouched.

### Fixed

- Running out of time on a tier now says how many rounds that tier really had, instead of always 20.
- All Fortified stays first among the editor's modifiers after Undo, Random or loading a setup, instead of moving to
  the end of its list.
- Custom games no longer count towards achievements, in the game or on Steam/Epic. XP and Monkey Money are still
  earned.
- The mod checks that all its changes to the game are in place. If a BTD6 update renamed or removed a part it needs,
  the Boss Creator button explains what is missing and stays closed until the mod is updated, instead of starting
  games without the guards for your records, achievements, saves and Monkey Money. Changes that only affect how
  things look, like the boss on the map picker, are just logged.
- The editor's Undo button now also takes the boss options (boss, Elite, tiers, spawn rounds, skulls) back to how
  they were when the Boss Creator was opened, like the rest of the setup. They used to stay as they were.
- The main menu no longer makes a little garbage every frame while placing the Boss Creator button.
- The Boss Creator's Play button was drawn on top of the editor's own windows (modifiers, towers, tower restrictions
  and maps) instead of behind their dark background, like Reset.
- Esc in the Boss Creator's Tower Restrictions, Add towers, map and modifier panels closes that panel instead of
  leaving for the main menu.

### Changed

- The last tier always has 20 rounds to be beaten; a later end round set in the editor no longer gives it more.
- The Boss Creator hides the editor's End Round field, since the game always ends 20 rounds after the last tier. The
  normal Challenge Editor still shows it.
- The Boss Creator button and editor are now tested on 16:9, 16:10 (Steam Deck), 4:3, 21:9 and 32:9 screens: nothing
  overlaps the game's own buttons or sticks out.

## [2.5.1] - 2026-10-24

### Changed

- New mod icon: the skull now looks like the boss skull the game uses.

## [2.5.0] - 2026-10-23


### Added

- **Saved Setups**: keep up to 50 favorite setups in the game, by the challenge's name, and load or delete them from
  a list in the Boss section. Load and Delete ask first, like Paste.

### Changed

- The challenge name the Boss Creator gives a setup now says Elite, e.g. **Custom Elite Lych**, and follows the Elite
  box like it follows the boss, as long as you haven't named the challenge yourself.
- The Tier Preview shows each tier's round on its own line under the tier, so the cards stay readable with 10 tiers.
- Play explains it when the start round is on or after the first boss tier's round, instead of starting a game that
  silently skips the tiers before it.

## [2.4.0] - 2026-10-23

### Added

- **Skull Preview**: a health bar like the boss's own, under the skull options, with a skull at every health
  percentage where the boss uses its special ability. It follows Evenly Spaced and Custom %, and shows the boss's
  own skulls for Vanilla.

### Changed

- **Skull Health %** is now called **Skulls at Boss HP %**, and its info explains that the percentages are the
  boss's health left when it reaches each skull, the same for every tier.
- A tier can now have up to **2 billion** health, Boss Health included. The limit used to leave room for the highest
  Boss Health in every game, which held tiers to about 67 million at 100 %. A test checks that the game still takes
  this much health, so a game update that changes it is noticed.

## [2.3.1] - 2026-10-18

### Changed

- When a tier would get more health than the game allows, the Tier Preview says so under the cards, with the limit
  (Boss Health included).

### Fixed

- The Boss Creator's Play button no longer overlaps Reset.

## [2.3.0] - 2026-10-18

### Added

- **Speed per Tier** and **Health per Tier**: every tier can be faster and tougher than the one before it, on top of
  the game's own steps, also in normal 5 tier games. The steps add up evenly: +100 % health makes tiers 2-5 ×2,
  ×3, ×4 and ×5. Below 0 makes later tiers easier.
- **Tier Preview**: a card per tier showing the round it arrives on, its speed and its health with every boss option
  applied, orange where it is harder than the normal game and blue where it is easier.

### Changed

- **All Fortified** moved from the Boss section to the editor's own modifiers, next to All Camo and All Regrow,
  with the same include/exclude button, info and icon as the game's modifiers. It is only there in the Boss Creator.
- **Reset** now also goes back to Bloonarius, the first boss, with its default challenge name, like a new setup.
  It used to keep the chosen boss.
- Health per Tier replaces Extra Tier Health. Tiers above 5 still get +50 % health each by default. Saved setups and
  share codes with another Extra Tier Health are converted to the Health per Tier that gives those tiers the same
  health; tiers 2-5 then get it too, and the log says so.
- Share codes that use Speed or Health per Tier start with `CBC2:`, so 2.2 asks for an update instead of loading them
  without those settings. Codes without them still start with `CBC1:` and work in 2.2 too.

## [2.2.1] - 2026-10-18

### Added

- The log records copied and loaded share codes and every value a code or setup file had brought into range, and
  the README explains how to report a bug.

### Fixed

- The editor's **Random** button now also randomizes the boss options: boss, Elite, speed, health, All Fortified,
  Max Paragons, tiers and skulls. Like the game's own randomizer, each option has a chance to change and otherwise
  keeps its default. Random no longer picks game modes a boss can't be played in (Deflation, Apopalypse) or a
  start round after the first tier.

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

[unreleased]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.7.0...HEAD
[2.7.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.6.0...2.7.0
[2.6.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.5.1...2.6.0
[2.5.1]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.5.0...2.5.1
[2.5.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.4.0...2.5.0
[2.4.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.3.1...2.4.0
[2.3.1]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.3.0...2.3.1
[2.3.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.2.1...2.3.0
[2.2.1]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.2.0...2.2.1
[2.2.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.1.0...2.2.0
[2.1.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.0.2...2.1.0
[2.0.2]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.0.0...2.0.2
[2.0.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/35e49dabca58e55918fa44ceb775b53ed06d84f2...2.0.0