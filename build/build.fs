module Build

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

let environVarAsBoolOrDefault varName defaultValue =
    let truthyConsts = [
        "1"
        "Y"
        "YES"
        "T"
        "TRUE"
    ]

    try
        let envvar = (Environment.environVar varName).ToUpper()

        truthyConsts
        |> List.exists ((=) envvar)
    with _ ->
        defaultValue

//-----------------------------------------------------------------------------
// Metadata and Configuration
//-----------------------------------------------------------------------------

let rootDirectory =
    __SOURCE_DIRECTORY__
    </> ".."

let productName = "MiniScaffold"

let sln =
    rootDirectory
    </> "MiniScaffold.sln"


let testsCodeGlob =
    !!(rootDirectory
       </> "tests/**/*.fs")
    ++ (rootDirectory
        </> "tests/**/*.fsx")
    -- (rootDirectory
        </> "tests/**/obj/**/*.fs")

let srcGlob =
    rootDirectory
    </> "*.csproj"

let testsGlob =
    rootDirectory
    </> "tests/**/*.??proj"

let srcAndTest =
    !!srcGlob
    ++ testsGlob

let distDir =
    rootDirectory
    </> "dist"

let distGlob =
    distDir
    </> "*.nupkg"

let docsDir =
    rootDirectory
    </> "docs"

let docsSrcDir =
    rootDirectory
    </> "docsSrc"

let temp =
    rootDirectory
    </> "temp"

let watchDocsDir =
    temp
    </> "watch-docs"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"

let gitHubRepoUrl = sprintf "https://github.com/%s/%s/" gitOwner gitRepoName

let documentationRootUrl = "https://www.jimmybyrd.me/MiniScaffold/"

let releaseBranch = "master"
let readme = "README.md"
let changelogFile = "CHANGELOG.md"

let tagFromVersionNumber versionNumber = sprintf "v%s" versionNumber

let READMElink = Uri(Uri(gitHubRepoUrl), $"blob/{releaseBranch}/{readme}")
let CHANGELOGlink = Uri(Uri(gitHubRepoUrl), $"blob/{releaseBranch}/{changelogFile}")

let LICENSElink = Uri(Uri(gitHubRepoUrl), $"blob/{releaseBranch}/LICENSE.md")

let changelogPath =
    rootDirectory
    </> changelogFile

let changelog = Fake.Core.Changelog.load changelogPath

let mutable latestEntry =
    if Seq.isEmpty changelog.Entries then
        Changelog.ChangelogEntry.New("0.0.1", "0.0.1-alpha.1", Some DateTime.Today, None, [], false)
    else
        changelog.LatestEntry

let mutable changelogBackupFilename = ""

let publishUrl = "https://www.nuget.org"

let docsSiteBaseUrl = sprintf "https://%s.github.io/%s" gitOwner gitRepoName

let githubToken = Environment.environVarOrNone "GITHUB_TOKEN"

let nugetToken = Environment.environVarOrNone "NUGET_TOKEN"

//-----------------------------------------------------------------------------
// Helpers
//-----------------------------------------------------------------------------


let isRelease (targets: Target list) =
    targets
    |> Seq.map (fun t -> t.Name)
    |> Seq.exists ((=) "PublishToNuGet")

let invokeAsync f = async { f () }

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


let isCI = lazy environVarAsBoolOrDefault "CI" true

// CI Servers can have bizarre failures that have nothing to do with your code
let rec retryIfInCI times fn =
    match isCI.Value with
    | true ->
        if times > 1 then
            try
                fn ()
            with _ ->
                retryIfInCI (times - 1) fn
        else
            fn ()
    | _ -> fn ()

let failOnWrongBranch () =
    if
        Git.Information.getBranchName ""
        <> releaseBranch
    then
        failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch

module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let run cmdParam args = DotNet.exec cmdParam "run" args

    let tool optionConfig command args =
        DotNet.exec optionConfig (sprintf "%s" command) args
        |> failOnBadExitAndPrint

    let fantomas args = DotNet.exec id "fantomas" args

