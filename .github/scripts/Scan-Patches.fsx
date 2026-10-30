// Static overlap scan for the compatibility tests (issue #24): reads the Harmony patches of this mod and of other mods'
// DLLs without loading them, and lists the methods both patch. Nothing in the DLLs is run.
//
//   dotnet fsi .github/scripts/Scan-Patches.fsx <CustomBossChallenge.dll> <folder with other mods> [<out.md>]
//
// It finds [HarmonyPatch(typeof(T), "Method")] classes, and manual harmony.Patch calls where the target is written as
// AccessTools.Method(typeof(T), "Method") or typeof(T).GetMethod("Method"). Patches set up in other ways aren't seen,
// so an empty row only means "nothing found", not "no overlap".

#r "nuget: Mono.Cecil, 0.11.6"

open System
open System.IO
open Mono.Cecil
open Mono.Cecil.Cil

type Patch = { Target: string; Kind: string; Priority: string; Source: string }

type ModInfo = { File: string; Name: string; Version: string; WorksOn: string; Dependencies: string; Patches: Patch list }

let ourGuards =
    set [ "Btd6Player.SetBossChallengeRecord"; "BossDefeatScreen.GetRetryMmCost"; "CheckpointDefeatScreen.GetContinueMmCost"
          "InGameData.SetupBossChallenge"; "ChallengeEditor.OverrideClose" ]

let shortType (t: TypeReference) = t.Name

let constant (field: FieldDefinition) =
    if field.HasConstant && not (isNull field.Constant) then string field.Constant else ""

let modData (md: ModuleDefinition) =
    md.Types
    |> Seq.tryFind (fun t -> t.Name = "ModHelperData")
    |> Option.map (fun t ->
        let get name = t.Fields |> Seq.tryFind (fun f -> f.Name = name) |> Option.map constant |> Option.defaultValue ""
        get "Name", get "Version", get "WorksOnVersion", get "Dependencies")
    |> Option.defaultValue ("", "", "", "")

let attributesNamed (name: string) (attrs: seq<CustomAttribute>) =
    attrs |> Seq.filter (fun a -> a.AttributeType.Name = name)

/// Type and method name from one [HarmonyPatch] attribute; either may be missing
let patchArgs (attr: CustomAttribute) =
    let args = attr.ConstructorArguments |> Seq.toList
    let typ = args |> List.tryPick (fun a -> match a.Value with :? TypeReference as t -> Some (shortType t) | _ -> None)
    let name = args |> List.tryPick (fun a -> match a.Value with :? string as s -> Some s | _ -> None)
    typ, name

let kindOf (m: MethodDefinition) =
    let has n = m.CustomAttributes |> Seq.exists (fun a -> a.AttributeType.Name = n)
    if has "HarmonyPrefix" || m.Name = "Prefix" then Some "prefix"
    elif has "HarmonyPostfix" || m.Name = "Postfix" then Some "postfix"
    elif has "HarmonyTranspiler" || m.Name = "Transpiler" then Some "transpiler"
    elif has "HarmonyFinalizer" || m.Name = "Finalizer" then Some "finalizer"
    else None

let priorityOf (attrs: seq<CustomAttribute>) =
    attributesNamed "HarmonyPriority" attrs
    |> Seq.tryHead
    |> Option.map (fun a -> string a.ConstructorArguments.[0].Value)
    |> Option.defaultValue ""

let rec allTypes (t: TypeDefinition) = seq { yield t; for n in t.NestedTypes do yield! allTypes n }

let attributePatches (md: ModuleDefinition) =
    [ for t in md.Types |> Seq.collect allTypes do
          let classAttrs = attributesNamed "HarmonyPatch" t.CustomAttributes |> Seq.map patchArgs |> Seq.toList
          if not classAttrs.IsEmpty then
              let classType = classAttrs |> List.tryPick fst
              let className = classAttrs |> List.tryPick snd
              let targetMethods = t.Methods |> Seq.exists (fun m -> m.CustomAttributes |> Seq.exists (fun a -> a.AttributeType.Name = "HarmonyTargetMethods"))
              for m in t.Methods do
                  match kindOf m with
                  | Some kind ->
                      let methodAttrs = attributesNamed "HarmonyPatch" m.CustomAttributes |> Seq.map patchArgs |> Seq.toList
                      let typ = methodAttrs |> List.tryPick fst |> Option.orElse classType
                      let name = methodAttrs |> List.tryPick snd |> Option.orElse className
                      let target =
                          match typ, name with
                          | Some ty, Some n -> sprintf "%s.%s" ty n
                          | Some ty, None when targetMethods -> sprintf "%s.(TargetMethods)" ty
                          | Some ty, None -> sprintf "%s.(constructor or unnamed)" ty
                          | _ -> sprintf "(TargetMethods in %s)" t.Name
                      let priority = priorityOf (Seq.append m.CustomAttributes t.CustomAttributes)
                      yield { Target = target; Kind = kind; Priority = priority; Source = t.FullName }
                  | None -> () ]

