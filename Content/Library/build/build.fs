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

let rootDirectory =
    __SOURCE_DIRECTORY__
    </> ".."

let productName = "MyLib.1"

let sln =
    rootDirectory
    </> "MyLib.1.sln"

let srcCodeGlob =
    !!(rootDirectory
       </> "src/**/*.fs")
    ++ (rootDirectory
        </> "src/**/*.fsx")
    -- (rootDirectory
        </> "src/**/obj/**/*.fs")

let testsCodeGlob =
    !!(rootDirectory
       </> "tests/**/*.fs")
    ++ (rootDirectory
        </> "tests/**/*.fsx")
    -- (rootDirectory
        </> "tests/**/obj/**/*.fs")

let srcGlob =
    rootDirectory
    </> "src/**/*.??proj"

let testsGlob =
    rootDirectory
    </> "tests/**/*.??proj"

let srcAndTest =
    !!srcGlob
    ++ testsGlob

let distDir =
    rootDirectory
    </> "dist"

let distGlob =
    distDir
    </> "*.nupkg"

let coverageThresholdPercent = 80

let coverageReportDir =
    rootDirectory
    </> "docs"
    </> "coverage"


let docsDir =
    rootDirectory
    </> "docs"

let docsSrcDir =
    rootDirectory
    </> "docsSrc"

let temp =
    rootDirectory
    </> "temp"

let watchDocsDir =
    temp
    </> "watch-docs"

let gitOwner = "MyGithubUsername"
let gitRepoName = "MyLib.1"

let gitHubRepoUrl = sprintf "https://github.com/%s/%s/" gitOwner gitRepoName

let documentationUrl = sprintf "https://%s.github.io/%s/" gitOwner gitRepoName

let releaseBranch = "MyReleaseBranch"
let readme = "README.md"
let changelogFile = "CHANGELOG.md"

let READMElink = Uri(Uri(gitHubRepoUrl), $"blob/{releaseBranch}/{readme}")
let CHANGELOGlink = Uri(Uri(gitHubRepoUrl), $"blob/{releaseBranch}/{changelogFile}")

let changelogPath =
    rootDirectory
    </> changelogFile

let changelog = Fake.Core.Changelog.load changelogPath

let mutable latestEntry =
    if Seq.isEmpty changelog.Entries then
        Changelog.ChangelogEntry.New("0.0.1", "0.0.1-alpha.1", Some DateTime.Today, None, [], false)
    else
        changelog.LatestEntry

let mutable changelogBackupFilename = ""

let publishUrl = "https://www.nuget.org"

let enableCodeCoverage = environVarAsBoolOrDefault "ENABLE_COVERAGE" false

let githubToken = Environment.environVarOrNone "GITHUB_TOKEN"

let nugetToken = Environment.environVarOrNone "NUGET_TOKEN"

//-----------------------------------------------------------------------------
// Helpers
//-----------------------------------------------------------------------------


let isRelease (targets: Target list) =
    targets
    |> Seq.map (fun t -> t.Name)
    |> Seq.exists ((=) "PublishToNuGet")

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


let isCI = lazy environVarAsBoolOrDefault "CI" false

// CI Servers can have bizzare failures that have nothing to do with your code
let rec retryIfInCI times fn =
    match isCI.Value with
    | true ->
        if times > 1 then
            try
                fn ()
            with _ ->
                retryIfInCI (times - 1) fn
        else
            fn ()
    | _ -> fn ()

let failOnWrongBranch () =
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

    let sourcelink optionConfig args = tool optionConfig "sourcelink" args

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


module DocsTool =
    let quoted s = $"\"%s{s}\""

    let fsDocsDotnetOptions (o: DotNet.Options) =
        { o with
            WorkingDirectory = rootDirectory
        }

    let fsDocsBuildParams configuration (p: Fsdocs.BuildCommandParams) =
        { p with
            Clean = Some true
            Input = Some(quoted docsSrcDir)
            Output = Some(quoted docsDir)
            Eval = Some true
            Projects = Some(Seq.map quoted (!!srcGlob))
            Properties = Some($"Configuration=%s{configuration}")
            Parameters =
                Some [
                    // https://fsprojects.github.io/FSharp.Formatting/content.html#Templates-and-Substitutions
                    "root", quoted documentationUrl
                    "fsdocs-collection-name", quoted productName
                    "fsdocs-repository-branch", quoted releaseBranch
                    "fsdocs-package-version", quoted latestEntry.NuGetVersion
                    "fsdocs-readme-link", quoted (READMElink.ToString())
                    "fsdocs-release-notes-link", quoted (CHANGELOGlink.ToString())
                ]
            Strict = Some true
        }


    let cleanDocsCache () = Fsdocs.cleanCache rootDirectory

    let build (configuration) =
        Fsdocs.build fsDocsDotnetOptions (fsDocsBuildParams configuration)


    let watch (configuration) =
        let buildParams bp =
            let bp =
                Option.defaultValue Fsdocs.BuildCommandParams.Default bp
                |> fsDocsBuildParams configuration

            { bp with
                Output = Some watchDocsDir
                Strict = None
            }

        Fsdocs.watch
            fsDocsDotnetOptions
            (fun p ->
                { p with
                    BuildCommandParams = Some(buildParams p.BuildCommandParams)
                }
            )

