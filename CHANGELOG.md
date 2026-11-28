# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--List any changes you make to your mod in the Unreleased section. If you trigger a release in GitHub, the sections will be automatically updated for the new version.-->

## [Unreleased]

### Added

- **Mystery map effects for a secret boss** (#285): while a boss Random picked is still a secret, the map shows the
  mod's own effects in place of the boss's: a soft haze coming in from the screen's edges with a few pips twinkling in
  it, and three small dice at the boss's spawn point that hop, tumble and land on new faces now and then, in the
  colours of the Random die. They go when the boss arrives, and a map or accessibility setting without boss effects
  gets none of them either. Modelled in Blender (`tools/blender/mystery-fx.py`).

- **Play again with the same Random bosses** (#270): after a game with Random tiers in Advanced Mode, the Boss Creator
  has a Last Game's Bosses row. Play Again plays the setup once more with the bosses Random picked last time, while
  the setup keeps its Random tiers, and Copy makes a share code with those bosses. The victory and defeat screens
  copy that code too, and the game's own Restart already keeps the bosses. Whoever pastes such a code sees Random on
  those tiers, in the Boss Creator, in the game until each boss arrives and on the website, and Play gives the same
  bosses; picking another boss for such a tier makes it a normal tier again. These codes start with `CBC7:`, so
  older versions of the mod ask for an update instead of showing the bosses; the website reads them.

- **Difficulty stars** (#263): the Boss Creator shows a difficulty rating from 1 to 10 stars under the Tier Preview,
  and the website shows it with every setup: on the home page, in the Studio and in Compare. It is a rough estimate
  from the setup's numbers: each tier's health and speed weighted by how late it comes, the boss (Phayze and Lych
  count for more than Bloonarius), Elite, the number of tiers, how early they arrive and how close together, the
  skulls, Boss Event Rounds
  and the CHIMPS events' rounds, the map, the mode and difficulty, lives, cash, max towers and paragons, banned towers and the
  other rules. Its i says it is an estimate. The mod and the website share the formula, checked by shared test cases;
  most past Elite events get 5 to 7 stars, most normal ones 1 to 3, and 10 is left for setups harder than
  any event so far.
- **What's new after an update** (#264): the first time the Boss Creator opens after the mod is updated, a popup in the
  game's own look shows what the new version added, once, in a list that scrolls. Not on a fresh install, and never
  twice for the same version; Don't Show Again in the popup, or Show What's New in Mod Settings, turns it off. The
  website has a What's new button in its menu with the newest versions' news, from the same changelog, and a dot on it
  after a release.
- **Boss event stats on the website** (#265): a new Stats page, linked in the menu and from Past Events, shows the
  official boss events in numbers: events per boss, normal and Elite, the days since each boss was last on, a timeline
  since July 2021, the difficulty stars of #263 for every event (averages per boss and per year, and the hardest
  events), how the events were ranked each year and the maps and modes used most, per boss and per year. Every chart
  has its numbers in a table, and the page follows the archive as events are added. The website's Past Events list
  shows each level's difficulty stars too, and can be sorted newest, oldest, hardest or easiest first.
- **A link to one past event** (#271): every event on the website's Past Events page has a Copy event link button,
  for a link like `past-events.html#event=Lych47_m5zq59de` to post on Discord or Reddit. The link opens the page on
  that event, marked and with the keyboard on it, and clears only the filters that would hide it; it works with the
  search, filters and order in the address too. A link to an event the list doesn't have shows the list with a note.
- **An issue the day BTD6 updates** (#272, #19, for contributors): a daily GitHub workflow watches Steam's news for a
  new version and Steam's public build for hotfixes, and opens an issue with the update checklist, once per version
  or build. The checklist is a new section of CONTRIBUTING.md, *Updating for a new BTD6 version*.
- **README pictures from the game** (#269, for contributors): `tools/screenshots/Make-Screenshots.ps1` runs the
  screenshot tour in the game and makes every picture in `docs/` from it, in English, at a fixed size, with fixed
  setups, and only replaces a picture that really changed. The tour now also takes the Rules panels, the Share Code
  row and the Tier Preview. It is a step before a release only: it writes `TestResults/screenshots.json`, and
  `Check-Release.ps1` warns when a release's pictures weren't made, as a reminder to look through them.
- **Pseudo language** (#267, for contributors): `CBC_LANGUAGES=Pseudo` runs `LanguageLayoutTest` with every text of
  the mod about 40 % longer, accented and in brackets, to find texts that would be cut off in a longer translation
  and English that skips the localization. The test now also reports one-line labels that wrap and dropdown texts
  that run into their arrow, in every language. It found the English-only default setup names, filed as #279.
- **In-game tests in every language** (#291, for contributors): `CBC_LANGUAGE=Arabic`, or any other language of the
  game including `Monkey`, runs every in-game test in that language, and `.github/scripts/Run-InGameTests.ps1` runs
  them in several languages one after the other. The tests compare what the game shows with the mod's own texts
  through `Tests/ScreenText.cs`, which reads Arabic the way the game shows it, so they no longer only pass in English
  and Norwegian. The test receipt says which language the run was in.

### Fixed

- **Dreadbloon's armour goes round with more skulls** (#304): Dreadbloon's armour is immune to Primary when it spawns,
  then Military, Magic and Support at its skulls. With more skulls than its own, it ran out after the fourth (Elite:
  the eighth) and the rest of the skulls gave armour without an immunity. Now the immunities go round, Primary again
  after Support, however many skulls it has. Phayze, who gets stronger at every skull, keeps getting stronger by the
  same step past its own skulls, as Elite Phayze does. A new in-game test pushes every boss, normal and Elite, through
  its own skulls, 8, 10, three close together and one at 1 %; the other bosses already went through all of them.
- **Skulls count per boss in the difficulty stars** (#304): each skull more than the boss's own makes the stars go
  up by how much that boss's skull hurts: Phayze the most, as it gets stronger at every skull, then Dreadbloon and Lych,
  Vortex and Blastapopoulos, Diamondback, and Bloonarius the least. More skulls used to count as easier, as breathers;
  fewer skulls than the boss's own still add a little. The website rates setups the same way.
- **Past Events show CHIMPS by its name** (#267): an event in CHIMPS mode showed the game's internal "Clicks" in its
  details and in the mode filter, in every language.
- **Dropdown texts keep clear of their arrow** (#267): in the Past Events filters and the Saved Setups list a longer
  entry ran into the arrow, in English too. The year filter is a little wider, so "Every Year" fits in every language,
  and the Normal button of a past event no longer wraps a long translation onto two lines.
- **A new setup's name is in your language** (#279): "Custom Lych", "Custom Elite Lych" and "Custom Boss Gauntlet" were
  English in every language. They are now translated where the Challenge Editor's name field keeps the name, like the
  example setups' names, and English otherwise. Changing the boss still renames a setup that has the name it was
  given, in English or any other language.
- **A Random boss stays a secret until it arrives** (#283): losing before a Random tier's boss arrived showed that
  boss on the defeat screen; it now shows the die, with the Elite glow for Random Elite, like the boss line-up. The
  map no longer gives it away either: when the first tier is Random, it showed that boss's effects around the map and
  at the spawn point from the start, and a later Random tier kept the beaten boss's effects. Now the map has no boss
  effects until a Random boss arrives, from the start or from the round after the boss before it is beaten.
- **Boss Event Rounds is said once in the Rules panel**: outside CHIMPS the pause screen's Rules box showed both the
  game's own Custom Rounds tile and a Boss Event Rounds tile for the same thing. Now it shows Custom Rounds only, like
  a real boss event; in CHIMPS the Boss Event Rounds tile stays, as the mod brings the CHIMPS events' rounds itself.
- **Tier Preview warnings stay readable in Arabic** (#291): a short warning, like the one for Boss Event Rounds in
  CHIMPS, shrank below the readable size on one line, because the Arabic letters are taller. It now gets a second line
  instead. In Russian, a warning on two lines touched the one below it; it sits a little higher now.
- **Arabic letters join everywhere** (#292): the mod's texts that the game or Mod Helper show by themselves, like All
  Fortified's title in the Challenge Editor and the mod's settings and their info in Mod Settings, showed loose
  Arabic letters. The mod now gives the game its Arabic shaped, the way the game keeps its own.

## [2.14.0] - 2026-11-22

### Added

- **Past events with Insta Monkeys off or only some powers** (#212): an event that turns off Insta Monkeys or allows
  only some powers opens with those rules set, instead of listing them as left out. The Challenge Editor has no
  control for them, so the Tier Preview says they are on, and that Copy and Save leave them out: share codes can't
  hold them. The last setup, Undo and Continue keep them. No event so far has used them.

### Changed

- **New setups play the boss events' rounds** (#251): Boss Event Rounds starts ticked on your first visit, after Reset
  Boss Options and Delete on the option, and in Random setups; in CHIMPS that is the newest CHIMPS events' rounds. The
  normal rounds are full of bloons that pull towers away from the boss; the event rounds are made for boss games.
  Share codes, saved setups, your last setup and unfinished games keep the rounds they were made with.
- **Share codes version 6** (#250, #251): a code with Boss Health or a tier's own health below 10 %, above 2000 % or
  (a tier's) on a step of 5 % starts with `CBC6:`, so older versions of the mod ask for an update instead of changing
  the health. Other codes keep the oldest version that holds them, and the website reads and makes them all.
- **Boss Health goes from 5 % to 5000 %** (#250), up from 10–2000 %, for Boss Health and each tier's own health in
  Advanced Mode. The slider moves in steps of 5 % up to 500 % and 10 % above, also with the arrows and the keyboard.
  The game's health limit still holds every tier below about 2 billion.
- **Two suites of in-game tests** (#246, for contributors): a quick suite of about 8 minutes for every PR, and the full
  suite of about 38 minutes before a release, which the release check now asks for by name. `TierLookTest` is about a
  third faster. CONTRIBUTING.md says which tests a change needs.
- **The Rules panel shows the boss setup as pictures** (#147): the pause screen's Rules box no longer lists the boss
  options as lines of text. The boss is a tile with its portrait (Elite's own when Elite), and Boss Event Rounds and
  "No other changes" are tiles too, after the game's own rules like No Selling. Custom spawn rounds and Advanced Mode
  line-ups are the Tier Preview's cards, with the round each tier arrives on, its speed and health, and in Advanced
  Mode its boss and skulls; a boss picked at random stays a die with "?" until it arrives. Evenly spaced and custom
  skulls are shown on a health bar like the Skull Preview. A 10-tier line-up fits at every screen format, and the box
  scrolls to it.
- **Elite goes where the language puts it**: the banner when a tier arrives says "Tier 1: Elite Bloonarius" in English
  instead of "Tier 1: Bloonarius Elite", while French, Spanish, Italian, Portuguese and Arabic keep it after the boss,
  like "Bloonarius Élite". The Rules panel's boss tile and a shared setup's summary follow the same order.

### Fixed

- **Arabic, Chinese, Japanese and Korean show properly** (#244): many of the mod's texts showed boxes in place of
  letters, because the game only swaps in a language's font for texts it translates itself. They now get the
  language's font. In Arabic the mod's texts also showed backwards with loose letters, and tags like `<noparse>` and
  `<color=…>` showed as text: they are now joined, right to left and coloured like the game's own, with brackets the
  right way round. The language fonts only have the letters the game needs, so a few Chinese, Japanese and Korean
  translations use other words now, and a test checks every translation against its fonts.
- **Share codes read the same in the mod and on the website** (#236): a code with the tiers a game picked at random
  kept that list in the mod, while the website drops it. The mod now drops it too, and a saved game keeps only tiers
  that exist, each once. Nothing played differently, since a game works the list out again when it starts.
- **No more errors in the log with All Fortified** (#228): every custom game with All Fortified on logged three
  `KeyNotFoundException` errors per Lych tier while it loaded, whichever boss was played, because the mod looked for
  fortified undead MOABs the game doesn't have. The game played the same, but the log no longer fills with them.
- **Loaded setups come back to the Boss Creator after their game** (#229): a setup from a pasted code, a saved setup,
  an example or a Past Event, played and then quit, lost or won, left the player in the game's plain Challenge Editor
  with the setup from before. Back from there logged an error, and after several such games the game could crash.
  Now every setup comes back to the Boss Creator as it was played.
- **Damaged saved setups are no longer lost when another is saved** (#230): when some setups in `SavedSetups.json`
  couldn't be read, saving a setup wrote the file without them and kept no copy. Now the file is kept as
  `SavedSetups.json.<time>.broken` first, like a file that can't be read at all. A file that couldn't be replaced, for
  example a read only one, also no longer leaves a `.tmp` file next to it.
- **Retry Last Round when the first round played is lost** (#231): losing the round a custom game starts on showed
  no retry at all, because the mod only kept a snapshot after a round was played. The first round now gets one as it
  starts, with the towers placed before it, so Retry Last Round is offered for free there too.
- **Past Event Diamondback 6 can be played** (#232): it starts on round 40 with its first tier on round 40, which
  the Boss Creator refused as if the tier would be skipped. The boss comes on the round a game starts on, so a start
  round equal to the first tier's round is now allowed; only a later one is refused.
- **Esc closes Past Events and the tier picker after typing** (#233): Esc in the Past Events search or the tier
  picker's skull list ended the edit, but the field stayed selected, so Enter or Space on an event started the edit
  again and Esc never got past ending it. A field the edit ended in now lets go, so the next Esc closes the window.
- **The Boss Creator no longer keeps its old windows in memory** (#235): every time the Boss Creator, a tier
  picker or Past Events was opened, the game kept the one before, about 3.4 MB for a visit with all three, because
  the game never lets go of a button's listener. Their listeners now let go of the window when it closes, which
  leaves about a third of that, from Mod Helper's own parts. The empty text assets Mod Helper leaves behind are
  cleaned up too.
- **Texts fit in every language** (#234): "Reset Boss Options" in German and Swedish, Elite in Japanese and Undo and
  Redo in Dutch and Finnish shrink to fit instead of being cut off with "…". In Arabic, Chinese, Japanese, Korean and
  Thai the tier picker's and Past Events' titles and the skull count on the Tier Preview's cards showed nothing, as
  those fonts' lines were taller than their boxes; the skull percentages and the Past Events search hint now fit too.
  A tier card's lines are a little further apart.
- **Text in several layers of `<...>` is read the same each time**: Paste and the website took off only the outer
  `<` and `>`, so pasting what they found gave a different result again. Every layer comes off now.

## [2.13.0] - 2026-11-17

### Changed

- **Boss Speed up to 2500 %** (#220), the fastest boss Ninja Kiwi has run (Bloonarius 61). Also each tier's own
  speed in Advanced, share codes, and the website. Bloonarius 61 and Dreadbloon 27 Elite now open in Past Events with
  their real boss speed instead of 1000 %. A code with more than 1000 % opens at 1000 % in older versions of the mod.

### Fixed

- **Diamondback 6 in Past Events**: its rules from Ninja Kiwi say 0 max lives and 0 max monkeys, which opened it with
  one max life. Those 0s now mean the game's default, so it plays with its 200 (Elite 100) lives and no monkey limit.

- **Setup names in every language** (#218): the Challenge Editor's name field only keeps plain letters (A-Z), digits
  and some punctuation, so a translated example or Past Events name lost letters as soon as it was loaded
  ("Ingen hodeskaller, ingen nåde", "Élite", "17. März 2025", and whole names in Chinese, Japanese, Russian or Thai),
  and the example no longer counted as one. Now a translated name the field would change, or a Past Events name longer
  than its 40 characters, is written in English, so it stays as it is; names the field keeps, like "Maraton Lych" or
  "17. jan. 2025", stay translated. The lists still show the translations.
- The in-game tests no longer need the game in English: the check of the website's game data leaves out the names
  of maps and towers in another language, and only writes them in English.

## [2.12.0] - 2026-11-16

### Added

- **Past Events** (#146): a new row in the Boss Creator opens every official boss event since July 2021 in a window of
  its own, with a search and filters for the boss, the year and the mode. **Normal** or **Elite** opens an event with
  its own rules (map, mode, cash, lives, towers, paragons, bloon and boss modifiers and its event rounds), named after
  the event and its date, as one Undo step. Rules the Boss Creator can't play are listed instead of half applied. The
  mod carries the events up to its release and downloads newer ones from the mod's website at the start of the game,
  in the background and with a cache, so new events show up without an update; **Download New Boss Events** in the
  mod's settings turns that off.
- **Past Events on the website** (#213): a new page, Past events, lists the same events with the same search and
  filters, which stay in the address so a filtered list can be shared. Each event opens in the Studio, normal or
  Elite, or is copied as a code or a link the Boss Creator loads with the event's rules; rules the Boss Creator can't
  play are named, and events that use wiki content link their source and licence.
- **Undo and Redo buttons** (#185): next to Reset Boss Options, two buttons undo and redo one step like Ctrl+Z and
  Ctrl+Y, so the mouse and touch screens can too. They name their shortcut, are greyed out when there is nothing to
  undo or redo, and the keyboard reaches them with Tab.
- **CHIMPS Throwback example** (#162): a new example setup plays Bloonarius in CHIMPS on Hard with Boss Event Rounds
  and the earlier CHIMPS boss events' rounds (regrow, camo regrow and DDTs), in the Boss Creator and on the website.
- **A map for every example**: the examples no longer all play Monkey Meadow. Marathon Lych plays Haunted, Speed
  Demon Vortex Logs, Glass Cannon Bloonarius Prime, No Skulls, No Mercy Ravine, Speedrun Dreadbloon Cargo, CHIMPS
  Throwback Resort and Boss Gauntlet In The Loop; Paragon Ban stays on Infernal.
- **The boss's own event rounds in CHIMPS**: the CHIMPS Boss Event Rounds dropdown has a fourth entry, "Boss's own
  event rounds", which plays the boss's own event rounds like the other modes (and CHIMPS before 2.11), with every
  tier of Advanced Mode on the round made for its own boss. It has no warning about the CHIMPS events' rounds. The
  website's Studio, preview and Compare know it too. Codes with it start with `CBC5:`, so 2.11 asks for an update
  instead of failing to read them.
- **Random and Reset in the Studio** (#12): the website's Studio has the Boss Creator's buttons above its rows.
  **Random** randomizes the boss options exactly like the mod (the same chances and weights, Advanced staying
  Advanced) and gives a setup that stands out the same describing name, unless you typed your own. The game's own
  randomizer for the Challenge Editor's rules isn't part of the mod, so the Studio only picks a map, difficulty and
  mode and keeps the other rules a new challenge has. **Reset Boss Options** puts the boss options back and keeps
  the rules; **Reset** starts over with a new setup. Both ask first. The names are checked against the mod's in
  shared test cases.
- **Info buttons** (#157): the Boss Creator's CHIMPS dropdown has an **i** of its own that explains how the two CHIMPS
  round sets differ: the newest have no regrow but denser rounds with more raw RBE (a damage check, most of all on
  rounds 41 to 120), the earlier ones have regrow, camo regrow and DDTs spread over longer rounds (a detection check),
  and the rounds made for the bosses are nearly the same. Every row of the website's Studio has an **i** too, showing the Boss
  Creator's explanation of the option, and the CHIMPS dropdown has its own. Shared tests keep the Studio's texts the
  same as the mod's.
- **The Boss Creator's warnings on the website** (#186): the preview, the Studio and Compare show the warnings the
  Tier Preview has before Play, in the same words and order: a start round just before the first boss, tiers a few
  rounds apart, tiers held to the game's health limit, tiers that end up the same, Boss Event Rounds in CHIMPS and a
  very hard challenge. Before, the website only showed the health limit and the CHIMPS warning. `@cbc/share-code` has
  the checks as `warnings`, tested with the mod's own cases and with what the mod warns about for codes made in the
  game. With a Boss Health below 100 %, the website's health limit is now the mod's too: it said half as much at 50 %.

### Changed

- **CHIMPS round sets named by what they throw at you** (#157): the dropdown says "Newest CHIMPS events (no regrow,
  dense)" and "Earlier CHIMPS events (regrow, DDTs)" instead of "hard" and "harder", which was only partly true. The
  Boss Event Rounds explanation has a sentence per set, and the Tier Preview's CHIMPS warning names what the picked set
  tests. In every language.
- **Codes pasted with other text** (#187): Paste and the website find a code in quotes or backticks, after "Try this:",
  before a full stop or an invisible character, and in a link with words after it. A code with a broken %-escape is
  still reported as damaged.
- **Decimal commas in Skull Health** (#187): a list split by `;` reads `,` between digits as a decimal point, so
  `33,3; 66,6` gives skulls at 66.6 % and 33.3 %, in the mod and on the website.

### Fixed

- **Your unfinished Boss Challenge is kept**: losing a custom game deleted your unfinished normal Boss Challenge of
  the same boss, since the game removes that boss's save when one of its games is lost. Custom games now leave it
  alone. The in-game tests also no longer leave a normal Boss Challenge among your unfinished games, and every test
  checks that your map saves are unchanged after it.
- **Advanced Mode after Load, Paste, Random or Undo** (#166): the tabs remembered the bosses per tier and Standard's
  Boss Speed, Boss Health and skulls across a new setup. Advanced could then get the old setup's lineup, and
  Standard could write the old 200 % into a pasted code. A loaded, pasted or random setup now starts over, and
  Ctrl+Z brings back what the tabs remembered at that step.
- **Huge tier speeds and health in hand-made codes** (#168): a tier's own speed or health near int's limits, like
  `Lych@2147483647/2147483647`, overflowed and flipped to the other end of its range, so the mod played 10 % where
  the website showed 1000 %. Both now clamp it alike, and the website also reads -2147483648 like the mod.
- **Boss Tiers help in 17 languages** (#172): it said every tier is stronger than the one before, which isn't true
  when Speed or Health per Tier is below 0. It now says so in every language, like in English.
- **Numbers on every system culture** (#172): the ranked stats panel's tiers and cash and the Boss Creator's sliders
  and per-tier labels are written the same everywhere ("12,345", "-10"), not with the system's own spaces, apostrophes
  or minus sign, which the game's font may not have.
- **CHIMPS boss event rounds on the website** (#139): the website and `@cbc/share-code` now know the CHIMPS
  dropdown. A code with the earlier CHIMPS events' rounds kept them only until it was changed in the Studio, which made
  it play the newest ones. In CHIMPS the Studio has the Boss Creator's dropdown (Off, Newest, Earlier) in place of the
  Boss Event Rounds tick, the preview shows the Boss Creator's warning about these rounds, and Compare tells which
  CHIMPS events' rounds each side plays.
- A code with a CHIMPS rounds value the mod doesn't know now plays the newest CHIMPS events' rounds and is saved that
  way, like an unknown boss or skull mode.
- **Hand-made and damaged codes read the same in the mod and on the website** (#174): the mod no longer reads codes
  whose JSON isn't strict JSON (single quotes, comments, trailing commas, `010`, `0x3`, `undefined`), that have spaces
  or line breaks inside, or bytes after or a piece missing at the end, which the website couldn't read. The website no
  longer reads codes nested deeper than the mod can. Text in options, like Spawn Rounds, Custom Skulls and the tiers'
  bosses, is trimmed and compared like in the mod, so a no-break space, byte order mark or Kelvin sign means the same
  on both. Codes the mod or the website made are not affected. Shared test cases keep the two the same.
- **Website pages** (#173): the skip link on Compare no longer empties both boxes while they hold text that isn't a
  code yet. See details on an example no longer scrolls smoothly when you asked for less motion, and moves the
  keyboard to the setup it opened. A browser that refuses to change the address bar (Safari, after many changes in a
  short time, e.g. a long slider drag in the Studio) no longer keeps a result from showing.
- **All six warnings under the Tier Preview** (#170): the Boss Creator had room for five warning lines, so "very hard
  setup", which comes last, was left out when all six kinds of warning applied at once (e.g. CHIMPS with Boss Event
  Rounds). It now has a line for every kind.
- **Room for more bosses** (#170): the Boss Type row works out the size of its boss buttons from how many bosses the
  game has, so a game update with an eighth boss doesn't put the Elite label on the reset button. A boss the mod has
  no portrait or HUD icon for is logged once.
- **Saved files** (#182): an unreadable saved setups file is logged and copied aside once, not again every time the
  list is shown or a name is checked. Spawn Rounds are kept as the first 10 rounds in them ("40, 60, 80"), so a
  hand-made code with a huge Spawn Rounds text no longer carries it into the last setup, the unfinished game and saved
  setups. The rounds played stay the same, and the text is still the format older versions read.
- **Older browsers on the website** (#181): a browser without `CompressionStream` (Safari before 16.4, Firefox before
  113, some in-app browsers) now says it can't read setup codes, on the main page, Compare and the Studio, instead of
  calling every code damaged. The Studio no longer fails silently there with an empty code box.
- **Tier picker and dropdowns in the Boss Creator** (#175): leaving the Boss Creator with a tier's picker open no
  longer leaves the picker on top of the editor. Enter or Space opens the Skulls and Saved Setups lists like the other
  dropdowns, and while a list is open, the arrows and Esc work in the list instead of moving the keyboard frame,
  changing the value behind it or closing the Boss Creator.
- **Every rule on the website** (#167): the setup view and Compare now show the regrow rate, ability cooldown,
  removable cost, least cash and least tiers, No Continues, No Double Cash, No Fast Track, the Insta Monkey reward,
  and each tower's blocked upgrades and Paragon lock. A code that only changed one of them used to compare as the
  same as the default setup. The Studio can change ability cooldown, removable cost, least cash and least tiers.
- **Sliders and Undo** (#171): Reset on an option that is already at its default, or an arrow at a slider's minimum
  or maximum, no longer adds an empty Undo step or clears Redo, and typing the same Spawn Rounds again keeps Undo and
  Redo. A dragged slider snaps to its step, like its arrows.
- **Smoother games and Boss Creator** (#183, #184): the HUDs, the keyboard frame and the tiers' look do less work every
  frame, the Boss Creator lays out its section only when its rows change instead of twice per slider tick and no longer
  reads the rules every half second, and the tiers' glow materials are freed after a game.

## [2.11.0] - 2026-11-12

### Added

- **CHIMPS boss event rounds** (#139): in CHIMPS, the Boss Event Rounds tick becomes a dropdown: Off, the newest
  CHIMPS boss event's rounds (`chimpsboss`, Diamondback in October 2026; hard, the default) or the earlier ones'
  (`chimpsbloonarius`, Bloonarius in 2024 and Dreadbloon in 2025; harder). Either set is the same for every boss and every
  tier in Advanced Mode. The game only gets these round sets from NK's servers during an event, so the mod brings its
  own copies, taken from the dumps on Blooncyclopedia (`chimpsbloonarius`) and in BTD6 API Explorer (`chimpsboss`).
  The rounds move with the Spawn Rounds like the other boss rounds, and games past round 140 go on into freeplay as
  before. Other modes still play the boss's own event rounds. The Tier Preview warns that these rounds are much
  harder than the normal ones.
- **Boss options in the Rules panel** (#141): in a custom game, the pause screen's Rules button now also shows the
  Boss Creator's options that differ from a normal boss game. The sliders (Boss Tiers, Speed per Tier, Health per
  Tier, Skull Count) are rows of the Modifiers list after the game's Boss Health and Boss Speed; the boss and Elite,
  spawn rounds, boss event rounds and custom skulls are lines in the Rules box. A setup with nothing else changed says
  so. In Advanced Mode (#142) the Rules box has a line per tier with its round, boss and what differs from the boss's
  own (speed, health, skulls); a boss picked at random shows as Random until it arrives, like on the HUD.
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

### Fixed

- **No selling in CHIMPS**: custom boss games in CHIMPS let towers be sold for $0, because in a challenge the game
  only turns selling off with the challenge's own Disable Selling. They now have no selling, like the game's own
  CHIMPS. A lost CHIMPS game's setup loads with Disable Selling off, so it doesn't stay on in another mode.

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

[unreleased]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.14.0...HEAD
[2.14.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.13.0...2.14.0
[2.13.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.12.0...2.13.0
[2.12.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.11.0...2.12.0
[2.11.0]: https://github.com/pedersebastian/CustomBossChallenge/compare/2.10.2...2.11.0
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
