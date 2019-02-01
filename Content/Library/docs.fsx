open FSharp.Literate
open System.Collections.Generic
open System.Reflection
open Fable.Import.React
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System.Net.WebSockets
open System.Net.WebSockets
open System.Threading
#load ".fake/build.fsx/intellisense.fsx"
#load "./docsSrc/templates/master.fsx"
#load "./docsSrc/templates/modules.fsx"
#load "./docsSrc/templates/namespaces.fsx"
#load "./docsSrc/templates/types.fsx"
#load "./docsSrc/templates/nav.fsx"
#if !FAKE
#r "Facades/netstandard"
#r "netstandard"
#endif

open System
open System.IO
open Paket
open Fake.SystemHelper
open Fake.Core
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api
open FSharp.Literate
open FSharp.MetadataFormat
open Fable.Helpers.React
open Fable.Helpers.React.Props

let docsDir = __SOURCE_DIRECTORY__ @@ "docs"
let docsApiDir = docsDir @@ "api"
let docsSrcDir = __SOURCE_DIRECTORY__ @@ "docsSrc"
let docsSrcGlob = docsSrcDir @@ "**/*.fsx"

let refereshWebpageEvent = new Event<string>()


let docsFileGlob =
    !! docsSrcGlob
    -- (docsSrcDir @@ "templates/*") // Don't want to generate from html templates

let render html =
    fragment [] [
        RawText "<!doctype html>"
        RawText "\n"
        html ]
    |> Fable.Helpers.ReactServer.renderToString




let renderWithMasterTemplate navBar titletext bodytext =
    Master.masterTemplate "gitRepoName" navBar titletext bodytext
    |> render

let renderWithMasterAndWrite (outPath : FileInfo) navBar titletext bodytext   =
    let contents = renderWithMasterTemplate navBar titletext bodytext
    IO.Directory.CreateDirectory(outPath.DirectoryName) |> ignore

    IO.File.WriteAllText(outPath.FullName, contents)
    Fake.Core.Trace.tracefn "Rendered to %s" outPath.FullName

let locateDLL name rid =
    let lockFile = Paket.LockFile.LoadFrom Paket.Constants.LockFileName
    let packageName = Paket.Domain.PackageName name
    let (_,package,version) =
        lockFile.InstalledPackages
        |> Seq.filter(fun (_,p,_) ->
            p =  packageName
        )
        |> Seq.maxBy(fun (_,_,semver) -> semver)
    Paket.NuGetCache.GetTargetUserFolder package version </> "lib" </> rid

let baseDir = Path.GetFullPath "."
let dllsAndLibDirs (dllPattern:IGlobbingPattern) =
    let dlls =
        dllPattern
        |> GlobbingPattern.setBaseDir baseDir
        |> Seq.distinctBy Path.GetFileName
        |> List.ofSeq
    let libDirs =
        dlls
        |> Seq.map Path.GetDirectoryName
        |> Seq.distinct
        |> List.ofSeq
    (dlls, libDirs)










let generateAPI gitRepoName (dllGlob : IGlobbingPattern) =
    let dlls, libDirs = dllsAndLibDirs dllGlob
    let fsharpCoreDir = locateDLL "FSharp.Core" "netstandard1.6"
    let mscorlibDir =
        (Uri(typedefof<System.Runtime.MemoryFailPoint>.GetType().Assembly.CodeBase)) //Find runtime dll
            .AbsolutePath // removes file protocol from path
            |> Path.GetDirectoryName

    let libDirs = fsharpCoreDir :: mscorlibDir  :: libDirs
    // printfn "%A" dlls
    // printfn "%A" libDirs
    let generatorOutput = MetadataFormat.Generate(dlls, libDirs = libDirs)
    // printfn "%A" generatorOutput
    // generatorOutput.AssemblyGroup.Namespaces
    let fi = FileInfo <| docsApiDir @@ "index.html"
    let nav = (Nav.generateNav gitRepoName)
    [Namespaces.generateNamespaceDocs generatorOutput.AssemblyGroup generatorOutput.Properties]
    |> renderWithMasterAndWrite fi nav "apiDocs"
    generatorOutput.ModuleInfos
    |> List.iter (fun m ->
        let fi = FileInfo <| docsApiDir @@ (sprintf "%s.html" m.Module.UrlName)
        Modules.generateModuleDocs m generatorOutput.Properties
        |> renderWithMasterAndWrite fi nav (sprintf "%s-%s" m.Module.Name gitRepoName)
    )
    generatorOutput.TypesInfos
    |> List.iter (fun m ->
        let fi = FileInfo <| docsApiDir @@ (sprintf "%s.html" m.Type.UrlName)
        Types.generateTypeDocs m generatorOutput.Properties
        |> renderWithMasterAndWrite fi nav (sprintf "%s-%s" m.Type.Name gitRepoName)
    )




