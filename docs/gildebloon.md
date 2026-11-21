# Gildebloon (#25): design notes and findings

Gildebloon is the mod's own boss: a golden vault that steals cash, hides behind a shield made of it and pays it back
when it is beaten. It is hidden behind `Config.Experimental.Gildebloon` (off in released versions) and the Mod
Settings option *Include Gildebloon* (shown only when the code switch is on).

## Spike findings (BTD6 57.4, 2026-11-18)

Measured with a throwaway in-game test.

- **Own boss type works.** Adding a cloned boss model `Gildebloon1` to the match's GameModel (in `OnNewGameModel`)
  and setting `Simulation.model.bossBloonType = "Gildebloon"` right before `BossBloonManager.SpawnBoss(1, false)`
  spawns `Gildebloon1` with the model's health. No errors were logged and the game's boss panel shows. The game sets
  `bossBloonType` from `InGameData.bossData.bossBloon` *after* `OnNewGameModel`, so the type must be set right before
  each spawn (as `Patches.MixedBosses` does), not in `OnNewGameModel`. `bossData.bossBloon` stays a game boss
  (Bloonarius), which keeps the game's boss panel and leaderboard code on known data.
- **Victory, defeated tiers and ranked time** work for the cloned boss as for a game boss (`gameWon`, tier 1
  defeated, `GetBossTotalCompletionTime` counted).
