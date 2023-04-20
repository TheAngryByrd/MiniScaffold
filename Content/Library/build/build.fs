open System
open Fake.Core
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api
open Fake.BuildServer
open Argu

let environVarAsBoolOrDefault varName defaultValue =
    let truthyConsts = [
        "1"
        "Y"
        "YES"
        "T"
        "TRUE"
    ]

    try
        let envvar = (Environment.environVar varName).ToUpper()

        truthyConsts
        |> List.exists ((=) envvar)
    with _ ->
        defaultValue

//-----------------------------------------------------------------------------
// Metadata and Configuration
//-----------------------------------------------------------------------------

let productName = "MyLib.1"

let sln =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "MyLib.1.sln"


let srcCodeGlob =
    !!(__SOURCE_DIRECTORY__
       </> ".."
       </> "src/**/*.fs")
    ++ (__SOURCE_DIRECTORY__
        </> ".."
        </> "src/**/*.fsx")
    -- (__SOURCE_DIRECTORY__
        </> ".."
        </> "src/**/obj/**/*.fs")

let testsCodeGlob =
    !!(__SOURCE_DIRECTORY__
       </> ".."
       </> "tests/**/*.fs")
    ++ (__SOURCE_DIRECTORY__
        </> ".."
        </> "tests/**/*.fsx")
    -- (__SOURCE_DIRECTORY__
        </> ".."
        </> "tests/**/obj/**/*.fs")

let srcGlob =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "src/**/*.??proj"

let testsGlob =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "tests/**/*.??proj"

let srcAndTest =
    !!srcGlob
    ++ testsGlob

let distDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "dist"

let distGlob =
    distDir
    </> "*.nupkg"

let coverageThresholdPercent = 80

let coverageReportDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "docs"
    </> "coverage"


let docsDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "docs"

let docsSrcDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "docsSrc"

let docsToolDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "docsTool"

let gitOwner = "MyGithubUsername"
let gitRepoName = "MyLib.1"

let gitHubRepoUrl = sprintf "https://github.com/%s/%s" gitOwner gitRepoName

let releaseBranch = "MyReleaseBranch"

let tagFromVersionNumber versionNumber = sprintf "v%s" versionNumber

let changelogFilename =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "CHANGELOG.md"

let changelog = Fake.Core.Changelog.load changelogFilename

let mutable latestEntry =
    if Seq.isEmpty changelog.Entries then
        Changelog.ChangelogEntry.New("0.0.1", "0.0.1-alpha.1", Some DateTime.Today, None, [], false)
    else
        changelog.LatestEntry


let publishUrl = "https://www.nuget.org"

let docsSiteBaseUrl = sprintf "https://%s.github.io/%s" gitOwner gitRepoName

let disableCodeCoverage = environVarAsBoolOrDefault "DISABLE_COVERAGE" false

let githubToken = Environment.environVarOrNone "GITHUB_TOKEN"


let nugetToken = Environment.environVarOrNone "NUGET_TOKEN"

//-----------------------------------------------------------------------------
// Helpers
//-----------------------------------------------------------------------------

let isRelease (targets: Target list) =
    targets
    |> Seq.map (fun t -> t.Name)
    |> Seq.exists ((=) "Release")

let invokeAsync f = async { f () }

let configuration (targets: Target list) =
    let defaultVal = if isRelease targets then "Release" else "Debug"

    match Environment.environVarOrDefault "CONFIGURATION" defaultVal with
    | "Debug" -> DotNet.BuildConfiguration.Debug
    | "Release" -> DotNet.BuildConfiguration.Release
    | config -> DotNet.BuildConfiguration.Custom config

let failOnBadExitAndPrint (p: ProcessResult) =
    if
        p.ExitCode
        <> 0
    then
        p.Errors
        |> Seq.iter Trace.traceError

        failwithf "failed with exitcode %d" p.ExitCode

// CI Servers can have bizzare failures that have nothing to do with your code
let rec retryIfInCI times fn =
    match Environment.environVarOrNone "CI" with
    | Some _ ->
        if times > 1 then
            try
                fn ()
            with _ ->
                retryIfInCI (times - 1) fn
        else
            fn ()
    | _ -> fn ()

let isReleaseBranchCheck () =
    if
        Git.Information.getBranchName ""
        <> releaseBranch
    then
        failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch

