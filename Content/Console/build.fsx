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


let release = Fake.Core.ReleaseNotes.load "RELEASE_NOTES.md"
let productName = "MyLib.1"
let sln = "MyLib.1.sln"

let src = __SOURCE_DIRECTORY__  @@ "src"

let srcGlob = src @@ "**/*.??proj"
let testsGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"

let mainApp = src @@ productName

let srcAndTest =
    !! srcGlob
    ++ testsGlob

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"
let toolsDir = __SOURCE_DIRECTORY__  @@ "tools"

let coverageReportDir =  __SOURCE_DIRECTORY__  @@ "docs" @@ "coverage"

let gitOwner = "MyGithubUsername"
let gitRepoName = "MyLib.1"

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

module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let tool optionConfig command args =
        DotNet.exec (fun p -> { p with WorkingDirectory = toolsDir} |> optionConfig ) (sprintf "%s" command) args
        |> failOnBadExitAndPrint

    let fantomas optionConfig args =
        tool optionConfig "fantomas" args

    let reportgenerator optionConfig args =
        tool optionConfig "reportgenerator" args


Target.create "Clean" <| fun _ ->
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

Target.create "DotnetRestore" <| fun _ ->
    Paket.restore(fun p ->
        {p with ToolPath = __SOURCE_DIRECTORY__ </> ".paket" </> (if Environment.isWindows then "paket.exe" else "paket")})

    [sln ; toolsDir]
    |> Seq.map(fun dir -> fun () ->
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
    |> Seq.iter(retryIfInCI 10)

Target.create "DotnetBuild" <| fun ctx ->

    let args =
        [
            sprintf "/p:PackageVersion=%s" release.NugetVersion
            "--no-restore"
        ] |> String.concat " "
    DotNet.build(fun c ->
        { c with
            Configuration = configuration (ctx.Context.AllExecutingTargets)
            Common =
                c.Common
                |> DotNet.Options.withCustomParams
                    (Some(args))
        }) sln


let invokeAsync f = async { f () }

let coverageThresholdPercent = 1

Target.create "DotnetTest" <| fun ctx ->
    let excludeCoverage =
        !! testsGlob
        |> Seq.map IO.Path.GetFileNameWithoutExtension
        |> String.concat "|"
    DotNet.test(fun c ->
        let args =
            [
                "--no-build"
                "/p:AltCover=true"
                sprintf "/p:AltCoverThreshold=%d" coverageThresholdPercent
                sprintf "/p:AltCoverAssemblyExcludeFilter=%s" excludeCoverage
            ] |> String.concat " "
        { c with
            Configuration = configuration (ctx.Context.AllExecutingTargets)
            Common =
                c.Common
                |> DotNet.Options.withCustomParams
                    (Some(args))
            }) sln


Target.create "GenerateCoverageReport" <| fun _ ->
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


Target.create "WatchApp" <| fun _ ->
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


Target.create "WatchTests" <| fun _ ->
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

let (|Fsproj|Csproj|Vbproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)


Target.create "AssemblyInfo" <| fun _ ->
    let releaseChannel =
        match release.SemVer.PreRelease with
        | Some pr -> pr.Name
        | _ -> "release"
    let getAssemblyInfoAttributes projectName =
        [ AssemblyInfo.Title (projectName)
          AssemblyInfo.Product productName
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.Metadata("ReleaseDate", release.Date.Value.ToString("o"))
          AssemblyInfo.FileVersion release.AssemblyVersion
          AssemblyInfo.InformationalVersion release.AssemblyVersion
          AssemblyInfo.Metadata("ReleaseChannel", releaseChannel)
          AssemblyInfo.Metadata("GitHash", Git.Information.getCurrentSHA1(null))
        ]

    let getProjectDetails projectPath =
        let projectName = IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
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

let runtimes = [
    "linux-x64", "CreateTarball"
    "osx-x64", "CreateTarball"
    "win-x64", "CreateZip"
]

Target.create "CreatePackages" <| fun _ ->

    let targetFramework =  "netcoreapp2.2"
    runtimes
    |> Seq.iter(fun (runtime, packageType) ->
        let args =
            [
                sprintf "/t:Restore;%s" packageType
                sprintf "/p:TargetFramework=%s" targetFramework
                sprintf "/p:CustomTarget=%s" packageType
                sprintf "/p:RuntimeIdentifier=%s" runtime
                sprintf "/p:Configuration=%s" "Release"
                sprintf "/p:PackageVersion=%s" release.NugetVersion
                sprintf "/p:PackagePath=%s" (distDir @@ (sprintf "%s-%s-%s" productName release.NugetVersion runtime ))
            ] |> String.concat " "
        let result =
            DotNet.exec (fun opt ->
                { opt with
                    WorkingDirectory = mainApp }
            ) "msbuild" args
        if result.OK |> not then
            result.Errors |> Seq.iter Trace.traceError
            failwith "package creation failed"
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

Target.create "FormatCode" <| fun _ ->
    srcAndTest
    |> Seq.map (IO.Path.GetDirectoryName)
    |> Seq.iter (fun projDir ->
        dotnet.fantomas id (sprintf "--recurse %s" projDir)
    )

Target.create "Release" ignore

// Only call Clean if DotnetPack was in the call chain
// Ensure Clean is called before DotnetRestore
"Clean" ?=> "DotnetRestore"
"Clean" ==> "CreatePackages"

// // Only call AssemblyInfo if Publish was in the call chain
// // Ensure AssemblyInfo is called after DotnetRestore and before DotnetBuild
"DotnetRestore" ?=> "AssemblyInfo"
"AssemblyInfo" ?=> "DotnetBuild"
"AssemblyInfo" ==> "GitRelease"

"DotnetRestore"
  ==> "DotnetBuild"
  ==> "DotnetTest"
  ==> "GenerateCoverageReport"
  ==> "CreatePackages"
  ==> "GitRelease"
  ==> "GitHubRelease"
  ==> "Release"

"DotnetRestore"
 ==> "WatchTests"


Target.runOrDefaultWithArguments "CreatePackages"
