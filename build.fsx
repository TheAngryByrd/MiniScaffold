#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open System
open System.IO

let release = LoadReleaseNotes "RELEASE_NOTES.md"
let srcGlob = "*.csproj"

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"

Target "Clean" (fun _ ->
    [ "obj" ;"dist"]
    |> CleanDirs
    )

Target "DotnetRestore" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Restore (fun c ->
            { c with
                Project = proj
                //This makes sure that Proj2 references the correct version of Proj1
                AdditionalArgs = [sprintf "/p:PackageVersion=%s" release.NugetVersion]
            })
))



Target "DotnetPack" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Pack (fun c ->
            { c with
                Project = proj
                Configuration = "Release"
                OutputPath = IO.Directory.GetCurrentDirectory() @@ "dist"
                AdditionalArgs =
                    [
                        sprintf "/p:PackageVersion=%s" release.NugetVersion
                        sprintf "/p:PackageReleaseNotes=\"%s\"" (String.Join("\n",release.Notes))
                    ]
            })
    )
)

let dispose (disposable : #IDisposable) = disposable.Dispose()
[<AllowNullLiteral>]
type DisposableDirectory (directory : string) =
    do
        tracefn "Created disposable directory %s" directory
    static member Create() =
        let tempPath = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString("n"))
        IO.Directory.CreateDirectory tempPath |> ignore

        new DisposableDirectory(tempPath)
    member x.Directory = directory
    member x.DirectoryInfo = IO.DirectoryInfo(directory)

    interface IDisposable with
        member x.Dispose() =
            tracefn "Deleting directory %s" directory
            IO.Directory.Delete(x.Directory,true)

type DisposeablePushd (directory : string) =
    do FileUtils.pushd directory
    member x.Directory = directory
    member x.DirectoryInfo = IO.DirectoryInfo(directory)
    interface IDisposable with
        member x.Dispose() =
            FileUtils.popd()


Target "IntegrationTests" (fun _ ->
    // uninstall current MiniScaffold
    DotNetCli.RunCommand id
        "new -u MiniScaffold"
    // install from dist/
    DotNetCli.RunCommand id
        <| sprintf "new -i dist/MiniScaffold.%s.nupkg" release.NugetVersion

    [
        "-n MyCoolLib --githubUsername CoolPersonNo2", "DotnetPack"
        // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
        "-n fsharp-data-sample --githubUsername CoolPersonNo2", "DotnetPack"
    ]
    |> Seq.iter(fun (param, testTarget) ->
        use directory = DisposableDirectory.Create()
        use pushd1 = new DisposeablePushd(directory.Directory)
        DotNetCli.RunCommand (fun commandParams ->
            { commandParams with WorkingDir = directory.Directory}
        )
            <| sprintf "new mini-scaffold -lang F# %s" param
        use pushd2 =
            directory.DirectoryInfo.GetDirectories ()
            |> Seq.head
            |> string
            |> fun x -> new DisposeablePushd(x)

        let ok =
            ProcessHelper.execProcess (fun psi ->
                psi.WorkingDirectory <- Environment.CurrentDirectory
                if isMono then
                    psi.FileName <- "sh"
                    psi.Arguments <- sprintf "./build.sh %s -nc" testTarget
                else
                    psi.FileName <- Environment.CurrentDirectory @@ "build.cmd"
                    psi.Arguments <- sprintf "%s -nc" testTarget

                ) (TimeSpan.FromMinutes(5.))

        if not ok then
            failwithf "Intregration test failed with params %s" param
    )

)

Target "Publish" (fun _ ->
    Paket.Push(fun c ->
            { c with
                PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )
)

Target "GitRelease" (fun _ ->

    if Git.Information.getBranchName "" <> "master" then failwith "Not on master"

    let releaseNotesGitCommitFormat = ("",release.Notes |> Seq.map(sprintf "* %s\n")) |> String.Join

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s \n%s" release.NugetVersion releaseNotesGitCommitFormat)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
)

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "GitHubRelease" (fun _ ->
    let client =
        match Environment.GetEnvironmentVariable "GITHUB_TOKEN" with
        | null ->
            let user =
                match getBuildParam "github-user" with
                | s when not (String.IsNullOrWhiteSpace s) -> s
                | _ -> getUserInput "Username: "
            let pw =
                match getBuildParam "github-pw" with
                | s when not (String.IsNullOrWhiteSpace s) -> s
                | _ -> getUserPassword "Password: "

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

Target "Release" DoNothing

"Clean"
  ==> "DotnetRestore"
  ==> "DotnetPack"
  ==> "IntegrationTests"
  ==> "Publish"
  ==> "GitRelease"
  ==> "GithubRelease"
  ==> "Release"

RunTargetOrDefault "IntegrationTests"
