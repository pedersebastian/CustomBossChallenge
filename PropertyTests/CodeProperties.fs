/// What must hold for every setup and every code, checked on thousands of random ones. A failing case is shrunk to the
/// smallest one that still fails; add it to the fixed tests (Tests/SetupCodeTests.cs or WebDataTest's fixtures).
module CustomBossChallenge.PropertyTests.CodeProperties

open System.Text
open FsCheck
open FsCheck.FSharp
open Newtonsoft.Json.Linq
open Xunit
open CustomBossChallenge.Config
open CustomBossChallenge.Persistence
open CustomBossChallenge.PropertyTests.Generators

/// A condition with what to show when it fails
let private (|@) (condition: bool) (label: string) = Prop.label label condition

let private check (property: Property) =
    Check.One(Config.QuickThrowOnFailure.WithMaxTest(2000), property)

[<Fact>]
let ``The generators only make setups the Boss Creator can make`` () =
    Prop.forAll (Arb.fromGen options) (fun o ->
        let int (name: string) = o.Value<int>(name)
        let skulls = o.Value<string>("CustomSkulls")
        TierMath.ClampTiers(int "Tiers") = int "Tiers"
        && TierMath.ClampSpeedPerTier(int "SpeedPerTier") = int "SpeedPerTier"
        && TierMath.ClampHealthPerTier(int "HealthPerTier") = int "HealthPerTier"
        && System.Math.Clamp(int "SkullCount", 0, SkullMath.MaxSkulls) = int "SkullCount"
        && SkullMath.NormalizeCustom(skulls, "fallback") = skulls)
    |> check

[<Fact>]
let ``A code reads back as the setup it was made from`` () =
    Prop.forAll setups (fun setup ->
        match decode (encode setup) with
        | Some read ->
            (JToken.DeepEquals(read.Options, setup.Options) |@ "options differ")
            .&. (JToken.DeepEquals(read.Challenge, setup.Challenge) |@ "challenge differs")
        | None -> false |@ "the code didn't load")
    |> check

[<Fact>]
let ``Encoding a code's setup again gives the same code`` () =
    Prop.forAll setups (fun setup ->
        let code = encode setup
        match decode code with
        | Some read -> encode read = code |@ $"gave {encode read}"
        | None -> false |@ "the code didn't load")
    |> check

[<Fact>]
let ``Codes use version 2 exactly when they need it, and only characters chats leave alone`` () =
    Prop.forAll setups (fun setup ->
        let code = encode setup
        let perTier = setup.Options.Value<int>("SpeedPerTier") <> 0 || setup.Options.Value<int>("HealthPerTier") <> 0
        (code.StartsWith(CodeFormat.Version2Prefix) = perTier |@ "version")
        .&. (code.Length <= CodeFormat.MaxLength |@ "too long to be read")
        .&. (code[CodeFormat.PrefixLength ..] |> Seq.forall (fun c -> System.Char.IsAsciiLetterOrDigit c || c = '-' || c = '_')
             |@ code))
    |> check

[<Fact>]
let ``Broken codes are refused without throwing`` () =
    Prop.forAll (Arb.fromGen junk) (fun code ->
        let loaded, payload, problem, _ = CodeFormat.TryUnpack(code)
        let trimmed = code.Trim()
        let known = trimmed.StartsWith(CodeFormat.Prefix) || trimmed.StartsWith(CodeFormat.Version2Prefix)
        let tooNew = not known && trimmed.Length > 4 && trimmed.StartsWith("CBC") && System.Char.IsDigit trimmed[3]
        if loaded then
            // A damaged code can still be a readable one; then it is a JSON object, like every code
            (problem = CodeProblem.None && known && not (isNull payload)) |@ $"loaded {code}"
        else
            (problem = (if tooNew then CodeProblem.TooNew else CodeProblem.Invalid)) |@ $"{problem} for {code}")
    |> check

[<Fact>]
let ``A small code that unpacks to too much is refused`` () =
    let huge = StringBuilder("{\"o\":{\"SpawnRounds\":\"").Append('1', CodeFormat.MaxJsonLength).Append("\"}}").ToString()
    let code = CodeFormat.Pack(huge)
    Assert.True(code.Length <= CodeFormat.MaxLength, $"The test code must be short enough to be read: {code.Length}")
    let loaded, _, problem, error = CodeFormat.TryUnpack(code)
    Assert.False(loaded)
    Assert.Equal(CodeProblem.Invalid, problem)
    Assert.NotNull(error)

[<Fact>]
let ``Null, empty and blank codes are refused`` () =
    for code in [ null; ""; "   "; "\n" ] do
        let loaded, _, problem, _ = CodeFormat.TryUnpack(code)
        Assert.False(loaded)
        Assert.Equal(CodeProblem.Invalid, problem)
