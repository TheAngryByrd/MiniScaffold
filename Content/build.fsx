#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open System

let release = LoadReleaseNotes "RELEASE_NOTES.md"
let productName = "MyLib"
let sln = "MyLib.sln"
let srcGlob =__SOURCE_DIRECTORY__  @@ "src/**/*.??proj"
let testsGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"
let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"
let toolsDir = __SOURCE_DIRECTORY__  @@ "tools"

let gitOwner = "MyGithubUsername"
let gitRepoName = "MyLib"

let configuration =
    EnvironmentHelper.environVarOrDefault "CONFIGURATION" "Release"


let isRelease () =
    Fake.TargetHelper.CurrentTargetOrder
    |> Seq.collect id
    |> Seq.exists ((=)"Release")

Target "Clean" (fun _ ->
    ["bin"; "temp" ; distDir]
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
    [sln ; toolsDir]
    |> Seq.iter(fun dir ->
        DotNetCli.Restore (fun c ->
            { c with
                Project = dir
                //This makes sure that Proj2 references the correct version of Proj1
                AdditionalArgs = [sprintf "/p:PackageVersion=%s" release.NugetVersion]
            }))
)

Target "DotnetBuild" (fun _ ->
    DotNetCli.Build (fun c ->
        { c with
            Project = sln
            Configuration = configuration
            //This makes sure that Proj2 references the correct version of Proj1
            AdditionalArgs =
                [
                    sprintf "/p:PackageVersion=%s" release.NugetVersion
                    sprintf "/p:SourceLinkCreate=%b" (isRelease ())
                    "--no-restore"
                ]
        }))

let invoke f = f ()
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
    let doc = Xml.XmlDocument()
    doc.Load(projFile)
    doc
    |> getTargetFrameworks
    |> Seq.map getTargetFramework
    |> Seq.toList

let selectRunnerForFramework tf =
    let runMono = sprintf "mono -f %s -c %s --loggerlevel Warn"
    let runCore = sprintf "run -f %s -c %s"
    match tf with
    | Full t when isMono-> runMono t configuration
    | Full t -> runCore t configuration
    | Core t -> runCore t configuration

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
        |> Seq.map (fun tf -> fun () ->
            DotNetCli.RunCommand (fun c ->
            { c with
                WorkingDir = IO.Path.GetDirectoryName proj
            }) (tf |> selectRunnerForFramework |> modifyArgs |> addLogNameParamToArgs tf))
    )


Target "DotnetTest" (fun _ ->
    runTests (sprintf "%s --no-build")
    |> Seq.iter invoke

)
let execProcAndReturnMessages filename args =
    let args' = args |> String.concat " "
    ProcessHelper.ExecProcessAndReturnMessages
                (fun psi ->
                    psi.FileName <- filename
                    psi.Arguments <-args'
                ) (TimeSpan.FromMinutes(1.))

let pkill args =
    execProcAndReturnMessages "pkill" args

let killParentsAndChildren processId=
    pkill [sprintf "-P %d" processId]


Target "WatchTests" (fun _ ->
    runTests (sprintf "watch %s")
    |> Seq.iter (invokeAsync >> Async.Catch >> Async.Ignore >> Async.Start)

    printfn "Press Ctrl+C (or Ctrl+Break) to stop..."
    let cancelEvent = Console.CancelKeyPress |> Async.AwaitEvent |> Async.RunSynchronously
    cancelEvent.Cancel <- true

    if isWindows |> not then
        startedProcesses
        |> Seq.iter(fst >> killParentsAndChildren >> ignore )
    else
        //Hope windows handles this right?
        ()
)

Target "AssemblyInfo" (fun _ ->
    let releaseChannel =
        match release.SemVer.PreRelease with
        | Some pr -> pr.Name
        | _ -> "release"
    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product productName
          Attribute.Version release.AssemblyVersion
          Attribute.Metadata("ReleaseDate", release.Date.Value.ToString("o"))
          Attribute.FileVersion release.AssemblyVersion
          Attribute.InformationalVersion release.AssemblyVersion
          Attribute.Metadata("ReleaseChannel", releaseChannel)
          Attribute.Metadata("GitHash", Information.getCurrentSHA1(null))
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
        | Fsproj -> CreateFSharpAssemblyInfo (folderName @@ "AssemblyInfo.fs") attributes
        | Csproj -> CreateCSharpAssemblyInfo ((folderName @@ "Properties") @@ "AssemblyInfo.cs") attributes
        | Vbproj -> CreateVisualBasicAssemblyInfo ((folderName @@ "My Project") @@ "AssemblyInfo.vb") attributes
        | _ -> ()
        )
)

Target "DotnetPack" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Pack (fun c ->
            { c with
                Project = proj
                Configuration = configuration
                OutputPath = distDir
                AdditionalArgs =
                    [
                        sprintf "/p:PackageVersion=%s" release.NugetVersion
                        sprintf "/p:PackageReleaseNotes=\"%s\"" (String.Join("\n",release.Notes))
                        sprintf "/p:SourceLinkCreate=%b" (isRelease ())
                    ]
            })
    )
)

Target "SourcelinkTest" (fun _ ->
    !! distGlob
    |> Seq.iter (fun nupkg ->
        DotNetCli.RunCommand
            (fun p -> { p with WorkingDir = toolsDir} )
            (sprintf "sourcelink test %s" nupkg)
    )
)

let isReleaseBranchCheck () =
    let releaseBranch = "master"
    if Git.Information.getBranchName "" <> releaseBranch then failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch

Target "Publish" (fun _ ->
    isReleaseBranchCheck ()

    Paket.Push(fun c ->
            { c with
                PublishUrl = "https://www.nuget.org"
                WorkingDir = "dist"
            }
        )
)

Target "GitRelease" (fun _ ->
    isReleaseBranchCheck ()

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
  ==> "DotnetPack"
  ==> "SourcelinkTest"
  ==> "Publish"
  ==> "GitRelease"
  ==> "GitHubRelease"
  ==> "Release"

"DotnetRestore"
 ==> "WatchTests"

RunTargetOrDefault "DotnetPack"