module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let run cmdParam args = DotNet.exec cmdParam "run" args

    let tool optionConfig command args =
        DotNet.exec optionConfig (sprintf "%s" command) args
        |> failOnBadExitAndPrint

    let reportgenerator optionConfig args =
        tool optionConfig "reportgenerator" args

    let fcswatch optionConfig args = tool optionConfig "fcswatch" args

    let fsharpAnalyzer optionConfig args =
        tool optionConfig "fsharp-analyzers" args

    let fantomas args = DotNet.exec id "fantomas" args

module FSharpAnalyzers =
    type Arguments =
        | Project of string
        | Analyzers_Path of string
        | Fail_On_Warnings of string list
        | Ignore_Files of string list
        | Verbose

        interface IArgParserTemplate with
            member s.Usage = ""


open DocsTool.CLIArgs

module DocsTool =
    open Argu
    let buildparser = ArgumentParser.Create<BuildArgs>(programName = "docstool")

    let buildCLI () =
        [
            BuildArgs.SiteBaseUrl docsSiteBaseUrl
            BuildArgs.ProjectGlob srcGlob
            BuildArgs.DocsOutputDirectory docsDir
            BuildArgs.DocsSourceDirectory docsSrcDir
            BuildArgs.GitHubRepoUrl gitHubRepoUrl
            BuildArgs.ProjectName gitRepoName
            BuildArgs.ReleaseVersion latestEntry.NuGetVersion
        ]
        |> buildparser.PrintCommandLineArgumentsFlat

    let build () =
        dotnet.run
            (fun args -> {
                args with
                    WorkingDirectory = docsToolDir
            })
            (sprintf " -- build %s" (buildCLI ()))
        |> failOnBadExitAndPrint

    let watchparser = ArgumentParser.Create<WatchArgs>(programName = "docstool")

    let watchCLI () =
        [
            WatchArgs.ProjectGlob srcGlob
            WatchArgs.DocsSourceDirectory docsSrcDir
            WatchArgs.GitHubRepoUrl gitHubRepoUrl
            WatchArgs.ProjectName gitRepoName
            WatchArgs.ReleaseVersion latestEntry.NuGetVersion
        ]
        |> watchparser.PrintCommandLineArgumentsFlat

    let watch () =
        dotnet.watch
            (fun args -> {
                args with
                    WorkingDirectory = docsToolDir
            })
            "run"
            (sprintf "-- watch %s" (watchCLI ()))
        |> failOnBadExitAndPrint

let allReleaseChecks () =
    isReleaseBranchCheck ()
    Changelog.isChangelogEmpty latestEntry

//-----------------------------------------------------------------------------
// Target Implementations
//-----------------------------------------------------------------------------


let clean _ =
    [
        "bin"
        "temp"
        distDir
        coverageReportDir
    ]
    |> Shell.cleanDirs

    !!srcGlob
    ++ testsGlob
    |> Seq.collect (fun p ->
        [
            "bin"
            "obj"
        ]
        |> Seq.map (fun sp ->
            IO.Path.GetDirectoryName p
            </> sp
        )
    )
    |> Shell.cleanDirs

    [ "paket-files/paket.restore.cached" ]
    |> Seq.iter Shell.rm

let dotnetRestore _ =
    [ sln ]
    |> Seq.map (fun dir ->
        fun () ->
            let args =
                []
                |> String.concat " "

            DotNet.restore
                (fun c -> {
                    c with
                        Common =
                            c.Common
                            |> DotNet.Options.withCustomParams (Some(args))
                })
                dir
    )
    |> Seq.iter (retryIfInCI 10)

let updateChangelog ctx =
    latestEntry <- Changelog.updateChangelog changelogFilename changelog gitHubRepoUrl ctx


let revertChangelog _ =
    if String.isNotNullOrEmpty Changelog.changelogBackupFilename then
        Changelog.changelogBackupFilename
        |> Shell.copyFile changelogFilename

let deleteChangelogBackupFile _ =
    if String.isNotNullOrEmpty Changelog.changelogBackupFilename then
        Shell.rm Changelog.changelogBackupFilename

let dotnetBuild ctx =
    let args = [
        sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
        "--no-restore"
    ]

    DotNet.build
        (fun c -> {
            c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                Common =
                    c.Common
                    |> DotNet.Options.withAdditionalArgs args

        })
        sln

