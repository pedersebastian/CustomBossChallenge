/// What the tests need to know about the game, read from the website's copy (web/src/data/game.json, written by the
/// in-game WebDataTest). Codes are diffed against the same neutral defaults the website uses.
module CustomBossChallenge.PropertyTests.Game

open System.IO
open Newtonsoft.Json.Linq
open CustomBossChallenge.Persistence

/// The repository folder, found from where the tests run
let repo =
    let rec up (dir: DirectoryInfo) =
        if isNull dir then failwith "Can't find web/src/data/game.json above the test folder"
        elif File.Exists(Path.Combine(dir.FullName, "web", "src", "data", "game.json")) then dir.FullName
        else up dir.Parent
    up (DirectoryInfo System.AppContext.BaseDirectory)

let private game = JObject.Parse(File.ReadAllText(Path.Combine(repo, "web", "src", "data", "game.json")))

let private keys (name: string) = (game[name] :?> JObject).Properties() |> Seq.map _.Name |> Array.ofSeq

let defaultOptions () = game["defaultOptions"].DeepClone() :?> JObject
let defaultChallenge () = game["defaultChallenge"].DeepClone() :?> JObject

let bosses = keys "bosses"
let maps = keys "maps"
let modes = keys "modes"
let difficulties = keys "difficulties"
let towerCount = (game["defaultChallenge"]["towers"] :?> JArray).Count

let private limit (name: string) = game["limits"][name]
let maxNameLength = limit("maxNameLength").Value<int>()
let minMultiplier = limit("minMultiplier").Value<float>()
let maxMultiplier = limit("maxMultiplier").Value<float>()
let minBossSpeed = limit("minBossSpeed").Value<float>()
let maxBossSpeed = limit("maxBossSpeed").Value<float>()
let minBossHealth = limit("minBossHealth").Value<float>()
let maxBossHealth = limit("maxBossHealth").Value<float>()
let maxCash = limit("maxCash").Value<int>()
let maxLives = limit("maxLives").Value<int>()
let maxTowerCount = limit("maxTowerCount").Value<int>()

/// The mod's limits must match what the website was told
do
    if limit("maxCodeLength").Value<int>() <> CodeFormat.MaxLength
       || limit("maxJsonLength").Value<int>() <> CodeFormat.MaxJsonLength then
        failwith "game.json's code limits differ from CodeFormat: write web/src/data again (see CONTRIBUTING.md)"
