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

//-----------------------------------------------------------------------------
// Metadata and Configuration
//-----------------------------------------------------------------------------


let srcGlob = "*.csproj"

let testsCodeGlob =
    !!(__SOURCE_DIRECTORY__
       </> ".."
       </> "tests/**/*.fs")
    ++ (__SOURCE_DIRECTORY__
        </> ".."
        </> "tests/**/*.fsx")
    -- (__SOURCE_DIRECTORY__
        </> ".."
        </> "tests/**/obj/**/*.fs")

let testsGlob =
    !!(__SOURCE_DIRECTORY__
       </> ".."
       </> "tests/**/*.??proj")

let distDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "dist"

let distGlob =
    distDir
    </> "*.nupkg"

let docsDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "docs"

let docsSrcDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "docsSrc"

let docsToolDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "docsTool"

let docsToolProj =
    docsToolDir
    </> "docsTool.fsproj"

let docsSrcGlob =
    docsSrcDir
    </> "**/*.fsx"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"

let contentDir =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "Content"


let tagFromVersionNumber versionNumber = sprintf "%s" versionNumber

let changelogFilename =
    __SOURCE_DIRECTORY__
    </> ".."
    </> "CHANGELOG.md"

let changelog = Fake.Core.Changelog.load changelogFilename

let mutable latestEntry =
    if Seq.isEmpty changelog.Entries then
        Changelog.ChangelogEntry.New("0.0.1", "0.0.1-alpha.1", Some DateTime.Today, None, [], false)
    else
        changelog.LatestEntry

let mutable linkReferenceForLatestEntry = ""
let mutable changelogBackupFilename = ""


let gitHubRepoUrl = sprintf "https://github.com/%s/%s" gitOwner gitRepoName
let docsSiteBaseUrl = "https://www.jimmybyrd.me/MiniScaffold"

let isCI = Environment.environVarAsBool "CI"

let githubToken = Environment.environVarOrNone "GITHUB_TOKEN"
let nugetToken = Environment.environVarOrNone "NUGET_TOKEN"

//-----------------------------------------------------------------------------
// Helpers
//-----------------------------------------------------------------------------

let isRelease (targets: Target list) =
    targets
    |> Seq.map (fun t -> t.Name)
    |> Seq.exists ((=) "Release")

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


let dispose (disposable: #IDisposable) = disposable.Dispose()

[<AllowNullLiteral>]
type DisposableDirectory(directory: string) =
    do Trace.tracefn "Created disposable directory %s" directory

    static member Create() =
        let tempPath = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString("n"))

        IO.Directory.CreateDirectory tempPath
        |> ignore

        new DisposableDirectory(tempPath)

    member x.Directory = directory
    member x.DirectoryInfo = IO.DirectoryInfo(directory)

    interface IDisposable with
        member x.Dispose() =
            Trace.tracefn "Deleting directory %s" directory
            IO.Directory.Delete(x.Directory, true)

type DisposeablePushd(directory: string) =
    do Shell.pushd directory
    member x.Directory = directory
    member x.DirectoryInfo = IO.DirectoryInfo(directory)

    interface IDisposable with
        member x.Dispose() = Shell.popd ()

let isReleaseBranchCheck () =
    let releaseBranch = "master"

    if
        Git.Information.getBranchName ""
        <> releaseBranch
    then
        failwithf "Not on %s.  If you want to release please switch to this branch." releaseBranch

let invokeAsync f = async { f () }


let isEmptyChange =
    function
    | Changelog.Change.Added s
    | Changelog.Change.Changed s
    | Changelog.Change.Deprecated s
    | Changelog.Change.Fixed s
    | Changelog.Change.Removed s
    | Changelog.Change.Security s
    | Changelog.Change.Custom(_, s) -> String.IsNullOrWhiteSpace s.CleanedText

let isChangelogEmpty () =
    let isEmpty =
        (latestEntry.Changes
         |> Seq.forall isEmptyChange)
        || latestEntry.Changes
           |> Seq.isEmpty

    if isEmpty then
        failwith
            "No changes in CHANGELOG. Please add your changes under a heading specified in https://keepachangelog.com/"

let allReleaseChecks () =
    isReleaseBranchCheck ()
    isChangelogEmpty ()

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

let mkReleaseNotes (linkReference: string) (latestEntry: Changelog.ChangelogEntry) =
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


open DocsTool.CLIArgs

