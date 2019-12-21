#load ".fake/build.fsx/intellisense.fsx"
#load "docsTool/CLI.fs"
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

//-----------------------------------------------------------------------------
// Metadata and Configuration
//-----------------------------------------------------------------------------

BuildServer.install [
    AppVeyor.Installer
    Travis.Installer
]

let release = ReleaseNotes.load "RELEASE_NOTES.md"
let srcGlob = "*.csproj"

let testsGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"

let docsDir = __SOURCE_DIRECTORY__ @@ "docs"
let docsSrcDir = __SOURCE_DIRECTORY__ @@ "docsSrc"
let docsToolDir = __SOURCE_DIRECTORY__ @@ "docsTool"
let docsToolProj = docsToolDir @@ "docsTool.fsproj"
let docsSrcGlob = docsSrcDir @@ "**/*.fsx"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"

let contentDir = __SOURCE_DIRECTORY__ @@ "Content"


let gitHubRepoUrl = sprintf "https://github.com/%s/%s" gitOwner gitRepoName
let docsSiteBaseUrl = "https://www.jimmybyrd.me/miniscaffold"

let isCI =  Environment.environVarAsBool "CI"


//-----------------------------------------------------------------------------
// Helpers
//-----------------------------------------------------------------------------

let isRelease (targets : Target list) =
    targets
    |> Seq.map(fun t -> t.Name)
    |> Seq.exists ((=)"Release")

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


let dispose (disposable : #IDisposable) = disposable.Dispose()
[<AllowNullLiteral>]
type DisposableDirectory (directory : string) =
    do
        Trace.tracefn "Created disposable directory %s" directory
    static member Create() =
        let tempPath = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString("n"))
        IO.Directory.CreateDirectory tempPath |> ignore

        new DisposableDirectory(tempPath)
    member x.Directory = directory
    member x.DirectoryInfo = IO.DirectoryInfo(directory)

    interface IDisposable with
        member x.Dispose() =
            Trace.tracefn "Deleting directory %s" directory
            IO.Directory.Delete(x.Directory,true)

type DisposeablePushd (directory : string) =
    do Shell.pushd directory
    member x.Directory = directory
    member x.DirectoryInfo = IO.DirectoryInfo(directory)
    interface IDisposable with
        member x.Dispose() =
            Shell.popd()

let isReleaseBranchCheck () =
    let releaseBranch = "master"
    if Git.Information.getBranchName "" <> releaseBranch then failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch

let invokeAsync f = async { f () }

open DocsTool.CLIArgs

module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let run cmdParam args =
        DotNet.exec cmdParam "run" args

module DocsTool =
    open Argu
    let buildparser = ArgumentParser.Create<BuildArgs>(programName = "docstool")
    let buildCLI =
        [
            BuildArgs.SiteBaseUrl docsSiteBaseUrl
            BuildArgs.ProjectGlob docsToolProj
            BuildArgs.DocsOutputDirectory docsDir
            BuildArgs.DocsSourceDirectory docsSrcDir
            BuildArgs.GitHubRepoUrl gitHubRepoUrl
            BuildArgs.ProjectName gitRepoName
            BuildArgs.ReleaseVersion release.NugetVersion
        ]
        |> buildparser.PrintCommandLineArgumentsFlat

    let build () =
        dotnet.run (fun args ->
            { args with WorkingDirectory = docsToolDir }
        ) (sprintf " -- build %s" (buildCLI))
        |> failOnBadExitAndPrint

    let watchparser = ArgumentParser.Create<WatchArgs>(programName = "docstool")
    let watchCLI =
        [
            WatchArgs.ProjectGlob docsToolProj
            WatchArgs.DocsOutputDirectory docsDir
            WatchArgs.DocsSourceDirectory docsSrcDir
            WatchArgs.GitHubRepoUrl gitHubRepoUrl
            WatchArgs.ProjectName gitRepoName
            WatchArgs.ReleaseVersion release.NugetVersion
        ]
        |> watchparser.PrintCommandLineArgumentsFlat

    let watch projectpath =
        dotnet.watch (fun args ->
           { args with WorkingDirectory = docsToolDir }
        ) "run" (sprintf "-- watch %s" (watchCLI))
        |> failOnBadExitAndPrint

//-----------------------------------------------------------------------------
// Target Implementations
//-----------------------------------------------------------------------------

