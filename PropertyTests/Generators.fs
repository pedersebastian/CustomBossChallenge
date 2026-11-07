/// Random setups the Boss Creator can make, and broken codes that it must refuse
module CustomBossChallenge.PropertyTests.Generators

open FsCheck
open FsCheck.FSharp
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open CustomBossChallenge.Config
open CustomBossChallenge.Persistence

/// A setup as the JSON a code holds: the boss options and the challenge, both complete. Shown as what differs from
/// the defaults, which is what the code holds.
[<StructuredFormatDisplay("{Display}")>]
type Setup =
    { Options: JObject
      Challenge: JObject }

    member this.Display =
        CodeFormat.Payload(this.Options, Game.defaultOptions (), this.Challenge, Game.defaultChallenge ())
            .ToString(Formatting.None)

/// The challenge values the generators change
let ChallengePaths =
    [ "name"; "map"; "difficulty"; "mode"; "seed"
      "startRules.cash"; "startRules.lives"; "startRules.maxLives"; "startRules.round"
      "bloonModifiers.bossSpeedMultiplier"; "bloonModifiers.healthMultipliers.boss"
      "bloonModifiers.speedMultiplier"; "bloonModifiers.moabSpeedMultiplier"
      "bloonModifiers.healthMultipliers.bloons"; "bloonModifiers.healthMultipliers.moabs"
      "bloonModifiers.allCamo"; "bloonModifiers.allRegen"; "bloonModifiers.allFortified"
      "disableMK"; "disableSelling"; "disablePowers"; "maxParagons"; "maxTowers" ]

/// Sometimes a value, mostly nothing, so most setups change only a few things like real ones do
let private maybe gen = Gen.frequency [ 2, Gen.constant None; 1, Gen.map Some gen ]

let private text (pieces: string list) maxPieces =
    gen {
        let! count = Gen.choose (0, maxPieces)
        let! chosen = Gen.listOfLength count (Gen.elements pieces)
        return String.concat "" chosen
    }

// ---- Boss options ----

let spawnRounds: Gen<JToken> =
    Gen.oneof
        [ Gen.constant (JValue.CreateNull() :> JToken)
          gen {
              let! count = Gen.choose (1, TierMath.MaxTiers)
              let! rounds = Gen.listOfLength count (Gen.choose (1, TierMath.MaxRound))
              return JValue(TierMath.FormatRounds(rounds |> List.distinct |> List.sort)) :> JToken
          }
          // What players type: spaces, semicolons, junk; the mod reads the text as it is
          text [ "0"; "5"; "40"; "999"; "1500"; ", "; ","; ";"; " "; "-"; "x" ] 12 |> Gen.map (fun t -> JValue t :> JToken) ]

let customSkulls: Gen<string> =
    gen {
        let! count = Gen.choose (0, 12)
        let! percents = Gen.listOfLength count (Gen.choose (0, 1200))
        let typed = percents |> List.map (fun p -> (float p / 10.0).ToString(System.Globalization.CultureInfo.InvariantCulture))
        return SkullMath.NormalizeCustom(String.concat ", " typed, "75, 50, 25")
    }

/// A boss per tier, or None for a single boss: bosses, Random and tiers that follow the setup's boss, Elite or not,
/// often at their normal speed, health and skulls and otherwise anywhere in range
let tierBosses: Gen<string option> =
    let value low high =
        Gen.frequency
            [ 2, Gen.constant TierBossMath.Normal
              1, Gen.choose (low / TierBossMath.ValueStep, high / TierBossMath.ValueStep) |> Gen.map (fun v -> v * TierBossMath.ValueStep) ]
    let token =
        gen {
            let! boss = Gen.frequency [ 1, Gen.constant None; 4, Gen.elements TierBossMath.Choices |> Gen.map Some ]
            let! elite = Gen.elements [ false; true ]
            let! speed = value TierBossMath.MinSpeed TierBossMath.MaxSpeed
            let! health = value TierBossMath.MinHealth TierBossMath.MaxHealth
            let! count = Gen.choose (0, SkullMath.MaxSkulls)
            let! custom = customSkulls
            let! skulls = Gen.elements [ SkullMode.Vanilla; SkullMode.Vanilla; SkullMode.EvenlySpaced; SkullMode.Custom ]
            return TierBoss(Option.toObj boss, elite, speed, health, skulls, count, custom)
        }
    Gen.frequency
        [ 3, Gen.constant None
          1, gen {
                 let! count = Gen.choose (0, TierMath.MaxTiers)
                 let! tokens = Gen.listOfLength count token
                 return Some(TierBossMath.Format tokens)
             } ]

