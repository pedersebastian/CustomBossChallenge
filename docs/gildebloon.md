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
- **What it takes** is a share of the cash with no cap (2026-11-21; the old caps took $150 a theft at tier 1, 0.15 % of
  $100,000): a Toll every 6 s takes 2 % (Elite 4 %), a skull 10 % (Elite 15 %). The Vault's shield is at most half
  the boss's health (`ShieldCapShare`), so a player with a lot of cash can still beat it; the Payout pays back all it
  took and a fifth more (`PayoutFactor` 1.2; half again was too much, 2026-11-22), in CHIMPS only what it took.
- **Shield per dollar by tier** (2026-11-22). Its health grows faster from tier to tier than a player's cash (tier 1:
  12,000 health at about round 40 with some $20,000; tier 5: 1.9 million at about round 120 with some $650,000), so at
  one point a dollar the Vault was up to 40 % of its health at tier 1 and about 1 % at tier 5.
  `GildebloonBrain.ShieldPerCash` is 1, 1.4, 2.4, 2.7, 4.9 by tier (Elite 1.5, 3.3, 8.2, 17, 34), which keeps a theft
  of the same share of the cash about the same share of its health; tiers past 5 use tier 5's.
- **The shield comes only at a skull** (2026-11-22). From every Toll it was rebuilt faster than a farming player's
  towers could break it: with $847,000 at tier 2 every Toll filled it to its cap, and capped at 4 % of its health a
  Toll, tier 3 still stood at 195,001 of 200,000 with 8,000 shield every 6 s. Now Tolls, Tax Collectors and Interest
  only fill the Vault (the cash paid back), and a skull's Repossession turns the cash it seizes into shield: at most
  10 % of its health (Elite 15 %; `SkullShield`), lasting 8 s (Elite 10 s; `ShieldSeconds`) after the skull's
  invulnerability, then it goes (VaultBreak). While it is up Gildebloon moves at half speed (`ShieldedSpeed`; the
  brain cuts back what it moved each frame, since bosses ignore slows), and a gold bar under the Vault empties as the
  towers hit it and blinks in its last two seconds. So it always gets on between skulls, and the shield is a short
  race you can see.
- **Tax Collectors by tier** (2026-11-22; a MOAB was no threat from tier 3 on): MOAB at tiers 1-2, BFB at 3-4, ZOMG at
  5; Elite one step up and fortified from tier 4 (`Gildebloon.CollectorFor`). A bigger one takes more when it leaks:
  MOAB 5 %, BFB 8 %, ZOMG 10 %, fortified ZOMG 12 % (`Gildebloon.CollectorKinds`). Their look is the gold blimp of
  Dispatch with a green $ on its back, as long and as wide as the bloon it replaces (measured in the game: MOAB 25.9 x
  42.4, BFB 43.6 x 60.1, ZOMG 49.1 x 75.9); at first about 60 % of a MOAB, it looked odd, and the game's effects on the
  MOAB (glue) lay off it.
- **It guards its gold against towers that make money** (2026-11-22): the upgrades that can make cash do half their
  damage to it (`Gildebloon.CashTowerDamage`), found by what their models have (`Gildebloon.CashBehaviours`: cash
  drops, income each round, cash from pops, bounty and cash zones), so only those upgrades count: a Sniper from x-4-x
  (Supply Drop), a Heli Pilot from x-4-x, an Engineer from x-x-4 (Bloon Trap), a Buccaneer from x-x-3 (Merchantman),
  an Alchemist from x-x-3 (Rubber to Gold), a Super Monkey from x-4-x, a Druid from x-4-x, a Desperado's x-4-x, the
  paragons that make cash, Benjamin and Obyn's levels that do. Their projectiles get a `DamageModifierForTagModel`
  (x0.5) for its tag, like Tarnish. Towers in a Monkey Town's reach (a Monkey Village x-x-3 or more, whose pops are
  worth more) do half too: the Monkey Town gets a `DamageModifierSupportModel` with the same modifier, unique, so two
  Monkey Towns don't halve it twice. `GildebloonCashGuardTest`: a Sniper beside a Monkey Town did 57 % of what one
  away from it did.
