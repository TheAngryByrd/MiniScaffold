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

let release = ReleaseNotes.load "RELEASE_NOTES.md"
let srcGlob = "*.csproj"

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"



let failOnBadExitAndPrint (p : ProcessResult) =
    if p.ExitCode <> 0 then
        p.Errors |> Seq.iter Trace.traceError
        failwithf "failed with exitcode %d" p.ExitCode

Target.create "Clean" <| fun _ ->
    [ "obj" ;"dist"]
    |> Shell.cleanDirs


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


Target.create "IntegrationTests" <| fun _ ->
    // uninstall current MiniScaffold
    DotNet.exec  id
        "new"
        "-u MiniScaffold"
    |> failOnBadExitAndPrint
    // install from dist/
    DotNet.exec  id
        "new"
        (sprintf "-i dist/MiniScaffold.%s.nupkg" release.NugetVersion)
    |> failOnBadExitAndPrint
    [
        "-n MyCoolLib --githubUsername CoolPersonNo2", "DotnetPack"
        // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
        "-n fsharp-data-sample --githubUsername CoolPersonNo2", "DotnetPack"
    ]
    |> Seq.iter(fun (param, testTarget) ->
        use directory = DisposableDirectory.Create()
        use pushd1 = new DisposeablePushd(directory.Directory)
        DotNet.exec (fun commandParams ->
            { commandParams with WorkingDirectory = directory.Directory}
        )
            "new"
            (sprintf "mini-scaffold -lang F# %s" param)
        |> failOnBadExitAndPrint
        use pushd2 =
            directory.DirectoryInfo.GetDirectories ()
            |> Seq.head
            |> string
            |> fun x -> new DisposeablePushd(x)

        if Environment.isUnix then
            Process.execSimple(fun psi ->
                psi
                    .WithWorkingDirectory(pushd2.Directory)
                    .WithFileName("chmod")
                    .WithArguments("+x ./build.sh")

            ) (TimeSpan.FromMinutes(5.))
            |> fun exitCode -> if exitCode <> 0 then failwith "Failed to chmod ./build.sh"

        let exitCode =
            Process.execSimple (fun psi ->
                let psi = psi.WithWorkingDirectory(pushd2.Directory)
                if Environment.isUnix then
                    psi
                        .WithFileName("bash")
                        .WithArguments(sprintf "./build.sh %s" testTarget)
                else
                    psi
                        .WithFileName(IO.Directory.GetCurrentDirectory() @@ "build.cmd")
                        .WithArguments(sprintf "%s" testTarget)

                ) (TimeSpan.FromMinutes(5.))

        if exitCode <> 0 then
            failwithf "Intregration test failed with params %s" param
    )



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
       match Environment.environVarOrDefault "github_token" "" with
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
  ==> "IntegrationTests"
  ==> "Publish"
  ==> "GitRelease"
  ==> "GithubRelease"
  ==> "Release"


Target.runOrDefaultWithArguments "IntegrationTests"
