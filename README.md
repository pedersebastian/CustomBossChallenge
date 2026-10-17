<h1 align="center">
<a href="https://github.com/pedersebastian/CustomBossChallenge/releases/latest/download/CustomBossChallenge.dll">
    <img align="left" alt="Icon" height="90" src="Icon.png">
    <img align="right" alt="Download" height="75" src="https://raw.githubusercontent.com/gurrenm3/BTD-Mod-Helper/master/BloonsTD6%20Mod%20Helper/Resources/DownloadBtn.png">
</a>
Custom Boss Challenge
</h1>

A Bloons TD 6 mod that adds a **Boss Creator** to the main menu: the game's own Challenge Editor, extended with
boss options. Make the boss faster or tankier, move or remove its skulls, play anywhere from 1 to 10 boss tiers on the
rounds you choose, and combine it with every rule the Challenge Editor already has. Ranked play, the weekly Boss
Event and your Boss Challenge records and saves are never affected.

![Boss Creator](docs/boss-creator.jpg)

## Features

- **Boss Creator button** on the main menu, just below the event buttons on the right.
- **The real Challenge Editor** with everything it normally has: map, difficulty, game mode, starting cash, lives
  and rounds, special conditions, tower restrictions, bloon sliders and modifiers.
- **Boss section** at the top of the editor:
  - Pick any boss (Bloonarius, Lych, Vortex, Dreadbloon, Phayze, Blastapopoulos, Diamondback) and toggle **Elite**.
  - **Boss Speed** and **Boss Health** sliders, using the game's own boss multipliers (the same ones Ninja Kiwi uses
    for weekly events such as "Vortex at 150% speed").
  - **All Fortified**, which the vanilla editor doesn't offer.
  - **Boss Tiers** from 1 to 10. Tiers above 5 are copies of tier 5 with **Extra Tier Health** (e.g. +50% per tier).
    With fewer than 5 tiers, beating the last one wins the game.
  - **Spawn Rounds** for every tier (default 40, 60, 80, 100, 120, then every 20 rounds).
  - **Skulls**: keep the vanilla skulls, space any number evenly across the health bar, or place them at exact health
    percentages. Works for every boss and Elite tier (Lych uses its ethereal thresholds).
- **Translated** into every language BTD6 supports (English, Arabic, Chinese Simplified and Traditional, Danish,
  Dutch, Finnish, French, German, Italian, Japanese, Korean, Norwegian, Polish, Brazilian Portuguese, Russian,
  Spanish, Latin American Spanish, Swedish, Thai and Turkish), following the game's language setting.
- Every boss option has an **info button** (the "i" on its icon) that explains what it does.
- **Play** starts the custom boss game straight from the editor.
- Your **last setup is remembered** between sessions.

![Boss options](docs/boss-options.jpg)

## Usage

1. Click **Boss Creator** on the main menu (below the event buttons on the right).
2. Choose a boss, Elite or not, and set the boss options in the **Boss** section.
3. Set the map, difficulty, rules, towers and sliders like in any custom challenge.
4. Press **Play**. Pressing back saves the setup and returns to the main menu. After a game you return to the
   Boss Creator with the same setup. **Reset** also resets the boss options (except the chosen boss).

![Main menu](docs/main-menu.jpg)

## Boss options

| Option | Range | Default | Notes |
|---|---|---|---|
| Boss Type | 7 bosses | Bloonarius | Elite toggle next to it. |
| Boss Speed | 10–1000 % | 100 % | |
| Boss Health | 10–2000 % | 100 % | |
| All Fortified | on / off | off | |
| Boss Tiers | 1–10 | 5 | The game is won when the last tier is defeated. |
| Extra Tier Health | 100–500 % | 150 % | Health of each tier above 5 relative to the tier before it. |
| Spawn Rounds | comma separated rounds | 40, 60, 80, … | Missing rounds continue every 20 rounds; rounds always ascend. |
| Skulls | Vanilla / Evenly Spaced / Custom % | Vanilla | |
| Skull Count | 0–10 | 3 | Evenly Spaced only; 0 removes all skulls. |
| Skull Health % | comma separated % | 75, 50, 25 | Custom only, e.g. `80, 60, 40, 20`. |