- **Cash.** In single player the only cash manager has index **-1**, not 0 (`Simulation.GetCash(0)` throws "No Cash
  Manager for Index: 0"). `Simulation.RemoveCash(amount, CashType.Normal, -1, CashSource.Normal)` and
  `AddCash(amount, CashType.Normal, -1, CashSource.Normal, null, false)` work. Neither changes `cashSpent`, so stealing
  doesn't count against Least Cash.
- **Shield at runtime.** `Bloon.AddArmourLayer(new BloonArmour {id, amount, original})` gives a live boss armour
  (`HasArmour`, `CurrentArmourAmount`, `CurrentArmourProportion`). This is the Vault.
- **Mod Settings.** `ModSettingCategory.modifyCategory` and `ModSetting.modifyOption` are `Action`s on the created
  UI component, so the category and option can hide themselves while the code switch is off.

## Why not a new BossType

The game's `BossType` enum can't get a value. The mod uses `(BossType) 100` with the name "Gildebloon"
(`Config.BossList`), writes it by name in files and codes (`Persistence.BossNameConverter`) and routes every
boss name through `BossList.Name`. The game itself only ever sees the string `bossBloonType` and model ids
`Gildebloon1`..`Gildebloon5`, `GildebloonElite1`..`GildebloonElite5`.

## How it is built

- `Config/Gildebloon.cs` makes the models: `Gildebloon1..5` and `GildebloonElite1..5` from Bloonarius's models
  (Bloonarius's spew, spawns and bleed taken out), and the Tax Collectors `GildebloonCollector` (a gold MOAB) and
  `GildebloonCollectorElite` (a gold BFB). They are added to the game's model at the main menu, and to each custom
  match's model, only while `Experimental.Gildebloon` is on.
- From the game: the skull trigger (Repossession: 3 s invulnerable with Blastapopoulos's own invulnerability actions,
  Tax Collectors sent). The game's `DamageTakenTriggerModel` + `DashForwardsActionModel` (for Skittish) and
  `IncreaseDamageFromTypeModel` (for Tarnish) did nothing on it when tried in the game, so those are the mod's own:
  `GildebloonBrain` makes it dash 60 ahead (`Bloon.SetPath`) after losing 10 % of its health within a second, and the
  Alchemist's and Glue Gunner's attacks get a `DamageModifierForTagModel` (x1.5) against the tag "Gildebloon" its
  models carry. Damage over time (the Glue Gunner's corrosive glue) isn't covered. A Harmony prefix on `Bloon.Damage`
  crashed the game natively at the first hit, so don't patch it.
- `Config/GildebloonBrain.cs` runs each simulation frame (`BossBloonManager.Process`): Toll, Vault (an armour
  layer `GildeVault`), Interest (Elite), seizing Banana Farms at a skull, Tax Collector leaks and the Payout.
- `Patches/MixedBosses.cs` sets the game's boss type to "Gildebloon" before each of its tiers spawns, also when the
  whole setup is Gildebloon; `bossData.bossBloon` gets Bloonarius (`BossList.Host`).
- `Visuals/GildebloonHud.cs` shows the Vault under the boss panel.

## Its look (M4)

- `UnityProject/Assets/Editor/BuildGildebloon.cs` builds the model in code: a gold hull with plate seams and rivets,
  a vault door with a dial on top (the game looks down on it), money bags on its sides and a ring of coins; Elite is
  darker with a red band and a crown of spikes. It writes `Bundles/gildebloon.layout.json`, the textures and the
  portraits in `Images/`.
- **No bundle.** In 57.4 the game crashes (natively, nothing logged) on `Il2CppAssetBundle.LoadAssetAsync` for a
  `GameObject` (with or without a shader in the bundle) and for a `Texture2D`, at the main menu as in a match.
  Only the TierGlow `Shader` loads. So `Visuals/GildebloonMeshes.cs` makes the same meshes in the game, and the
  textures are pictures of the mod loaded with `ModContent.GetTexture`.
- `Visuals/GildebloonLook.cs` hides the Bloonarius body, builds the parts under the boss's display, about as wide as
  Bloonarius (45 units; the body's own bounds can't be used, its meshes reach across the map), gives them materials
  made from the body's own material, and moves them: the dial spins, the coins circle, the bags empty with its
  health, and the door bursts open with a spray of coins at each skull. Without the layout or textures the body is
  tinted gold instead.
- A hand-made model later (M6) only has to keep the part names `Hull`, `VaultDoor`, `Dial`, `BagLeft`,
  `BagRight` and `CoinRing`. Since nothing can come from a bundle, it would have to be exported as meshes the mod
  can read (for example as vertex data in the layout file) rather than as an FBX in a bundle.

## No Bloonarius left (round 2)

What could still show of Bloonarius, and how each is handled:

- **Its map effects.** The game puts Bloonarius's slime (`BloonariusTrackFx`, `BloonariusAmbientMapFx`) on the map
  for the boss it is launched as. While Gildebloon is shown they are hidden, and `Visuals/GildebloonMapLook.cs` puts a
  pile of coins and money bags where the slime lay. A game boss in Advanced Mode gets its effects back.
  The pile is modelled in Blender (`tools/blender/gildebloon-pile.py`) in the game's cartoon style: toon bands and
  dark outlines (inverted shells). `tools/blender/build-pile.py` writes it to `Bundles/gildebloon-pile.mesh.gz`, with
  the bands baked into UVs on a ramp texture that `Visuals/GildebloonPileMesh.cs` makes, since a model can't come from
  an asset bundle. Without the file the mod builds a simple pile itself.
  The pile only takes the place of the track's slime: the ambient one round the map's edges is hidden with nothing in
  its place, though every game boss has one. `tools/blender/gildebloon-map.py` is a proposal for it, not used yet:
  molten gold oozing in from the edges with drips, coins and gold bars stuck in it and sparkles, slowly swelling.
- **Its own body and animations (proposal).** `tools/blender/gildebloon-boss.py` builds a body in the game's style
  (bold outlines, yellow gold with a bloon's shine, glints in the eyes, a soft round shadow instead of a cast one)
  that keeps the part names the mod moves, plus a face, and keys eight animations: Spawn (up out of the pile), Idle,
  Hit, Toll, Repossession, Skittish, VaultBreak and Death. Its parts are rigid, so each animation could be exported as
  a position, rotation and scale per part and frame for `GildebloonLook`. Not used yet.
- **Its body as it takes damage.** The game's damage-state animation turns the body's parts back on every frame
  (setting `Renderer.enabled`), so turning them off didn't hold: after a skull the Elite's damaged parts drew through.
  They are now kept from drawing with `Renderer.forceRenderingOff`, which the animations leave alone, checked every
  frame for parts the game adds.
- **Its death.** `SpawnDeathAnimModel` is taken out of the models; the look bursts into coins instead, and the body
  stays hidden until the game puts the display away.
- **Its pops and sounds** are a ZOMG's (`PopEffectModel`, `CreateSoundOnDamageBloonModel`,
  `CreateSoundOnSpawnBloonModel`).
- **Its skin on the Tax Collectors.** Bloons a boss sends get a `BossSkinMutator`, which adds a separate display
  (`BfbBloonariusTag`, Bloonarius's toad-skin bumps) outside the bloon's own. `Patches.Bloon_AddMutator_Gildebloon`
  keeps it off the Tax Collectors, which get a small gold vault of their own as a look.
- **Skulls.** The skull trigger now has `preventFallthrough` like the game's bosses, so no single hit takes it past a
  skull, and every Repossession happens.

`Tests.GildebloonNoBloonariusTest` scans every renderer drawing during an Elite match, through every skull and its
death, for "Bloonarius" in its object, mesh or material.

## The Repossession on screen

At each skull Gildebloon seizes cash and the towers near it (50 to 70 units, by tier). `Visuals/GildebloonSeizeLook.cs`
shows it on the map: a gold ring grows out to its reach, coins fly from the seized towers to it, and every seized tower
has a padlock over it whose ring shrinks as its time runs out (3 s, Banana Farms 12 s), bursting into coins when it is
let go. The banner says what it took ("froze 1 tower for 3 s", the Banana Farms on their own line), and a selected
seized tower shows its time left in the middle of the screen. The Vault makes way while the tower menu is open.

Things drawn on the map with a material made from the map's own are drawn before the towers and bloons (render queue
1998), so they would be hidden under them; the padlocks and the ring use copies drawn last (3500). The game's camera is
orthographic and looks down at 60 degrees; towers stand at y = 0, bosses fly at y = 180.

## Gold Rush, its own rounds

With Boss Event Rounds on, a Gildebloon match plays Gildebloon's own 140 rounds, `Config/RoundSets/gildebloon.json`
(`BossOptions.GildebloonRoundSet`), like Dreadbloon's full set; in CHIMPS the CHIMPS events' rounds, unless the setup
picks the boss's own. The game can't merge a set it doesn't have, so `BossEventRounds` swaps the rounds in like the
CHIMPS sets, and past round 140 freeplay goes on as usual.

`tools/make-gildebloon-rounds.py` makes the file from the game's default rounds and bloon RBE in `tools/data`
(written by the in-game `RoundDataTest` with `CBC_ROUND_DATA=<repo>/tools/data`); `--check` tells whether the file is
up to date. Rounds 1-10 and the boss rounds are the default ones; from round 11 plain bloons turn to metal (Lead, Zebra,
Purple, later camo and fortified Lead and fortified Ceramics), MOAB-class groups come in half the time from round 40,
and the rounds before the boss rounds (39, 59, 79, 99, 119) are short heists. Every round stays within 10 % of the
default round's RBE (`UnitTests/GildebloonRoundsTests`).

## Checked once by hand (2026-11-18)

A throwaway in-game probe, not kept as a test, checked what the tests don't: Elite's Interest (the Vault grew 303 ->
315 in 4 s without a theft), the CHIMPS payout (x1), Skittish (one 12 % hit: one dash of 60), Tarnish (an Alchemist's
100-damage potion did 150, a dart 100), a leaking Tax Collector (takes 5 % of the cash and no lives, also with 50
lives) and Retry Last Round after Gildebloon leaked (offered, and Gildebloon comes back with its Vault and shield).

It found that a leaking Gildebloon counted as beaten (and paid out, so no retry was offered): the game takes a boss
away through `BossBloonManager.BloonDestroyed` also when it leaks, so a Gildebloon with health left isn't counted
there now. A leaking Tax Collector cost the lives of the bloons inside it whatever its model's leak damage said, so
`Patches.Bloon_Leaked` holds the lives up while it leaks and puts them back.
