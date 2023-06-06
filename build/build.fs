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

let documentationUrl = "https://www.jimmybyrd.me/MiniScaffold/"

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


let isCI = lazy environVarAsBoolOrDefault "CI" false

// CI Servers can have bizzare failures that have nothing to do with your code
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

module Changelog =

    let isEmptyChange =
        function
        | Changelog.Change.Added s
        | Changelog.Change.Changed s
        | Changelog.Change.Deprecated s
        | Changelog.Change.Fixed s
        | Changelog.Change.Removed s
        | Changelog.Change.Security s
        | Changelog.Change.Custom(_, s) -> String.IsNullOrWhiteSpace s.CleanedText

    let failOnEmptyChangelog () =
        let isEmpty =
            (latestEntry.Changes
             |> Seq.forall isEmptyChange)
            || latestEntry.Changes
               |> Seq.isEmpty

        if isEmpty then
            failwith
                "No changes in CHANGELOG. Please add your changes under a heading specified in https://keepachangelog.com/"

    let mkLinkReference (newVersion: SemVerInfo) (changelog: Changelog.Changelog) =
        if
            changelog.Entries
            |> List.isEmpty
        then
            // No actual changelog entries yet: link reference will just point to the Git tag
            sprintf
                "[%s]: %s/releases/tag/%s"
                newVersion.AsString
                gitHubRepoUrl
                (tagFromVersionNumber newVersion.AsString)
        else
            let versionTuple version =
                (version.Major, version.Minor, version.Patch)
            // Changelog entries come already sorted, most-recent first, by the Changelog module
            let prevEntry =
                changelog.Entries
                |> List.skipWhile (fun entry ->
                    entry.SemVer.PreRelease.IsSome
                    && versionTuple entry.SemVer = versionTuple newVersion
                )
                |> List.tryHead

            let linkTarget =
                match prevEntry with
                | Some entry ->
                    sprintf
                        "%s/compare/%s...%s"
                        gitHubRepoUrl
                        (tagFromVersionNumber entry.SemVer.AsString)
                        (tagFromVersionNumber newVersion.AsString)
                | None ->
                    sprintf
                        "%s/releases/tag/%s"
                        gitHubRepoUrl
                        (tagFromVersionNumber newVersion.AsString)

            sprintf "[%s]: %s" newVersion.AsString linkTarget

    let mkReleaseNotes (latestEntry: Changelog.ChangelogEntry) =
        let linkReference = mkLinkReference latestEntry.SemVer changelog

        if String.isNullOrEmpty linkReference then
            latestEntry.ToString()
        else
            // Add link reference target to description before building release notes, since in main changelog file it's at the bottom of the file
            let description =
                match latestEntry.Description with
                | None -> linkReference
                | Some desc when desc.Contains(linkReference) -> desc
                | Some desc -> sprintf "%s\n\n%s" (desc.Trim()) linkReference

            {
                latestEntry with
                    Description = Some description
            }
                .ToString()

    let getVersionNumber envVarName ctx =
        let args = ctx.Context.Arguments

        let verArg =
            args
            |> List.tryHead
            |> Option.defaultWith (fun () -> Environment.environVarOrDefault envVarName "")

        if SemVer.isValid verArg then
            verArg
        elif
            verArg.StartsWith("v")
            && SemVer.isValid verArg.[1..]
        then
            let target = ctx.Context.FinalTarget

            Trace.traceImportantfn
                "Please specify a version number without leading 'v' next time, e.g. \"./build.sh %s %s\" rather than \"./build.sh %s %s\""
                target
                verArg.[1..]
                target
                verArg

            verArg.[1..]
        elif String.isNullOrEmpty verArg then
            let target = ctx.Context.FinalTarget

            Trace.traceErrorfn
                "Please specify a version number, either at the command line (\"./build.sh %s 1.0.0\") or in the %s environment variable"
                target
                envVarName

            failwith "No version number found"
        else
            Trace.traceErrorfn
                "Please specify a valid version number: %A could not be recognized as a version number"
                verArg

            failwith "Invalid version number"


