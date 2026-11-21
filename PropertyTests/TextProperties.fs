/// What must hold for the text players type or paste, beyond the codes themselves: a tier's own speed and health, the
/// bosses per tier, custom skulls and a pasted code inside other text. Each is checked on thousands of random texts,
/// with values at int's limits and the separators players use, which found #168 and #187 too late, by hand.
module CustomBossChallenge.PropertyTests.TextProperties

open System
open FsCheck
open FsCheck.FSharp
open Xunit
open CustomBossChallenge.Config
open CustomBossChallenge.Persistence

let private (|@) (condition: bool) (label: string) = Prop.label label condition

let private check (property: Property) =
    Check.One(Config.QuickThrowOnFailure.WithMaxTest(2000), property)

let private text (pieces: string list) maxPieces =
    gen {
        let! count = Gen.choose (0, maxPieces)
        let! chosen = Gen.listOfLength count (Gen.elements pieces)
        return String.concat "" chosen
    }

/// Any int, often one near its limits or around the range a tier's values may have
let private anyInt =
    Gen.oneof
        [ Gen.choose (Int32.MinValue, Int32.MaxValue)
          Gen.choose (-3000, 3000)
          Gen.elements
              [ Int32.MinValue; Int32.MinValue + 1; Int32.MinValue + 5; -2147483645; 2147483645
                Int32.MaxValue - 5; Int32.MaxValue - 1; Int32.MaxValue; -5; 0; 4; 5; 6; 15; 995; 1005; 1995; 2005 ] ]

// ---- A tier's speed and health ----

[<Fact>]
let ``A tier's speed and health stay in their range, on their step, for any int (#168, #250)`` () =
    // Speed moves in steps of 10; health in steps of 5 up to 500 % and 10 above (HealthSteps)
    let speed =
        "speed", (fun value -> TierBossMath.ClampValue(value, TierBossMath.MinSpeed, TierBossMath.MaxSpeed)),
        (fun clamped -> clamped % TierBossMath.ValueStep = 0), TierBossMath.MinSpeed, TierBossMath.MaxSpeed
    let health =
        "health", (fun (value: int) -> HealthSteps.Snap(float value)),
        (fun clamped -> HealthSteps.ValueAt(HealthSteps.PositionOf(float clamped)) = clamped),
        TierBossMath.MinHealth, TierBossMath.MaxHealth
    Prop.forAll (Arb.fromGen (Gen.zip anyInt (Gen.elements [ speed; health ]))) (fun (value, (name, clamp, onStep, min, max)) ->
        let clamped = clamp value
        ((clamped >= min && clamped <= max) |@ $"{name} {value} gave {clamped}, outside {min}-{max}")
        .&. (onStep clamped |@ $"{name} {value} gave {clamped}, not on the step")
        // Values in the range only move to the nearest step, and those outside it go to its nearer end, however far
        // out (an int overflow sent 2147483647 to the bottom, #168)
        .&. ((value < min || value > max || abs (int64 clamped - int64 value) <= 5L) |@ $"{name} {value} moved to {clamped}")
        .&. ((value <= max || clamped = max) |@ $"{name} {value} is above {max} but gave {clamped}")
        .&. ((value >= min || clamped = min) |@ $"{name} {value} is below {min} but gave {clamped}"))
    |> check

/// Bosses per tier as players and old codes write them: names in any case, Elite, Random, values at int's limits,
/// skulls, empty tiers and junk
let private tierBossLists =
    let pieces =
        [ yield! TierBossMath.Bosses; yield! TierBossMath.Bosses |> Array.map (fun b -> b.ToLowerInvariant())
          "Random"; "Elite"; "RandomElite"; ","; ","; ","; " "; "@"; "/"; "#"; "%"; ";"
          "100"; "150"; "0"; "-15"; "2147483647"; "-2147483648"; "2147483648"; "99999999999"; "4"; "75%"; "x"; "@@" ]
    text pieces 30