let allReleaseChecks () =
    failOnWrongBranch ()
    Changelog.failOnEmptyChangelog latestEntry


let failOnLocalBuild () =
    if not isCI.Value then
        failwith "Not on CI. If you want to publish, please use CI."

let failOnCIBuild () =
    if isCI.Value then
        failwith "On CI. If you want to run this target, please use a local build."

let allPublishChecks () =
    failOnLocalBuild ()
    Changelog.failOnEmptyChangelog latestEntry

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
                (fun c ->
                    { c with
                        Common =
                            c.Common
                            |> DotNet.Options.withCustomParams (Some(args))
                    }
                )
                dir
    )
    |> Seq.iter (retryIfInCI 10)

let updateChangelog ctx =
    latestEntry <- Changelog.updateChangelog changelogPath changelog gitHubRepoUrl ctx

let revertChangelog _ =
    if String.isNotNullOrEmpty Changelog.changelogBackupFilename then
        Changelog.changelogBackupFilename
        |> Shell.copyFile changelogPath

let deleteChangelogBackupFile _ =
    if String.isNotNullOrEmpty Changelog.changelogBackupFilename then
        Shell.rm Changelog.changelogBackupFilename


let dotnetBuild ctx =
    let args = [
        sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
        "--no-restore"
    ]

    DotNet.build
        (fun c ->
            { c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                Common =
                    c.Common
                    |> DotNet.Options.withAdditionalArgs args

            }
        )
        sln

