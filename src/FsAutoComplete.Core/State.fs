﻿namespace FsAutoComplete

open System
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open System.Collections.Generic



type State =
  {
    Files : Map<string,VolatileFile>
    FilesId : Dictionary<string,Guid>
    FileCheckOptions : Map<string,FSharpProjectOptions>
    ProjectLoadTimes : Map<string,DateTime>
    HelpText : Map<String, FSharpToolTipText>
    ColorizationOutput: bool
  }

  static member Initial =
    { Files = Map.empty
      FilesId = Dictionary()
      FileCheckOptions = Map.empty
      ProjectLoadTimes = Map.empty
      HelpText = Map.empty
      ColorizationOutput = false }

  member x.WithFileTextGetCheckerOptions(file, lines) : State * FSharpProjectOptions =
    let file = Utils.normalizePath file

    let opts =
      match x.FileCheckOptions.TryFind file with
      | None -> State.FileWithoutProjectOptions(file)
      | Some opts -> opts
    let fileState = { Lines = lines; Touched = DateTime.Now }
    { x with Files = Map.add file fileState x.Files
             FileCheckOptions = Map.add file opts x.FileCheckOptions }, opts

  member x.WithFileTextAndCheckerOptions(file, lines, opts) =
    let file = Utils.normalizePath file
    let fileState = { Lines = lines; Touched = DateTime.Now }
    { x with Files = Map.add file fileState x.Files
             FileCheckOptions = Map.add file opts x.FileCheckOptions }

  static member private FileWithoutProjectOptions(file) =
    { ProjectFileName = file + ".fsproj"
      ProjectFileNames = [|file|]
      OtherOptions = [|"--noframework"|]
      ReferencedProjects = [| |]
      IsIncompleteTypeCheckEnvironment = true
      UseScriptResolutionRules = false
      LoadTime = DateTime.Now
      UnresolvedReferences = None }

  member x.TryGetFileCheckerOptionsWithLines(file) : Result<FSharpProjectOptions * string[]> =
    let file = Utils.normalizePath file
    match x.Files.TryFind(file) with
    | None -> Failure (sprintf "File '%s' not parsed" file)
    | Some (volFile) ->

      match x.FileCheckOptions.TryFind(file) with
      | None -> Success (State.FileWithoutProjectOptions(file), volFile.Lines)
      | Some opts -> Success (opts, volFile.Lines)

  member x.TryGetFileCheckerOptionsWithSource(file) : Result<FSharpProjectOptions * string> =
    let file = Utils.normalizePath file
    match x.TryGetFileCheckerOptionsWithLines(file) with
    | Failure x -> Failure x
    | Success (opts, lines) -> Success (opts, String.concat "\n" lines)

  member x.TryGetFileCheckerOptionsWithLinesAndLineStr(file, pos) : Result<FSharpProjectOptions * string[] * string> =
    let file = Utils.normalizePath file
    match x.TryGetFileCheckerOptionsWithLines(file) with
    | Failure x -> Failure x
    | Success (opts, lines) ->
      let ok = pos.Line <= lines.Length && pos.Line >= 1 &&
               pos.Col <= lines.[pos.Line - 1].Length + 1 && pos.Col >= 1
      if not ok then Failure "Position is out of range"
      else Success (opts, lines, lines.[pos.Line - 1])

  member x.TryGetFileId file =
    let file = Utils.normalizePath file
    x.FilesId |> Seq.tryFind (fun kv -> kv.Key = file)

  member x.SetFileId file id =
    let file = Utils.normalizePath file
    x.FilesId.[file] <- id