The game's end round is moved to 20 rounds after the last tier (round 140 for the default 5 tiers, like the weekly
events) unless you set a later one yourself, and a starting round after the last tier is reset. Otherwise the
difficulty's normal last round (e.g. 60 on medium) would end the game before the later tiers arrive.

Game modes without boss support (Deflation and Apopalypse) can't be played as a boss game.

## Safety guarantees

- Only games started from the Boss Creator are changed. Normal Boss Challenges, the weekly **Boss Event**, co-op and
  every other mode are left untouched.
- Custom games are **always unranked**, are **never saved** (your Boss Challenge save is never overwritten) and never
  update Boss Challenge records, boss tier progress or badges.
- Your own Challenge Editor draft is never replaced by the boss setup.

## Upgrading from 1.x

Version 2 replaces the Mod Settings and the "Customize" button on the Boss Challenge screen with the Boss Creator.
Old settings are not carried over, and `BloonsTD6/Btd6ModHelper/Mod Settings/CustomBossChallenge.json` can be deleted.
The Boss Creator setup is stored in `BloonsTD6/UserData/CustomBossChallenge/LastSetup.json`.

## Known limitations

- Co-op is not supported.
- Tiers above 5 reuse the tier 5 look and abilities; the in-game tier display shows them like tier 5.

## Installation

### Mod Helper / Mod Browser (recommended)

1. Install [MelonLoader](https://melonwiki.xyz/) and [BTD6 Mod Helper](https://github.com/gurrenm3/BTD-Mod-Helper#readme).
2. Launch the game, open the **Mod Browser** from the Mod Helper menu, search for **Custom Boss Challenge** and
   download it.
3. Restart the game.

### Manual

1. Install MelonLoader and BTD6 Mod Helper.
2. Download `CustomBossChallenge.dll` from the
   [releases page](https://github.com/pedersebastian/CustomBossChallenge/releases).
3. Put the file in the `Mods` folder of your BTD6 installation and start the game.

## Running the tests

The mod ships with in-game automated tests built on Mod Helper's `ModTest`. They cover:

- tier, spawn round and skull math, and preparing the editor's challenge for a boss game
- extra tier models for every boss and Elite tier, and real games played to victory with 6 and with 2 tiers
- a vanilla and a custom Bloonarius game compared side by side (speed, health, skulls, cash, unranked)
- the ranked and co-op guards, and saving and loading the last setup
- the main menu button, and the whole Boss Creator flow in the real Challenge Editor, from editing the boss options
  to pressing Play, including that the normal Challenge Editor and your own draft stay untouched

Run them from Visual Studio / Rider with the **BloonsTD6 (Tests)** launch profile, or from the game folder:

```
BloonsTD6.exe moddingDev -batchmode --modhelper.offline --modhelper.only='CustomBossChallenge;' --modhelper.noaudio --modhelper.run -q test mod CustomBossChallenge
```

The result is written to `MelonLoader/Latest.log`, e.g. `Ran 13 test(s), 13 succeeded, 0 failed`.

The screenshots above come from the `ScreenshotTour` test. It only runs in a windowed game started with
`--cbc.screenshots=<folder>` (and without `-batchmode`).

## Building from source

1. Install the .NET SDK, MelonLoader and BTD6 Mod Helper (with the game run at least once so Mod Helper can set up
   its references).
2. Clone this repository into your BTD6 mod sources folder.
3. Build `CustomBossChallenge.sln` (for example `dotnet build -c Release`). The built DLL is copied to the game's
   `Mods` folder automatically.

`Icon.png` is generated by `tools/GenerateIcon.ps1`
(`powershell -ExecutionPolicy Bypass -File tools/GenerateIcon.ps1`).

## License

[MIT](LICENSE)

[![Requires BTD6 Mod Helper](https://raw.githubusercontent.com/gurrenm3/BTD-Mod-Helper/master/banner.png)](https://github.com/gurrenm3/BTD-Mod-Helper#readme)