let fsharpAnalyzers _ =
    let argParser =
        ArgumentParser.Create<FSharpAnalyzers.Arguments>(programName = "fsharp-analyzers")

    !!srcGlob
    |> Seq.iter (fun proj ->
        let args =
            [
                FSharpAnalyzers.Analyzers_Path(
                    rootDirectory
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

    let isGenerateCoverageReport = ctx.Context.TryFindTarget("GenerateCoverageReport").IsSome

    let args = [
        "--no-build"
        if enableCodeCoverage || isGenerateCoverageReport then
            sprintf "/p:AltCover=true"
            if not isGenerateCoverageReport then
                sprintf "/p:AltCoverThreshold=%d" coverageThresholdPercent
            sprintf "/p:AltCoverAssemblyExcludeFilter=%s" excludeCoverage
            "/p:AltCoverLocalSource=true"
    ]

    DotNet.test
        (fun c ->

            { c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                Common =
                    c.Common
                    |> DotNet.Options.withAdditionalArgs args
            }
        )
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

let showCoverageReport _ =
    failOnCIBuild ()
    coverageReportDir </> "index.html"
    |> Command.ShellCommand
    |> CreateProcess.fromCommand
    |> Proc.start
    |> ignore


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

    !!srcGlob
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
    let releaseNotes =
        Changelog.mkReleaseNotes changelog latestEntry gitHubRepoUrl

    let args = [
        $"/p:PackageVersion={latestEntry.NuGetVersion}"
        $"/p:PackageReleaseNotes=\"{releaseNotes}\""
    ]

    DotNet.pack
        (fun c ->
            { c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                OutputPath = Some distDir
                Common =
                    c.Common
                    |> DotNet.Options.withAdditionalArgs args
            }
        )
        sln

let sourceLinkTest _ =
    !!distGlob
    |> Seq.iter (fun nupkg -> dotnet.sourcelink id (sprintf "test %s" nupkg))

let publishToNuget _ =
    allPublishChecks ()

    Paket.push (fun c ->
        { c with
            ToolType = ToolType.CreateLocalTool()
            PublishUrl = publishUrl
            WorkingDir = "dist"
            ApiKey =
                match nugetToken with
                | Some s -> s
                | _ -> c.ApiKey // assume paket-config was set properly
        }
    )

let gitRelease _ =
    allReleaseChecks ()

    let releaseNotesGitCommitFormat = latestEntry.ToString()

    Git.Staging.stageFile "" "CHANGELOG.md"
    |> ignore

    !!(rootDirectory </> "src/**/AssemblyInfo.fs")
    ++ (rootDirectory </> "tests/**/AssemblyInfo.fs")
    |> Seq.iter (
        Git.Staging.stageFile ""
        >> ignore
    )

    let msg = sprintf "Bump version to %s\n\n%s" latestEntry.NuGetVersion releaseNotesGitCommitFormat
    Git.Commit.exec "" msg

    Target.deactivateBuildFailure "RevertChangelog"

    Git.Branches.push ""

    let tag = Changelog.tagFromVersionNumber latestEntry.NuGetVersion

    Git.Branches.tag "" tag
    Git.Branches.pushTag "" "origin" tag

let githubRelease _ =
    allPublishChecks ()

    let token =
        match githubToken with
        | Some s -> s
        | _ ->
            failwith
                "please set the github_token environment variable to a github personal access token with repo access."

    let files = !!distGlob
    // Get release notes with properly-linked version number

    let releaseNotes =
        Changelog.mkReleaseNotes changelog latestEntry gitHubRepoUrl

    GitHub.createClientWithToken token
    |> GitHub.draftNewRelease
        gitOwner
        gitRepoName
        (Changelog.tagFromVersionNumber latestEntry.NuGetVersion)
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

let cleanDocsCache _ = DocsTool.cleanDocsCache ()

let buildDocs ctx =
    let configuration = configuration (ctx.Context.AllExecutingTargets)
    DocsTool.build (string configuration)

let watchDocs ctx =
    let configuration = configuration (ctx.Context.AllExecutingTargets)
    DocsTool.watch (string configuration)


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
    Target.create "ShowCoverageReport" showCoverageReport
    Target.create "WatchTests" watchTests
    Target.create "GenerateAssemblyInfo" generateAssemblyInfo
    Target.create "DotnetPack" dotnetPack
    Target.create "SourceLinkTest" sourceLinkTest
    Target.create "PublishToNuGet" publishToNuget
    Target.create "GitRelease" gitRelease
    Target.create "GitHubRelease" githubRelease
    Target.create "FormatCode" formatCode
    Target.create "CheckFormatCode" checkFormatCode
    Target.create "Release" ignore // For local
    Target.create "Publish" ignore //For CI
    Target.create "CleanDocsCache" cleanDocsCache
    Target.create "BuildDocs" buildDocs
    Target.create "WatchDocs" watchDocs

    //-----------------------------------------------------------------------------
    // Target Dependencies
    //-----------------------------------------------------------------------------


    // Only call Clean if DotnetPack was in the call chain
    // Ensure Clean is called before DotnetRestore
    "Clean"
    ?=>! "DotnetRestore"

    "Clean"
    ==>! "DotnetPack"

    // Only call GenerateAssemblyInfo if GitRelease was in the call chain
    // Ensure GenerateAssemblyInfo is called after DotnetRestore and before DotnetBuild
    "DotnetRestore"
    ?=>! "GenerateAssemblyInfo"

    "GenerateAssemblyInfo"
    ?=>! "DotnetBuild"

    // Ensure UpdateChangelog is called after DotnetRestore
    "DotnetRestore"
    ?=>! "UpdateChangelog"

    "UpdateChangelog"
    ?=>! "GenerateAssemblyInfo"

    "CleanDocsCache"
    ==>! "BuildDocs"

    "DotnetBuild"
    ?=>! "BuildDocs"

    "DotnetBuild"
    ==>! "BuildDocs"


    "DotnetBuild"
    ==>! "WatchDocs"

    "DotnetTest"
    ==> "GenerateCoverageReport"
    ==>! "ShowCoverageReport"

    "UpdateChangelog"
    ==> "GenerateAssemblyInfo"
    ==> "GitRelease"
    ==>! "Release"


    "DotnetRestore"
    ==> "CheckFormatCode"
    ==> "DotnetBuild"
    ==> "DotnetTest"
    ==> "DotnetPack"
    ==> "PublishToNuGet"
    ==> "GitHubRelease"
    ==>! "Publish"

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