module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let run cmdParam args = DotNet.exec cmdParam "run" args

    let tool optionConfig command args =
        DotNet.exec optionConfig (sprintf "%s" command) args
        |> failOnBadExitAndPrint

    let fcswatch optionConfig args = tool optionConfig "fcswatch" args

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
                    "root", quoted documentationUrl
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
    Changelog.failOnEmptyChangelog ()


let failOnLocalBuild () =
    if not isCI.Value then
        failwith "Not on CI. If you want to publish, please use CI."

let allPublishChecks () =
    failOnLocalBuild ()
    Changelog.failOnEmptyChangelog ()

//-----------------------------------------------------------------------------
// Target Implementations
//-----------------------------------------------------------------------------


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

    [ "paket-files/paket.restore.cached" ]
    |> Seq.iter Shell.rm

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
                        Common =
                            c.Common
                            |> DotNet.Options.withCustomParams (Some(args))
                })
                dir
    )
    |> Seq.iter (retryIfInCI 10)

let updateChangelog ctx =
    let description, unreleasedChanges =
        match changelog.Unreleased with
        | None -> None, []
        | Some u -> u.Description, u.Changes

    let verStr =
        ctx
        |> Changelog.getVersionNumber "RELEASE_VERSION"

    let newVersion = SemVer.parse verStr

    changelog.Entries
    |> List.tryFind (fun entry -> entry.SemVer = newVersion)
    |> Option.iter (fun entry ->
        Trace.traceErrorfn
            "Version %s already exists in %s, released on %s"
            verStr
            changelogPath
            (if entry.Date.IsSome then
                 entry.Date.Value.ToString("yyyy-MM-dd")
             else
                 "(no date specified)")

        failwith "Can't release with a duplicate version number"
    )

    changelog.Entries
    |> List.tryFind (fun entry -> entry.SemVer > newVersion)
    |> Option.iter (fun entry ->
        Trace.traceErrorfn
            "You're trying to release version %s, but a later version %s already exists, released on %s"
            verStr
            entry.SemVer.AsString
            (if entry.Date.IsSome then
                 entry.Date.Value.ToString("yyyy-MM-dd")
             else
                 "(no date specified)")

        failwith "Can't release with a version number older than an existing release"
    )

    let versionTuple version =
        (version.Major, version.Minor, version.Patch)

    let prereleaseEntries =
        changelog.Entries
        |> List.filter (fun entry ->
            entry.SemVer.PreRelease.IsSome
            && versionTuple entry.SemVer = versionTuple newVersion
        )

    let prereleaseChanges =
        prereleaseEntries
        |> List.collect (fun entry ->
            entry.Changes
            |> List.filter (
                not
                << Changelog.isEmptyChange
            )
        )
        |> List.distinct

    let assemblyVersion, nugetVersion = Changelog.parseVersions newVersion.AsString

    let newEntry =
        Changelog.ChangelogEntry.New(
            assemblyVersion.Value,
            nugetVersion.Value,
            Some System.DateTime.Today,
            description,
            unreleasedChanges
            @ prereleaseChanges,
            false
        )

    let newChangelog =
        Changelog.Changelog.New(
            changelog.Header,
            changelog.Description,
            None,
            newEntry
            :: changelog.Entries
        )

    latestEntry <- newEntry

    // Save changelog to temporary file before making any edits
    changelogBackupFilename <- System.IO.Path.GetTempFileName()

    changelogPath
    |> Shell.copyFile changelogBackupFilename

    Target.activateFinal "DeleteChangelogBackupFile"

    newChangelog
    |> Changelog.save changelogPath

    // Now update the link references at the end of the file
    let linkReferenceForLatestEntry = Changelog.mkLinkReference newVersion changelog

    let linkReferenceForUnreleased =
        sprintf
            "[Unreleased]: %s/compare/%s...%s"
            gitHubRepoUrl
            (tagFromVersionNumber newVersion.AsString)
            "HEAD"

    let tailLines =
        File.read changelogPath
        |> List.ofSeq
        |> List.rev

    let isRef (line: string) =
        System.Text.RegularExpressions.Regex.IsMatch(line, @"^\[.+?\]:\s?[a-z]+://.*$")

    let linkReferenceTargets =
        tailLines
        |> List.skipWhile String.isNullOrWhiteSpace
        |> List.takeWhile isRef
        |> List.rev // Now most recent entry is at the head of the list

    let newLinkReferenceTargets =
        match linkReferenceTargets with
        | [] -> [
            linkReferenceForUnreleased
            linkReferenceForLatestEntry
          ]
        | first :: rest when
            first
            |> String.startsWith "[Unreleased]:"
            ->
            linkReferenceForUnreleased
            :: linkReferenceForLatestEntry
            :: rest
        | first :: rest ->
            linkReferenceForUnreleased
            :: linkReferenceForLatestEntry
            :: first
            :: rest

    let blankLineCount =
        tailLines
        |> Seq.takeWhile String.isNullOrWhiteSpace
        |> Seq.length

    let linkRefCount =
        linkReferenceTargets
        |> List.length

    let skipCount =
        blankLineCount
        + linkRefCount

    let updatedLines =
        List.rev (
            tailLines
            |> List.skip skipCount
        )
        @ newLinkReferenceTargets

    File.write false changelogPath updatedLines

    // If build fails after this point but before we commit changes, undo our modifications
    Target.activateBuildFailure "RevertChangelog"

