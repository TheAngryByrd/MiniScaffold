#r "nuget: Fake.Core.ReleaseNotes, 6.2.0"

open System
open System.IO
open System.Text.RegularExpressions
open Fake.Core

let usage =
    sprintf
        "Usage: dotnet fsi %s <github-owner> <github-repository> <release-notes-path>"
        fsi.CommandLineArgs[0]

let fail (message: string) =
    eprintfn "%s" message
    exit 1

let owner, repository, releaseNotesPath =
    match
        fsi.CommandLineArgs
        |> Array.tail
    with
    | [| owner; repository; path |] -> owner, repository, Path.GetFullPath path
    | _ -> fail usage

if not (File.Exists releaseNotesPath) then
    fail (sprintf "Release notes file not found: %s\n%s" releaseNotesPath usage)

let changelogPath =
    Path.Combine(Path.GetDirectoryName releaseNotesPath, "CHANGELOG.md")

if File.Exists changelogPath then
    fail (sprintf "%s already exists. Move or delete it, then run the script again." changelogPath)

let gitHubRepoUrl = sprintf "https://github.com/%s/%s" owner repository

let tagFromVersion (version: SemVerInfo) = sprintf "v%s" version.AsString

let dashBullet = Regex(@"^-\s+")

let titledNote = Regex(@"^(?<title>[A-Za-z]+):\s+(?<text>.*)$")

let changeText text : Changelog.ChangeText = {
    CleanedText = text
    OriginalText = None
}

let toChange (note: string) =
    let note = dashBullet.Replace(note, "")
    let titled = titledNote.Match note

    if titled.Success then
        let text = changeText titled.Groups["text"].Value

        match titled.Groups["title"].Value with
        | "FEATURE" -> Changelog.Change.Added text
        | "BUGFIX" -> Changelog.Change.Fixed text
        | "MINOR"
        | "MAINTENANCE" -> Changelog.Change.Changed text
        | title -> Changelog.Change.Custom(title, text)
    else
        Changelog.Change.Changed(changeText note)

let toEntry (releaseNotes: ReleaseNotes.ReleaseNotes) =
    Changelog.ChangelogEntry.New(
        releaseNotes.AssemblyVersion,
        releaseNotes.NugetVersion,
        releaseNotes.Date,
        None,
        List.map toChange releaseNotes.Notes,
        false
    )

let reference version (url: string) : Changelog.Reference = { SemVer = version; RepoUrl = Uri url }

let noVersions = sprintf "No versions found in %s" releaseNotesPath

let releaseNotesLines = File.ReadAllLines releaseNotesPath

let parsedReleaseNotes =
    try
        ReleaseNotes.parseAll releaseNotesLines
    with ex ->
        fail (sprintf "%s: %s" noVersions ex.Message)

let entries = List.map toEntry parsedReleaseNotes

let latest, oldest =
    match entries with
    | [] -> fail noVersions
    | latest :: _ -> latest, List.last entries

let linkReferences = [
    reference
        Changelog.UnreleasedRef
        (sprintf "%s/compare/%s...HEAD" gitHubRepoUrl (tagFromVersion latest.SemVer))
    for newer, older in List.pairwise entries do
        reference
            (Changelog.SemVerRef newer.SemVer)
            (sprintf
                "%s/compare/%s...%s"
                gitHubRepoUrl
                (tagFromVersion older.SemVer)
                (tagFromVersion newer.SemVer))
    reference
        (Changelog.SemVerRef oldest.SemVer)
        (sprintf "%s/releases/tag/%s" gitHubRepoUrl (tagFromVersion oldest.SemVer))
]

let description =
    """All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)."""

// An empty description makes FAKE render the Unreleased heading, which the release target fills.
Changelog.Changelog.New(
    "Changelog",
    Some description,
    Changelog.Unreleased.New(Some "", []),
    entries,
    linkReferences
)
|> Changelog.save changelogPath

printfn "Wrote %s" changelogPath