[<Fact>]
let ``Bosses per tier read back the same once normalized`` () =
    Prop.forAll (Arb.fromGen tierBossLists) (fun list ->
        let once = TierBossMath.Normalize list
        let tiers = TierBossMath.Parse once
        (TierBossMath.Normalize once = once |@ $"{list} gave {once}, then {TierBossMath.Normalize once}")
        .&. (tiers.Length <= TierMath.MaxTiers |@ $"{tiers.Length} tiers")
        .&. (tiers
             |> Array.forall (fun tier ->
                 tier.Speed >= TierBossMath.MinSpeed && tier.Speed <= TierBossMath.MaxSpeed
                 && tier.Health >= TierBossMath.MinHealth && tier.Health <= TierBossMath.MaxHealth
                 && tier.SkullCount >= 0 && tier.SkullCount <= SkullMath.MaxSkulls)
             |@ $"{once} has a value out of range"))
    |> check

// ---- Custom skulls ----

/// Skull lists as typed in any language: decimal points and commas, ; and , as separators, %, junk
let private skullTexts =
    text [ "0"; "1"; "3"; "5"; "9"; "33"; "50"; "99"; "100"; ","; ", "; ";"; "; "; " "; "."; "%"; "-"; "e"; "x" ] 20

[<Fact>]
let ``Custom skulls are at most ten, falling, between 1 and 99 percent`` () =
    Prop.forAll (Arb.fromGen skullTexts) (fun text ->
        match SkullMath.ParseCustom text with
        | null -> true |@ "nothing valid"
        | values ->
            ((values.Length >= 1 && values.Length <= SkullMath.MaxSkulls) |@ $"{values.Length} skulls")
            .&. (values |> Array.forall (fun v -> v >= 0.01f && v <= 0.99f) |@ $"{text}: a skull out of range")
            .&. (values |> Array.pairwise |> Array.forall (fun (a, b) -> a > b) |@ $"{text}: not falling"))
    |> check

[<Fact>]
let ``Custom skulls shown back read as the same skulls (#187)`` () =
    Prop.forAll (Arb.fromGen skullTexts) (fun text ->
        let shown = SkullMath.NormalizeCustom(text, "fallback")
        SkullMath.NormalizeCustom(shown, "fallback") = shown |@ $"{text} showed {shown}")
    |> check

// ---- A pasted code ----

let private codes =
    gen {
        let! version = Gen.choose (1, 9)
        let! length = Gen.choose (1, 40)
        let! body = Gen.arrayOfLength length (Gen.elements ("ABCXYZabcxyz0189-_" |> Seq.toList))
        return $"CBC{version}:{String(body)}"
    }

/// What people put before a code: nothing, words, "Try this:", quotes, backticks, a bracket
let private before =
    Gen.elements [ ""; "Try this: "; "Try this:"; "\""; "`"; "```\n"; "("; "“"; "here -> "; "​"; "> " ]

/// What can follow a code: nothing, a full stop, quotes, backticks, an invisible character, words after a space
let private after =
    Gen.elements [ ""; "."; "!"; ","; "\""; "`"; "\n```"; ")"; "”"; "​"; "﻿"; " out"; " 😀"; "\n" ]

[<Fact>]
let ``A code is found inside the text around it (#187)`` () =
    Prop.forAll (Arb.fromGen (Gen.zip3 before codes after)) (fun (prefix, code, suffix) ->
        let pasted = prefix + code + suffix
        let link = $"check {CodeFormat.Link(code)}{suffix}"
        (CodeFormat.ExtractCode pasted = code |@ $"{pasted} gave {CodeFormat.ExtractCode pasted}")
        .&. (CodeFormat.ExtractCode link = code |@ $"{link} gave {CodeFormat.ExtractCode link}"))
    |> check

/// Any text, with pieces of codes and links in it
let private pastedTexts =
    Gen.oneof
        [ ArbMap.defaults |> ArbMap.generate<string> |> Gen.map (fun s -> if isNull s then "" else s)
          text [ "CBC"; "2"; ":"; "abc"; "#"; "code="; "%"; "3A"; "C3"; "A6"; "E0"; " "; "<"; ">"; "x"; "æ"; "\ud83d"; "\ude00"; "." ] 16 ]

[<Fact>]
let ``Finding the code never throws, and looking again finds the same`` () =
    Prop.forAll (Arb.fromGen pastedTexts) (fun text ->
        let found = CodeFormat.ExtractCode text
        CodeFormat.ExtractCode found = found |@ $"{text} gave {found}, then {CodeFormat.ExtractCode found}")
    |> check
