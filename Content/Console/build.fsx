#load ".fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif
open System
open Fake.SystemHelper
open Fake.Core
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api
open Fake.BuildServer
open Fantomas
open Fantomas.FakeHelpers

BuildServer.install [
    AppVeyor.Installer
    Travis.Installer
]

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
        truthyConsts |> List.exists((=)envvar)
    with
    | _ ->  defaultValue

//-----------------------------------------------------------------------------
// Metadata and Configuration
//-----------------------------------------------------------------------------

let productName = "MyLib.1"
let sln = "MyLib.1.sln"

let src = __SOURCE_DIRECTORY__  @@ "src"

let srcCodeGlob =
    !! ( src  @@ "**/*.fs")
    ++ ( src  @@ "**/*.fsx")

let testsCodeGlob =
    !! (__SOURCE_DIRECTORY__  @@ "tests/**/*.fs")
    ++ (__SOURCE_DIRECTORY__  @@ "tests/**/*.fsx")

let srcGlob = src @@ "**/*.??proj"
let testsGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"

let mainApp = src @@ productName

let srcAndTest =
    !! srcGlob
    ++ testsGlob

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob =
    !! (distDir @@ "*.zip")
    ++ (distDir @@ "*.tgz")
    ++ (distDir @@ "*.tar.gz")

let coverageThresholdPercent = 1
let coverageReportDir =  __SOURCE_DIRECTORY__  @@ "docs" @@ "coverage"

let gitOwner = "MyGithubUsername"
let gitRepoName = "MyLib.1"

let gitHubRepoUrl = sprintf "https://github.com/%s/%s" gitOwner gitRepoName

let releaseBranch = "master"

let tagFromVersionNumber versionNumber = sprintf "v%s" versionNumber

let changelogFilename = "CHANGELOG.md"
let changelog = Fake.Core.Changelog.load changelogFilename
let mutable latestEntry =
    if Seq.isEmpty changelog.Entries
    then Changelog.ChangelogEntry.New("0.0.1", "0.0.1-alpha.1", Some DateTime.Today, None, [], false)
    else changelog.LatestEntry
let mutable linkReferenceForLatestEntry = ""

let targetFramework =  "netcoreapp3.1"

// RuntimeIdentifiers: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
// dotnet-packaging Tasks: https://github.com/qmfrederik/dotnet-packaging/blob/0c8e063ada5ba0de2b194cd3fad8308671b48092/Packaging.Targets/build/Packaging.Targets.targets
let runtimes = [
    "linux-x64", "CreateTarball"
    "osx-x64", "CreateTarball"
    "win-x64", "CreateZip"
]

let disableCodeCoverage = environVarAsBoolOrDefault "DISABLE_COVERAGE" false


//-----------------------------------------------------------------------------
// Helpers
//-----------------------------------------------------------------------------
let invokeAsync f = async { f () }

let isRelease (targets : Target list) =
    targets
    |> Seq.map(fun t -> t.Name)
    |> Seq.exists ((=)"Release")


let isReleaseBranchCheck () =
    if Git.Information.getBranchName "" <> releaseBranch then failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch

let configuration (targets : Target list) =
    let defaultVal = if isRelease targets then "Release" else "Debug"
    match Environment.environVarOrDefault "CONFIGURATION" defaultVal with
    | "Debug" -> DotNet.BuildConfiguration.Debug
    | "Release" -> DotNet.BuildConfiguration.Release
    | config -> DotNet.BuildConfiguration.Custom config

let failOnBadExitAndPrint (p : ProcessResult) =
    if p.ExitCode <> 0 then
        p.Errors |> Seq.iter Trace.traceError
        failwithf "failed with exitcode %d" p.ExitCode

let rec retryIfInCI times fn =
    match Environment.environVarOrNone "CI" with
    | Some _ ->
        if times > 1 then
            try
                fn()
            with
            | _ -> retryIfInCI (times - 1) fn
        else
            fn()
    | _ -> fn()