module DocsTool =
    let quoted s = $"\"%s{s}\""

    let fsDocsDotnetOptions (o: DotNet.Options) = {
        o with
            WorkingDirectory = rootDirectory
    }

    let fsDocsBuildParams configuration (p: Fsdocs.BuildCommandParams) = {
        p with
            Clean = Some true
            Input = Some(quoted docsSrcDir)
            Output = Some(quoted docsDir)
            Eval = Some true
            //Projects = Some(Seq.map quoted (!!srcGlob))
            Properties = Some($"Configuration=%s{configuration}")
            Parameters =
                Some [
                    // https://fsprojects.github.io/FSharp.Formatting/content.html#Templates-and-Substitutions
                    "root", quoted documentationRootUrl
                    "fsdocs-collection-name", quoted productName
                    "fsdocs-repository-branch", quoted releaseBranch
                    "fsdocs-repository-link", quoted (gitHubRepoUrl)
                    "fsdocs-package-version", quoted latestEntry.NuGetVersion
                    "fsdocs-readme-link", quoted (READMElink.ToString())
                    "fsdocs-release-notes-link", quoted (CHANGELOGlink.ToString())
                    "fsdocs-license-link", quoted (LICENSElink.ToString())
                ]
            IgnoreProjects = Some true
            NoApiDocs = Some true
            Strict = Some true
    }


    let cleanDocsCache () = Fsdocs.cleanCache rootDirectory

    let build (configuration) =
        Fsdocs.build fsDocsDotnetOptions (fsDocsBuildParams configuration)


    let watch (configuration) =
        let buildParams bp =
            let bp =
                Option.defaultValue Fsdocs.BuildCommandParams.Default bp
                |> fsDocsBuildParams configuration

            {
                bp with
                    Output = Some watchDocsDir
                    Strict = None
            }

        Fsdocs.watch
            fsDocsDotnetOptions
            (fun p -> {
                p with
                    BuildCommandParams = Some(buildParams p.BuildCommandParams)
            })

let allReleaseChecks () =
    failOnWrongBranch ()
    Changelog.failOnEmptyChangelog latestEntry


let failOnLocalBuild () =
    if not isCI.Value then
        failwith "Not on CI. If you want to publish, please use CI."

let allPublishChecks () =
    failOnLocalBuild ()
    Changelog.failOnEmptyChangelog latestEntry

//-----------------------------------------------------------------------------
// Target Implementations
//-----------------------------------------------------------------------------


/// So we don't require always being on the latest MSBuild.StructuredLogger
let disableBinLog (p: MSBuild.CliArguments) = { p with DisableInternalBinLog = true }

let clean _ =
    [
        "bin"
        "temp"
        distDir
    ]
    |> Shell.cleanDirs

    !!srcGlob
    ++ testsGlob
    |> Seq.collect (fun p ->
        [
            "bin"
            "obj"
        ]
        |> Seq.map (fun sp ->
            IO.Path.GetDirectoryName p
            </> sp
        )
    )
    |> Shell.cleanDirs

let dotnetRestore _ =
    [ sln ]
    |> Seq.map (fun dir ->
        fun () ->
            let args =
                []
                |> String.concat " "

            DotNet.restore
                (fun c -> {
                    c with
                        MSBuildParams = disableBinLog c.MSBuildParams
                        Common =
                            c.Common
                            |> DotNet.Options.withCustomParams (Some(args))
                })
                dir
    )
    |> Seq.iter (retryIfInCI 10)

let updateChangelog ctx =
    latestEntry <- Changelog.updateChangelog changelogPath changelog gitHubRepoUrl ctx

let revertChangelog _ =
    if String.isNotNullOrEmpty Changelog.changelogBackupFilename then
        Changelog.changelogBackupFilename
        |> Shell.copyFile changelogPath

let deleteChangelogBackupFile _ =
    if String.isNotNullOrEmpty Changelog.changelogBackupFilename then
        Shell.rm Changelog.changelogBackupFilename

let dotnetBuild ctx =
    let args = [
        sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
        "--no-restore"
    ]

    DotNet.build
        (fun c -> {
            c with
                MSBuildParams = disableBinLog c.MSBuildParams
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                Common =
                    c.Common
                    |> DotNet.Options.withAdditionalArgs args

        })
        sln

let getPkgPath () =
    !!distGlob
    |> Seq.head

