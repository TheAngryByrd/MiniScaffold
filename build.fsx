#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open System
open Fake.Tools

let release = ReleaseNotes.load "RELEASE_NOTES.md"
let srcGlob = "*.csproj"

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"

Target.create "Clean" (fun _ ->
    [ "obj" ;"dist"]
    |> Shell.CleanDirs
    )

Target.create "DotnetRestore" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNet.restore (fun c ->
            { c with
                Common = c.Common |> DotNet.Options.withCustomParams (Some(sprintf "/p:PackageVersion=%s" release.NugetVersion)) }) proj
))

Target.create "DotnetPack" (fun _ ->
    let customArgs = DotNet.Options.withCustomParams (Some (sprintf "/p:PackageVersion=%s /p:PackageReleaseNotes=\"%s\"" release.NugetVersion (release.Notes |> String.concat "\n")))
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNet.pack (fun c ->
            { c with
                Configuration = DotNet.BuildConfiguration.Release
                OutputPath = IO.Directory.GetCurrentDirectory() @@ "dist" |> Some
                Common = c.Common |> customArgs
            }) proj
    )
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

Target.create "IntegrationTests" (fun _ ->
    // uninstall current MiniScaffold
    DotNet.exec id "new" "-u MiniScaffold" |> ignore
    // install from dist/
    DotNet.exec id "new" (sprintf "-i dist/MiniScaffold.%s.nupkg" release.NugetVersion) |> ignore

    [
        "-n MyCoolLib --githubUsername CoolPersonNo2", "DotnetPack"
        // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
        "-n fsharp-data-sample --githubUsername CoolPersonNo2", "DotnetPack"
    ]
    |> Seq.iter(fun (param, testTarget) ->
        use directory = DisposableDirectory.Create()
        use pushd1 = new DisposeablePushd(directory.Directory)

        DotNet.exec (DotNet.Options.withWorkingDirectory directory.Directory) "new" (sprintf "mini-scaffold -lang F# %s" param)
        |> ignore

        use pushd2 =
            directory.DirectoryInfo.GetDirectories ()
            |> Seq.head
            |> string
            |> fun x -> new DisposeablePushd(x)

        Git.Repository.init pushd2.Directory false false
        Git.Staging.stageAll pushd2.Directory
        Git.CommandHelper.directRunGitCommandAndFail pushd2.Directory (sprintf "remote add origin https://github.com/CoolPersonNo2/%s" gitRepoName)
        Git.Commit.exec pushd2.Directory "demo commit to make sourcelink work"
        let ok =

            Process.execWithResult (fun psi ->
                { psi with
                    WorkingDirectory = Environment.CurrentDirectory
                    FileName = if Environment.isMono then "./build.sh" else Environment.CurrentDirectory @@ "build.cmd"
                    Arguments = sprintf "%s -nc" testTarget }
                ) (TimeSpan.FromMinutes(5.))

        if not ok.OK then
            failwithf "Intregration test failed with params %s. Logs:\n%s" param (ok.Results |> Seq.map (fun r -> r.Message) |> String.concat "\n")
    )

)

Target.create "Publish" (fun _ ->
    Paket.push(fun c ->
            { c with
                PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )
)

Target.create "GitRelease" (fun _ ->

    if Git.Information.getBranchName "" <> "master" then failwith "Not on master"

    let releaseNotesGitCommitFormat = ("",release.Notes |> Seq.map(sprintf "* %s\n")) |> String.Join

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s \n%s" release.NugetVersion releaseNotesGitCommitFormat)
    Git.Branches.push ""

    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" "origin" release.NugetVersion
)

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target.create "GitHubRelease" (fun _ ->
    let client =
        match Environment.GetEnvironmentVariable "GITHUB_TOKEN" with
        | null ->
            let user =
                match Environment.environVarOrDefault "github-user" String.Empty with
                | s when not (String.IsNullOrWhiteSpace s) -> s
                | _ -> Fake.UserInputHelper.getUserInput "Username: "
            let pw =
                match Environment.environVarOrDefault "github-pw" String.Empty with
                | s when not (String.IsNullOrWhiteSpace s) -> s
                | _ -> Fake.UserInputHelper.getUserPassword "Password: "

            createClient user pw
        | token -> createClientWithToken token


    client
    |> createDraft gitOwner gitRepoName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    |> fun draft ->
        !! distGlob
        |> Seq.fold (fun draft pkg -> draft |> uploadFile pkg) draft
    |> releaseDraft
    |> Async.RunSynchronously

)

Target.create "Release" Target.DoNothing

open Fake.Core.TargetOperators
"Clean"
  ==> "DotnetRestore"
  ==> "DotnetPack"
  ==> "IntegrationTests"
  ==> "Publish"
  ==> "GitRelease"
  ==> "GithubRelease"
  ==> "Release"

Target.runOrDefault "IntegrationTests"