let isEmptyChange = function
    | Changelog.Change.Added s
    | Changelog.Change.Changed s
    | Changelog.Change.Deprecated s
    | Changelog.Change.Fixed s
    | Changelog.Change.Removed s
    | Changelog.Change.Security s
    | Changelog.Change.Custom (_, s) ->
        String.IsNullOrWhiteSpace s.CleanedText

let mkLinkReference (newVersion : SemVerInfo) (changelog : Changelog.Changelog) =
    if changelog.Entries |> List.isEmpty then
        // No actual changelog entries yet: link reference will just point to the Git tag
        sprintf "[%s]: %s/releases/tag/%s" newVersion.AsString gitHubRepoUrl (tagFromVersionNumber newVersion.AsString)
    else
        let versionTuple version = (version.Major, version.Minor, version.Patch)
        // Changelog entries come already sorted, most-recent first, by the Changelog module
        let prevEntry = changelog.Entries |> List.skipWhile (fun entry -> entry.SemVer.PreRelease.IsSome && versionTuple entry.SemVer = versionTuple newVersion) |> List.tryHead
        let linkTarget =
            match prevEntry with
            | Some entry -> sprintf "%s/compare/%s...%s" gitHubRepoUrl (tagFromVersionNumber entry.SemVer.AsString) (tagFromVersionNumber newVersion.AsString)
            | None -> sprintf "%s/releases/tag/%s" gitHubRepoUrl (tagFromVersionNumber newVersion.AsString)
        sprintf "[%s]: %s" newVersion.AsString linkTarget

let mkReleaseNotes (linkReference : string) (latestEntry : Changelog.ChangelogEntry) =
    if String.isNullOrEmpty linkReference then latestEntry.ToString()
    else
        // Add link reference target to description before building release notes, since in main changelog file it's at the bottom of the file
        let description =
            match latestEntry.Description with
            | None -> linkReference
            | Some desc when desc.Contains(linkReference) -> desc
            | Some desc -> sprintf "%s\n\n%s" (desc.Trim()) linkReference
        { latestEntry with Description = Some description }.ToString()

let getVersionNumber envVarName ctx =
    let args = ctx.Context.Arguments
    let verArg =
        args
        |> List.tryHead
        |> Option.defaultWith (fun () -> Environment.environVarOrDefault envVarName "")
    if SemVer.isValid verArg then verArg
    elif verArg.StartsWith("v") && SemVer.isValid verArg.[1..] then
        let target = ctx.Context.FinalTarget
        Trace.traceImportantfn "Please specify a version number without leading 'v' next time, e.g. \"./build.sh %s %s\" rather than \"./build.sh %s %s\"" target verArg.[1..] target verArg
        verArg.[1..]
    elif String.isNullOrEmpty verArg then
        let target = ctx.Context.FinalTarget
        Trace.traceErrorfn "Please specify a version number, either at the command line (\"./build.sh %s 1.0.0\") or in the %s environment variable" target envVarName
        failwith "No version number found"
    else
        Trace.traceErrorfn "Please specify a valid version number: %A could not be recognized as a version number" verArg
        failwith "Invalid version number"

module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let tool optionConfig command args =
        DotNet.exec optionConfig (sprintf "%s" command) args
        |> failOnBadExitAndPrint

    let reportgenerator optionConfig args =
        tool optionConfig "reportgenerator" args

//-----------------------------------------------------------------------------
// Target Implementations
//-----------------------------------------------------------------------------

let clean _ =
    ["bin"; "temp" ; distDir; coverageReportDir]
    |> Shell.cleanDirs

    !! srcGlob
    ++ testsGlob
    |> Seq.collect(fun p ->
        ["bin";"obj"]
        |> Seq.map(fun sp ->
            IO.Path.GetDirectoryName p @@ sp)
        )
    |> Shell.cleanDirs

    [
        "paket-files/paket.restore.cached"
    ]
    |> Seq.iter Shell.rm

