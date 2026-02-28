namespace MiniScaffold.Tests


module Tests =
    open System
    open Fake.Core
    open Expecto
    open Infrastructure

    let ptestCase (reason: string) name test = Expecto.Tests.ptestCase name test

    let logger = Expecto.Logging.Log.create "setup"
    let nugetPkgName = "MiniScaffold"
    let templateName = "mini-scaffold"

    let nugetPkgPath =
        match Environment.environVarOrNone "MINISCAFFOLD_NUPKG_LOCATION" with
        | Some v ->
            printfn "using MINISCAFFOLD_NUPKG_LOCATION"
            v
        | None ->
            let dist =
                IO.Path.Combine(__SOURCE_DIRECTORY__, "../../dist")
                |> IO.DirectoryInfo

            dist.EnumerateFiles("*.nupkg")
            |> Seq.head
            |> fun fi -> fi.FullName

    let debug () =
        if not (System.Diagnostics.Debugger.IsAttached) then
            printfn
                "Please attach a debugger, PID: %d"
                (System.Diagnostics.Process.GetCurrentProcess().Id)

        while not (System.Diagnostics.Debugger.IsAttached) do
            System.Threading.Thread.Sleep(100)

        System.Diagnostics.Debugger.Break()
        ()

    let showTemplateHelp directory =
        let newArgs =
            Arguments.Empty
            |> Arguments.append [
                templateName
                "--help"
            ]

        let opts =
            match directory with
            | Some d -> (fun (opt: Fake.DotNet.DotNet.Options) -> { opt with WorkingDirectory = d })
            | None -> id

        Fake.DotNet.DotNet.getVersion (fun vOpt -> vOpt.WithCommon opts)
        |> printfn "dotnet --version %s"

        Dotnet.New.cmd opts newArgs.ToStartInfo

    let setup () =
        // debug ()
        // ensure we're installing the one from our dist folder
        printfn "nugetPkgPath %s" nugetPkgPath

        Fake.DotNet.DotNet.getSDKVersionFromGlobalJson ()
        |> printfn "dotnet global.json version %s"

        Fake.DotNet.DotNet.getVersion id
        |> printfn "dotnet --version %s"

        printfn "Uninstalling template..."

        try
            Dotnet.New.uninstall nugetPkgName
        with e ->
            printfn "Uninstall failing is fine: %A" e

        printfn "Installing template..."
        Dotnet.New.install nugetPkgPath
        printfn "Installed template..."

        showTemplateHelp None

    let runTemplate (directory) args =
        let newArgs =
            Arguments.Empty
            |> Arguments.append [ templateName ]
            // |> Arguments.appendNotEmpty "-lang" "F#"
            |> Arguments.appendRaw args

        Dotnet.New.cmd
            (fun opt -> {
                opt with
                    WorkingDirectory = directory
            })
            newArgs.ToStartInfo

    let copyGlobalJson (directory: IO.DirectoryInfo) =
        let globalJson = IO.Path.Join(__SOURCE_DIRECTORY__, "../../global.json")
        let destination = IO.Path.Join(directory.FullName, "global.json")
        IO.File.Copy(globalJson, destination)

    let projectStructureAsserts = [
        Assert.``CHANGELOG exists``
        Assert.``.config/dotnet-tools.json exists``
        Assert.``.github ISSUE_TEMPLATE bug_report exists``
        Assert.``.github ISSUE_TEMPLATE feature_request exists``
        Assert.``.github workflows build exists``
        Assert.``.github ISSUE_TEMPLATE exists``
        Assert.``.github PULL_REQUEST_TEMPLATE exists``
        Assert.``.editorconfig exists``
        Assert.``.gitattributes exists``
        Assert.``.gitignore exists``
        Assert.``LICENSE exists``
        Assert.``NuGet.config exists``
        Assert.``Directory.Packages.props exists``
        Assert.``README exists``
    ]

    [<Tests>]
    let tests =
        testSequenced
        <| // uncomment to get better logs
        testList "samples" [
            do setup ()
            yield!
                [
                    testCase,
                    "-n MyCoolLib --githubUsername CoolPersonNo2",
                    [
                        yield! projectStructureAsserts
                        Assert.``File exists`` ".github/workflows/benchmark.yml"
                        Assert.``File exists``
                            "benchmarks/MyCoolLib.Benchmarks/MyCoolLib.Benchmarks.fsproj"
                        Assert.``File exists``
                            "benchmarks/MyCoolLib.Benchmarks/Library.Benchmarks.fs"
                        Assert.``File exists`` "benchmarks/MyCoolLib.Benchmarks/Program.fs"
                        Assert.``project can build target`` "DotnetPack"
                        Assert.``project can build target`` "BuildDocs"
                        Assert.``project can build target`` "RunBenchmarks"
                    ]


                    testCase,
                    "-n ProjLibTest --githubUsername CoolPersonNo0",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "DotnetPack"
                        Effect.``dotnet new``
                            "mini-scaffold -n MyCoolLib3 --githubUsername CoolPersonNo3 --outputType projLib"
                            "src"
                        Effect.``dotnet sln add`` "src/MyCoolLib3/MyCoolLib3.fsproj"
                        Assert.``File exists`` "src/MyCoolLib3/MyCoolLib3.fsproj"
                        Assert.``project can build target`` "DotnetPack"
                    ]

                    testCase,
                    "-n ProjConsoleTest --githubUsername CoolPersonNo0",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "DotnetPack"
                        Effect.``dotnet new``
                            "mini-scaffold -n MyCoolConsole --githubUsername CoolPersonNo3 --outputType projConsole"
                            "src"
                        Effect.``dotnet sln add`` "src/MyCoolConsole/MyCoolConsole.fsproj"
                        Assert.``File exists`` "src/MyCoolConsole/MyCoolConsole.fsproj"
                        Assert.``project can build target`` "DotnetPack"
                        Effect.``dotnet run`` "" "src/MyCoolConsole/"
                    ]


                    testCase,
                    "-n ProjTestTest --githubUsername CoolPersonNo0",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "DotnetPack"
                        Effect.``dotnet new``
                            "mini-scaffold -n MyCoolLib3 --githubUsername CoolPersonNo3 --outputType projLib"
                            "src"
                        Effect.``dotnet sln add`` "src/MyCoolLib3/MyCoolLib3.fsproj"
                        Effect.``dotnet new``
                            "mini-scaffold -n MyCoolLib3.Tests --githubUsername CoolPersonNo3 --outputType projTest"
                            "tests"
                        Effect.``dotnet sln add`` "tests/MyCoolLib3.Tests/MyCoolLib3.Tests.fsproj"
                        Effect.``dotnet add reference``
                            "../../src/MyCoolLib3/MyCoolLib3.fsproj"
                            "tests/MyCoolLib3.Tests/"
                        Assert.``File exists`` "tests/MyCoolLib3.Tests/MyCoolLib3.Tests.fsproj"
                        Assert.``project can build target`` "DotnetPack"
                    ]

                    // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
                    testCase,
                    "-n fsharp-data-sample --githubUsername CoolPersonNo2",
                    [
                        yield! projectStructureAsserts
                        Assert.``File exists`` ".github/workflows/benchmark.yml"
                        Assert.``File exists``
                            "benchmarks/fsharp-data-sample.Benchmarks/fsharp-data-sample.Benchmarks.fsproj"
                        Assert.``File exists``
                            "benchmarks/fsharp-data-sample.Benchmarks/Library.Benchmarks.fs"
                        Assert.``File exists`` "benchmarks/fsharp-data-sample.Benchmarks/Program.fs"
                        Assert.``project can build target`` "DotnetPack"
                    ]
                    testCase,
                    "-n MyCoolApp --githubUsername CoolPersonNo2 --outputType Console",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "CreatePackages"
                    ]
                    // Test that CHANGELOG.md is not modified during build failures,
                    // *unless* at least one step has pushed a release to the outside world.

                    testCase,
                    "-n AssemblyInfoFail --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let generateAssemblyInfo"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n GitReleaseFailBeforeCommit --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Effect.``make failure in gitRelease function`` "Git.Commit.exec"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n GitReleaseFailAfterCommit --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Effect.``make failure in gitRelease function`` "Git.Branches.push \"\""
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG does not contain Unreleased section``
                    ]
                    testCase,
                    "-n GitReleaseSuccess --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``project can build target`` "Release"
                        Assert.``CHANGELOG does not contain Unreleased section``
                    ]

                    testCase,
                    "-n DotnetRestoreFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let dotnetRestore"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    testCase,
                    "-n DotnetBuildFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let dotnetBuild"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    testCase,
                    "-n DotnetTestFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let dotnetTest"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    testCase,
                    "-n CoverageReportFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let generateCoverageReport"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    testCase,
                    "-n DotnetPackFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let dotnetPack"
                        Assert.``build target with failure expected`` "Publish"
                    ]

                    testCase,
                    "-n PublishToNugetFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let publishToNuget"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    testCase,
                    "-n PublishToNugetSuccess --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Assert.``project can build target`` "PublishToNuget"
                    ]

                    testCase,
                    "-n GitHubReleaseFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "githubRelease"
                        Assert.``build target with failure expected`` "GitHubRelease"
                    ]

                    testCase,
                    "-n DotnetRestoreFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let dotnetRestore"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n AssemblyInfoFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let generateAssemblyInfo"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n DotnetBuildFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let dotnetBuild"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n DotnetTestFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let dotnetTest"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n CoverageReportFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let generateCoverageReport"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n CreatePackagesFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let createPackages"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n GitReleaseFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let gitRelease"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    testCase,
                    "-n GitHubReleaseFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let githubRelease"
                        Effect.``disable pushing in gitRelease function``
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        // Since the GitRelease step "succeeded", we no longer revert the changelog from that point on
                        Assert.``CHANGELOG does not contain Unreleased section``
                    ]

                    // Try to run a release on an alternate release branch name.
                    testCase,
                    "-n AlternateReleaseBranch --githubUsername TestAccount --releaseBranch alternateBranch",
                    [
                        Effect.``setup for release branch tests`` "alternateBranch"
                        Assert.``project can build target`` "Release"
                    ]

                    // This should fail since the release branch specified in the build script doesn't match the
                    // branch from which the script is executed.
                    testCase,
                    "-n ReleaseBranchMissingFailure --githubUsername TestAccount --releaseBranch notExist",
                    [
                        Effect.``setup for release branch tests`` "anotherBranch"
                        Assert.``build target with failure expected`` "Release"
                    ]

                ]
                |> Seq.map (fun (testCase, args, additionalAsserts) ->
                    testCase args
                    <| fun _ ->
                        use d = Disposables.DisposableDirectory.Create()
                        copyGlobalJson d.DirectoryInfo

                        showTemplateHelp
                        <| Some d.Directory

                        runTemplate d.Directory args

                        // The project we just generated is the only one in here
                        let projectDir =
                            d.DirectoryInfo.GetDirectories()
                            |> Seq.head

                        additionalAsserts
                        |> Seq.iter (fun asserter -> asserter projectDir)
                )

        ]
