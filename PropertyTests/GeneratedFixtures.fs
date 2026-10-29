/// Random codes for the website (property 4): web/test/generated-codes.json holds codes made here from a fixed seed,
/// with the setup each one holds, and web/test/generated.test.ts checks that the website reads them the same way.
/// <br/>
/// With the environment variable CBC_GENERATED_CODES=1 this writes the file; otherwise it checks that the committed one
/// still matches what the generators and CodeFormat make now.
module CustomBossChallenge.PropertyTests.GeneratedFixtures

open System.IO
open FsCheck
open FsCheck.FSharp
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Xunit
open CustomBossChallenge.Persistence
open CustomBossChallenge.PropertyTests.Generators

let EnvironmentVariable = "CBC_GENERATED_CODES"
let private file = Path.Combine(Game.repo, "web", "test", "generated-codes.json")
let private seed = Rnd(2026UL)

let private challengeValues (challenge: JObject) =
    let values = JObject()
    for path in ChallengePaths do
        values[path] <- challenge.SelectToken(path).DeepClone()
    // Only the towers whose limit differs from the default, by index
    let defaults = Game.defaultChallenge().["towers"]
    let towers = JObject()
    challenge["towers"]
    |> Seq.iteri (fun i tower ->
        if not (JToken.DeepEquals(tower["max"], defaults.[i].["max"])) then towers[string i] <- tower["max"].DeepClone())
    values["towerMax"] <- towers
    values

let private make () =
    let valid = JArray()
    for setup in Gen.sampleWithSeed seed 50 200 Generators.setup do
        valid.Add(JObject(JProperty("code", encode setup), JProperty("options", setup.Options),
                          JProperty("challenge", challengeValues setup.Challenge)))

    let invalid = JArray()
    for code in Gen.sampleWithSeed seed 30 60 junk do
        // A damaged code that still unpacks is left out: whether it loads depends on more than CodeFormat
        match CodeFormat.TryUnpack(code) with
        | false, _, problem, _ -> invalid.Add(JObject(JProperty("code", code), JProperty("problem", string problem)))
        | _ -> ()

    JObject(JProperty("valid", valid), JProperty("invalid", invalid))

[<Fact>]
let ``The website's generated codes are up to date`` () =
    let current = make ()
    if System.Environment.GetEnvironmentVariable(EnvironmentVariable) = "1" then
        // One code per line keeps the file small and its diffs readable
        let list (name: string) =
            let lines = current[name] |> Seq.map (fun entry -> "    " + entry.ToString(Formatting.None))
            $"  \"{name}\": [\n" + String.concat ",\n" lines + "\n  ]"
        File.WriteAllText(file, "{\n" + list "valid" + ",\n" + list "invalid" + "\n}\n")
    else
        let fix = $"run the property tests with {EnvironmentVariable}=1 (see CONTRIBUTING.md)"
        Assert.True(File.Exists file, $"{file} is missing: {fix}")
        let committed = JObject.Parse(File.ReadAllText file)
        // Compressed bytes may differ between .NET versions, so the codes are compared by what they hold
        let withoutCodes (json: JObject) =
            let copy = json.DeepClone() :?> JObject
            for entry in copy["valid"] do (entry :?> JObject).Remove("code") |> ignore
            copy
        Assert.True(JToken.DeepEquals(withoutCodes committed, withoutCodes current), $"{file} is out of date: {fix}")
        for entry in committed["valid"] do
            match decode (entry.Value<string>("code")) with
            | Some setup ->
                Assert.True(JToken.DeepEquals(setup.Options, entry["options"])
                            && JToken.DeepEquals(challengeValues setup.Challenge, entry["challenge"]),
                            $"A code in {file} doesn't hold its setup: {fix}")
            | None -> Assert.Fail($"A code in {file} doesn't load: {fix}")