- `Config/GildebloonBrain.cs` runs each simulation frame (`BossBloonManager.Process`): Toll, Vault (an armour
  layer `GildeVault`), Interest (Elite), seizing Banana Farms at a skull, Tax Collector leaks and the Payout.
- `Patches/MixedBosses.cs` sets the game's boss type to "Gildebloon" before each of its tiers spawns, also when the
  whole setup is Gildebloon; `bossData.bossBloon` gets Bloonarius (`BossList.Host`).
- `Visuals/GildebloonHud.cs` shows the Vault under the boss panel, and what it takes and pays back at the player's own
  cash (top left): "-$700" under it and coins flying on the screen from the cash to Gildebloon (the Payout: "+$840"
  and a shower of coins back). Before (2026-11-22) only "-$700" next to the Vault showed it, far from the cash.

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
  The ambient effect round the map's edges gets Gildebloon's own (`Visuals/GildebloonMapOoze.cs`): a soft golden glow
  coming in from the screen's edges and fading out inwards, with a few sparkles twinkling in it, subtle like the game
  bosses' own (a band of molten gold with drips and coins was too much). Modelled in `tools/blender/gildebloon-map.py`
  and written by `tools/blender/build-map.py` to `Bundles/gildebloon-map.gba.gz` in the body's format (one looping
  animation, "Ooze"; the glow's fade baked as a see-through ramp row of kind 3). It sits on the camera, facing it and
  as big as its view: laid on the map's ground it went askew and hid under walls, and the hidden effect's own bounds
  can't size it. The camera stays from match to match, so the glow goes with the map it was put round (and whenever
  Gildebloon's effects aren't shown): `GildebloonOffTest` checks a normal Boss Challenge has none.
- **Its portraits** (`Images/Gildebloon.png`, `Images/GildebloonElite.png`: the boss panel by the skulls, the Boss
  Creator, the banner and the victory screen) are rendered from the Blender body by `tools/blender/build-portraits.py`.