let options: Gen<JObject> =
    gen {
        let! boss = Gen.elements Game.bosses
        let! elite = Gen.elements [ false; true ]
        let! skullMode = Gen.elements [ "Vanilla"; "EvenlySpaced"; "Custom" ]
        let! skullCount = Gen.choose (0, SkullMath.MaxSkulls)
        let! skulls = customSkulls
        let! tiers = Gen.choose (TierMath.MinTiers, TierMath.MaxTiers)
        let! speed = Gen.frequency [ 2, Gen.constant 0; 1, Gen.choose (TierMath.MinSpeedPerTier, TierMath.MaxSpeedPerTier) ]
        let! health = Gen.frequency [ 2, Gen.constant 0; 1, Gen.choose (TierMath.MinHealthPerTier, TierMath.MaxHealthPerTier) ]
        let! rounds = spawnRounds
        let! perTier = tierBosses
        let! eventRounds = Gen.frequency [ 3, Gen.constant false; 1, Gen.constant true ]
        let json =
            JObject(
                JProperty("Boss", boss),
                JProperty("Elite", elite),
                JProperty("SkullMode", skullMode),
                JProperty("SkullCount", skullCount),
                JProperty("CustomSkulls", skulls),
                JProperty("Tiers", tiers),
                JProperty("SpeedPerTier", speed),
                JProperty("HealthPerTier", health),
                JProperty("SpawnRounds", rounds),
                JProperty("BossEventRounds", eventRounds)
            )
        perTier |> Option.iter (fun list -> json["TierBosses"] <- JValue list)
        return json
    }

// ---- The challenge ----

/// Names with what breaks naive code: quotes, backslashes, control characters, other scripts and emoji
let name =
    text [ "a"; "Z"; "7"; " "; "\""; "\\"; "\u0007"; "\n"; "\t"; "é"; "ø"; "日本"; "😀"; "{"; "}"; "$items"; "CBC1:" ] 20

/// A multiplier with two decimals, like the editor's sliders, between the limits
let private multiplier min max = Gen.choose (int (min * 100.0), int (max * 100.0)) |> Gen.map (fun x -> float x / 100.0)

/// -1 is the game's "default"
let private orDefault low high = Gen.oneof [ Gen.constant -1; Gen.choose (low, high) ]