let updateChangelog ctx =
    let description, unreleasedChanges =
        match changelog.Unreleased with
        | None -> None, []
        | Some u -> u.Description, u.Changes
    let verStr = ctx |> getVersionNumber "RELEASE_VERSION"
    let newVersion = SemVer.parse verStr
    if changelog.Entries |> List.exists (fun entry -> entry.SemVer = newVersion) then
        let entry = changelog.Entries |> List.find (fun entry -> entry.SemVer = newVersion)
        Trace.traceErrorfn "Version %s already exists in %s, released on %s" verStr changelogFilename (if entry.Date.IsSome then entry.Date.Value.ToString("yyyy-MM-dd") else "(no date specified)")
        failwith "Can't release with a duplicate version number"
    if changelog.Entries |> List.exists (fun entry -> entry.SemVer > newVersion) then
        let entry = changelog.Entries |> List.find (fun entry -> entry.SemVer > newVersion)
        Trace.traceErrorfn "You're trying to release version %s, but a later version %s already exists, released on %s" verStr entry.SemVer.AsString (if entry.Date.IsSome then entry.Date.Value.ToString("yyyy-MM-dd") else "(no date specified)")
        failwith "Can't release with a version number older than an existing release"
    let versionTuple version = (version.Major, version.Minor, version.Patch)
    let prereleaseEntries = changelog.Entries |> List.filter (fun entry -> entry.SemVer.PreRelease.IsSome && versionTuple entry.SemVer = versionTuple newVersion)
    let prereleaseChanges = prereleaseEntries |> List.collect (fun entry -> entry.Changes |> List.filter (not << isEmptyChange))
    let assemblyVersion, nugetVersion = Changelog.parseVersions newVersion.AsString
    linkReferenceForLatestEntry <- mkLinkReference newVersion changelog
    let newEntry = Changelog.ChangelogEntry.New(assemblyVersion.Value, nugetVersion.Value, Some System.DateTime.Today, description, unreleasedChanges @ prereleaseChanges, false)
    let newChangelog = Changelog.Changelog.New(changelog.Header, changelog.Description, None, newEntry :: changelog.Entries)
    latestEntry <- newEntry
    newChangelog
    |> Changelog.save changelogFilename
    // Changelog.save doesn't write a final newline, so we add one when writing out the new link reference
    sprintf "\n%s\n" linkReferenceForLatestEntry |> File.writeString true changelogFilename

let dotnetRestore _ =
    [sln]
    |> Seq.map(fun dir -> fun () ->
        let args =
            [
                sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
            ]
        DotNet.restore(fun c ->
            { c with
                Common =
                    c.Common
                    |> DotNet.Options.withAdditionalArgs args
            }) dir)
    |> Seq.iter(retryIfInCI 10)

let dotnetBuild ctx =
    let args =
        [
            sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
            "--no-restore"
        ]
    DotNet.build(fun c ->
        { c with
            Configuration = configuration (ctx.Context.AllExecutingTargets)
            Common =
                c.Common
                |> DotNet.Options.withAdditionalArgs args
        }) sln

let dotnetTest ctx =
    let excludeCoverage =
        !! testsGlob
        |> Seq.map IO.Path.GetFileNameWithoutExtension
        |> String.concat "|"
    DotNet.test(fun c ->
        let args =
            [
                "--no-build"
                sprintf "/p:AltCover=%b" (not disableCodeCoverage)
                sprintf "/p:AltCoverThreshold=%d" coverageThresholdPercent
                sprintf "/p:AltCoverAssemblyExcludeFilter=%s" excludeCoverage
            ]
        { c with
            Configuration = configuration (ctx.Context.AllExecutingTargets)
            Common =
                c.Common
                |> DotNet.Options.withAdditionalArgs args
            }) sln

