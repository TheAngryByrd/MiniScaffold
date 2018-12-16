open System.IO
open Paket
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

let release = ReleaseNotes.load "RELEASE_NOTES.md"
let srcGlob = "*.csproj"

let testsGlob = __SOURCE_DIRECTORY__  @@ "tests/**/*.??proj"

let distDir = __SOURCE_DIRECTORY__  @@ "dist"
let distGlob = distDir @@ "*.nupkg"

let docsDir = __SOURCE_DIRECTORY__ @@ "docs"
let docsSrcDir = __SOURCE_DIRECTORY__ @@ "docsSrc"
let docsSrcGlob = docsSrcDir @@ "*.fsx"

let gitOwner = "TheAngryByrd"
let gitRepoName = "MiniScaffold"

let contentDir = __SOURCE_DIRECTORY__ @@ "Content"

let isCI =  Environment.environVarAsBool "CI"

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

Target.create "Clean" <| fun _ ->
    [ "obj" ;"dist"]
    |> Shell.cleanDirs

    Git.CommandHelper.directRunGitCommandAndFail contentDir "clean -xfd"


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


Target.create "IntegrationTests" <| fun ctx ->
    !! testsGlob
    |> Seq.iter (fun proj ->
        DotNet.test(fun c ->
            let args =
                [

                ] |> String.concat " "
            { c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                Common =
                    c.Common
                    |> DotNet.Options.withCustomParams
                        (Some(args))
                }) proj)

open FSharp.Literate
open Fable.Helpers.React
open Fable.Helpers.React.Props


let template titletext bodytext =
    html [Lang "en"] [
        head [] [
            title [] [ str (sprintf "%s docs / %s" gitRepoName titletext) ]
            link [
                Href "https://netdna.bootstrapcdn.com/twitter-bootstrap/2.2.1/css/bootstrap-combined.min.css"
                Type "text/css"
                Rel "stylesheet"
            ]
            link [
                Href "content/style.css"
                Type "text/css"
                Rel "stylesheet"
            ]
            script [Src "https://code.jquery.com/jquery-1.8.0.js" ] []
            script [Src "https://netdna.bootstrapcdn.com/twitter-bootstrap/2.2.1/js/bootstrap.min.js" ] []
            script [Src "content/tips.js" ] []
        ]
        body [] [
            RawText bodytext
        ]
    ]

let render html =
  fragment [] [
    RawText "<!doctype html>"
    RawText "\n"
    html ]
  |> Fable.Helpers.ReactServer.renderToString

Target.create "GenerateDocs" <| fun _ ->
    // This finds the current fsharp.core version of your solution to use for fsharp.literate
    let lockFile = Paket.LockFile.LoadFrom Paket.Constants.LockFileName
    let packageName = Paket.Domain.PackageName "FSharp.Core"
    let (_,package,version) =
        lockFile.InstalledPackages
        |> Seq.filter(fun (_,p,_) ->
            p =  packageName
        )
        |> Seq.maxBy(fun (_,_,semver) -> semver)
    let fsharpCoreDir = Paket.NuGetCache.GetTargetUserFolder package version </> "lib" </> "netstandard1.6"
    let parse source =
        let doc =
          let fsharpCoreDir = sprintf "-I:%s" fsharpCoreDir
          let systemRuntime = "-r:System.Runtime"
          Literate.ParseScriptString(
                      source,
                      compilerOptions = systemRuntime + " " + fsharpCoreDir,
                      fsiEvaluator = FSharp.Literate.FsiEvaluator([|fsharpCoreDir|]))
        FSharp.Literate.Literate.FormatLiterateNodes(doc, OutputKind.Html, "", true, true)
    let format (doc: LiterateDocument) =
        Formatting.format doc.MarkdownDocument true OutputKind.Html
        + doc.FormattedTips



    !! docsSrcGlob
    |> Seq.iter(fun filePath ->
        sprintf "Rendering %s" filePath
        |> Fake.Core.Trace.trace
        let file = IO.File.ReadAllLines filePath |> String.concat "\n"
        let outPath =
            filePath.Replace(docsSrcDir, docsDir).Replace(".fsx", ".html")
            |> FileInfo
        let fs =
            file
            |> parse
            |> format
        let contents =
            fs
            |> template outPath.Name
            |> render
        IO.File.WriteAllText(outPath.FullName, contents)

        sprintf "Rendered %s to %s" filePath outPath.FullName
        |> Fake.Core.Trace.trace

    )
    Shell.copyDir (docsDir </> "content")   ( docsSrcDir </> "content") (fun _ -> true)
    Shell.copyDir (docsDir </> "files")   ( docsSrcDir </> "files") (fun _ -> true)



open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.FileProviders

let openBrowser url =
    //https://github.com/dotnet/corefx/issues/10361
    let result =
        Process.execSimple (fun info ->
                { info with
                    FileName = url
                    UseShellExecute = true })
                TimeSpan.MaxValue
    if result <> 0 then failwithf "opening browser failed"


let waitForPortInUse (hostname : string) port =
    let mutable portInUse = false
    while not portInUse do
        Async.Sleep(10) |> Async.RunSynchronously
        use client = new Net.Sockets.TcpClient()
        try
            client.Connect(hostname,port)
            portInUse <- client.Connected
            client.Close()
        with e ->
            client.Close()

Target.create "ServeDocs" <| fun _ ->
    let hostname = "localhost"
    let port = 5000
    async {
        waitForPortInUse hostname port
        sprintf "http://%s:%d/index.html" hostname port |> openBrowser
    } |> Async.Start
    WebHostBuilder()
        .UseKestrel()
        .Configure(fun app ->
            let opts =
                StaticFileOptions(
                    FileProvider =  new PhysicalFileProvider(docsDir)
                )
            app.UseStaticFiles(opts) |> ignore
        )
        .Build()
        .Run()

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
       match Environment.environVarOrDefault "GITHUB_TOKEN" "" with
       | s when not (String.IsNullOrWhiteSpace s) -> s
       | _ -> failwith "please set the github_token environment variable to a github personal access token with repro access."

   let files = !! distGlob

   GitHub.createClientWithToken token
   |> GitHub.draftNewRelease gitOwner gitRepoName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
   |> GitHub.uploadFiles files
   |> GitHub.publishDraft
   |> Async.RunSynchronously

Target.create "Release" ignore

"GenerateDocs"
  ==> "ServeDocs"

"Clean"
  ==> "DotnetRestore"
  ==> "DotnetPack"
//https://github.com/dotnet/templating/issues/1736#issuecomment-464847242
  =?> ("IntegrationTests", isCI)
  ==> "Publish"
  ==> "GitRelease"
  ==> "GithubRelease"
  ==> "Release"


Target.runOrDefaultWithArguments (if isCI then "IntegrationTests" else "DotnetPack")
