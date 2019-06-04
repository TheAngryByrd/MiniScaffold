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

BuildServer.install [
    AppVeyor.Installer
    Travis.Installer
]

let release = ReleaseNotes.load "RELEASE_NOTES.md"
let srcGlob = "*.csproj"

let testsGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"

let contentDir = __SOURCE_DIRECTORY__ @@ "Content"

let isCI =  Environment.environVarAsBool "CI"

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

Target.create "Clean" <| fun _ ->
    [ "obj" ;"dist"]
    |> Shell.cleanDirs

    Git.CommandHelper.directRunGitCommandAndFail contentDir "clean -xfd"


Target.create "DotnetRestore" <| fun _ ->
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


Target.create "DotnetPack" <| fun _ ->
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


Target.create "IntegrationTests" <| fun ctx ->
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



Target.create "Publish" <| fun _ ->
    Paket.push(fun c ->
            { c with
                PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )


let isReleaseBranchCheck () =
    let releaseBranch = "master"
    if Git.Information.getBranchName "" <> releaseBranch then failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch


Target.create "GitRelease" <| fun _ ->
    isReleaseBranchCheck ()

    let releaseNotesGitCommitFormat = release.Notes |> Seq.map(sprintf "* %s\n") |> String.concat ""

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s \n%s" release.NugetVersion releaseNotesGitCommitFormat)
    Git.Branches.push ""

    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" "origin" release.NugetVersion

Target.create "GitHubRelease" <| fun _ ->
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

Target.create "Release" ignore

"Clean"
  ==> "DotnetRestore"
  ==> "DotnetPack"
//https://github.com/dotnet/templating/issues/1736#issuecomment-464847242
  =?> ("IntegrationTests", isCI)
  ==> "Publish"
  ==> "GitRelease"
  ==> "GithubRelease"
  ==> "Release"


Target.runOrDefaultWithArguments "IntegrationTests"
