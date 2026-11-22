# Boss portraits

The bosses' portraits on the website (#206): `<Boss>.webp` and `<Boss>Elite.webp` for every boss in `BOSSES`, the
same pictures the Boss Creator shows in the game (`BossEditorSection.Portrait`).

They are **© Ninja Kiwi**, from Bloons TD 6, and used here as fan content. The website credits them in every page's
footer. They are not covered by this repository's license.

They come straight from the game, not from another site: the in-game test `BossPortraitsTest` saves them as PNGs, and
`web/scripts/portraits.py` makes these 128 x 128 WebP files from them. To make them again, e.g. after a game update
or for a new boss:

1. Build the mod, and run the game windowed (not `-batchmode`) with
   `CBC_TESTS=BossPortraitsTest` and `CBC_PORTRAITS=<a scratch folder>` (see CONTRIBUTING.md, *In-game tests*).
2. `python -I web/scripts/portraits.py <the scratch folder> web/public/bosses`

`web/test/boss-portraits.test.ts` fails while a boss in `BOSSES` has no portraits here.
