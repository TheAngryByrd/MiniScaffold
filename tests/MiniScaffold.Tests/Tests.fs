namespace MiniScaffold.Tests



module Tests =
    open System
    open Fake.Core
    open Expecto
    open Infrastructure

    let logger = Expecto.Logging.Log.create "setup"
    let nugetPkgName =  "MiniScaffold"
    let templateName = "mini-scaffold"
    let nugetPkgPath =
        match Environment.environVarOrNone "MINISCAFFOLD_NUPKG_LOCATION" with
        | Some v ->
            printfn "using MINISCAFFOLD_NUPKG_LOCATION"
            v
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

    let showTemplateHelp directory =
        let newArgs =
            Arguments.Empty
            |> Arguments.append [
                templateName
                "--help"
            ]
        let opts =
            match directory with
            | Some d -> (fun (opt : Fake.DotNet.DotNet.Options) -> { opt with WorkingDirectory = d})
            | None -> id
        Fake.DotNet.DotNet.getVersion (fun vOpt -> vOpt.WithCommon opts) |> printfn "dotnet --version %s"
        Dotnet.New.cmd opts newArgs.ToStartInfo
    let setup () =
        // debug ()
        // ensure we're installing the one from our dist folder
        printfn "nugetPkgPath %s" nugetPkgPath
        Fake.DotNet.DotNet.getSDKVersionFromGlobalJson () |> printfn "dotnet global.json version %s"
        Fake.DotNet.DotNet.getVersion id |> printfn "dotnet --version %s"
        printfn "Uninstalling template..."
        try Dotnet.New.uninstall nugetPkgName with e -> printfn "Uninstall failing is fine: %A" e

        printfn "Installing template..."
        Dotnet.New.install nugetPkgPath
        printfn "Installed template..."

        showTemplateHelp None

    let runTemplate (directory) args =
        let newArgs =
            Arguments.Empty
            |> Arguments.append [
                templateName
            ]
            // |> Arguments.appendNotEmpty "-lang" "F#"
            |> Arguments.appendRaw args

        Dotnet.New.cmd (fun opt -> { opt with WorkingDirectory = directory}) newArgs.ToStartInfo


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
                "-n MyCoolLib --githubUsername CoolPersonNo2", [
                    yield! projectStructureAsserts
                    Assert.``project can build target`` "DotnetPack"
                    Assert.``project can build target`` "BuildDocs"
                    ]
                // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
                "-n fsharp-data-sample --githubUsername CoolPersonNo2", [
                    yield! projectStructureAsserts
                    Assert.``project can build target`` "DotnetPack"
                    ]
                "-n MyCoolApp --githubUsername CoolPersonNo2 --outputType Console", [
                    yield! projectStructureAsserts
                    Assert.``project can build target`` "CreatePackages"
                    ]
                // Test that CHANGELOG.md is not modified during build failures,
                // *unless* at least one step has pushed a release to the outside world.
                "-n DotnetRestoreFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let dotnetRestore"
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
                    Effect.``make build function fail`` "let dotnetBuild"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetTestFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let dotnetTest"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n CoverageReportFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateCoverageReport"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetPackFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let dotnetPack"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n SourceLinkTestFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let sourceLinkTest"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n PublishToNugetFail --githubUsername TestAccount", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let publishToNuget"
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
                    Effect.``make build function fail`` "let dotnetRestore"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n AssemblyInfoFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateAssemblyInfo"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetBuildFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let dotnetBuild"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n DotnetTestFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let dotnetTest"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n CoverageReportFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let generateCoverageReport"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n CreatePackagesFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let createPackages"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n GitReleaseFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let gitRelease"
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    Assert.``CHANGELOG contains Unreleased section``
                    ]
                "-n GitHubReleaseFail --githubUsername TestAccount --outputType Console", [
                    Effect.``setup for release tests``
                    Effect.``make build function fail`` "let githubRelease"
                    Effect.``disable pushing in gitRelease function``
                    Assert.``CHANGELOG contains Unreleased section``
                    Assert.``build target with failure expected`` "Release"
                    // Since the GitRelease step "succeeded", we no longer revert the changelog from that point on
                    Assert.``CHANGELOG does not contain Unreleased section``
                    ]

            ] |> Seq.map(fun (args, additionalAsserts) -> testCase args <| fun _ ->
                use d = Disposables.DisposableDirectory.Create()
                showTemplateHelp <| Some d.Directory

                runTemplate d.Directory args

                // The project we just generated is the only one in here
                let projectDir =
                    d.DirectoryInfo.GetDirectories ()
                    |> Seq.head

                additionalAsserts
                |> Seq.iter(fun asserter -> asserter projectDir)
            )

        ]
