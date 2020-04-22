namespace MiniScaffold.Tests



module Tests =
    open System
    open Fake.Core
    open Expecto
    open Infrastructure

    let logger = Expecto.Logging.Log.create "setup"
    let nugetPkgName =  "MiniScaffold"
    let nugetPkgPath =
        match Environment.environVarOrNone "MINISCAFFOLD_NUPKG_LOCATION" with
        | Some v -> v
        | None ->
            let dist = IO.Path.Combine(__SOURCE_DIRECTORY__, "../../dist") |> IO.DirectoryInfo
            dist.EnumerateFiles("*.nupkg")
            |> Seq.head
            |> fun fi -> fi.FullName

    let debug () =
        if not(System.Diagnostics.Debugger.IsAttached) then
            printfn "Please attach a debugger, PID: %d" (System.Diagnostics.Process.GetCurrentProcess().Id)
        while not(System.Diagnostics.Debugger.IsAttached) do
            System.Threading.Thread.Sleep(100)
        System.Diagnostics.Debugger.Break()
        ()
    let setup () =
        // debug ()
        // ensure we're installing the one from our dist folder
        // printfn "nugetPkgPath %s" nugetPkgPath

        Dotnet.New.uninstall nugetPkgName
        Dotnet.New.install nugetPkgPath




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
        Assert.``paket.dependencies exists``
        Assert.``paket.lock exists``
        Assert.``README exists``
    ]

    [<Tests>]
    let tests =
        testSequenced <| // uncomment to get better logs
        testList "samples" [
            do setup ()
            yield! [
                // "-n MyCoolLib --githubUsername CoolPersonNo2", [
                //     yield! projectStructureAsserts
                //     Assert.``project can build target`` "DotnetPack"
                //     Assert.``project can build target`` "BuildDocs"
                //     ]
                // // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
                // "-n fsharp-data-sample --githubUsername CoolPersonNo2", [
                //     yield! projectStructureAsserts
                //     Assert.``project can build target`` "DotnetPack"
                //     ]
                // "-n MyCoolApp --githubUsername CoolPersonNo2 --outputType Console", [
                //     yield! projectStructureAsserts
                //     Assert.``project can build target`` "CreatePackages"
                //     ]
                // Test that CHANGELOG.md is not modified during build failures,
                // *unless* at least one step has pushed a release to the outside world.
                "-n DotnetRestoreFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n AssemblyInfoFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetBuildFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetTestFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n CoverageReportFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetPackFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n SourceLinkTestFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n PublishToNugetFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n PublishToNugetSuccess --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``disable publishToNuget function`` // Simulates success, since it would fail due to NUGET_API being unset
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``project can build target`` "PublishToNuget"
                    // Since the PublishToNuget step "succeeded", we no longer revert the changelog from that point on
                    Assert.``CHANGELOG does not contain Unreleased section``
                    ]
                "-n GitReleaseFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``disable publishToNuget function`` // Simulates success, since it would fail due to NUGET_API being unset
                    Effect.``make build function fail`` "gitRelease"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    // Since the PublishToNuget step "succeeded", we no longer revert the changelog from that point on
                    Assert.``CHANGELOG does not contain Unreleased section``
                    ]
                "-n GitHubReleaseFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``disable publishToNuget function`` // Simulates success, since it would fail due to NUGET_API being unset
                    Effect.``make build function fail`` "githubRelease"
                    Effect.``disable pushing in gitRelease function``
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    // Since the PublishToNuget step "succeeded", we no longer revert the changelog from that point on
                    Assert.``CHANGELOG does not contain Unreleased section``
                    ]

                "-n DotnetRestoreFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "dotnetRestore"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n AssemblyInfoFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetBuildFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "dotnetBuild"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetTestFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "dotnetTest"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n CoverageReportFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "generateCoverageReport"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n CreatePackagesFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "createPackages"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n GitReleaseFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "gitRelease"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n GitHubReleaseFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "githubRelease"
                    Effect.``disable pushing in gitRelease function``
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    // Since the GitRelease step "succeeded", we no longer revert the changelog from that point on
                    Assert.``CHANGELOG does not contain Unreleased section``
                    ]

            ] |> Seq.map(fun (args, additionalAsserts) -> testCase args <| fun _ ->
                use d = Disposables.DisposableDirectory.Create()
                printfn "dir -> %s" d.Directory
                let newArgs = [
                    sprintf "mini-scaffold -lang F# %s" args
                ]
                Dotnet.New.cmd (fun opt -> { opt with WorkingDirectory = d.Directory}) newArgs

                // The project we just generated is the only one in here
                let projectDir =
                    d.DirectoryInfo.GetDirectories ()
                    |> Seq.head

                additionalAsserts
                |> Seq.iter(fun asserter -> asserter projectDir)
            )

        ]
