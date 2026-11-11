# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--List any changes you make to your mod in the Unreleased section. If you trigger a release in GitHub, the sections will be automatically updated for the new version.-->

## [Unreleased]

### Added

- **Boss options in the Rules panel** (#141): in a custom game, the pause screen's Rules button now also shows the
  Boss Creator's options that differ from a normal boss game. The sliders (Boss Tiers, Speed per Tier, Health per
  Tier, Skull Count) are rows of the Modifiers list after the game's Boss Health and Boss Speed; the boss and Elite,
  spawn rounds, boss event rounds and custom skulls are lines in the Rules box. A setup with nothing else changed says
  so. Advanced Mode's tier-by-tier list follows in #142.
- **Warnings before Play** (#70): the Tier Preview has a line for each thing in the setup that is allowed but
  probably not meant, and they never stop Play: a start round only a few rounds before the first boss, tiers that come
  only a few rounds apart (like rounds 80 and 81), tiers held to
  the game's health limit (the line that was already there), tiers that end up the same because their speed and
  health reached the game's limits, and rules that together make a very hard challenge. They follow the editor's
  own rules too, like the start round, lives and the bloon sliders, as soon as they change.
- **Ranked practice** (#83): a new Mod Settings page, Ranked Practice, with **Show Ranked Stats**, off by default. In custom games it
  shows the time the way the ranked boss events score it (how long the bosses were alive, added up, plus the penalty
  for a round without a boss that drags on past its grace time), with the upgrade tiers and cash used that Least Tiers and Least Cash events count, so the time can break a tie. The victory screen
  adds the time of every tier. The game keeps the time itself, so free retries and Continue keep it right.
- **Make and change setups on the website** (#12): a page of its own,
  [studio.html](https://pedersebastian.github.io/CustomBossChallenge/studio.html), with the Boss Creator's options:
  boss and Elite, Boss Speed and Health, Max Paragons, tiers, speed and health per tier, spawn rounds (First Boss
  Round and Rounds Between Tiers too), Boss Event Rounds and skulls, with the same ranges and steps. Start from a new
  setup or a code; the Tier Preview follows every change, and **Copy code** gives a code the mod loads exactly as
  shown. What the page doesn't edit yet, like the map, the rules or Advanced Mode's tier bosses, stays as it was in the
  code. **Edit** under a code on the main page opens it there, and **Compare with the original** shows what changed.
- **Advanced Mode in the Studio** (#95): the Standard and Advanced tabs work like the Boss Creator's, so every tier
  starts from Standard's boss, speed, health and skulls, and switching back brings them back. Each tier gets its own
  boss, Elite or Random, speed, health and skulls. Put a boss on a tier by dragging it there, by tapping the tier and
  then the boss (on a phone too), or with the arrow keys on the tier's card; Delete makes a tier follow the setup's
  boss again.
- **Timeline of the tiers on the website** (#94): under the Tier Preview, a bar from the first round to the game's
  end with a mark for each tier's spawn round, and the same rounds as a list. With Boss Event Rounds it says which
  event round each tier plays.
- **Strength per tier on the website** (#96): a chart of the boss's health and speed per tier next to the normal
  game (the same bosses without any changes), with the values as a table; in Advanced Mode each row names the tier's
  boss. Both follow the code on the main page and every change in the Studio.
- **The Challenge Editor's rules in the Studio** (#97): the challenge's name, map (grouped Beginner to Expert, like the
  game's map lists), difficulty and mode, starting cash,
  lives, max lives and start round, bloon and MOAB speed and health, regrow rate, All Camo, All Regrow, All
  Fortified, Max Towers and the on/off rules (no Monkey Knowledge, no selling, no powers, no continues, no Double
  Cash, no Fast Track, no Insta Monkey reward). Values are held to the mod's own limits, and an empty number is the
  game's default. The end round isn't there: in a boss game it always comes 20 rounds after the last tier.
- **A package for share codes** (#98): `@cbc/share-code`, in `web/packages/share-code`, reads, checks and makes
  share codes exactly like the mod, for anyone building their own tools. The website uses it for every code it reads
  and makes. It has a README with examples, and shares its test codes with the mod's own tests.
- **Tower restrictions in the Studio** (#128): every tower's and hero's limit (no limit, not allowed or a number),
  the highest tier of each upgrade path and the paragon lock for the 14 towers that have one, in a list per class
  like the game's (Heroes, Primary, Military, Magic, Support) with Allow all and Allow none for each.

### Changed

- **The Studio looks like the game's Boss Creator** (#133): each option is a row with an icon, its name, the control
  and Reset, on one line where there is room. The boss is picked from a row of boss buttons with a tick, Elite next to
  them, and the arrow keys step through them. The Tier Preview sits right after the tier rows and the Skull Preview
  under the skulls, like in the game; in Advanced Mode a tier's card opens its picker, which Esc closes. Sliders,
  checkboxes and the Challenge Editor's groups got the game's look too. All drawn for the website, with no art from
  the game.
- **No boss medal skulls in the Boss Creator's map picker**: the blue and purple skulls for the maps where you beat
  a boss in the game's own boss events, and the Hide Defeated filter, are gone from the Boss Creator's map list.
  Custom games never give those medals, so the skulls only made it look like they did. The normal Challenge Editor
  and the game's boss menus show the map list as before.

## [2.10.2] - 2026-11-07

### Changed

- **On Thunderstore**: the mod is also listed under AI Generated, since it is made with AI help (see
  CONTRIBUTING.md). Nothing in the game changes.

## [2.10.1] - 2026-11-07

### Fixed

- **Setups from a newer version of the mod are kept** (#107, #110): an older version no longer overwrites the last
  Boss Creator setup or the saved setups that a newer version wrote. The Boss Creator says so once per session, the
  saved setups list stays empty, and the files are kept as they are.
- **Unfinished games from a newer version of the mod are kept** (#107): playing a custom game in an older version no longer
  overwrites or deletes the unfinished game a newer version left. It is kept as `Continue.newer.json` and is picked
  up again by a version that can continue it.
- **Share codes can only set what a player can** (#108): a code now only sets the Challenge Editor's and the Boss
  Creator's own settings, with the right types and within range. Anything else in a hand made code, like cash
  modifiers, round sets or power limits, is ignored, and the game log says what. The website reads codes the same way.
- **The mod and the website read every code the same way** (#113): a hand made code whose boss options have the wrong
  type, like `5.5` for a number of tiers or `"1"` for a boss, or that holds `NaN` or `Infinity`, can't be read on
  either, where the mod used to guess. Links with a broken `%` escape open the same code on both. The website also
  rounds custom skulls like the game and shows a Max Paragons of every paragon as Unlimited.
- **An unfinished game keeps saving after a round that couldn't be saved** (#112): the rounds after it are still saved for
  Continue, and that round can still be retried in the same session.
- **Boss Speed and Boss Health show in Advanced Mode when they still apply** (#112): a code or an older setup can set them,
  and they apply on top of the tiers' own speed and health, so their rows now stay until they are back at 100 %.
- **Fewer tiers end the game sooner** (#112): when a game update leaves the game without a later tier's model, the game now
  ends after the last tier it has.
- **One part of the boss display failing no longer stops the others** (#112) for the rest of the match, and is logged once.
- **One damaged saved setup no longer hides the others** (#110): a setup with a broken value is left out, and the rest of
  the saved setups still show.
- **Files can't bring in other game objects** (#110): the challenge in `LastSetup.json` and `Continue.json` is read without
  custom maps or simulation behaviours, as codes already were, and a map save in `Continue.json` may only name the
  game's own types.
- **Texts** (#114): the help for Health per Tier says that tiers above 5 compound (×1.5 of the tier before, tier 7 ×2.25),
  Saved Setups says that a setup with the same name is only replaced after asking, and the Norwegian, Danish and
  Swedish texts no longer have English words left in them.

## [2.10.0] - 2026-11-06

### Added

- **Random tiers stay a secret in the game**: the boss line-up and the boss panel show a die for a Random tier until
  its boss arrives, instead of the boss the game picked for it.
- **Boss effects per tier** (#85): in Advanced Mode the map's boss effects, the ambient ones and those along the
  track, change to the next boss's when the round after a boss is beaten starts, and are put back after a restart
  or when a game is continued. A Random tier's effects wait until its boss arrives. The boss music already followed
  each boss.
- **Boss Event Rounds** (#80): a new tick under Spawn Rounds plays the rounds of the official boss events for
  the boss instead of the normal rounds, in every game mode, CHIMPS included. When the tiers spawn on other rounds
  than 40, 60, 80, 100 and 120, the event's boss rounds move with them. In Advanced Mode (#84) every tier arrives
  on the round made for its own boss, and the other rounds follow the first tier's boss. Setups with it are shared as `CBC4:` codes, so older versions of the mod ask
  for an update instead of playing the normal rounds. The Speed Demon Vortex, Speedrun Dreadbloon and Paragon Ban examples now
  play with it.
- **Website easter eggs** (#61): the Konami code (↑ ↑ ↓ ↓ ← → ← → B A) lets a handful of bloons float up and pop,
  or shows a short message with reduced motion, and a few joke codes such as `CBC0:MOAB` get a joke card instead of
  the error. Real codes, and broken ones, read exactly as before.
- **Describing names for random setups** (#62): when **Random** makes a setup that stands out, it gets a fun name
  that describes it, like "The Unkillable Snail", "Blink and It's Gone" or "Lych, Lych, Lych...", instead of "Custom
  Lych". A setup without a striking trait keeps the default name, and a name the player typed is never replaced.
  Changing the boss afterwards still updates the name. The names are English only for now.

### Fixed

- **Bloons spawned by a boss get All Camo, All Regrow and All Fortified** (#90): Bloonarius's and Diamondback's
  bloons, Elite or not and in every tier, now follow the modifiers like the rounds' bloons do. Bloons without such a
  variant (Lych's minis, Dreadbloon's rocks) stay as they are, and the bloon health and speed sliders already reached
  them. The game's own Boss Challenge is unchanged.

## [2.9.0] - 2026-11-03

### Added

- **Advanced Mode** (#28): pick the Advanced tab at the top of the Boss section, next to Standard, to choose the boss
  of every tier, for example Bloonarius on tier 1 and Lych on tier 2. The Boss Type is hidden, and the Tier Preview,
  now right below it, gets cards that open a picker with every boss, Elite or not, and Random (picked when the game
  starts, and kept through restarts, retries and Continue). In the game each tier spawns its own boss with its
  abilities, the boss panel shows its portrait and its health bar extras, a banner names each boss as it arrives, and
  a line-up on the left shows every tier's boss, the defeated ones ticked. Tiers above 5 are made from tier 5 of their
  own boss. New example: Boss Gauntlet, all seven bosses in a row. Setups in Advanced Mode are shared as `CBC3:` codes,
  which older versions of the mod refuse instead of playing them with one boss; the website reads them. Each tier
  card shows how many skulls its tier has (Elite bosses often have more of their own). Boss Speed, Boss Health and
  the skulls (Vanilla, Evenly Spaced or Custom, with a Skull Preview of the tier) are set per tier in Advanced Mode,
  in each tier's picker, in place of their rows in the Boss section.
- **Reset and undo for the boss options** (#36): the boss, the skulls and the custom skulls get a reset button like
  the sliders have, and Delete or Backspace resets the option in the yellow keyboard frame. **Reset Boss Options** at
  the top of the Boss section puts every boss option back to its default after asking, and leaves the Challenge Editor
  rules alone. Ctrl+Z undoes the last change (up to 20 steps; a slider drag is one step) and Ctrl+Y or Ctrl+Shift+Z
  redoes it, Cmd on a Mac: a boss option, a Challenge Editor rule (map, difficulty, cash, lives, towers, modifiers,
  ...; a visit to one of the editor's panels is one step), or a pasted, loaded or random setup, a reset or the
  editor's own Undo as one step each, so an accidental Paste can be undone. The challenge name isn't undone. Not while
  a text field, a popup or one of the editor's panels has the keyboard.

### Changed

- Max Paragons moved down below the skulls, and the Tier Preview up, right under the boss.
- The editor's **Random** button keeps the mode: in Advanced every tier gets a random boss (now and then Random, Elite
  more often in later tiers), Standard stays one boss. Random boss speed and health now go up to 400 %, with values
  near 100 % far more likely than extreme ones. The number of tiers is a bell curve around 5 instead of 5 half the
  time and anything from 1 to 10 the other half.

### Fixed

- The website showed some boss health one step lower than the game, e.g. 1.4M for Phayze's 1.45M tier 5 where the
  Boss Creator shows 1.5M.

## [2.8.0] - 2026-11-01

### Added

- **Tiers above 5 look different** (#11): every boss has a colour theme of its own for tiers 6-10 (Elite shares it),
  and the effect gets stronger with each tier: a glowing rim with a light sweeping over the body, the outline in the
  tier's colour, a pulsing aura and a slightly bigger model. Vortex's lightning takes the colour too. The boss panel
  colours the first stars in the tier's colour and shows the tier's number. Tiers 1-5 and other games keep the game's
  look.

- **Compare two setups on the website** (#35): a page of its own,
  [compare.html](https://pedersebastian.github.io/CustomBossChallenge/compare.html), where you paste two codes and see
  only the options that differ (the rest one click away), a short verdict like "B has 40 % more boss health at tier 5",
  both setups' boss health and speed per tier in one chart (solid and dashed lines, round and square points, and the
  values as a table) and the tiers side by side. Copy link shares the comparison as `compare.html#a=CBC2:…&b=CBC2:…`,
  and **Compare** under a code on the main page opens it there as A.
- **Links to a setup:** after Copy, **Copy link** gives a link like
  `https://pedersebastian.github.io/CustomBossChallenge/#code=CBC2:…` that opens the setup on the website. Paste
  reads such a link too, also as Discord shows it in `<…>`. On the website, a link opens the setup straight away, the
  address bar always links to the setup shown, and a link pasted into an open tab or Back works as well.
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

- **The Tier Preview no longer relies on colour** (#18): speed and health above the normal game get ▲ next to the
  orange, below it ▼ next to the blue, like on the website. On narrow cards (many tiers) the values shrink to fit.
- **The website is easier to read:** darker blue panels so all text on them meets WCAG AA contrast, a full outline
  around the button text, white underlined links on the panels, and screen readers hear one short line when a code is
  read instead of the whole setup.
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

[unreleased]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.10.2...HEAD
[2.10.2]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.10.1...2.10.2
[2.10.1]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.10.0...2.10.1
[2.10.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.9.0...2.10.0
[2.9.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.8.0...2.9.0
[2.8.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.7.0...2.8.0
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
[1.0.0]: https://github.com/pedersebastian/CustomBossChallenge/tree/35e49dabca58e55918fa44ceb775b53ed06d84f2