let fsharpAnalyzers _ =
    let argParser =
        ArgumentParser.Create<FSharpAnalyzers.Arguments>(programName = "fsharp-analyzers")

    !!srcGlob
    |> Seq.iter (fun proj ->
        let args =
            [
                FSharpAnalyzers.Analyzers_Path(
                    __SOURCE_DIRECTORY__
                    </> ".."
                    </> "packages/analyzers"
                )
                FSharpAnalyzers.Arguments.Project proj
                FSharpAnalyzers.Arguments.Fail_On_Warnings [ "BDH0002" ]
                FSharpAnalyzers.Arguments.Ignore_Files [ "*AssemblyInfo.fs" ]
                FSharpAnalyzers.Verbose
            ]
            |> argParser.PrintCommandLineArgumentsFlat

        dotnet.fsharpAnalyzer id args
    )

let dotnetTest ctx =
    let excludeCoverage =
        !!testsGlob
        |> Seq.map IO.Path.GetFileNameWithoutExtension
        |> String.concat "|"

    let args = [
        "--no-build"
        sprintf "/p:AltCover=%b" (not disableCodeCoverage)
        sprintf "/p:AltCoverThreshold=%d" coverageThresholdPercent
        sprintf "/p:AltCoverAssemblyExcludeFilter=%s" excludeCoverage
        "/p:AltCoverLocalSource=true"
    ]

    DotNet.test
        (fun c ->

            {
                c with
                    Configuration = configuration (ctx.Context.AllExecutingTargets)
                    Common =
                        c.Common
                        |> DotNet.Options.withAdditionalArgs args
            })
        sln

let generateCoverageReport _ =
    let coverageReports =
        !! "tests/**/coverage*.xml"
        |> String.concat ";"

    let sourceDirs =
        !!srcGlob
        |> Seq.map Path.getDirectory
        |> String.concat ";"

    let independentArgs = [
        sprintf "-reports:\"%s\"" coverageReports
        sprintf "-targetdir:\"%s\"" coverageReportDir
        // Add source dir
        sprintf "-sourcedirs:\"%s\"" sourceDirs
        // Ignore Tests and if AltCover.Recorder.g sneaks in
        sprintf "-assemblyfilters:\"%s\"" "-*.Tests;-AltCover.Recorder.g"
        sprintf "-Reporttypes:%s" "Html"
    ]

    let args =
        independentArgs
        |> String.concat " "

    dotnet.reportgenerator id args

let watchTests _ =
    !!testsGlob
    |> Seq.map (fun proj ->
        fun () ->
            dotnet.watch
                (fun opt ->
                    opt
                    |> DotNet.Options.withWorkingDirectory (IO.Path.GetDirectoryName proj)
                )
                "test"
                ""
            |> ignore
    )
    |> Seq.iter (
        invokeAsync
        >> Async.Catch
        >> Async.Ignore
        >> Async.Start
    )

    printfn "Press Ctrl+C (or Ctrl+Break) to stop..."

    let cancelEvent =
        Console.CancelKeyPress
        |> Async.AwaitEvent
        |> Async.RunSynchronously

    cancelEvent.Cancel <- true

let generateAssemblyInfo _ =

    let (|Fsproj|Csproj|Vbproj|) (projFileName: string) =
        match projFileName with
        | f when f.EndsWith("fsproj") -> Fsproj
        | f when f.EndsWith("csproj") -> Csproj
        | f when f.EndsWith("vbproj") -> Vbproj
        | _ ->
            failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

    let releaseChannel =
        match latestEntry.SemVer.PreRelease with
        | Some pr -> pr.Name
        | _ -> "release"

    let getAssemblyInfoAttributes projectName = [
        AssemblyInfo.Title(projectName)
        AssemblyInfo.Product productName
        AssemblyInfo.Version latestEntry.AssemblyVersion
        AssemblyInfo.Metadata("ReleaseDate", latestEntry.Date.Value.ToString("o"))
        AssemblyInfo.FileVersion latestEntry.AssemblyVersion
        AssemblyInfo.InformationalVersion latestEntry.AssemblyVersion
        AssemblyInfo.Metadata("ReleaseChannel", releaseChannel)
        AssemblyInfo.Metadata("GitHash", Git.Information.getCurrentSHA1 (null))
    ]

    let getProjectDetails (projectPath: string) =
        let projectName = IO.Path.GetFileNameWithoutExtension(projectPath)

        (projectPath,
         projectName,
         IO.Path.GetDirectoryName(projectPath),
         (getAssemblyInfoAttributes projectName))

    srcAndTest
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
        match projFileName with
        | Fsproj ->
            AssemblyInfoFile.createFSharp
                (folderName
                 </> "AssemblyInfo.fs")
                attributes
        | Csproj ->
            AssemblyInfoFile.createCSharp
                ((folderName
                  </> "Properties")
                 </> "AssemblyInfo.cs")
                attributes
        | Vbproj ->
            AssemblyInfoFile.createVisualBasic
                ((folderName
                  </> "My Project")
                 </> "AssemblyInfo.vb")
                attributes
    )