/// Manual patches: ldtoken T, ldstr "Method", then a call to AccessTools.Method or Type.GetMethod
let manualPatches (md: ModuleDefinition) =
    [ for t in md.Types |> Seq.collect allTypes do
          for m in t.Methods do
              if m.HasBody then
                  let code = m.Body.Instructions |> Seq.toArray
                  for i in 0 .. code.Length - 1 do
                      match code.[i].Operand with
                      | :? MethodReference as call when
                            (call.DeclaringType.Name = "AccessTools" && call.Name = "Method")
                            || (call.DeclaringType.Name = "Type" && call.Name = "GetMethod") ->
                          let back = code.[max 0 (i - 6) .. i - 1]
                          let typ = back |> Array.tryPick (fun ins -> match ins.Operand with :? TypeReference as tr when ins.OpCode = OpCodes.Ldtoken -> Some (shortType tr) | _ -> None)
                          let name = back |> Array.tryPick (fun ins -> if ins.OpCode = OpCodes.Ldstr then Some (string ins.Operand) else None)
                          match typ, name with
                          | Some ty, Some n -> yield { Target = sprintf "%s.%s" ty n; Kind = "manual"; Priority = ""; Source = t.FullName }
                          | _ -> ()
                      | _ -> () ]

/// Folders with Harmony, MelonLoader and the game's assemblies, needed to read some attributes. Found next to our DLL
/// when it is the one in the game's Mods folder.
let mutable referenceFolders: string list = []

let read (path: string) =
    let resolver = new DefaultAssemblyResolver()
    resolver.AddSearchDirectory(Path.GetDirectoryName path)
    for folder in referenceFolders do resolver.AddSearchDirectory folder
    use asm = AssemblyDefinition.ReadAssembly(path, ReaderParameters(AssemblyResolver = resolver, ReadingMode = ReadingMode.Deferred))
    let md = asm.MainModule
    let name, version, worksOn, deps = modData md
    { File = Path.GetFileName path
      Name = (if name = "" then asm.Name.Name else name)
      Version = version
      WorksOn = worksOn
      Dependencies = deps
      Patches = attributePatches md @ manualPatches md |> List.distinct }

let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length < 2 then
    eprintfn "usage: dotnet fsi Scan-Patches.fsx <CustomBossChallenge.dll> <mods folder> [<out.md>]"
    exit 2

let melonLoader = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath args.[0])), "MelonLoader")
if Directory.Exists melonLoader then
    referenceFolders <- Path.GetDirectoryName(Path.GetFullPath args.[0]) :: (Directory.GetDirectories(melonLoader, "*", SearchOption.AllDirectories) |> Array.toList)

let ours = read args.[0]
let ourTargets = ours.Patches |> List.groupBy (fun p -> p.Target) |> Map.ofList
let others =
    Directory.GetFiles(args.[1], "*.dll", SearchOption.AllDirectories)
    |> Array.filter (fun f -> Path.GetFileName f <> ours.File)
    |> Array.choose (fun f ->
        try Some (read f)
        with e -> eprintfn "%s: can't read (%s)" f e.Message; None)
    |> Array.sortBy (fun m -> m.Name.ToLowerInvariant())

let describe (ps: Patch list) =
    ps |> List.map (fun p -> if p.Priority = "" then p.Kind else sprintf "%s (priority %s)" p.Kind p.Priority) |> List.distinct |> String.concat ", "

let out = Text.StringBuilder()
let line (s: string) = out.AppendLine s |> ignore
line "# Patch overlap scan"
line ""
line (sprintf "Our DLL: %s %s, %d patches on %d methods." ours.Name ours.Version ours.Patches.Length ourTargets.Count)
line ""
line "| Mod | Version | Works on | Depends on | Patches | Methods we both patch |"
line "|---|---|---|---|---|---|"
for m in others do
    let shared =
        m.Patches
        |> List.groupBy (fun p -> p.Target)
        |> List.filter (fun (t, _) -> ourTargets.ContainsKey t)
        |> List.map (fun (t, theirs) ->
            let guard = if ourGuards.Contains t then " **guard**" else ""
            sprintf "`%s`%s: theirs %s / ours %s" t guard (describe theirs) (describe ourTargets.[t]))
    let sharedText = if shared.IsEmpty then "none found" else String.concat "<br>" shared
    line (sprintf "| %s | %s | %s | %s | %d | %s |" m.Name m.Version m.WorksOn m.Dependencies m.Patches.Length sharedText)

line ""
line "## Same types, other methods"
line ""
line "Patches on a type we also patch, but on another method. Not a conflict by itself, but where to look if one shows up."
line ""
let ourTypes = ourTargets |> Map.toSeq |> Seq.map (fun (t, _) -> t.Split('.').[0]) |> set
for m in others do
    let near =
        m.Patches
        |> List.filter (fun p -> ourTypes.Contains (p.Target.Split('.').[0]) && not (ourTargets.ContainsKey p.Target))
        |> List.map (fun p -> sprintf "`%s` (%s)" p.Target p.Kind)
        |> List.distinct
    if not near.IsEmpty then line (sprintf "- **%s**: %s" m.Name (String.concat ", " near))

let text = out.ToString()
if args.Length > 2 then File.WriteAllText(args.[2], text)
printf "%s" text