let challenge: Gen<JObject> =
    gen {
        let c = Game.defaultChallenge ()
        let set (path: string) (value: JToken option) =
            match value with
            | Some v -> c.SelectToken(path).Replace(v)
            | None -> ()
        let value (gen: Gen<'T>) = gen |> Gen.map (fun v -> JToken.FromObject(v)) |> maybe

        let! name = name |> Gen.map (fun n -> JValue n :> JToken) |> maybe
        let! map = value (Gen.elements Game.maps)
        let! difficulty = value (Gen.elements Game.difficulties)
        let! mode = value (Gen.elements Game.modes)
        let! seed = Gen.choose (0, System.Int32.MaxValue) |> Gen.map (fun s -> JValue(int64 s) :> JToken) |> Gen.map Some
        let! cash = value (orDefault 0 Game.maxCash)
        let! lives = value (orDefault 1 Game.maxLives)
        let! maxLives = value (orDefault 1 Game.maxLives)
        let! round = value (orDefault 1 TierMath.MaxRound)
        let! bossSpeed = value (multiplier Game.minBossSpeed Game.maxBossSpeed)
        let! bossHealth = value (multiplier Game.minBossHealth Game.maxBossHealth)
        let! bloonSpeed = value (multiplier Game.minMultiplier Game.maxMultiplier)
        let! moabSpeed = value (multiplier Game.minMultiplier Game.maxMultiplier)
        let! bloonHealth = value (multiplier Game.minMultiplier Game.maxMultiplier)
        let! moabHealth = value (multiplier Game.minMultiplier Game.maxMultiplier)
        let! flags = Gen.listOfLength 6 (value (Gen.elements [ false; true ]))
        let! maxParagons = value (Gen.choose (-1, 10))
        let! maxTowers = value (orDefault 0 Game.maxTowerCount)
        let! towers = Gen.listOf (Gen.zip (Gen.choose (0, Game.towerCount - 1)) (orDefault 0 Game.maxTowerCount))

        set "name" name
        set "map" map
        set "difficulty" difficulty
        set "mode" mode
        set "seed" seed
        set "startRules.cash" cash
        set "startRules.lives" lives
        set "startRules.maxLives" maxLives
        set "startRules.round" round
        set "bloonModifiers.bossSpeedMultiplier" bossSpeed
        set "bloonModifiers.healthMultipliers.boss" bossHealth
        set "bloonModifiers.speedMultiplier" bloonSpeed
        set "bloonModifiers.moabSpeedMultiplier" moabSpeed
        set "bloonModifiers.healthMultipliers.bloons" bloonHealth
        set "bloonModifiers.healthMultipliers.moabs" moabHealth
        List.iter2 set (ChallengePaths |> List.filter (fun p -> p.Contains ".all" || p.StartsWith "disable")) flags
        set "maxParagons" maxParagons
        set "maxTowers" maxTowers
        for index, max in towers do
            (c["towers"].[index]).["max"] <- JValue(int64 max)
        return c
    }

let setup = Gen.map2 (fun o c -> { Options = o; Challenge = c }) options challenge

/// Smaller setups for a failing one: each changed value put back to its default, one at a time
let shrink (setup: Setup) =
    let options = Game.defaultOptions ()
    let challenge = Game.defaultChallenge ()
    let reset (source: JObject) (defaults: JObject) (path: string) make =
        let value = source.SelectToken(path)
        let standard = defaults.SelectToken(path)
        if JToken.DeepEquals(value, standard) then None
        else
            let copy = source.DeepClone() :?> JObject
            copy.SelectToken(path).Replace(standard.DeepClone())
            Some(make copy)
    let towers = [ for i in 0 .. Game.towerCount - 1 -> $"towers[{i}].max" ]
    seq {
        for property in options.Properties() do
            yield! reset setup.Options options property.Name (fun o -> { setup with Options = o }) |> Option.toList
        for path in ChallengePaths @ towers do
            yield! reset setup.Challenge challenge path (fun c -> { setup with Challenge = c }) |> Option.toList
    }

let setups = Arb.fromGenShrink (setup, shrink)

// ---- Encoding and reading, like SetupCode without the game ----

let private mixed (options: JObject) =
    not (isNull options["TierBosses"]) && options["TierBosses"].Type <> JTokenType.Null

let prefix (options: JObject) =
    if options.Value<bool>("BossEventRounds") then CodeFormat.Version4Prefix
    elif mixed options then CodeFormat.Version3Prefix
    elif options.Value<int>("SpeedPerTier") <> 0 || options.Value<int>("HealthPerTier") <> 0
    then CodeFormat.Version2Prefix
    else CodeFormat.Prefix

let encode setup =
    let payload = CodeFormat.Payload(setup.Options, Game.defaultOptions (), setup.Challenge, Game.defaultChallenge ())
    CodeFormat.Pack(payload.ToString(Formatting.None), prefix setup.Options)

/// The setup in a code, put back on top of the defaults the way SetupCode.TryDecode does
let decode code =
    match CodeFormat.TryUnpack(code) with
    | true, payload, _, _ ->
        let options = Game.defaultOptions ()
        let challenge = Game.defaultChallenge ()
        match payload["o"] with
        | :? JObject as diff -> CodeFormat.Apply(options, diff) |> ignore
        | _ -> ()
        match payload["c"] with
        | :? JObject as diff ->
            let filtered = CodeFormat.FilterChallenge(diff, Game.defaultChallenge (), ResizeArray())
            CodeFormat.Apply(challenge, filtered) |> ignore
        | _ -> ()
        Some { Options = options; Challenge = challenge }
    | _ -> None

// ---- Broken codes ----

let private base64Url = Gen.elements ([ 'A' .. 'Z' ] @ [ 'a' .. 'z' ] @ [ '0' .. '9' ] @ [ '-'; '_' ])

let junk: Gen<string> =
    let anyString = ArbMap.defaults |> ArbMap.generate<string> |> Gen.map (fun s -> if isNull s then "" else s)
    Gen.oneof
        [ anyString
          // Looks like a code from some version
          gen {
              let! digit = Gen.elements [ '0' .. '9' ]
              let! rest = anyString
              return $"CBC{digit}{rest}"
          }
          gen {
              let! version = Gen.elements (List.ofArray CodeFormat.KnownPrefixes)
              let! body = Gen.arrayOf base64Url
              return version + System.String body
          }
          // A real code cut short
          gen {
              let! code = Gen.map encode setup
              let! length = Gen.choose (0, code.Length - 1)
              return code[.. length - 1]
          }
          // A real code with one character changed
          gen {
              let! code = Gen.map encode setup
              let! index = Gen.choose (CodeFormat.PrefixLength, code.Length - 1)
              let! c = base64Url
              return code[.. index - 1] + string c + code[index + 1 ..]
          } ]

// ---- Hostile challenge diffs ----

/// Any JSON value, of every type a hand made code could hold
let rec private anyJson depth : Gen<JToken> =
    let leaf =
        Gen.oneof
            [ Gen.constant (JValue.CreateNull() :> JToken)
              Gen.elements [ true; false ] |> Gen.map (fun b -> JValue b :> JToken)
              Gen.elements [ 0.0; -1.0; 5.0; 2.5; 1e30; -1e30; nan; infinity; -infinity ] |> Gen.map (fun d -> JValue d :> JToken)
              Gen.elements [ 0L; -1L; 7L; 2147483648L; -2147483649L; System.Int64.MaxValue ] |> Gen.map (fun l -> JValue l :> JToken)
              Gen.elements [ ""; "x"; "Logs"; "NaN"; "__proto__"; "SuperMonkey" ] |> Gen.map (fun t -> JValue t :> JToken) ]
    if depth <= 0 then leaf
    else
        Gen.frequency
            [ 4, leaf
              1, Gen.listOfLength 2 (anyJson (depth - 1)) |> Gen.map (fun items -> JArray(items) :> JToken)
              1, hostileObject (depth - 1) ]

/// An object with keys the challenge has, keys it doesn't, and keys that are tricks
and hostileObject depth : Gen<JToken> =
    gen {
        let keys =
            [ "name"; "map"; "maxParagons"; "seed"; "startRules"; "cash"; "revives"; "bloonModifiers"; "healthMultipliers"
              "boss"; "allCamo"; "towers"; "max"; "tower"; "isHero"; "$items"; "0"; "1"; "99"; "cashModifiers"
              "roundSets"; "powers"; "$type"; "__proto__"; "customMapModel" ]
        let! count = Gen.choose (0, 4)
        let! names = Gen.listOfLength count (Gen.elements keys)
        let! values = Gen.listOfLength count (anyJson depth)
        let o = JObject()
        for name, value in List.zip names values do
            o[name] <- value
        return o :> JToken
    }

/// A challenge diff a hand made code could hold
let hostileDiffs =
    Arb.fromGen (hostileObject 3 |> Gen.map (fun t -> t :?> JObject))