module dotnet =
    let watch cmdParam program args =
        DotNet.exec cmdParam (sprintf "watch %s" program) args

    let run cmdParam args = DotNet.exec cmdParam "run" args

    let fantomas args = DotNet.exec id "fantomas" args

module DocsTool =
    open Argu
    let buildparser = ArgumentParser.Create<BuildArgs>(programName = "docstool")

    let buildCLI () =
        [
            BuildArgs.SiteBaseUrl docsSiteBaseUrl
            BuildArgs.ProjectGlob docsToolProj
            BuildArgs.DocsOutputDirectory docsDir
            BuildArgs.DocsSourceDirectory docsSrcDir
            BuildArgs.GitHubRepoUrl gitHubRepoUrl
            BuildArgs.ProjectName gitRepoName
            BuildArgs.ReleaseVersion latestEntry.NuGetVersion
        ]
        |> buildparser.PrintCommandLineArgumentsFlat

    let build () =
        dotnet.run
            (fun args -> {
                args with
                    WorkingDirectory = docsToolDir
            })
            (sprintf " -- build %s" (buildCLI ()))
        |> failOnBadExitAndPrint

    let watchparser = ArgumentParser.Create<WatchArgs>(programName = "docstool")

    let watchCLI () =
        [
            WatchArgs.ProjectGlob docsToolProj
            WatchArgs.DocsSourceDirectory docsSrcDir
            WatchArgs.GitHubRepoUrl gitHubRepoUrl
            WatchArgs.ProjectName gitRepoName
            WatchArgs.ReleaseVersion latestEntry.NuGetVersion
        ]
        |> watchparser.PrintCommandLineArgumentsFlat

    let watch () =
        dotnet.watch
            (fun args -> {
                args with
                    WorkingDirectory = docsToolDir
            })
            "run"
            (sprintf "-- watch %s" (watchCLI ()))
        |> failOnBadExitAndPrint

//-----------------------------------------------------------------------------
// Target Implementations
//-----------------------------------------------------------------------------

let clean _ =
    [
        "obj"
        "dist"
    ]
    |> Shell.cleanDirs

    Git.CommandHelper.directRunGitCommandAndFail contentDir "clean -xfd"

let ``dotnet restore`` _ =
    !!srcGlob
    |> Seq.iter (fun dir ->
        let args =
            [ sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion ]
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

let ``revert changelog`` _ =
    if String.isNotNullOrEmpty changelogBackupFilename then
        changelogBackupFilename
        |> Shell.copyFile changelogFilename

let ``delete changelogBackupFile`` _ =
    if String.isNotNullOrEmpty changelogBackupFilename then
        Shell.rm changelogBackupFilename

let ``update changelog`` ctx =
    let description, unreleasedChanges =
        match changelog.Unreleased with
        | None -> None, []
        | Some u -> u.Description, u.Changes

    let verStr =
        ctx
        |> getVersionNumber "RELEASE_VERSION"

    let newVersion = SemVer.parse verStr

    changelog.Entries
    |> List.tryFind (fun entry -> entry.SemVer = newVersion)
    |> Option.iter (fun entry ->
        Trace.traceErrorfn
            "Version %s already exists in %s, released on %s"
            verStr
            changelogFilename
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
                << isEmptyChange
            )
        )

    let assemblyVersion, nugetVersion = Changelog.parseVersions newVersion.AsString
    linkReferenceForLatestEntry <- mkLinkReference newVersion changelog

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

    changelogFilename
    |> Shell.copyFile changelogBackupFilename

    Target.activateFinal "DeleteChangelogBackupFile"

    newChangelog
    |> Changelog.save changelogFilename

    // Now update the link references at the end of the file
    linkReferenceForLatestEntry <- mkLinkReference newVersion changelog

    let linkReferenceForUnreleased =
        sprintf
            "[Unreleased]: %s/compare/%s...%s"
            gitHubRepoUrl
            (tagFromVersionNumber newVersion.AsString)
            "HEAD"

    let tailLines =
        File.read changelogFilename
        |> List.ofSeq
        |> List.rev

    let isRef line =
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

    File.write false changelogFilename updatedLines

    // If build fails after this point but before we push the release out, undo our modifications
    Target.activateBuildFailure "RevertChangelog"

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


