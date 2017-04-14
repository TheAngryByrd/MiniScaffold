#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open System

let release = LoadReleaseNotes "RELEASE_NOTES.md"
let srcGlob = "src/**/*.fsproj"
let testsGlob = "tests/**/*.fsproj"

Target "Clean" (fun _ ->
    ["bin"; "temp" ;"dist"]
    |> CleanDirs

    !! srcGlob
    ++ testsGlob
    |> Seq.collect(fun p -> 
        ["bin";"obj"] 
        |> Seq.map(fun sp ->
             IO.Path.GetDirectoryName p @@ sp)
        )
    |> CleanDirs

    )

Target "DotnetRestore" (fun _ ->
    !! srcGlob
    ++ testsGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Restore (fun c ->
            { c with
                Project = proj
                //This makes sure that Proj2 references the correct version of Proj1
                AdditionalArgs = [sprintf "/p:PackageVersion=%s" release.NugetVersion]
            }) 
))

Target "DotnetBuild" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Build (fun c ->
            { c with
                Project = proj
                //This makes sure that Proj2 references the correct version of Proj1
                AdditionalArgs = [sprintf "/p:PackageVersion=%s" release.NugetVersion]
            }) 
))

Target "DotnetTest" (fun _ ->
    !! testsGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Test (fun c ->
            { c with
                Project = proj
                WorkingDir = IO.Path.GetDirectoryName proj
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
                AdditionalArgs = [sprintf "/p:PackageVersion=%s" release.NugetVersion]
            }) 
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

Target "Release" (fun _ ->

    if Git.Information.getBranchName "" <> "master" then failwith "Not on master"

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
)

"Clean"
  ==> "DotnetRestore"
  ==> "DotnetBuild"
  ==> "DotnetTest"
  ==> "DotnetPack"
  ==> "Publish"
  ==> "Release"

RunTargetOrDefault "DotnetPack"