let revertChangelog _ =
    if String.isNotNullOrEmpty changelogBackupFilename then
        changelogBackupFilename
        |> Shell.copyFile changelogPath

let deleteChangelogBackupFile _ =
    if String.isNotNullOrEmpty changelogBackupFilename then
        Shell.rm changelogBackupFilename

let dotnetBuild ctx =
    let args = [
        sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
        "--no-restore"
    ]

    DotNet.build
        (fun c -> {
            c with
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
    let releaseNotes =
        latestEntry
        |> Changelog.mkReleaseNotes

    let args = [
        $"/p:PackageVersion={latestEntry.NuGetVersion}"
        $"/p:PackageReleaseNotes=\"{releaseNotes}\""
    ]

    !!srcGlob
    |> Seq.iter (fun proj ->
        DotNet.pack
            (fun c -> {
                c with
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

    Paket.push (fun c -> {
        c with
            ToolType = ToolType.CreateLocalTool()
            PublishUrl = publishUrl
            WorkingDir = "dist"
            ApiKey =
                match nugetToken with
                | Some s -> s
                | _ -> c.ApiKey // assume paket-config was set properly
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

    let releaseNotes =
        latestEntry
        |> Changelog.mkReleaseNotes

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
    let result =
        [ testsCodeGlob ]
        |> Seq.collect id
        // Ignore AssemblyInfo
        |> Seq.filter (fun f ->
            f.EndsWith("AssemblyInfo.fs")
            |> not
        )
        |> String.concat " "
        |> dotnet.fantomas

    if not result.OK then
        printfn "Errors while formatting all files: %A" result.Messages

let checkFormatCode _ =
    let result =
        [ testsCodeGlob ]
        |> Seq.collect id
        // Ignore AssemblyInfo
        |> Seq.filter (fun f ->
            f.EndsWith("AssemblyInfo.fs")
            |> not
        )
        |> String.concat " "
        |> sprintf "%s --check"
        |> dotnet.fantomas

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
    ?=>! "UpdateChangelog"

    "UpdateChangelog"
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
    ==> "CheckFormatCode"
    ==> "DotnetBuild"
    ==> "DotnetPack"
    ==> "IntegrationTests"
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
    Target.runOrDefaultWithArguments (if isCI.Value then "IntegrationTests" else "DotnetPack")

    0 // return an integer exit code
