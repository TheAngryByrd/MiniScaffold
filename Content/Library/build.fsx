#load ".fake/build.fsx/intellisense.fsx"
#load "docsTool/CLI.fs"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif
open System
open System.Text.RegularExpressions
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


let srcCodeGlob =
    !! (__SOURCE_DIRECTORY__  @@ "src/**/*.fs")
    ++ (__SOURCE_DIRECTORY__  @@ "src/**/*.fsx")

let testsCodeGlob =
    !! (__SOURCE_DIRECTORY__  @@ "tests/**/*.fs")
    ++ (__SOURCE_DIRECTORY__  @@ "tests/**/*.fsx")

let srcGlob =__SOURCE_DIRECTORY__  @@ "src/**/*.??proj"
let testsGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"

let srcAndTest =
    !! srcGlob
    ++ testsGlob

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"

let coverageThresholdPercent = 80
let coverageReportDir =  __SOURCE_DIRECTORY__  @@ "docs" @@ "coverage"


let docsDir = __SOURCE_DIRECTORY__  @@ "docs"
let docsSrcDir = __SOURCE_DIRECTORY__  @@ "docsSrc"
let docsToolDir = __SOURCE_DIRECTORY__ @@ "docsTool"

let gitOwner = "MyGithubUsername"
let gitRepoName = "MyLib.1"

let gitHubRepoUrl = sprintf "https://github.com/%s/%s" gitOwner gitRepoName

let releaseBranch = "master"

let changelogFilename = "CHANGELOG.md"
let changelog = Fake.Core.Changelog.load changelogFilename
let mutable latestEntry = changelog.LatestEntry

let publishUrl = "https://www.nuget.org"

let docsSiteBaseUrl = sprintf "https://%s.github.io/%s" gitOwner gitRepoName

let disableCodeCoverage = environVarAsBoolOrDefault "DISABLE_COVERAGE" false

//-----------------------------------------------------------------------------
// Helpers
//-----------------------------------------------------------------------------

let isRelease (targets : Target list) =
    targets
    |> Seq.map(fun t -> t.Name)
    |> Seq.exists ((=)"Release")

let invokeAsync f = async { f () }

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

// CI Servers can have bizzare failures that have nothing to do with your code
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

let isReleaseBranchCheck () =
    if Git.Information.getBranchName "" <> releaseBranch then failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch


module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let run cmdParam args =
        DotNet.exec cmdParam "run" args

    let tool optionConfig command args =
        DotNet.exec optionConfig (sprintf "%s" command) args
        |> failOnBadExitAndPrint

    let reportgenerator optionConfig args =
        tool optionConfig "reportgenerator" args

    let sourcelink optionConfig args =
        tool optionConfig "sourcelink" args

    let fcswatch optionConfig args =
        tool optionConfig "fcswatch" args

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
        dotnet.run (fun args ->
            { args with WorkingDirectory = docsToolDir }
        ) (sprintf " -- build %s" (buildCLI()))
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

    let watch projectpath =
        dotnet.watch (fun args ->
           { args with WorkingDirectory = docsToolDir }
        ) "run" (sprintf "-- watch %s" (watchCLI()))
        |> failOnBadExitAndPrint

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
        |> Seq.map(fun sp -> IO.Path.GetDirectoryName p @@ sp ))
    |> Shell.cleanDirs

    [
        "paket-files/paket.restore.cached"
    ]
    |> Seq.iter Shell.rm

