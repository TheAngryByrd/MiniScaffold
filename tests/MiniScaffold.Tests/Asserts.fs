namespace MiniScaffold.Tests

open System.IO
open Expecto
open Infrastructure
open Fake.IO.FileSystemOperators

module Array =
    let insert v i (l: 'a array) =
        let newArray =
            Array.zeroCreate (
                l.Length
                + 1
            )

        let mutable newPointer = 0

        l
        |> Array.iteri (fun j y ->
            if i = j then
                newArray.[newPointer] <- v

                newPointer <-
                    newPointer
                    + 1

                newArray.[newPointer] <- y

                newPointer <-
                    newPointer
                    + 1
            else
                newArray.[newPointer] <- y

                newPointer <-
                    newPointer
                    + 1
        )

        newArray

module Assert =
    open System

    let private failIfNoneWithMsg msg opt =
        match opt with
        | Some _ -> ()
        | None -> failtest msg

    let private tryFindFile file (d: DirectoryInfo) =
        let filepath = Path.Combine(d.FullName, file)

        if
            filepath
            |> File.Exists
            |> not
        then
            let message =
                sprintf "Could not find %s, all files currently in folder are:" filepath

            let message =
                (message, d.EnumerateFiles())
                ||> Seq.fold (fun state next ->
                    sprintf "%s%s%s" state Environment.NewLine next.FullName
                )

            failtest message

    let ``project can build target`` target (d: DirectoryInfo) =
        Builds.executeBuild d.FullName target

    let ``build target with failure expected`` target (d: DirectoryInfo) =
        let failed =
            try
                Builds.executeBuild d.FullName target
                false
            with _ ->
                true

        Expect.isTrue
            failed
            (sprintf "Building target %s succeeded when it should have failed" target)

    let ``CHANGELOG exists`` = tryFindFile "CHANGELOG.md"

    let ``CHANGELOG contains Unreleased section`` (d: DirectoryInfo) =
        let changelogContents =
            Path.Combine(d.FullName, "CHANGELOG.md")
            |> File.ReadAllLines

        Expect.contains
            changelogContents
            "## [Unreleased]"
            "Changelog should contain Unreleased section"

    let ``CHANGELOG does not contain Unreleased section`` (d: DirectoryInfo) =
        let changelogContents =
            Path.Combine(d.FullName, "CHANGELOG.md")
            |> File.ReadAllLines

        Expect.isFalse
            (changelogContents
             |> Array.contains "## [Unreleased]")
            "Changelog should not contain Unreleased section"

    let ``.config/dotnet-tools.json exists`` = tryFindFile ".config/dotnet-tools.json"

    let ``.github ISSUE_TEMPLATE bug_report exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE/bug_report.md"

    let ``.github ISSUE_TEMPLATE feature_request exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE/feature_request.md"

    let ``.github workflows build exists`` = tryFindFile ".github/workflows/build.yml"

    let ``.github ISSUE_TEMPLATE exists`` = tryFindFile ".github/ISSUE_TEMPLATE.md"

    let ``.github PULL_REQUEST_TEMPLATE exists`` =
        tryFindFile ".github/PULL_REQUEST_TEMPLATE.md"

    let ``.editorconfig exists`` = tryFindFile ".editorconfig"

    let ``.gitattributes exists`` = tryFindFile ".gitattributes"

    let ``.gitignore exists`` = tryFindFile ".gitignore"

    let ``LICENSE exists`` = tryFindFile "LICENSE.md"

    let ``NuGet.config exists`` = tryFindFile "NuGet.config"

    let ``Directory.Packages.props exists`` = tryFindFile "Directory.Packages.props"

    let ``README exists`` = tryFindFile "README.md"

    let ``File exists`` path = tryFindFile path

module Effect =
    open System
    open Fake.IO
    open Fake.Tools
    open Fake.Core
    open Fake.DotNet
    open Infrastructure.Dotnet

    let ``dotnet run`` argStr projectPath (d: DirectoryInfo) =

        DotNet.exec
            (fun opt -> {
                opt with
                    WorkingDirectory = Path.Join(d.FullName, projectPath)
            })
            "run"
            argStr
        |> failOnBadExitAndPrint

    let ``dotnet new`` argsStr (path: string) (d: DirectoryInfo) =

        DotNet.exec
            (fun opt -> {
                opt with
                    WorkingDirectory = Path.Join(d.FullName, path)
            })
            "new"
            argsStr
        |> failOnBadExitAndPrint

    let ``dotnet add reference`` args path (d: DirectoryInfo) =
        let args =
            Arguments.Empty
            |> Arguments.appendNotEmpty "reference" args

        DotNet.exec
            (fun opt -> {
                opt with
                    WorkingDirectory = Path.Join(d.FullName, path)
            })
            "add"
            args.ToStartInfo
        |> failOnBadExitAndPrint

    let ``dotnet sln add`` argsStr (d: DirectoryInfo) =
        let args =
            Arguments.Empty
            |> Arguments.appendNotEmpty "add" argsStr

        DotNet.exec
            (fun opt -> {
                opt with
                    WorkingDirectory = d.FullName
            })
            "sln"
            args.ToStartInfo
        |> failOnBadExitAndPrint

    let ``format build`` (d: DirectoryInfo) = Dotnet.fantomas d.FullName "build"


    let ``replace section with`` startIdx endIdx replace (data: string array) =
        printfn "replacing lines %i to %i with %s" startIdx endIdx replace

        for i = startIdx to endIdx do
            data.[i] <- sprintf "// %s" data.[i]

        Array.insert replace (endIdx + 1) data

    let ``find section`` startPredicate endPredicate data =
        let datai =
            data
            |> Array.indexed

        match
            datai
            |> Array.tryFind (startPredicate)
        with
        | None -> None
        | Some(startIdx, _) ->
            let (endIdx, _) =
                datai
                |> Array.skip (
                    startIdx
                    + 1
                )
                |> Array.takeWhile (endPredicate)
                |> Array.last

            Some(startIdx, endIdx)

    let ``get build.fs`` (d: DirectoryInfo) =
        d.FullName
        </> "build"
        </> "build.fs"


    let ``transform build script with`` (replaceFn: string -> string) (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d

        buildScript
        |> File.applyReplace replaceFn

    let ``disable restore`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let dotnetRestore")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable build`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let dotnetBuild")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable fsharpAnalyzers`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let fsharpAnalyzers")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d


    let ``disable tests`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let dotnetTest")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d


    let ``disable generateCoverage`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        let startPred =
            (fun (idx, line: string) -> line.Contains "let generateCoverageReport")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d


    let ``disable createPackages`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let createPackages")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d


    let ``disable dotnetPack`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let dotnetPack")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable sourceLinkTest function`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "let sourceLinkTest")
        with
        | None -> ()
        | Some startIdx ->
            let mutable i =
                startIdx
                + 1

            let mutable keepGoing = true

            while keepGoing
                  && i < Array.length lines do
                if String.IsNullOrWhiteSpace lines.[i] then
                    lines.[i] <- "    ()"
                    keepGoing <- false
                else
                    lines.[i] <-
                        "// "
                        + lines.[i]

                i <- i + 1

            lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable publishToNuget function`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "NuGet.NuGet.NuGetPublish(")
        with
        | None -> ()
        | Some startIdx ->
            let mutable i = startIdx
            let mutable keepGoing = true

            while keepGoing
                  && i < Array.length lines do
                if lines.[i].Trim() = "})" then
                    keepGoing <- false
                // Then fall through to comment out this line, too
                lines.[i] <-
                    "// "
                    + lines.[i]

                i <- i + 1

            lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable pushing in gitRelease function`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "let gitRelease")
        with
        | None -> ()
        | Some startIdx ->
            let mutable i =
                startIdx
                + 1

            let mutable keepGoing = true

            while keepGoing
                  && i < Array.length lines do
                if lines.[i].StartsWith("let githubRelease") then
                    keepGoing <- false
                else if lines.[i].Trim().StartsWith("Git.Branches.push") then
                    lines.[i] <-
                        "// "
                        + lines.[i]

                i <- i + 1

            lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``change githubRelease function to only run release checks`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        let githubReleaseStartIndexOpt =
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "let githubRelease")

        let githubReleaseEndIndexOpt =
            githubReleaseStartIndexOpt
            |> Option.bind (fun startIndex ->
                lines
                |> Array.skip startIndex
                |> Array.tryFindIndex (fun line -> line.Contains "|> Async.RunSynchronously")
                |> Option.map (fun endIndex ->
                    endIndex
                    + startIndex
                )
            )

        match (githubReleaseStartIndexOpt, githubReleaseEndIndexOpt) with
        | Some startIdx, Some endIdx ->
            lines
            |> ``replace section with`` startIdx endIdx "let githubRelease _ = allReleaseChecks ()"
            |> File.writeNew buildScript

            ``format build`` d
        | _ -> failwith "couldn't find bounds of `let githubRelease` function in build.fs"

    let ``git init`` (d: DirectoryInfo) (branchName: string) =
        Git.CommandHelper.runGitCommand d.FullName $"init --initial-branch={branchName}"
        |> ignore

        Git.CommandHelper.runGitCommand d.FullName "config --local user.email nobody@example.org"
        |> ignore

        Git.CommandHelper.runGitCommand d.FullName "config --local user.name TestUser"
        |> ignore

    let ``git commit all`` message (d: DirectoryInfo) =
        Git.Staging.stageAll d.FullName
        Git.Commit.exec d.FullName message

    let ``make build function fail`` (failureFunction: string) (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        let idx =
            lines
            |> Array.findIndex (fun line -> line.Contains failureFunction)

        let msg =
            sprintf "    failwith \"Deliberate failure in unit test for %s\"\n" failureFunction

        Array.insert msg (idx + 1) lines
        // |> fun lines -> lines |> Seq.iter(printfn "%s") ; lines
        |> File.writeNew buildScript

        ``format build`` d

    let ``make failure in gitRelease function`` (failureCode: string) (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "let gitRelease")
        with
        | None -> ()
        | Some startIdx ->
            let mutable i =
                startIdx
                + 1

            let mutable keepGoing = true

            while keepGoing
                  && i < Array.length lines do
                if lines.[i].StartsWith("let githubRelease") then
                    keepGoing <- false
                elif lines.[i].Contains(failureCode) then
                    let fail = "    failwith \"Deliberate failure in unit test for gitRelease\"\n"

                    lines.[i] <-
                        fail
                        + "// "
                        + lines.[i]

                i <- i + 1

            lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``set environment variable`` name value (d: DirectoryInfo) =
        Environment.SetEnvironmentVariable(name, value)

    let ``add change to CHANGELOG`` (d: DirectoryInfo) =
        let changelog = Path.combine d.FullName "CHANGELOG.md"
        let lines = File.ReadAllLines changelog

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "## [Unreleased]")
        with
        | None -> ()
        | Some startIdx ->
            let newLines =
                lines
                |> Array.insert
                    "### Changed"
                    (startIdx
                     + 1)
                |> Array.insert
                    "- This is a test change from (@TheAngryByrd)"
                    (startIdx
                     + 2)

            newLines
            |> File.writeNew changelog

    let private ``internal setup for all tests`` (branchName: string) (d: DirectoryInfo) =
        ``git init`` d branchName
        ``disable restore`` d
        ``disable build`` d
        ``disable fsharpAnalyzers`` d
        ``disable tests`` d
        ``disable generateCoverage`` d
        ``disable createPackages`` d
        ``disable dotnetPack`` d
        ``git commit all`` "Initial commit" d
        // SourceLinkTest requires an actual repo to exist, which we won't have in these tests
        ``disable sourceLinkTest function`` d
        // We don't want to actually release anything during integration tests!
        ``set environment variable`` "NUGET_KEY" "" d
        ``set environment variable`` "GITHUB_TOKEN" "" d
        ``disable pushing in gitRelease function`` d

    let private ``internal setup for release tests`` (branchName: string) (d: DirectoryInfo) =
        ``internal setup for all tests`` branchName d
        ``set environment variable`` "RELEASE_VERSION" "2.0.0" d
        ``add change to CHANGELOG`` d

    let ``setup for release tests`` (d: DirectoryInfo) =
        ``internal setup for release tests`` "main" d

    let ``setup for release branch tests`` (branchName: string) (d: DirectoryInfo) =
        ``internal setup for release tests`` branchName d

    let ``setup for publish tests`` (d: DirectoryInfo) =
        ``internal setup for all tests`` "main" d
        // To make publish work locally during integration tests
        ``set environment variable`` "CI" "1" d