let copyAssets () =
    Shell.copyDir (docsDir </> "content")   ( docsSrcDir </> "content") (fun _ -> true)
    Shell.copyDir (docsDir </> "files")   ( docsSrcDir </> "files") (fun _ -> true)



let generateDocs (docSourcePaths : IGlobbingPattern) githubRepoName =
    // This finds the current fsharp.core version of your solution to use for fsharp.literate
    let fsharpCoreDir = locateDLL "FSharp.Core" "netstandard1.6"

    let parse fileName source =
        let doc =
            let fsharpCoreDir = sprintf "-I:%s" fsharpCoreDir
            let systemRuntime = "-r:System.Runtime"
            //TODO: possibly make a global so we don't have the spinup cost everytime we call
            Literate.ParseScriptString(
                source,
                path = fileName,
                compilerOptions = systemRuntime + " " + fsharpCoreDir,
                fsiEvaluator = FSharp.Literate.FsiEvaluator([|fsharpCoreDir|]))
        FSharp.Literate.Literate.FormatLiterateNodes(doc, OutputKind.Html, "", true, true)

    let format (doc: LiterateDocument) =
        if not <| Seq.isEmpty doc.Errors
        then
            failwithf "error while formatting file %s. Errors are:\n%A" doc.SourceFile doc.Errors
        else
            Formatting.format doc.MarkdownDocument true OutputKind.Html
            + doc.FormattedTips

    let relativePaths = Nav.generateNav githubRepoName


    docSourcePaths
    |> Seq.iter(fun filePath ->
        Fake.Core.Trace.tracefn "Rendering %s" filePath
        let file = IO.File.ReadAllText filePath
        let outPath =
            filePath.Replace(docsSrcDir, docsDir).Replace(".fsx", ".html")
            |> FileInfo
        let fs =
            file
            |> parse filePath
            |> format
        let contents =
            [div [] [
                fs
                |> RawText
            ]]

            |> renderWithMasterTemplate relativePaths outPath.Name
        IO.Directory.CreateDirectory(outPath.DirectoryName) |> ignore

        IO.File.WriteAllText(outPath.FullName, contents)
        Fake.Core.Trace.tracefn "Rendered %s to %s" filePath outPath.FullName

    )

    copyAssets()


let watchDocs githubRepoName =
    docsFileGlob
    |> ChangeWatcher.run (fun changes ->
        changes
        |> Seq.iter (fun m ->
            generateDocs (!! m.FullPath) githubRepoName
            refereshWebpageEvent.Trigger m.FullPath
        )
    )

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


let createWebsocket (httpContext : HttpContext) (next : unit -> Async<unit>) = async {
    if httpContext.WebSockets.IsWebSocketRequest then
        let! websocket = httpContext.WebSockets.AcceptWebSocketAsync() |> Async.AwaitTask
        use d =
            refereshWebpageEvent.Publish
            |> Observable.subscribe (fun m ->
                let segment = ArraySegment<byte>(m |> Text.Encoding.UTF8.GetBytes)
                websocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.Start

            )
        while websocket.State <> WebSocketState.Closed do
            do! Async.Sleep(100)
    else
        do! next ()
}

let useAsync (middlware : HttpContext -> (unit -> Async<unit>) -> Async<unit>) (app:IApplicationBuilder) =
    app.Use(fun env next ->
        middlware env (next.Invoke >> Async.AwaitTask)
        |> Async.StartAsTask
        :> System.Threading.Tasks.Task
    )

let configureWebsocket (appBuilder : IApplicationBuilder) =
    appBuilder.UseWebSockets()
    |> useAsync (createWebsocket)
    |> ignore

let startWebserver (url : string) =
    WebHostBuilder()
        .UseKestrel()
        .UseUrls(url)
        .Configure(fun app ->
            let opts =
                StaticFileOptions(
                    FileProvider =  new PhysicalFileProvider(docsDir)
                )
            app.UseStaticFiles(opts) |> ignore
            configureWebsocket app
        )
        .Build()
        .Run()

let serveDocs () =
    let hostname = "localhost"
    let port = 5000
    async {
        waitForPortInUse hostname port
        sprintf "http://%s:%d/index.html" hostname port |> openBrowser
    } |> Async.Start
    startWebserver (sprintf "http://%s:%d" hostname port)

