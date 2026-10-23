namespace MiniScaffold.Tests


module Tests =
    open System
    open System.Diagnostics
    open System.IO.Compression
    open System.Xml.Linq
    open Fake.Core
    open Expecto
    open Infrastructure

    let ptestCase (reason: string) name test = Expecto.Tests.ptestCase name test

    let logger = Expecto.Logging.Log.create "setup"
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

    type private PackageHost =
        | Library
        | Console

    let private packedLibrariesSupplyFSharpCore host _ =
        let outputType, solutionName, packageNames =
            match host with
            | Library ->
                "library",
                "DependencyLibrary",
                [
                    "DependencyLibrary"
                    "DependencyProjectLibrary"
                ]
            | Console -> "console", "DependencyConsole", [ "DependencyProjectLibrary" ]

        use generated = Disposables.DisposableDirectory.Create()
        copyGlobalJson generated.DirectoryInfo

        runTemplate
            generated.Directory
            $"-n {solutionName} --githubUsername TestUser --outputType {outputType}"

        let projectDir =
            IO.DirectoryInfo(IO.Path.Combine(generated.Directory, solutionName))

        Effect.``dotnet new``
            "mini-scaffold -n DependencyProjectLibrary --githubUsername TestUser --outputType projLib"
            "src"
            projectDir

        Effect.``dotnet sln add``
            "src/DependencyProjectLibrary/DependencyProjectLibrary.fsproj"
            projectDir

        let runDotnet context workingDirectory environment arguments =
            let startInfo =
                ProcessStartInfo(
                    "dotnet",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                )

            for argument in arguments do
                startInfo.ArgumentList.Add argument

            for key, value in environment do
                startInfo.Environment.[key] <- value

            startInfo.Environment.Remove "MSBuildSDKsPath"
            |> ignore

            startInfo.Environment.Remove "MSBuildExtensionsPath"
            |> ignore

            use child = new Process(StartInfo = startInfo)
            Expect.isTrue (child.Start()) $"{context} should start dotnet"
            let output = child.StandardOutput.ReadToEndAsync()
            let errors = child.StandardError.ReadToEndAsync()
            child.WaitForExit()

            Expect.equal
                child.ExitCode
                0
                (sprintf
                    "%s dotnet %s failed.\n%s\n%s"
                    context
                    (String.concat " " arguments)
                    output.Result
                    errors.Result)

        match host with
        | Library -> Builds.executeBuild projectDir.FullName "DotnetPack"
        | Console ->
            Builds.executeBuild projectDir.FullName "DotnetTest"

            runDotnet "Console-hosted library pack" projectDir.FullName [] [
                "pack"
                "src/DependencyProjectLibrary/DependencyProjectLibrary.fsproj"
                "--configuration"
                "Release"
                "--output"
                IO.Path.Combine(projectDir.FullName, "dist")
                "-p:PackageVersion=0.1.0"
                "-p:PackageLicenseFile="
                "-p:PackageReadmeFile="
            ]


        let attribute name (element: XElement) =
            match element.Attribute(XName.Get name) with
            | null -> failtestf "%s is missing attribute %s" (string element) name
            | value -> value.Value

        let centralPackages =
            XDocument.Load(IO.Path.Combine(projectDir.FullName, "Directory.Packages.props"))

        let coreVersion =
            centralPackages.Descendants(XName.Get "PackageVersion")
            |> Seq.filter (fun package -> attribute "Include" package = "FSharp.Core")
            |> Seq.exactlyOne
            |> attribute "Version"

        let consumerSource =
            """using System;
using Say = PackageUnderTest.Say;

var person = new Say.Person("Ada", 42, Say.FavoriteColor.Blue,
    new DateTimeOffset(2000, 1, 2, 0, 0, 0, TimeSpan.Zero));
var actual = Say.helloPerson(person);
const string expected = "Hello Ada. You were born on 2000/01/02 and your favorite number is 42. You like Blue.";
if (actual != expected)
    throw new Exception($"Expected '{expected}', but got '{actual}'.");
"""

        for projectName in packageNames do
            let project =
                XDocument.Load(
                    IO.Path.Combine(
                        projectDir.FullName,
                        "src",
                        projectName,
                        $"{projectName}.fsproj"
                    )
                )

            let frameworks =
                project.Descendants(XName.Get "TargetFrameworks")
                |> Seq.exactlyOne
                |> fun value ->
                    value.Value.Split(';')
                    |> Array.toList

            let packagePath =
                IO.Path.Combine(projectDir.FullName, "dist", $"{projectName}.0.1.0.nupkg")

            Expect.isTrue (IO.File.Exists packagePath) $"{packagePath} should exist"
            use archive = ZipFile.OpenRead packagePath
            use nuspecStream = archive.GetEntry($"{projectName}.nuspec").Open()
            let nuspec = XDocument.Load nuspecStream
            let ns = nuspec.Root.Name.Namespace

            let groups =
                nuspec.Descendants(
                    ns
                    + "group"
                )
                |> Seq.toList

            Expect.equal
                (groups
                 |> List.map (attribute "targetFramework")
                 |> List.sort)
                (frameworks
                 |> List.sort)
                $"{packagePath} should contain exactly one dependency group for each target framework"

            for group in groups do
                let framework = attribute "targetFramework" group

                let dependencies =
                    group.Elements(
                        ns
                        + "dependency"
                    )
                    |> Seq.filter (fun dependency -> attribute "id" dependency = "FSharp.Core")
                    |> Seq.toList

                let context = $"{projectName} {framework}"

                Expect.equal
                    dependencies.Length
                    1
                    $"{context} should declare FSharp.Core exactly once"

                let dependency = List.exactlyOne dependencies

                Expect.equal
                    (attribute "version" dependency)
                    coreVersion
                    $"{context} should use the central FSharp.Core version as its minimum dependency version"

                let assets name defaults =
                    match dependency.Attribute(XName.Get name) with
                    | null -> defaults
                    | value ->
                        value.Value.Split(
                            [|
                                ','
                                ';'
                            |],
                            StringSplitOptions.RemoveEmptyEntries
                        )
                        |> Seq.map (fun asset -> asset.Trim().ToLowerInvariant())
                        |> Set.ofSeq

                let included = assets "include" (Set.singleton "all")
                let excluded = assets "exclude" Set.empty

                for asset in
                    [
                        "compile"
                        "runtime"
                    ] do
                    Expect.isTrue
                        ((Set.contains "all" included
                          || Set.contains asset included)
                         && not (
                             Set.contains "all" excluded
                             || Set.contains asset excluded
                         ))
                        $"{context} should supply FSharp.Core {asset} assets"

            use consumer = Disposables.DisposableDirectory.Create()
            copyGlobalJson consumer.DirectoryInfo
            let element name (content: obj array) = XElement(XName.Get name, content)

            let consumerProject =
                element "Project" [|
                    XAttribute(XName.Get "Sdk", "Microsoft.NET.Sdk")
                    element "PropertyGroup" [|
                        element "OutputType" [| "Exe" |]
                        element "TargetFramework" [| "net8.0" |]
                    |]
                    element "ItemGroup" [|
                        element "PackageReference" [|
                            XAttribute(XName.Get "Include", projectName)
                            XAttribute(XName.Get "Version", "0.1.0")
                        |]
                    |]
                |]

            consumerProject.Save(IO.Path.Combine(consumer.Directory, "Consumer.csproj"))

            IO.File.WriteAllText(
                IO.Path.Combine(consumer.Directory, "Program.cs"),
                consumerSource.Replace("PackageUnderTest", projectName)
            )

            let config =
                element "configuration" [|
                    element "packageSources" [|
                        element "clear" [||]
                        element "add" [|
                            XAttribute(XName.Get "key", "generated")
                            XAttribute(
                                XName.Get "value",
                                IO.Path.Combine(projectDir.FullName, "dist")
                            )
                        |]
                        element "add" [|
                            XAttribute(XName.Get "key", "nuget.org")
                            XAttribute(XName.Get "value", "https://api.nuget.org/v3/index.json")
                        |]
                    |]
                    element "fallbackPackageFolders" [| element "clear" [||] |]
                |]

            let configPath = IO.Path.Combine(consumer.Directory, "NuGet.config")
            config.Save configPath

            let runConsumer =
                runDotnet $"{projectName} consumer" consumer.Directory [
                    "NUGET_PACKAGES", IO.Path.Combine(consumer.Directory, "packages")
                ]

            runConsumer [
                "restore"
                "--configfile"
                configPath
                "--no-http-cache"
            ]

            runConsumer [
                "build"
                "--no-restore"
            ]

            runConsumer [
                "run"
                "--no-build"
                "--no-restore"
            ]

    [<Tests>]
    let tests =
        testSequenced
        <| // uncomment to get better logs
        testList "samples" [
            do setup ()
            testCase
                "packed libraries supply FSharp.Core to clean CSharp consumers in Library solutions"
                (packedLibrariesSupplyFSharpCore Library)
            testCase
                "packed libraries supply FSharp.Core to clean CSharp consumers in Console solutions"
                (packedLibrariesSupplyFSharpCore Console)
            yield!
                [
                    testCase,
                    "-n MyCoolLib --githubUsername CoolPersonNo2",
                    [
                        yield! projectStructureAsserts
                        Assert.``File does not exist`` "src/MyCoolLib/AssemblyInfo.fs"
                        Assert.``project can build target`` "DotnetPack"
                        Assert.``assembly info values are set after pack`` "MyCoolLib"
                        Assert.``project can build target`` "BuildDocs"
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
                        Assert.``File does not exist`` "src/MyCoolLib3/AssemblyInfo.fs"
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
                        Assert.``File does not exist`` "src/MyCoolConsole/AssemblyInfo.fs"
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
                        Assert.``File does not exist`` "tests/MyCoolLib3.Tests/AssemblyInfo.fs"
                        Assert.``project can build target`` "DotnetPack"
                    ]

                    // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
                    testCase,
                    "-n fsharp-data-sample --githubUsername CoolPersonNo2",
                    [
                        yield! projectStructureAsserts
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
                        Effect.``make build function fail`` "let updateChangelog"
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
                        Effect.``make build function fail`` "let updateChangelog"
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
