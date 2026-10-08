<h1 align="center">
<a href="https://github.com/pedersebastian/CustomBossChallenge/releases/latest/download/CustomBossChallenge.dll">
    <img align="left" alt="Icon" height="90" src="Icon.png">
    <img align="right" alt="Download" height="75" src="https://raw.githubusercontent.com/gurrenm3/BTD-Mod-Helper/master/BloonsTD6%20Mod%20Helper/Resources/DownloadBtn.png">
</a>
Custom Boss Challenge
</h1>

A Bloons TD 6 mod that lets you build your own Boss Challenge: make the boss faster or tankier, change bloon stats,
move or remove skulls, and set your own starting rules. Ranked play, the weekly Boss Event and your Boss Challenge
records are never affected.

## Features

- **Customize button** on the Boss Challenge screen (Boss Events menu -> Boss Challenge) that opens the mod's
  settings. It turns green and reads "Custom Rules: ON" while custom rules are enabled. The same settings are also
  available from the Mod Helper **Mod Settings** menu.
- **Boss modifiers** – boss speed and boss health, using the game's own challenge modifiers
  (`bossSpeedMultiplier` / boss health multiplier – the same ones Ninja Kiwi uses for weekly events such as
  "Vortex at 150% speed").
- **Bloon modifiers** – bloon speed, MOAB speed, ceramic health, MOAB health and regrow rate.
- **Skulls** – keep the vanilla skulls, space any number of skulls evenly across the health bar, or place them at
  exact health percentages. Works for every boss and every Elite tier: Bloonarius, Lych (uses its ethereal
  thresholds), Vortex, Dreadbloon, Phayze, Blastapopoulos and Diamondback.
- **Start rules** – starting cash, lives, max lives, starting/ending round, plus No Monkey Knowledge, No Selling,
  No Powers, No Continues, All Camo, All Regrow and All Fortified.

## Usage

1. Open **Boss Events** from the main menu and go to **Boss Challenge**.
2. Click the **Customize** button.
3. Turn on **Enable Custom Boss Challenge** (it is off by default).
4. Adjust the boss, bloon, skull and start rule settings you want.
5. Pick a boss, tier and difficulty and start the game as usual. The settings apply to the next Boss Challenge you
   start.
6. To play normal Boss Challenges again, turn **Enable Custom Boss Challenge** off.

## Modifiers

| Category | Setting | Range | Default | Notes |
|---|---|---|---|---|
| General | Enable Custom Boss Challenge | on / off | off | Master toggle; nothing changes while off. |
| Boss | Boss Speed | 10–1000 % | 100 % | Speed of the boss bloon. |
| Boss | Boss Health | 10–2000 % | 100 % | Health of the boss bloon. |
| Bloons | Bloon Speed | 25–400 % | 100 % | Regular bloons. |
| Bloons | MOAB Speed | 25–400 % | 100 % | MOAB-class bloons. |
| Bloons | Ceramic Health | 25–1000 % | 100 % | |
| Bloons | MOAB Health | 25–1000 % | 100 % | MOAB-class bloons. |
| Bloons | Regrow Rate | 25–400 % | 100 % | How fast regrow bloons regrow. |
| Skulls | Skull Mode | Vanilla / Evenly Spaced / Custom | Vanilla | |
| Skulls | Skull Count | 0–10 | 3 | Used by Evenly Spaced; 0 removes all skulls. |
| Skulls | Custom Skulls | comma separated % | `75,50,25` | Used by Custom, e.g. `80,60,40,20` (health percentages). |
| Start Rules | Starting Cash | 0–100000 | 0 | 0 keeps the normal value. |
| Start Rules | Starting Lives | 0–10000 | 0 | 0 keeps the normal value. |
| Start Rules | Max Lives | 0–10000 | 0 | 0 keeps the normal value. |
| Start Rules | Starting Round | 0–140 | 0 | 0 keeps the normal value. |
| Start Rules | Ending Round | 0–200 | 0 | 0 keeps the normal value. |
| Start Rules | No Monkey Knowledge, No Selling, No Powers, No Continues, All Camo, All Regrow, All Fortified | on / off | off | |

## Safety guarantees

- Only **Boss Challenges** are changed. The weekly **Boss Event**, co-op Boss Challenges and every other game mode
  are left untouched.
- Custom games are **always unranked**.
- Custom games are **never saved**, so they will not overwrite your existing Boss Challenge save.
- Custom games **never update your Boss Challenge records**.

## Known limitations

- Co-op is not supported; co-op Boss Challenges always play with vanilla rules.
- Settings apply to the **next** Boss Challenge you start. Loading an existing save keeps that save's rules.

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

The mod ships with in-game automated tests built on Mod Helper's `ModTest`. They cover config application, skull
math, the skulls of every boss and Elite tier, the ranked and co-op guards, the Customize button, and a game test
that plays a vanilla and a custom Bloonarius challenge and compares the spawned bosses (speed, health, skulls,
starting cash and unranked mode).

- From Visual Studio / Rider: run the **BloonsTD6 (Tests)** launch profile.
- From the command line, in the game folder:

  ```
  BloonsTD6.exe moddingDev -batchmode --modhelper.offline --modhelper.only='CustomBossChallenge;' --modhelper.noaudio --modhelper.run -q test mod CustomBossChallenge
  ```

The result is written to `MelonLoader/Latest.log`, e.g. `Ran N test(s), N succeeded, 0 failed`.

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