let ``dotnet pack`` ctx =
    !!srcGlob
    |> Seq.iter (fun proj ->
        // Get release notes with properly-linked version number
        let releaseNotes =
            latestEntry
            |> mkReleaseNotes linkReferenceForLatestEntry

        let args = [
            sprintf "/p:PackageVersion=%s" latestEntry.NuGetVersion
            sprintf "/p:PackageReleaseNotes=\"%s\"" releaseNotes
        ]

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

let getPkgPath () =
    !!distGlob
    |> Seq.head

let ``integration tests`` ctx =
    testsGlob
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


let publish _ =
    allReleaseChecks ()

    NuGet.NuGet.NuGetPublish(fun c -> {
        c with
            PublishUrl = "https://www.nuget.org"
            WorkingDir = "dist"
            AccessKey =
                match nugetToken with
                | Some s -> s
                | _ -> c.AccessKey
    })


let ``git release`` _ =
    allReleaseChecks ()

    let releaseNotesGitCommitFormat = latestEntry.ToString()

    Git.Staging.stageFile "" "CHANGELOG.md"
    |> ignore

    !! "Content/**/AssemblyInfo.fs"
    |> Seq.iter (
        Git.Staging.stageFile ""
        >> ignore
    )

    Git.Commit.exec
        ""
        (sprintf "Bump version to %s\n\n%s" latestEntry.NuGetVersion releaseNotesGitCommitFormat)

    Git.Branches.push ""

    let tag = tagFromVersionNumber latestEntry.NuGetVersion

    Git.Branches.tag "" tag
    Git.Branches.pushTag "" "origin" tag


let ``github release`` _ =
    allReleaseChecks ()

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
        |> mkReleaseNotes linkReferenceForLatestEntry

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


let ``build docs`` _ = DocsTool.build ()

let ``watch docs`` _ = DocsTool.watch ()

let ``release docs`` ctx =
    isReleaseBranchCheck () // Docs changes don't need a full release to the library

    Git.Staging.stageAll docsDir
    Git.Commit.exec "" (sprintf "Documentation release of version %s" latestEntry.NuGetVersion)

    if
        isRelease (ctx.Context.AllExecutingTargets)
        |> not
    then
        // We only want to push if we're only calling "ReleaseDocs" target
        // If we're calling "Release" target, we'll let the "GitRelease" target do the git push
        Git.Branches.push ""

let initTargets () =
    BuildServer.install [ GitHubActions.Installer ]

    /// Defines a dependency - y is dependent on x
    let (==>!) x y =
        x ==> y
        |> ignore

    /// Defines a soft dependency. x must run before y, if it is present, but y does not require x to be run.
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
    Target.create "DotnetRestore" ``dotnet restore``
    Target.create "UpdateChangelog" ``update changelog``
    Target.createBuildFailure "RevertChangelog" ``revert changelog`` // Do NOT put this in the dependency chain
    Target.createFinal "DeleteChangelogBackupFile" ``delete changelogBackupFile`` // Do NOT put this in the dependency chain
    Target.create "DotnetPack" ``dotnet pack``
    Target.create "IntegrationTests" ``integration tests``
    Target.create "PublishToNuGet" publish
    Target.create "GitRelease" ``git release``
    Target.create "GitHubRelease" ``github release``
    Target.create "Release" ignore
    Target.create "BuildDocs" ``build docs``
    Target.create "WatchDocs" ``watch docs``
    Target.create "ReleaseDocs" ``release docs``
    Target.create "FormatCode" formatCode
    Target.create "CheckFormatCode" checkFormatCode
    //-----------------------------------------------------------------------------
    // Target Dependencies
    //-----------------------------------------------------------------------------
    "DotnetPack"
    ==>! "BuildDocs"

    "BuildDocs"
    ==>! "ReleaseDocs"

    "BuildDocs"
    ?=>! "PublishToNuGet"

    "IntegrationTests"
    ?=>! "ReleaseDocs"

    "ReleaseDocs"
    ?=>! "GitRelease"

    "ReleaseDocs"
    ==>! "Release"

    // Only call UpdateChangelog if Publish was in the call chain
    // Ensure UpdateChangelog is called after DotnetRestore and before GenerateAssemblyInfo
    "DotnetRestore"
    ?=>! "UpdateChangelog"

    "UpdateChangelog"
    ?=>! "DotnetPack"

    "UpdateChangelog"
    ==>! "PublishToNuGet"

    "Clean"
    ==> "DotnetRestore"
    ==> "DotnetPack"
    =?> ("CheckFormatCode", isCI)
    =?> ("IntegrationTests", isCI)
    ==> "PublishToNuGet"
    ==> "GitRelease"
    ==> "GithubRelease"
    ==>! "Release"

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
    Target.runOrDefaultWithArguments (if isCI then "IntegrationTests" else "DotnetPack")

    0 // return an integer exit code