let generateCoverageReport _ =
    let coverageReports =
        !!"tests/**/coverage.*.xml"
        |> String.concat ";"
    let sourceDirs =
        !! srcGlob
        |> Seq.map Path.getDirectory
        |> String.concat ";"
    let independentArgs =
            [
                sprintf "-reports:%s"  coverageReports
                sprintf "-targetdir:%s" coverageReportDir
                // Add source dir
                sprintf "-sourcedirs:%s" sourceDirs
                // Ignore Tests and if AltCover.Recorder.g sneaks in
                sprintf "-assemblyfilters:\"%s\"" "-*.Tests;-AltCover.Recorder.g"
                sprintf "-Reporttypes:%s" "Html"
            ]
    let args =
        independentArgs
        |> String.concat " "
    dotnet.reportgenerator id args

let watchApp _ =

    let appArgs =
        [
            "World"
        ]
        |> String.concat " "
    dotnet.watch
        (fun opt -> opt |> DotNet.Options.withWorkingDirectory (mainApp))
        "run"
        appArgs
    |> ignore

let watchTests _ =
    !! testsGlob
    |> Seq.map(fun proj -> fun () ->
        dotnet.watch
            (fun opt ->
                opt |> DotNet.Options.withWorkingDirectory (IO.Path.GetDirectoryName proj))
            "test"
            ""
        |> ignore
    )
    |> Seq.iter (invokeAsync >> Async.Catch >> Async.Ignore >> Async.Start)

    printfn "Press Ctrl+C (or Ctrl+Break) to stop..."
    let cancelEvent = Console.CancelKeyPress |> Async.AwaitEvent |> Async.RunSynchronously
    cancelEvent.Cancel <- true

let generateAssemblyInfo _ =

    let (|Fsproj|Csproj|Vbproj|) (projFileName:string) =
        match projFileName with
        | f when f.EndsWith("fsproj") -> Fsproj
        | f when f.EndsWith("csproj") -> Csproj
        | f when f.EndsWith("vbproj") -> Vbproj
        | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

    let releaseChannel =
        match latestEntry.SemVer.PreRelease with
        | Some pr -> pr.Name
        | _ -> "release"
    let getAssemblyInfoAttributes projectName =
        [ AssemblyInfo.Title (projectName)
          AssemblyInfo.Product productName
          AssemblyInfo.Version latestEntry.AssemblyVersion
          AssemblyInfo.Metadata("ReleaseDate", latestEntry.Date.Value.ToString("o"))
          AssemblyInfo.FileVersion latestEntry.AssemblyVersion
          AssemblyInfo.InformationalVersion latestEntry.AssemblyVersion
          AssemblyInfo.Metadata("ReleaseChannel", releaseChannel)
          AssemblyInfo.Metadata("GitHash", Git.Information.getCurrentSHA1(null))
        ]

    let getProjectDetails projectPath =
        let projectName = IO.Path.GetFileNameWithoutExtension(projectPath)
        (
            projectPath,
            projectName,
            IO.Path.GetDirectoryName(projectPath),
            (getAssemblyInfoAttributes projectName)
        )

    srcAndTest
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
        match projFileName with
        | Fsproj -> AssemblyInfoFile.createFSharp (folderName @@ "AssemblyInfo.fs") attributes
        | Csproj -> AssemblyInfoFile.createCSharp ((folderName @@ "Properties") @@ "AssemblyInfo.cs") attributes
        | Vbproj -> AssemblyInfoFile.createVisualBasic ((folderName @@ "My Project") @@ "AssemblyInfo.vb") attributes
        )

let createPackages _ =
    runtimes
    |> Seq.iter(fun (runtime, packageType) ->
        let args =
            [
                sprintf "/t:Restore;%s" packageType
                sprintf "/p:TargetFramework=%s" targetFramework
                sprintf "/p:CustomTarget=%s" packageType
                sprintf "/p:RuntimeIdentifier=%s" runtime
                sprintf "/p:Configuration=%s" "Release"
                sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
                sprintf "/p:PackagePath=%s" (distDir @@ (sprintf "%s-%s-%s" productName latestEntry.NuGetVersion runtime ))
            ] |> String.concat " "

        DotNet.exec (fun opt ->
            { opt with
                WorkingDirectory = mainApp }
        ) "msbuild" args
        |> failOnBadExitAndPrint
    )

