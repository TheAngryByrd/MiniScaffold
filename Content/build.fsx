#r @"packages/build/FAKE/tools/FakeLib.dll"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.Tools

let release = ReleaseNotes.load "RELEASE_NOTES.md"
let productName = "MyLib"
let sln = "MyLib.sln"
let srcGlob =__SOURCE_DIRECTORY__  @@ "src/**/*.??proj"
let testsGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"
let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"
let toolsDir = __SOURCE_DIRECTORY__  @@ "tools"

let coverageReportDir =  __SOURCE_DIRECTORY__  @@ "docs" @@ "coverage"

let gitOwner = "MyGithubUsername"
let gitRepoName = "MyLib.1"

let configuration =
    match Environment.environVarOrDefault "CONFIGURATION" "Release" with
    | "Debug" -> DotNet.BuildConfiguration.Debug
    | "Release" -> DotNet.BuildConfiguration.Release
    | config -> DotNet.BuildConfiguration.Custom config

Target.create "Clean" (fun _ ->
    ["bin"; "temp" ; distDir]
    |> Shell.CleanDirs

    !! srcGlob
    ++ testsGlob
    |> Seq.collect(fun p ->
        ["bin";"obj"]
        |> Seq.map (fun sp -> System.IO.Path.GetDirectoryName p @@ sp)
    )
    |> Shell.CleanDirs

    )

Target.create "DotnetRestore" (fun _ ->
    [sln ; toolsDir]
    |> Seq.iter(fun dir ->
        DotNet.restore (fun c ->
            { c with Common = c.Common |> DotNet.Options.withCustomParams (Some <| sprintf "/p:PackageVersion=%s" release.NugetVersion) }
        ) dir
    )
)

Target.create "DotnetBuild" (fun _ ->
    DotNet.build (fun c ->
        { c with
            Configuration = configuration
            //This makes sure that Proj2 references the correct version of Proj1
            Common = c.Common |> DotNet.Options.withCustomParams (Some <| sprintf "/p:PackageVersion=%s /p:SourceLinkCreate=true --no-restore" release.NugetVersion)
        }) sln
)

let invokeAsync f = async { f () }

type TargetFramework =
| Full of string
| Core of string

let (|StartsWith|_|) prefix (s: string) =
    if s.StartsWith prefix then Some () else None

let getTargetFramework tf =
    match tf with
    | StartsWith "net4" -> Full tf
    | StartsWith "netcoreapp" -> Core tf
    | _ -> failwithf "Unknown TargetFramework %s" tf

let getTargetFrameworks (doc: Xml.XmlDocument) =
    let multiFrameworks = doc.GetElementsByTagName("TargetFrameworks")
    if multiFrameworks.Count = 0 then
        //  assume that if there is no TargetFrameworks element
        //  then there will be a TargetFramework element instead.
        let tf = doc.GetElementsByTagName("TargetFramework").[0].InnerText
        [|tf|]
    else
        multiFrameworks.[0].InnerText.Split(';')

let getTargetFrameworksFromProjectFile (projFile : string)=
    let doc = System.Xml.XmlDocument()
    doc.Load(projFile)
    doc
    |> getTargetFrameworks
    |> Seq.map getTargetFramework
    |> Seq.toList

let commandForFramework = function
    | Full _ when Environment.isMono -> "mono"
    | Full _ -> "run"
    | Core _ -> "run"

let argsForFramework = function
    | Full t when Environment.isMono -> sprintf "-f %s -c %A --loggerlevel Warn" t configuration
    | Full t
    | Core t -> sprintf "-f %s -c %A" t configuration

let addLogNameParamToArgs tf args =
    let frameworkName =
        match tf with
        | Full t -> t
        | Core t -> t
    sprintf "%s -- --log-name Expecto.%s" args frameworkName

let runTests modifyArgs =
    !! testsGlob
    |> Seq.map(fun proj -> proj, getTargetFrameworksFromProjectFile proj)
    |> Seq.collect(fun (proj, targetFrameworks) ->
        targetFrameworks
        |> Seq.map (fun tf ->
            fun () ->
                DotNet.exec (DotNet.Options.withWorkingDirectory (System.IO.Path.GetDirectoryName proj)) (commandForFramework tf) (tf |> argsForFramework |> modifyArgs |> addLogNameParamToArgs tf)
                |> ignore
        )
    )

Target.create "DotnetTest" (fun _ ->
    runTests (sprintf "%s --no-build")
    |> Seq.iter invoke

)

