# Samples for the tests of the game version check

- `steam-news.json`: Steam's
  [news for BTD6](https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=960090&count=20&maxlength=1&feeds=steam_community_announcements)
  on 22 Nov 2026, cut down to the newest eight posts without their text and author. The newest update in it is 57.0.
- `steamcmd-info.json`: [steamcmd.net's app info](https://api.steamcmd.net/v1/info/960090) the same day, cut down to
  the name and the branches. The public build is 26122510 (BTD6 57.4).