let gitRelease _ =
    isReleaseBranchCheck ()

    let releaseNotesGitCommitFormat = latestEntry.ToString()

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s\n%s" latestEntry.NuGetVersion releaseNotesGitCommitFormat)
    Git.Branches.push ""

    let tag = tagFromVersionNumber latestEntry.NuGetVersion

    Git.Branches.tag "" tag
    Git.Branches.pushTag "" "origin" tag

let githubRelease _ =
    let token =
        match Environment.environVarOrDefault "GITHUB_TOKEN" "" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> failwith "please set the github_token environment variable to a github personal access token with repo access."

    let files = distGlob
    // Get release notes with properly-linked version number
    let releaseNotes = latestEntry |> mkReleaseNotes linkReferenceForLatestEntry

    GitHub.createClientWithToken token
    |> GitHub.draftNewRelease gitOwner gitRepoName latestEntry.NuGetVersion (latestEntry.SemVer.PreRelease <> None) (releaseNotes |> Seq.singleton)
    |> GitHub.uploadFiles files
    |> GitHub.publishDraft
    |> Async.RunSynchronously

let formatCode _ =
    [
        srcCodeGlob
        testsCodeGlob
    ]
    |> Seq.collect id
    // Ignore AssemblyInfo
    |> Seq.filter(fun f -> f.EndsWith("AssemblyInfo.fs") |> not)
    |> formatFilesAsync FormatConfig.FormatConfig.Default
    |> Async.RunSynchronously
    |> Seq.iter(fun result ->
        match result with
        | Formatted(original, tempfile) ->
            tempfile |> Shell.copyFile original
            Trace.logfn "Formatted %s" original
        | _ -> ()
    )

//-----------------------------------------------------------------------------
// Target Declaration
//-----------------------------------------------------------------------------

Target.create "Clean" clean
Target.create "UpdateChangelog" updateChangelog
Target.create "DotnetRestore" dotnetRestore
Target.create "DotnetBuild" dotnetBuild
Target.create "DotnetTest" dotnetTest
Target.create "GenerateCoverageReport" generateCoverageReport
Target.create "WatchApp" watchApp
Target.create "WatchTests" watchTests
Target.create "AssemblyInfo" generateAssemblyInfo
Target.create "CreatePackages" createPackages
Target.create "GitRelease" gitRelease
Target.create "GitHubRelease" githubRelease
Target.create "FormatCode" formatCode
Target.create "Release" ignore

//-----------------------------------------------------------------------------
// Target Dependencies
//-----------------------------------------------------------------------------

// Only call Clean if DotnetPack was in the call chain
// Ensure Clean is called before DotnetRestore
"Clean" ?=> "DotnetRestore"
"Clean" ==> "CreatePackages"

// Only call UpdateChangelog if Publish was in the call chain
// Ensure UpdateChangelog is called before DotnetRestore
"UpdateChangelog" ?=> "DotnetRestore"
"UpdateChangelog" ==> "GitRelease"

// Only call AssemblyInfo if Publish was in the call chain
// Ensure AssemblyInfo is called after DotnetRestore and before DotnetBuild
"DotnetRestore" ?=> "AssemblyInfo"
"AssemblyInfo" ?=> "DotnetBuild"
"AssemblyInfo" ==> "GitRelease"

"DotnetRestore"
  ==> "DotnetBuild"
  ==> "DotnetTest"
  =?> ("GenerateCoverageReport", not disableCodeCoverage)
  ==> "CreatePackages"
  ==> "GitRelease"
  ==> "GitHubRelease"
  ==> "Release"

"DotnetRestore"
 ==> "WatchTests"

//-----------------------------------------------------------------------------
// Target Start
//-----------------------------------------------------------------------------

Target.runOrDefaultWithArguments "CreatePackages"