let dotnetPack ctx =
    // Get release notes with properly-linked version number

    let releaseNotes = Changelog.mkReleaseNotes changelog latestEntry gitHubRepoUrl


    let args = [
        sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
        sprintf "/p:PackageReleaseNotes=\"%s\"" releaseNotes
    ]

    DotNet.pack
        (fun c -> {
            c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                OutputPath = Some distDir
                Common =
                    c.Common
                    |> DotNet.Options.withAdditionalArgs args
        })
        sln


let publishToNuget _ =
    allReleaseChecks ()

    Paket.push (fun c -> {
        c with
            ToolType = ToolType.CreateLocalTool()
            PublishUrl = publishUrl
            WorkingDir = "dist"
            ApiKey =
                match nugetToken with
                | Some s -> s
                | _ -> c.ApiKey // assume paket-config was set properly
    })
    // If build fails after this point, we've pushed a release out with this version of CHANGELOG.md so we should keep it around
    Target.deactivateBuildFailure "RevertChangelog"

let gitRelease _ =
    allReleaseChecks ()

    let releaseNotesGitCommitFormat = latestEntry.ToString()

    Git.Staging.stageFile "" "CHANGELOG.md"
    |> ignore

    !! "src/**/AssemblyInfo.fs"
    |> Seq.iter (
        Git.Staging.stageFile ""
        >> ignore
    )

    Git.Commit.exec
        ""
        (sprintf "Bump version to %s\n\n%s" latestEntry.NuGetVersion releaseNotesGitCommitFormat)

    Git.Branches.push ""

    let tag = tagFromVersionNumber latestEntry.NuGetVersion

    Git.Branches.tag "" tag
    Git.Branches.pushTag "" "origin" tag

let githubRelease _ =
    allReleaseChecks ()

    let token =
        match githubToken with
        | Some s -> s
        | _ ->
            failwith
                "please set the github_token environment variable to a github personal access token with repo access."

    let files = !!distGlob
    // Get release notes with properly-linked version number
    let releaseNotes = Changelog.mkReleaseNotes changelog latestEntry gitHubRepoUrl

    GitHub.createClientWithToken token
    |> GitHub.draftNewRelease
        gitOwner
        gitRepoName
        (tagFromVersionNumber latestEntry.NuGetVersion)
        (latestEntry.SemVer.PreRelease
         <> None)
        (releaseNotes
         |> Seq.singleton)
    |> GitHub.uploadFiles files
    |> GitHub.publishDraft
    |> Async.RunSynchronously

let formatCode _ =
    let result =
        [
            srcCodeGlob
            testsCodeGlob
        ]
        |> Seq.collect id
        // Ignore AssemblyInfo
        |> Seq.filter (fun f ->
            f.EndsWith("AssemblyInfo.fs")
            |> not
        )
        |> String.concat " "
        |> dotnet.fantomas

    if not result.OK then
        printfn "Errors while formatting all files: %A" result.Messages

let checkFormatCode _ =
    let result =
        [
            srcCodeGlob
            testsCodeGlob
        ]
        |> Seq.collect id
        // Ignore AssemblyInfo
        |> Seq.filter (fun f ->
            f.EndsWith("AssemblyInfo.fs")
            |> not
        )
        |> String.concat " "
        |> sprintf "%s --check"
        |> dotnet.fantomas

    if result.ExitCode = 0 then
        Trace.log "No files need formatting"
    elif result.ExitCode = 99 then
        failwith "Some files need formatting, check output for more info"
    else
        Trace.logf "Errors while formatting: %A" result.Errors

let buildDocs _ = DocsTool.build ()

let watchDocs _ = DocsTool.watch ()

let releaseDocs ctx =
    isReleaseBranchCheck () // Docs changes don't need a full release to the library

    Git.Staging.stageAll docsDir
    Git.Commit.exec "" (sprintf "Documentation release of version %s" latestEntry.NuGetVersion)

    if
        isRelease (ctx.Context.AllExecutingTargets)
        |> not
    then
        // We only want to push if we're only calling "ReleaseDocs" target
        // If we're calling "Release" target, we'll let the "GitRelease" target do the git push
        Git.Branches.push ""