let execProcAndReturnMessages filename args =
    let args' = args |> String.concat " "
    Process.execWithResult (fun psi -> { psi with FileName = filename; Arguments = args'}) (System.TimeSpan.FromMinutes(1.))
    |> fun result -> result.Results |> List.map (sprintf "%A")

Target "GenerateCoverageReport" (fun _ ->
    let reportGenerator = "packages/build/ReportGenerator/tools/ReportGenerator.exe"
    let coverageReports =
        !!"tests/**/_Reports/MSBuildTest.xml"
        |> String.concat ";"
    let sourceDirs =
        !! srcGlob
        |> Seq.map DirectoryName
        |> String.concat ";"

    let args =
        String.concat " " <|
            [
                sprintf "-reports:%s"  coverageReports
                sprintf "-targetdir:%s" coverageReportDir
                // Add source dir
                sprintf "-sourcedirs:%s" sourceDirs
                // Ignore Tests and if AltCover.Recorder.g sneaks in
                sprintf "-assemblyfilters:%s" "-*.Tests;-AltCover.Recorder.g"
                sprintf "-Reporttypes:%s" "Html"
            ]
    tracefn "%s %s" reportGenerator args
    let exitCode = Shell.Exec(reportGenerator, args = args)
    if exitCode <> 0 then
        failwithf "%s failed with exit code: %d" reportGenerator exitCode
)


Target.create "WatchTests" (fun _ ->
    runTests (sprintf "watch %s")
    |> Seq.iter (invokeAsync >> Async.Catch >> Async.Ignore >> Async.Start)

    printfn "Press Ctrl+C (or Ctrl+Break) to stop..."
    let cancelEvent = System.Console.CancelKeyPress |> Async.AwaitEvent |> Async.RunSynchronously
    cancelEvent.Cancel <- true

    if Environment.isWindows |> not then
        Process.killAllCreatedProcesses ()
    else
        //Hope windows handles this right?
        ()
)


Target.create "AssemblyInfo" (fun _ ->
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
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! srcGlob
    ++ testsGlob
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->
        match projFileName with
        | Fsproj -> AssemblyInfoFile.createFSharp (folderName @@ "AssemblyInfo.fs") attributes
        | Csproj -> AssemblyInfoFile.createCSharp ((folderName @@ "Properties") @@ "AssemblyInfo.cs") attributes
        | Vbproj -> AssemblyInfoFile.createVisualBasic ((folderName @@ "My Project") @@ "AssemblyInfo.vb") attributes
        | _ -> ()
        )
)

Target.create "DotnetPack" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNet.pack (fun c ->
            { c with
                Configuration = configuration
                OutputPath = Some distDir
                Common = c.Common |> DotNet.Options.withCustomParams (Some <| sprintf "/p:PackageVersion=%s /p:PackageReleaseNotes=\"%s\" /p:SourceLinkCreate=true" release.NugetVersion (String.concat "\n" release.Notes))
            }) proj
    )
)

Target.create "SourcelinkTest" (fun _ ->
    !! distGlob
    |> Seq.iter (fun nupkg ->
        DotNet.exec (DotNet.Options.withWorkingDirectory toolsDir) "sourcelink" (sprintf "test %s" nupkg)
        |> ignore
    )
)

let isReleaseBranchCheck () =
    let releaseBranch = "master"
    if Git.Information.getBranchName "" <> releaseBranch then failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch

Target.create "Publish" (fun _ ->
    isReleaseBranchCheck ()

    Paket.push(fun c ->
            { c with
                PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )
)

Target.create "GitRelease" (fun _ ->
    isReleaseBranchCheck ()

    let releaseNotesGitCommitFormat = release.Notes |> Seq.map(sprintf "* %s\n") |> String.concat ""

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
        match Environment.environVarOrNone "GITHUB_TOKEN" with
        | None ->
            let user =
                match Environment.environVarOrDefault "github-user" "" with
                | "" -> Fake.UserInputHelper.getUserInput "Username: "
                | s -> s
            let pw =
                match Environment.environVarOrDefault "github-pw" "" with
                | "" -> Fake.UserInputHelper.getUserPassword "Password: "
                | s -> s

            createClient user pw
        | Some token -> createClientWithToken token


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
// Only call Clean if DotnetPack was in the call chain
// Ensure Clean is called before DotnetRestore
"Clean" ?=> "DotnetRestore"
"Clean" ==> "DotnetPack"

// Only call AssemblyInfo if Publish was in the call chain
// Ensure AssemblyInfo is called after DotnetRestore and before DotnetBuild
"DotnetRestore" ?=> "AssemblyInfo"
"AssemblyInfo" ?=> "DotnetBuild"
"AssemblyInfo" ==> "Publish"

"DotnetRestore"
  ==> "DotnetBuild"
  ==> "DotnetTest"
  ==> "GenerateCoverageReport"
  ==> "DotnetPack"
  ==> "SourcelinkTest"
  ==> "Publish"
  ==> "GitRelease"
  ==> "GitHubRelease"
  ==> "Release"

"DotnetRestore"
 ==> "WatchTests"

Target.runOrDefault "DotnetPack"