let integrationTests ctx =
    !!testsGlob
    |> Seq.iter (fun proj ->

        dotnet.run
            (fun c ->

                let args =
                    [
                        // sprintf "-C %A" (configuration (ctx.Context.AllExecutingTargets))
                        sprintf "--project %s" proj
                        "--summary"
                        if isCI.Value then
                            "--fail-on-focused-tests"
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

let dotnetPack ctx =
    // Get release notes with properly-linked version number
    let releaseNotes = Changelog.mkReleaseNotes changelog latestEntry gitHubRepoUrl

    let args = [
        $"/p:PackageVersion={latestEntry.NuGetVersion}"
        $"/p:PackageReleaseNotes=\"{releaseNotes}\""
    ]

    !!srcGlob
    |> Seq.iter (fun proj ->
        DotNet.pack
            (fun c -> {
                c with
                    MSBuildParams = disableBinLog c.MSBuildParams
                    Configuration = configuration (ctx.Context.AllExecutingTargets)
                    OutputPath = Some distDir
                    Common =
                        c.Common
                        |> DotNet.Options.withAdditionalArgs args
            })
            proj
    )

let publishToNuget _ =
    allPublishChecks ()

    NuGet.NuGet.NuGetPublish(fun c -> {
        c with
            PublishUrl = "https://www.nuget.org"
            WorkingDir = "dist"
            AccessKey =
                match nugetToken with
                | Some s -> s
                | _ -> c.AccessKey
    })

let gitRelease _ =
    allReleaseChecks ()

    let releaseNotesGitCommitFormat = latestEntry.ToString()

    Git.Staging.stageFile "" "CHANGELOG.md"
    |> ignore

    Git.Commit.exec
        ""
        (sprintf "Bump version to %s\n\n%s" latestEntry.NuGetVersion releaseNotesGitCommitFormat)

    Target.deactivateBuildFailure "RevertChangelog"

    Git.Branches.push ""

    let tag = tagFromVersionNumber latestEntry.NuGetVersion

    Git.Branches.tag "" tag
    Git.Branches.pushTag "" "origin" tag

let githubRelease _ =
    allPublishChecks ()

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
        (tagFromVersionNumber latestEntry.NuGetVersion)
        (latestEntry.SemVer.PreRelease
         <> None)
        (releaseNotes
         |> Seq.singleton)
    |> GitHub.uploadFiles files
    |> GitHub.publishDraft
    |> Async.RunSynchronously

let formatCode _ =
    let result = dotnet.fantomas $"{rootDirectory}"

    if not result.OK then
        printfn "Errors while formatting all files: %A" result.Messages

let checkFormatCode ctx =
    let result = dotnet.fantomas $"{rootDirectory} --check"

    if result.ExitCode = 0 then
        Trace.log "No files need formatting"
    elif result.ExitCode = 99 then
        failwith "Some files need formatting, check output for more info"
    else
        Trace.logf "Errors while formatting: %A" result.Errors


let cleanDocsCache _ = DocsTool.cleanDocsCache ()

let buildDocs ctx =
    let configuration = configuration (ctx.Context.AllExecutingTargets)
    DocsTool.build (string configuration)

let watchDocs ctx =
    let configuration = configuration (ctx.Context.AllExecutingTargets)
    DocsTool.watch (string configuration)


let initTargets () =
    BuildServer.install [ GitHubActions.Installer ]

    /// Defines a dependency - y is dependent on x. Finishes the chain.
    let (==>!) x y =
        x ==> y
        |> ignore

    /// Defines a soft dependency. x must run before y, if it is present, but y does not require x to be run. Finishes the chain.
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
    Target.create "DotnetRestore" dotnetRestore
    Target.create "UpdateChangelog" updateChangelog
    Target.createBuildFailure "RevertChangelog" revertChangelog // Do NOT put this in the dependency chain
    Target.createFinal "DeleteChangelogBackupFile" deleteChangelogBackupFile // Do NOT put this in the dependency chain
    Target.create "DotnetBuild" dotnetBuild
    Target.create "IntegrationTests" integrationTests
    Target.create "DotnetPack" dotnetPack
    Target.create "PublishToNuGet" publishToNuget
    Target.create "GitRelease" gitRelease
    Target.create "GitHubRelease" githubRelease
    Target.create "FormatCode" formatCode
    Target.create "CheckFormatCode" checkFormatCode
    Target.create "Release" ignore // For local
    Target.create "Publish" ignore //For CI
    Target.create "CleanDocsCache" cleanDocsCache
    Target.create "BuildDocs" buildDocs
    Target.create "WatchDocs" watchDocs

    //-----------------------------------------------------------------------------
    // Target Dependencies
    //-----------------------------------------------------------------------------


    // Only call Clean if DotnetPack was in the call chain
    // Ensure Clean is called before DotnetRestore
    "Clean"
    ?=>! "DotnetRestore"

    "Clean"
    ==>! "DotnetPack"

    // Only call UpdateChangelog if GitRelease was in the call chain
    // Ensure UpdateChangelog is called after DotnetRestore and before DotnetBuild
    "DotnetRestore"
    ?=> "UpdateChangelog"
    ?=>! "DotnetBuild"

    "CleanDocsCache"
    ==>! "BuildDocs"

    "DotnetBuild"
    ?=>! "BuildDocs"

    "DotnetBuild"
    ==>! "BuildDocs"

    "DotnetBuild"
    ==>! "WatchDocs"

    "UpdateChangelog"
    ==> "GitRelease"
    ==>! "Release"

    "DotnetRestore"
    =?> ("CheckFormatCode", isCI.Value)
    ==> "DotnetBuild"
    ==>! "DotnetPack"

    "DotnetPack"
    ==>! "IntegrationTests"

    "DotnetPack"
    ==> "PublishToNuGet"
    ==> "GithubRelease"
    ==>! "Publish"


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
    Target.runOrDefaultWithArguments ("DotnetPack")

    0 // return an integer exit code
