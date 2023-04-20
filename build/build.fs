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

//-----------------------------------------------------------------------------
// Metadata and Configuration
//-----------------------------------------------------------------------------


let srcGlob = "*.csproj"

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

let testsGlob =
    !!(__SOURCE_DIRECTORY__
       </> ".."
       </> "tests/**/*.??proj")

let distDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "dist"

let distGlob =
    distDir
    </> "*.nupkg"

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

let docsToolProj =
    docsToolDir
    </> "docsTool.fsproj"

let docsSrcGlob =
    docsSrcDir
    </> "**/*.fsx"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"

let contentDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "Content"


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


let gitHubRepoUrl = sprintf "https://github.com/%s/%s" gitOwner gitRepoName
let docsSiteBaseUrl = "https://www.jimmybyrd.me/MiniScaffold"

let isCI = Environment.environVarAsBool "CI"

let githubToken = Environment.environVarOrNone "GITHUB_TOKEN"
let nugetToken = Environment.environVarOrNone "NUGET_TOKEN"

//-----------------------------------------------------------------------------
// Helpers
//-----------------------------------------------------------------------------

let isRelease (targets: Target list) =
    targets
    |> Seq.map (fun t -> t.Name)
    |> Seq.exists ((=) "Release")

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


let dispose (disposable: #IDisposable) = disposable.Dispose()

[<AllowNullLiteral>]
type DisposableDirectory(directory: string) =
    do Trace.tracefn "Created disposable directory %s" directory

    static member Create() =
        let tempPath = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString("n"))

        IO.Directory.CreateDirectory tempPath
        |> ignore

        new DisposableDirectory(tempPath)

    member x.Directory = directory
    member x.DirectoryInfo = IO.DirectoryInfo(directory)

    interface IDisposable with
        member x.Dispose() =
            Trace.tracefn "Deleting directory %s" directory
            IO.Directory.Delete(x.Directory, true)

type DisposeablePushd(directory: string) =
    do Shell.pushd directory
    member x.Directory = directory
    member x.DirectoryInfo = IO.DirectoryInfo(directory)

    interface IDisposable with
        member x.Dispose() = Shell.popd ()

let isReleaseBranchCheck () =
    let releaseBranch = "master"

    if
        Git.Information.getBranchName ""
        <> releaseBranch
    then
        failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch

let invokeAsync f = async { f () }


let allReleaseChecks () =
    isReleaseBranchCheck ()
    Changelog.isChangelogEmpty latestEntry


open DocsTool.CLIArgs

module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let run cmdParam args = DotNet.exec cmdParam "run" args

    let fantomas args = DotNet.exec id "fantomas" args

