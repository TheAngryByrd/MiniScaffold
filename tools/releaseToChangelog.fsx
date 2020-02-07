#r "paket:
nuget Fake.IO.FileSystem
nuget Fake.Core.ReleaseNotes
nuget Fake.Core.Target //"
#load "./.fake/releaseToChangelog.fsx/intellisense.fsx"

open System
open Fake.Core
open Fake.IO
open System.Text.RegularExpressions

let allReleaseNotes =
    System.IO.File.ReadLines "RELEASE_NOTES.md"
    |> ReleaseNotes.parseAll

let notesTitleRegexp =
    Regex("[a-zA-Z]+?:", RegexOptions.Multiline)

// Use this to figure out what your categories are
// Assumes `TITLE: Some notes`
let discoverTitleGroups (releaseNotes : list<ReleaseNotes.ReleaseNotes>) =
    let titles =
        releaseNotes
        |> Seq.collect(fun rn -> rn.Notes)
        |> Seq.map(fun n -> (notesTitleRegexp.Match n).Value)
        |> Seq.distinct
    titles |> Seq.iter(printfn "%A")

let convertTileToCategory title note =
    let newTitle =
        // https://github.com/fsharp/FAKE/blob/master/src/app/Fake.Core.ReleaseNotes/Changelog.fs#L82
        match title with
        | "FEATURE:" -> "added"
        | "BUGFIX:" -> "fixed"
        | "MINOR:" | "MAINTENANCE:" -> "changed"
        | s -> s
    Changelog.Change.New(newTitle, note)

let notesToChanges (releaseNote : ReleaseNotes.ReleaseNotes) =
    releaseNote.Notes
    |> Seq.map(fun n ->
        let title = (notesTitleRegexp.Match n).Value
        let remainingNote = n.Substring(title.Length).Trim() |> sprintf "-  %s"
        convertTileToCategory title remainingNote
    )
    |> Seq.toList

let longstr (s: string) = System.Text.RegularExpressions.Regex.Replace(s, "\s+", " ")

let changeLogDescription =
        """
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
        """

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"
let gitHubRepoUrl = sprintf "https://github.com/%s/%s" gitOwner gitRepoName

// let tagFromVersionNumber versionNumber = sprintf "v%s" versionNumber

let createLinkReferences (entries : Changelog.ChangelogEntry list) =
    let init = []
    let rec step state (entries : Changelog.ChangelogEntry list) =
        match entries with
        | x::y::tail ->
            let link = sprintf "%s/compare/%s...%s" gitHubRepoUrl (y.SemVer.AsString) (x.SemVer.AsString)
            let reference = sprintf "[%s]: %s" x.SemVer.AsString link
            // printfn "%A -> %A" x.NuGetVersion y.NuGetVersion
            step (reference :: state) (y :: tail)
        | [x] ->
            let link = sprintf "%s/releases/tag/%s" gitHubRepoUrl (x.SemVer.AsString)
            let reference = sprintf "[%s]: %s" x.SemVer.AsString link
            // printfn "%A" x.NuGetVersion
            reference :: state
        | [] ->
            state
    step init entries
    |> List.rev

let changelogFilename = "CHANGELOG.md"

let convert _ =
    // discoverTitleGroups allReleaseNotes
    let changeLogEntries =
        allReleaseNotes
        |> List.map(fun rn ->
            let changes = notesToChanges rn
            Changelog.ChangelogEntry.New(rn.AssemblyVersion, rn.NugetVersion, rn.Date, None, changes, false)
        )
    let links = [Environment.NewLine] @ createLinkReferences changeLogEntries
    // links |> Seq.iter(printfn "%A")
    Changelog.Changelog.New("Changelog",Some changeLogDescription ,None,changeLogEntries )
    |> Changelog.save changelogFilename
    File.write true changelogFilename links

Target.create "Convert" convert

Target.runOrDefaultWithArguments "Convert"
