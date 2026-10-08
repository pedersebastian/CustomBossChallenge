# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--List any changes you make to your mod in the Unreleased section. If you trigger a release in GitHub, the sections will be automatically updated for the new version.-->

## [Unreleased]

### Added

- "Customize" button on the Boss Challenge screen that opens the mod's settings and turns green while custom rules are enabled (the same settings are also available in the Mod Helper Mod Settings menu).
- Master toggle "Enable Custom Boss Challenge" (off by default).
- Boss modifiers: Boss Speed % (10-1000) and Boss Health % (10-2000), using the game's own challenge multipliers.
- Bloon modifiers: Bloon Speed, MOAB Speed, Ceramic Health, MOAB Health and Regrow Rate.
- Skull options: Vanilla, Evenly Spaced (0-10 skulls) or Custom health thresholds (e.g. `80,60,40,20`), for every boss and Elite tier, including Lych's ethereal thresholds.
- Start rules: Starting Cash, Starting Lives, Max Lives, Starting Round, Ending Round, and toggles for No Monkey Knowledge, No Selling, No Powers, No Continues, All Camo, All Regrow and All Fortified.
- Safety guards: only Boss Challenges are affected; custom games are always unranked, never saved and never update Boss Challenge records. The weekly Boss Event, co-op and all other modes are untouched.
- In-game automated tests (Mod Helper `ModTest`) covering config application, skull math, all boss/Elite skulls, ranked/co-op guards, the Customize button and a game test comparing a vanilla and a custom Bloonarius challenge.
