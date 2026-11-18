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
  Tax Collectors sent), Skittish (`DamageTakenTriggerModel` + `DashForwardsActionModel`) and Tarnish (+50 % acid
  damage, `IncreaseDamageFromTypeModel`).
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