let clean _ =
    [ "obj" ;"dist"]
    |> Shell.cleanDirs

    Git.CommandHelper.directRunGitCommandAndFail contentDir "clean -xfd"

let ``dotnet restore`` _ =
    !! srcGlob
    |> Seq.iter(fun dir ->
        let args =
            [
                sprintf "/p:PackageVersion=%s" release.NugetVersion
            ] |> String.concat " "
        DotNet.restore(fun c ->
            { c with
                 Common =
                    c.Common
                    |> DotNet.Options.withCustomParams
                        (Some(args))
            }) dir)

let ``dotnet pack`` _ =
    !! srcGlob
    |> Seq.iter (fun proj ->
        let args =
            [
                sprintf "/p:PackageVersion=%s" release.NugetVersion
                sprintf "/p:PackageReleaseNotes=\"%s\"" (release.Notes |> String.concat "\n")
            ] |> String.concat " "
        DotNet.pack (fun c ->
            { c with
                Configuration = DotNet.BuildConfiguration.Release
                OutputPath = Some distDir
                Common =
                    c.Common
                    |> DotNet.Options.withCustomParams (Some args)
            }) proj
    )

let ``integration tests`` ctx =
    !! testsGlob
    |> Seq.iter (fun proj ->
        DotNet.test(fun c ->
            let args =
                [

                ] |> String.concat " "
            { c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                Common =
                    c.Common
                    |> DotNet.Options.withCustomParams
                        (Some(args))
                }) proj)

let publish _ =
    Paket.push(fun c ->
        { c with
            ToolType = ToolType.CreateLocalTool()
            PublishUrl = "https://www.nuget.org"
            WorkingDir = "dist"
        }
    )

let ``git release`` _ =
    isReleaseBranchCheck ()

    let releaseNotesGitCommitFormat = release.Notes |> Seq.map(sprintf "* %s\n") |> String.concat ""

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s \n%s" release.NugetVersion releaseNotesGitCommitFormat)
    Git.Branches.push ""

    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" "origin" release.NugetVersion

let ``github release`` _ =
    let token =
        match Environment.environVarOrDefault "GITHUB_TOKEN" "" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> failwith "please set the github_token environment variable to a github personal access token with repro access."

    let files = !! distGlob

    GitHub.createClientWithToken token
    |> GitHub.draftNewRelease gitOwner gitRepoName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    |> GitHub.uploadFiles files
    |> GitHub.publishDraft
    |> Async.RunSynchronously


let ``build docs`` _ =
    DocsTool.build ()

let ``watch docs`` _ =
    DocsTool.watch ()

let ``release docs`` ctx =
    isReleaseBranchCheck ()

    Git.Staging.stageAll docsDir
    Git.Commit.exec "" (sprintf "Documentation release of version %s" release.NugetVersion)
    if isRelease (ctx.Context.AllExecutingTargets) |> not then
        // We only want to push if we're only calling "ReleaseDocs" target
        // If we're calling "Release" target, we'll let the "GitRelease" target do the git push
        Git.Branches.push ""

//-----------------------------------------------------------------------------
// Target Declaration
//-----------------------------------------------------------------------------

Target.create "Clean" clean
Target.create "DotnetRestore" ``dotnet restore``
Target.create "DotnetPack" ``dotnet pack``
Target.create "IntegrationTests" ``integration tests``
Target.create "Publish" publish
Target.create "GitRelease" ``git release``
Target.create "GitHubRelease" ``github release``
Target.create "Release" ignore
Target.create "BuildDocs" ``build docs``
Target.create "WatchDocs" ``watch docs``
Target.create "ReleaseDocs" ``release docs``

//-----------------------------------------------------------------------------
// Target Dependencies
//-----------------------------------------------------------------------------
"DotnetPack" ==> "BuildDocs"
"BuildDocs" ==> "ReleaseDocs"


"Clean"
  ==> "DotnetRestore"
  ==> "DotnetPack"
//https://github.com/dotnet/templating/issues/1736#issuecomment-464847242
  =?> ("IntegrationTests", isCI)
  ==> "Publish"
  ==> "ReleaseDocs"
  ==> "GitRelease"
  ==> "GithubRelease"
  ==> "Release"

//-----------------------------------------------------------------------------
// Target Start
//-----------------------------------------------------------------------------

Target.runOrDefaultWithArguments (if isCI then "IntegrationTests" else "DotnetPack")