let dotnetRestore _ =
    [sln]
    |> Seq.map(fun dir -> fun () ->
        let args =
            [
                sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
            ] |> String.concat " "
        DotNet.restore(fun c ->
            { c with
                Common =
                    c.Common
                    |> DotNet.Options.withCustomParams
                        (Some(args))
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
    let args =
        [
            "--no-build"
            sprintf "/p:AltCover=%b" (not disableCodeCoverage)
            sprintf "/p:AltCoverThreshold=%d" coverageThresholdPercent
            sprintf "/p:AltCoverAssemblyExcludeFilter=%s" excludeCoverage
        ]
    DotNet.test(fun c ->

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
        [
            AssemblyInfo.Title (projectName)
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

let dotnetPack ctx =
    let args =
        [
            sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
            sprintf "/p:PackageReleaseNotes=\"%s\"" (latestEntry.ToString())
        ]
    DotNet.pack (fun c ->
        { c with
            Configuration = configuration (ctx.Context.AllExecutingTargets)
            OutputPath = Some distDir
            Common =
                c.Common
                |> DotNet.Options.withAdditionalArgs args
        }) sln

let sourceLinkTest _ =
    !! distGlob
    |> Seq.iter (fun nupkg ->
        dotnet.sourcelink id (sprintf "test %s" nupkg)
    )

let publishToNuget _ =
    isReleaseBranchCheck ()
    Paket.push(fun c ->
        { c with
            ToolType = ToolType.CreateLocalTool()
            PublishUrl = publishUrl
            WorkingDir = "dist"
        }
    )

let gitRelease _ =
    isReleaseBranchCheck ()

    let releaseNotesGitCommitFormat = latestEntry.ToString()

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s \n%s" latestEntry.NuGetVersion releaseNotesGitCommitFormat)
    Git.Branches.push ""

    Git.Branches.tag "" latestEntry.NuGetVersion
    Git.Branches.pushTag "" "origin" latestEntry.NuGetVersion

let githubRelease _ =
    let token =
        match Environment.environVarOrDefault "GITHUB_TOKEN" "" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> failwith "please set the github_token environment variable to a github personal access token with repro access."

    let files = !! distGlob

    GitHub.createClientWithToken token
    |> GitHub.draftNewRelease gitOwner gitRepoName latestEntry.NuGetVersion (latestEntry.SemVer.PreRelease <> None) (latestEntry.ToString() |> Seq.singleton)
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


let buildDocs _ =
    DocsTool.build ()

let watchDocs _ =
    let watchBuild () =
        !! srcGlob
        |> Seq.map(fun proj -> fun () ->
            dotnet.watch
                (fun opt ->
                    opt |> DotNet.Options.withWorkingDirectory (IO.Path.GetDirectoryName proj))
                "build"
                ""
            |> ignore
        )
        |> Seq.iter (invokeAsync >> Async.Catch >> Async.Ignore >> Async.Start)
    watchBuild ()
    DocsTool.watch ()

let releaseDocs ctx =
    isReleaseBranchCheck ()

    Git.Staging.stageAll docsDir
    Git.Commit.exec "" (sprintf "Documentation release of version %s" latestEntry.NuGetVersion)
    if isRelease (ctx.Context.AllExecutingTargets) |> not then
        // We only want to push if we're only calling "ReleaseDocs" target
        // If we're calling "Release" target, we'll let the "GitRelease" target do the git push
        Git.Branches.push ""

let getLinkReference s =
        let m = Regex.Match(s, @"^\[(.+)\]:\s?([a-z]+://.*)$")
        if m.Success && m.Groups.Count > 2 then
            let versionStr = m.Groups.[1].Value
            let url = m.Groups.[2].Value
            Some url  // Could be Some (versionStr, url) if we end up wanting to preserve the associated version, but probably not
        else
            None

let findLinkReferences (change : Changelog.Change) =
    let parts = change.ToString().Split([|": "|], 2, StringSplitOptions.None)
    Trace.tracefn "Parts: %A" parts
    if parts.Length < 2 then None
    else
        let header = parts.[0]
        parts.[1] |> getLinkReference |> Option.map (fun s -> Some header, s)

let addLinkReferenceToChangesOrDescription (linkReference : string) (changes : Changelog.Change list) (description : string option) =
    let links =
        if not (changes |> List.isEmpty) then
            changes |> List.choose findLinkReferences
        else
            match description with
            | Some s ->
                let lines = s.Split([|String.WindowsLineBreaks; String.LinuxLineBreaks; String.MacLineBreaks|], StringSplitOptions.None)
                lines |> Array.choose getLinkReference |> List.ofArray |> List.map (fun link -> (None, link))
            | None ->
                []  // A completely empty entry means we won't find any links here
    if links |> List.isEmpty then
        changes, description  // If we can't tell whereChangelog.Change.New to add the links, don't add them
    else
        // Find the header with the most links, which should be the one to add the new one to
        let header = links |> List.countBy fst |> List.sortByDescending snd |> List.tryHead |> Option.bind fst
        match header with
        | Some section ->
            let newChanges = changes @ [Changelog.Change.New(section, linkReference)]
            newChanges, description
        | None ->  // No header means it was in the description
            let newDescription =
                match description with
                | Some desc -> sprintf "%s\n%s" (desc.TrimEnd()) linkReference
                | None -> linkReference
            changes, Some newDescription

let isEmptyChange = function
    | Changelog.Change.Added s
    | Changelog.Change.Changed s
    | Changelog.Change.Deprecated s
    | Changelog.Change.Fixed s
    | Changelog.Change.Removed s
    | Changelog.Change.Security s
    | Changelog.Change.Custom (_, s) ->
        String.IsNullOrWhiteSpace s.CleanedText

let addLinkReference (newVersion : SemVerInfo) (changelog : Changelog.Changelog) =
    if changelog.Entries |> List.isEmpty then
        // No actual changelog entries yet: Add the link reference to the Unreleased section, if one exists; if one doesn't, then create one with a "## Changed" section
        let linkReference = sprintf "[%s]: %s/releases/tag/v%s" newVersion.AsString gitHubRepoUrl newVersion.AsString
        match changelog.Unreleased with
        | None ->
            { changelog with Unreleased = Changelog.Unreleased.New(None, [Changelog.Change.New("Changed", linkReference)]) }
        | Some u ->
            let changes, description = addLinkReferenceToChangesOrDescription linkReference u.Changes u.Description
            { changelog with Unreleased = Changelog.Unreleased.New(description, changes) }
    else
        let versionTuple version = (version.Major, version.Minor, version.Patch)
        // Changelog entries come already sorted, most-recent first, by the Changelog module
        let prevEntry = changelog.Entries |> List.skipWhile (fun entry -> entry.SemVer.PreRelease.IsSome && versionTuple entry.SemVer = versionTuple newVersion) |> List.tryHead
        let linkTarget =
            match prevEntry with
            | Some entry -> sprintf "%s/compare/v%s...v%s" gitHubRepoUrl entry.SemVer.AsString newVersion.AsString
            | None -> sprintf "%s/releases/tag/v%s" gitHubRepoUrl newVersion.AsString
        let linkReference = sprintf "[%s]: %s" newVersion.AsString linkTarget
        let oldestEntry = changelog.Entries |> List.last
        let changes, description = addLinkReferenceToChangesOrDescription linkReference oldestEntry.Changes oldestEntry.Description
        let newEntry = { oldestEntry with Changes = changes; Description = description }
        let entries = (newEntry :: (changelog.Entries |> List.rev |> List.tail)) |> List.rev  // This replaces the last entry
        { changelog with Entries = entries }

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
        Trace.traceErrorfn "Please specify a version number, either at the command line (\"./build.sh %s 1.0.0\") or in the VERSION_NUMBER environment variable" target
        failwith "No version number found"
    else
        Trace.traceErrorfn "Please specify a valid version number: %A could not be recognized as a version number" verArg
        failwith "Invalid version number"

let promoteChangelog ctx =
    let verStr = ctx |> getVersionNumber "VERSION_NUMBER" // TODO: Pick a name for the environment variable
    let newVersion = SemVer.parse verStr
    let entries = changelog.Entries
    let versionTuple version = (version.Major, version.Minor, version.Patch)
    let entriesMatchingThis = entries |> List.filter (fun entry -> entry.SemVer.PreRelease.IsSome && versionTuple entry.SemVer = versionTuple newVersion)
    let desciption, changesForThisVersion =
        match changelog.Unreleased with
        | None -> None, []
        | Some u -> u.Description, u.Changes
        // Or:
        // match changelog.Unreleased with
        // | None -> sprintf "Released version %s" newVersion.AsString |> Some, []
        // | Some u -> sprintf "Released version %s\n\n%s" newVersion.AsString u.Description.Value |> Some, u.Changes
    let allChanges = entriesMatchingThis |> List.collect (fun entry -> entry.Changes |> List.filter (not << isEmptyChange))
    let assemblyVersion, nugetVersion = Changelog.parseVersions newVersion.AsString
    let newMinimalEntry = Changelog.ChangelogEntry.New(assemblyVersion.Value, nugetVersion.Value, Some System.DateTime.Today, desciption, changesForThisVersion, false)
    let newVerboseEntry = Changelog.ChangelogEntry.New(assemblyVersion.Value, nugetVersion.Value, Some System.DateTime.Today, desciption, changesForThisVersion @ allChanges, false)
    let newChangelog = Changelog.Changelog.New(changelog.Header, changelog.Description, None, newMinimalEntry :: changelog.Entries)
    // Or: let newChangelog = Changelog.Changelog.New(changelog.Header, changelog.Description, None, newVerboseEntry :: changelog.Entries)
    latestEntry <- newVerboseEntry
    newChangelog
    |> addLinkReference newVersion
    |> Changelog.save changelogFilename


//-----------------------------------------------------------------------------
// Target Declaration
//-----------------------------------------------------------------------------

Target.create "Clean" clean
Target.create "DotnetRestore" dotnetRestore
Target.create "DotnetBuild" dotnetBuild
Target.create "DotnetTest" dotnetTest
Target.create "GenerateCoverageReport" generateCoverageReport
Target.create "WatchTests" watchTests
Target.create "GenerateAssemblyInfo" generateAssemblyInfo
Target.create "DotnetPack" dotnetPack
Target.create "SourceLinkTest" sourceLinkTest
Target.create "PublishToNuGet" publishToNuget
Target.create "GitRelease" gitRelease
Target.create "GitHubRelease" githubRelease
Target.create "FormatCode" formatCode
Target.create "Release" ignore
Target.create "BuildDocs" buildDocs
Target.create "WatchDocs" watchDocs
Target.create "ReleaseDocs" releaseDocs

//-----------------------------------------------------------------------------
// Target Dependencies
//-----------------------------------------------------------------------------


// Only call Clean if DotnetPack was in the call chain
// Ensure Clean is called before DotnetRestore
"Clean" ?=> "DotnetRestore"
"Clean" ==> "DotnetPack"

// Only call AssemblyInfo if Publish was in the call chain
// Ensure AssemblyInfo is called after DotnetRestore and before DotnetBuild
"DotnetRestore" ?=> "GenerateAssemblyInfo"
"GenerateAssemblyInfo" ?=> "DotnetBuild"
"GenerateAssemblyInfo" ==> "PublishToNuGet"

"DotnetBuild" ==> "BuildDocs"
"BuildDocs" ==> "ReleaseDocs"
"BuildDocs" ?=> "PublishToNuget"
"DotnetPack" ?=> "BuildDocs"
"GenerateCoverageReport" ?=> "ReleaseDocs"

"DotnetBuild" ==> "WatchDocs"

"DotnetRestore"
    ==> "DotnetBuild"
    ==> "DotnetTest"
    =?> ("GenerateCoverageReport", not disableCodeCoverage)
    ==> "DotnetPack"
    ==> "SourceLinkTest"
    ==> "PublishToNuGet"
    ==> "GitRelease"
    ==> "GitHubRelease"
    ==> "Release"

"DotnetRestore"
    ==> "WatchTests"

//-----------------------------------------------------------------------------
// Target Start
//-----------------------------------------------------------------------------

Target.runOrDefaultWithArguments "DotnetPack"
