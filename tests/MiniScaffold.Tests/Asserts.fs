namespace MiniScaffold.Tests
open System.IO
open Expecto
open Infrastructure

module Assert =
    open System


    let private failIfNoneWithMsg msg opt =
        match opt with
        | Some _ -> ()
        | None -> failtest msg

    let private tryFindFile file (d : DirectoryInfo) =
        let filepath = Path.Combine(d.FullName, file)
        if  filepath |> File.Exists |> not then
            failtestf "Could not find %s" filepath

    let ``project can build target`` target (d : DirectoryInfo) =
        Builds.executeBuild d.FullName target

    let ``build target with failure expected`` target (d : DirectoryInfo) =
        let failed =
            try
                Builds.executeBuild d.FullName target
                false
            with _ ->
                true
        Expect.isTrue failed (sprintf "Building target %s succeeded when it should have failed" target)

    let ``CHANGELOG exists`` =
        tryFindFile "CHANGELOG.md"

    let ``CHANGELOG contains Unreleased section`` (d : DirectoryInfo) =
        let changelogContents = Path.Combine(d.FullName, "CHANGELOG.md") |> File.ReadAllLines
        Expect.contains changelogContents "## [Unreleased]" "Changelog should contain Unreleased section"

    let ``CHANGELOG does not contain Unreleased section`` (d : DirectoryInfo) =
        let changelogContents = Path.Combine(d.FullName, "CHANGELOG.md") |> File.ReadAllLines
        Expect.isFalse (changelogContents |> Array.contains "## [Unreleased]") "Changelog should not contain Unreleased section"

    let ``.config/dotnet-tools.json exists`` =
        tryFindFile ".config/dotnet-tools.json"

    let ``.github ISSUE_TEMPLATE bug_report exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE/bug_report.md"

    let ``.github ISSUE_TEMPLATE feature_request exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE/feature_request.md"

    let ``.github workflows build exists`` =
        tryFindFile ".github/workflows/build.yml"

    let ``.github ISSUE_TEMPLATE exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE.md"

    let ``.github PULL_REQUEST_TEMPLATE exists`` =
        tryFindFile ".github/PULL_REQUEST_TEMPLATE.md"

    let ``.editorconfig exists`` =
        tryFindFile ".editorconfig"

    let ``.gitattributes exists`` =
        tryFindFile ".gitattributes"

    let ``.gitignore exists`` =
        tryFindFile ".gitignore"

    let ``LICENSE exists`` =
        tryFindFile "LICENSE.md"

    let ``paket.lock exists`` =
        tryFindFile "paket.lock"

    let ``paket.dependencies exists`` =
        tryFindFile "paket.dependencies"

    let ``README exists`` =
        tryFindFile "README.md"


module Effect =
    open System
    open Fake.IO
    open Fake.Tools

    let ``transform build script with`` (replaceFn : string -> string) (d : DirectoryInfo) =
        let buildScript = Path.combine d.FullName "build.fsx"
        buildScript |> File.applyReplace replaceFn

    let ``disable sourceLinkTest function`` (d : DirectoryInfo) =
        let buildScript = Path.combine d.FullName "build.fsx"
        let lines = File.ReadAllLines buildScript
        match lines |> Array.tryFindIndex (fun line -> line.Contains "let sourceLinkTest") with
        | None -> ()
        | Some startIdx ->
            let mutable i = startIdx+1
            let mutable keepGoing = true
            while keepGoing && i < Array.length lines do
                if String.IsNullOrWhiteSpace lines.[i] then
                    lines.[i] <- "    ()"
                    keepGoing <- false
                else
                    lines.[i] <- "// " + lines.[i]
                i <- i + 1
            lines |> File.writeNew buildScript

    let ``disable publishToNuget function`` (d : DirectoryInfo) =
        let buildScript = Path.combine d.FullName "build.fsx"
        let lines = File.ReadAllLines buildScript
        match lines |> Array.tryFindIndex (fun line -> line.Contains "Paket.push(") with
        | None -> ()
        | Some startIdx ->
            let mutable i = startIdx
            let mutable keepGoing = true
            while keepGoing && i < Array.length lines do
                if lines.[i].Trim() = ")" then
                    keepGoing <- false
                    // Then fall through to comment out this line, too
                lines.[i] <- "// " + lines.[i]
                i <- i + 1
            lines |> File.writeNew buildScript

    let ``disable pushing in gitRelease function`` (d : DirectoryInfo) =
        let buildScript = Path.combine d.FullName "build.fsx"
        let lines = File.ReadAllLines buildScript
        match lines |> Array.tryFindIndex (fun line -> line.Contains "let gitRelease") with
        | None -> ()
        | Some startIdx ->
            let mutable i = startIdx+1
            let mutable keepGoing = true
            while keepGoing && i < Array.length lines do
                if lines.[i].StartsWith("let githubRelease") then
                    keepGoing <- false
                else
                    if lines.[i].Trim().StartsWith("Git.Branches.push") then
                        lines.[i] <- "// " + lines.[i]
                i <- i + 1
            lines |> File.writeNew buildScript

    let ``git init`` (d : DirectoryInfo) =
        Git.CommandHelper.runGitCommand d.FullName "init" |> ignore

    let ``git commit all`` message (d : DirectoryInfo) =
        Git.Staging.stageAll d.FullName
        Git.Commit.exec d.FullName message

    let ``make build function fail`` (failureFunction : string) (d : DirectoryInfo) =
        let buildScript = Path.combine d.FullName "build.fsx"
        buildScript |> File.applyReplace (fun text ->
            text.Replace(sprintf "let %s _ =\n" failureFunction,
                         sprintf "let %s _ =\n    failwith \"Deliberate failure in unit test\"\n" failureFunction)
                .Replace(sprintf "let %s ctx =\n" failureFunction,
                         sprintf "let %s ctx =\n    failwith \"Deliberate failure in unit test\"\n" failureFunction)
        )

    let ``set environment variable`` name value (d : DirectoryInfo) =
        Environment.SetEnvironmentVariable(name, value)

    let ``setup for release tests`` (d : DirectoryInfo) =
        ``git init`` d
        ``git commit all`` "Initial commit" d
        ``set environment variable`` "RELEASE_VERSION" "2.0.0" d
        // SourceLinkTest requires an actual repo to exist, which we won't have in these tests
        ``disable sourceLinkTest function`` d
        // We don't want to actually release anything during integration tests!
        ``set environment variable`` "NUGET_KEY" "" d
        ``set environment variable`` "GITHUB_TOKEN" "" d