let initTargets () =
    BuildServer.install [ GitHubActions.Installer ]

    /// Defines a dependency - y is dependent on x. Finishes the chain.
    let (==>!) x y =
        x ==> y
        |> ignore

    /// Defines a soft dependency. x must run before y, if it is present, but y does not require x to be run. Finishes the chain.
    let (?=>!) x y =
        x ?=> y
        |> ignore
    //-----------------------------------------------------------------------------
    // Hide Secrets in Logger
    //-----------------------------------------------------------------------------
    Option.iter (TraceSecrets.register "<GITHUB_TOKEN>") githubToken
    Option.iter (TraceSecrets.register "<NUGET_TOKEN>") nugetToken
    //-----------------------------------------------------------------------------
    // Target Declaration
    //-----------------------------------------------------------------------------

    Target.create "Clean" clean
    Target.create "DotnetRestore" dotnetRestore
    Target.create "UpdateChangelog" updateChangelog
    Target.createBuildFailure "RevertChangelog" revertChangelog // Do NOT put this in the dependency chain
    Target.createFinal "DeleteChangelogBackupFile" deleteChangelogBackupFile // Do NOT put this in the dependency chain
    Target.create "DotnetBuild" dotnetBuild
    Target.create "FSharpAnalyzers" fsharpAnalyzers
    Target.create "DotnetTest" dotnetTest
    Target.create "GenerateCoverageReport" generateCoverageReport
    Target.create "WatchTests" watchTests
    Target.create "GenerateAssemblyInfo" generateAssemblyInfo
    Target.create "DotnetPack" dotnetPack
    Target.create "PublishToNuGet" publishToNuget
    Target.create "GitRelease" gitRelease
    Target.create "GitHubRelease" githubRelease
    Target.create "FormatCode" formatCode
    Target.create "CheckFormatCode" checkFormatCode
    Target.create "Release" ignore
    Target.create "BuildDocs" buildDocs
    Target.create "WatchDocs" watchDocs
    Target.create "ReleaseDocs" releaseDocs

    //-----------------------------------------------------------------------------
    // Target Dependencies
    //-----------------------------------------------------------------------------


    // Only call Clean if DotnetPack was in the call chain
    // Ensure Clean is called before DotnetRestore
    "Clean"
    ?=>! "DotnetRestore"

    "Clean"
    ==>! "DotnetPack"

    // Only call GenerateAssemblyInfo if Publish was in the call chain
    // Ensure GenerateAssemblyInfo is called after DotnetRestore and before DotnetBuild
    "DotnetRestore"
    ?=>! "GenerateAssemblyInfo"

    "GenerateAssemblyInfo"
    ?=>! "DotnetBuild"

    "GenerateAssemblyInfo"
    ==>! "PublishToNuGet"

    // Only call UpdateChangelog if Publish was in the call chain
    // Ensure UpdateChangelog is called after DotnetRestore and before GenerateAssemblyInfo
    "DotnetRestore"
    ?=>! "UpdateChangelog"

    "UpdateChangelog"
    ?=>! "GenerateAssemblyInfo"

    "UpdateChangelog"
    ==>! "PublishToNuGet"

    "BuildDocs"
    ==>! "ReleaseDocs"

    "BuildDocs"
    ?=>! "PublishToNuget"

    "DotnetPack"
    ?=>! "BuildDocs"

    "GenerateCoverageReport"
    ?=>! "ReleaseDocs"


    "DotnetRestore"
    ==> "CheckFormatCode"
    ==> "DotnetBuild"
    ==> "FSharpAnalyzers"
    ==> "DotnetTest"
    =?> ("GenerateCoverageReport", not disableCodeCoverage)
    ==> "DotnetPack"
    ==> "PublishToNuGet"
    ==> "GitRelease"
    ==> "GitHubRelease"
    ==>! "Release"

    "DotnetRestore"
    ==>! "WatchTests"

//-----------------------------------------------------------------------------
// Target Start
//-----------------------------------------------------------------------------
[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> Context.FakeExecutionContext.Create false "build.fsx"
    |> Context.RuntimeContext.Fake
    |> Context.setExecutionContext

    initTargets ()
    Target.runOrDefaultWithArguments "DotnetPack"

    0 // return an integer exit code