- **Its own body and animations.** `tools/blender/gildebloon-boss.py` builds a body in the game's style (bold
  outlines, yellow gold with a bloon's shine, glints in the eyes, a soft round shadow instead of a cast one) that keeps
  the part names the mod moves, plus a face, and keys 21 animations, each from the same rest pose and held on its last
  frame so it plays on its own. `tools/blender/render-sheet.py` renders them as a contact sheet.
  `tools/blender/build-boss.py` (background Blender) writes `Bundles/gildebloon-body.gba.gz` through
  `tools/blender/export-boss.py`, which describes the format: every part is rigid, so an animation is a position,
  rotation and scale per part and frame, and the toon bands are baked into a ramp like the pile's, for 12 ways to face:
  the mod gives the meshes the set nearest to how far the body is turned from facing the camera, so the light stays
  where it is on the screen as Gildebloon turns.
  `Visuals/GildebloonBodyData.cs` reads it and holds the player (one animation over a base loop, a priority for what
  may cut in, a short blend; free of the game's types, so `UnitTests/GildebloonBodyTests.cs` checks both);
  `Visuals/GildebloonBody.cs` builds the game's meshes and poses them. The boss gets this body when the file loaded,
  else the older code-made one; Tax Collectors keep the code-made vault. What starts each animation
  (`GildebloonLook.AnimateBody`, `Finish`, `PlayEnd`):

  | Animation | What | Starts on |
  |---|---|---|
  | Spawn, EliteSpawn | up out of the pile; Elite's crown of spikes shoots up | the body attaching to a boss for the first time; Elite by its model id |
  | Idle, IdleCount, IdlePolish, IdleSmug | hovering; flips a coin; polishes its shine; smug | the base loop; every 9-15 s when nothing plays; after a Toll of $250 or more |
  | Damaged66, Damaged33, Damaged10 | dents; cracks, the door rattling, sweat; coins trickling out (loops) | its health share at or under 0.66, 0.33, 0.10 (they replace Idle) |
  | Hit, TarnishHit | squash and squint; grey-green tarnish flashing on the gold | a drop in health (at most every 0.4 s); TarnishHit when an acid tower's projectile (one carrying `Gildebloon.Tarnish`) was within 60 units of it in the last 0.3 s: the game's damage can't be hooked (`Bloon.Damage` crashes), so the look watches the projectiles instead (`GildebloonTarnishTest`) |
  | Toll, Interest, VaultUp, VaultBreak | coins into the slot; the Vault pulsing (Elite); the shield growing; the shield shattering | `GildebloonBrain.CashTaken` above 0 (a Toll or a Tax Collector's leak); `GildebloonBrain.InterestAdded`; the armour from 0 up; to 0 |
  | Repossession, Skittish, Dispatch | a skull; the dash ahead; a Tax Collector popping out of the door | a skull passed; `GildebloonBrain.Dashes` going up; a Tax Collector's look attaching |
  | Death, Escape, Payout | bursts into coins; grins and dives out; the coins fountaining up and out (which way the cash is on the screen depends on how it faces, so the HUD flies them on to the cash) | gone with no health left; gone with health left (it leaked); `CashTaken` below 0 after Death |

  Seen in the game (2026-11-21, the Gildebloon tests' screenshots): it faces along the track, is about Bloonarius's
  size, rises out of the pile, and Elite's band, crown, dents and cracks show. Its see-through parts, the Vault's shell
  and the soft shadow under it, are drawn with Unity's `Sprites/Default`, which blends by the ramp's alpha (the shadow
  fades out from its middle, the shell shows most at its rim); the game's URP Unlit set to blend came out as a dark,
  solid film. Tax Collectors are the gold MOAB of Dispatch (the parts under `CollectorProxy`). Reading the file straight
  from the gzip stream took 6 s at the main menu; unpacked into memory first, 50 ms.
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

- **What towers put on it** (2026-11-22). The game draws towers' effects on a boss from its overlay class, and
  Gildebloon's is Bloonarius's: they are children of Bloonarius's body under `BloonariusOverlays`, switched on and off
  by the game. The look hid them with the rest of Bloonarius's body; now it leaves that group drawn
  (`GildebloonLook.HideBody`) and shrinks Bloonarius's hidden rig onto Gildebloon (`OverlayScale`, `OverlayOffset`;
  Bloonarius's body is about twice Gildebloon's), a little bigger than its hull, so the coats lie on the hull and not
  in it. The effects that coat a whole boss are shaped for Bloonarius's long body with fins, and no scaling laid them
  tight on Gildebloon's round hull, so those are Gildebloon's own, modelled on its hull in
  `tools/blender/gildebloon-boss.py` (groups `Overlay<name>` under `Hull`, hidden in Blender's renders): glue in its four
  colours (yellow made lime to show on the gold, green, pink, pink with bubbles), goo bent onto the hull with wavy
  edges, drips and a glob over the door; Unstable Concoction's purple acid spots with bubbles; fire and Laser Shock
  like the game's, flat on the hull without outlines: red-orange flame tongues with yellow hearts trailing back and red
  flames licking up round its rim, and broad white lightning with a red edge round the door (cartoon flames standing out
  as cones and thin pink lines didn't read as fire and laser shock, 2026-11-22). `GildebloonLook.Coats` maps the game's overlay to the coat:
  the coat shows while the game's is on, and the game's is kept from drawing. Sticky bombs, dynamite, drones, MOAB
  Maim and the Skywarden's are the game's own on the shrunk rig. `GildebloonOverlayTest` puts a Bloon Dissolver's
  glue, a Wizard's fire, a Dartling's Laser Shock and a sticky bomb on it. The body file grew to 1.3 MB (271 parts),
  loaded in about 100 ms.
  What a boss shows, found by
  scanning every tower's models for an overlay type (game 57.4) and checked in tests: glue (Glue Gunner: yellow for
  most, green from Bloon Dissolver 3-x-x, pink with bubbles from 2-0-5; plain pink is 2-0-3's and 2-0-4's in the
  models but didn't glue a boss in a test, and 0-0-4 and 0-0-5 glue no boss; also the Desperado's Sheriff), sticky bombs
  (Ninja 0-0-4, Master Bomber 0-0-5, the Ninja Paragon's bomb tags), fire (Mortar, Wizard, Gwendolin, Buccaneer,
  Desperado and more), Laser Shock (Dartling 2-x-x), MOAB Maim (Bomb Shooter 0-3-x, Sniper 4-x-x), Unstable
  Concoction (Alchemist 0-3-x), the Skywarden's Chilled, Galvanised and Windlash, the Sheriff's dynamite and a kinetic
  charge drone. Other overlays (acid, ice, pins, Ezili's hex, radiation, ink...) have no boss version, so no boss
  shows them in the game either.

`Tests.GildebloonNoBloonariusTest` scans every renderer drawing during an Elite match, through every skull and its
death, for "Bloonarius" in its object, mesh or material.

## The Repossession on screen

At each skull Gildebloon seizes cash and the towers near it (50 to 70 units, by tier). `Visuals/GildebloonSeizeLook.cs`
shows it on the map: a gold ring grows out to its reach, coins fly from the seized towers to it, and every seized tower
is outlined in gold, greyed a little and has a padlock over it. The padlock is modelled in Blender
(`tools/blender/gildebloon-padlock.py`, written by `build-padlock.py` to `Bundles/gildebloon-padlock.gba.gz` in the
body's format; `Visuals/GildebloonPadlock.cs`): a gold padlock facing the camera with a green $ on it (a banana on a
Banana Farm's, which is bigger) and a ring of 16 gold ticks that go one by one as its time runs out (3 s, Banana Farms
12 s). It drops in (Lock), sways (Hold), and springs open (Unlock) and bursts into coins when the tower is let go. The
flat padlock before it read as a crate from the game's camera (2026-11-22); it stays as the fallback without the file.

The banner shows a big "Repossession!" with a line under it for each thing it took ("-$3,947", "1 tower frozen for
3 s", "2 Banana Farms locked for 12 s"), on the half of the screen Gildebloon isn't on, so it doesn't cover it. A
selected seized tower shows its time left in the middle of the screen. The Vault makes way while the tower menu is
open.

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

## What it costs (2026-11-22)

`GildebloonCostTest` plays busy Elite matches (glue, fire, a sticky bomb, a skull with padlocks, Tax Collectors and
coins flying) and times the mod's per-frame parts (`CustomBossChallenge.UpdateTicks`, kept by `Guarded`):

- **Time.** Gildebloon took 0.9 ms a frame, 0.78 of it its look: looking through all of the boss's renderers every
  frame for the game's parts to keep from drawing (0.40 ms), looking through the game's overlays and their renderers
  (0.17 ms) and posing all 271 parts of its body (0.18 ms). Now the body is looked through every frame only while the
  game builds its display (its first 30 frames) and then every 15th, the game's coats are hidden when they come on,
  and a part is only moved when its pose changed: 0.5 ms a frame, the look 0.41 ms.
- **Memory.** Nothing piles up from match to match: the game's meshes, materials, textures, sprites and objects are
  as many after three more matches, and managed memory stays put. The game's own memory grows each match, by itself
  (Bloonarius: 11.6 MB a match); with Gildebloon about 2.7 MB more, of which about 1.2 MB is the Vault's HUD: Mod
  Helper's panels and texts are never quite freed (see `LeakTest`), and the HUD makes about a dozen each match.