module DocsTool =
    open Argu
    let buildparser = ArgumentParser.Create<BuildArgs>(programName = "docstool")

    let buildCLI () =
        [
            BuildArgs.SiteBaseUrl docsSiteBaseUrl
            BuildArgs.ProjectGlob docsToolProj
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
            WatchArgs.ProjectGlob docsToolProj
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

//-----------------------------------------------------------------------------
// Target Implementations
//-----------------------------------------------------------------------------

let clean _ =
    [
        "obj"
        "dist"
    ]
    |> Shell.cleanDirs

    Git.CommandHelper.directRunGitCommandAndFail contentDir "clean -xfd"

let ``dotnet restore`` _ =
    !!srcGlob
    |> Seq.iter (fun dir ->
        let args =
            [ sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion ]
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

let ``revert changelog`` _ =
    if String.isNotNullOrEmpty Changelog.changelogBackupFilename then
        Changelog.changelogBackupFilename
        |> Shell.copyFile changelogFilename

let ``delete changelogBackupFile`` _ =
    if String.isNotNullOrEmpty Changelog.changelogBackupFilename then
        Shell.rm Changelog.changelogBackupFilename

let ``update changelog`` ctx =
    latestEntry <- Changelog.updateChangelog changelogFilename changelog gitHubRepoUrl ctx

let formatCode _ =
    let result = dotnet.fantomas "."

    if not result.OK then
        printfn "Errors while formatting all files: %A" result.Messages

let checkFormatCode _ =
    let result = dotnet.fantomas "--check ."

    if result.ExitCode = 0 then
        Trace.log "No files need formatting"
    elif result.ExitCode = 99 then
        failwith "Some files need formatting, check output for more info"
    else
        Trace.logf "Errors while formatting: %A" result.Errors


let ``dotnet pack`` ctx =
    !!srcGlob
    |> Seq.iter (fun proj ->
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
            proj
    )

let getPkgPath () =
    !!distGlob
    |> Seq.head

let ``integration tests`` ctx =
    testsGlob
    |> Seq.iter (fun proj ->

        dotnet.run
            (fun c ->

                let args =
                    [
                        // sprintf "-C %A" (configuration (ctx.Context.AllExecutingTargets))
                        sprintf "--project %s" proj
                        "--summary"
                    ]
                    |> String.concat " "

                {
                    c with
                        CustomParams = Some args
                        Environment =
                            c.Environment
                            |> Map.add "MINISCAFFOLD_NUPKG_LOCATION" (getPkgPath ())
                // Configuration = configuration (ctx.Context.AllExecutingTargets)
                // Common =
                //     c.Common
                //     |> DotNet.Options.withCustomParams
                //         (Some(args))
                }
            )
            ""
        |> failOnBadExitAndPrint
    )


let publish _ =
    allReleaseChecks ()

    Paket.push (fun c -> {
        c with
            ToolType = ToolType.CreateLocalTool()
            PublishUrl = "https://www.nuget.org"
            WorkingDir = "dist"
            ApiKey =
                match nugetToken with
                | Some s -> s
                | _ -> c.ApiKey // assume paket-config was set properly
    })

let ``git release`` _ =
    allReleaseChecks ()

    let releaseNotesGitCommitFormat = latestEntry.ToString()

    Git.Staging.stageFile "" "CHANGELOG.md"
    |> ignore

    !! "Content/**/AssemblyInfo.fs"
    |> Seq.iter (
        Git.Staging.stageFile ""
        >> ignore
    )

    Git.Commit.exec
        ""
        (sprintf "Bump version to %s\n\n%s" latestEntry.NuGetVersion releaseNotesGitCommitFormat)

    Git.Branches.push ""

    let tag = Changelog.tagFromVersionNumber latestEntry.NuGetVersion

    Git.Branches.tag "" tag
    Git.Branches.pushTag "" "origin" tag


let ``github release`` _ =
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
        (Changelog.tagFromVersionNumber latestEntry.NuGetVersion)
        (latestEntry.SemVer.PreRelease
         <> None)
        (releaseNotes
         |> Seq.singleton)
    |> GitHub.uploadFiles files
    |> GitHub.publishDraft
    |> Async.RunSynchronously


let ``build docs`` _ = DocsTool.build ()

let ``watch docs`` _ = DocsTool.watch ()

let ``release docs`` ctx =
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

    /// Defines a dependency - y is dependent on x
    let (==>!) x y =
        x ==> y
        |> ignore

    /// Defines a soft dependency. x must run before y, if it is present, but y does not require x to be run.
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
    Target.create "DotnetRestore" ``dotnet restore``
    Target.create "UpdateChangelog" ``update changelog``
    Target.createBuildFailure "RevertChangelog" ``revert changelog`` // Do NOT put this in the dependency chain
    Target.createFinal "DeleteChangelogBackupFile" ``delete changelogBackupFile`` // Do NOT put this in the dependency chain
    Target.create "DotnetPack" ``dotnet pack``
    Target.create "IntegrationTests" ``integration tests``
    Target.create "PublishToNuGet" publish
    Target.create "GitRelease" ``git release``
    Target.create "GitHubRelease" ``github release``
    Target.create "Release" ignore
    Target.create "BuildDocs" ``build docs``
    Target.create "WatchDocs" ``watch docs``
    Target.create "ReleaseDocs" ``release docs``
    Target.create "FormatCode" formatCode
    Target.create "CheckFormatCode" checkFormatCode
    //-----------------------------------------------------------------------------
    // Target Dependencies
    //-----------------------------------------------------------------------------
    "DotnetPack"
    ==>! "BuildDocs"

    "BuildDocs"
    ==>! "ReleaseDocs"

    "BuildDocs"
    ?=>! "PublishToNuGet"

    "IntegrationTests"
    ?=>! "ReleaseDocs"

    "ReleaseDocs"
    ?=>! "GitRelease"

    "ReleaseDocs"
    ==>! "Release"

    // Only call UpdateChangelog if Publish was in the call chain
    // Ensure UpdateChangelog is called after DotnetRestore and before GenerateAssemblyInfo
    "DotnetRestore"
    ?=>! "UpdateChangelog"

    "UpdateChangelog"
    ?=>! "DotnetPack"

    "UpdateChangelog"
    ==>! "PublishToNuGet"

    "Clean"
    ==> "DotnetRestore"
    ==> "DotnetPack"
    =?> ("CheckFormatCode", isCI)
    =?> ("IntegrationTests", isCI)
    ==> "PublishToNuGet"
    ==> "GitRelease"
    ==> "GithubRelease"
    ==>! "Release"

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
    Target.runOrDefaultWithArguments (if isCI then "IntegrationTests" else "DotnetPack")

    0 // return an integer exit code
