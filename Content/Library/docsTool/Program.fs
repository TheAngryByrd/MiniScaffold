// Learn more about F# at http://fsharp.org

open System
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.FileProviders
open Microsoft.AspNetCore.Http
open System.Net.WebSockets
open Fake.Core
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.IO
open Fable.React
open Fable.React.Helpers
open FSharp.Literate
open System.IO
open FSharp.MetadataFormat
open System.Diagnostics

let refereshWebpageEvent = new Event<string>()

let docsDir = FileInfo(__SOURCE_DIRECTORY__ @@ ".." @@ "docs").FullName
let docsApiDir = docsDir @@ "api"
let docsSrcDir = FileInfo(__SOURCE_DIRECTORY__ @@ ".." @@ "docsSrc").FullName

module GenerateDocs =
    let docsFileGlob =
        !! (docsSrcDir @@ "**/*.fsx")
        ++ (docsSrcDir @@ "**/*.md")
        -- (docsSrcDir @@ "templates/*") // Don't want to generate from html templates

    let render html =
        fragment [] [
            RawText "<!doctype html>"
            RawText "\n"
            html ]
        |> Fable.ReactServer.renderToString
    let renderWithMasterTemplate navBar titletext bodytext =
        Master.masterTemplate "gitRepoName" navBar titletext bodytext
        |> render

    let renderWithMasterAndWrite (outPath : FileInfo) navBar titletext bodytext   =
        let contents = renderWithMasterTemplate navBar titletext bodytext
        IO.Directory.CreateDirectory(outPath.DirectoryName) |> ignore

        IO.File.WriteAllText(outPath.FullName, contents)
        Fake.Core.Trace.tracefn "Rendered to %s" outPath.FullName

    let locateDLL name rid =
        let lockFile = Paket.LockFile.LoadFrom (__SOURCE_DIRECTORY__ @@ ".." @@ Paket.Constants.LockFileName)
        let packageName = Paket.Domain.PackageName name
        let (_,package,version) =
            lockFile.InstalledPackages
            |> Seq.filter(fun (_,p,_) ->
                p =  packageName
            )
            |> Seq.maxBy(fun (_,_,semver) -> semver)
        Paket.NuGetCache.GetTargetUserFolder package version </> "lib" </> rid


    let copyAssets () =
        Shell.copyDir (docsDir </> "content")   ( docsSrcDir </> "content") (fun _ -> true)
        Shell.copyDir (docsDir </> "files")   ( docsSrcDir </> "files") (fun _ -> true)

    let generateDocs (docSourcePaths : IGlobbingPattern) githubRepoName =
        // This finds the current fsharp.core version of your solution to use for fsharp.literate
        let fsharpCoreDir = locateDLL "FSharp.Core" "netstandard2.0"
        let newtonsoft = locateDLL "Newtonsoft.Json" "netstandard2.0"
        let parse (fileName : string) source =
            let doc =
                let fsharpCoreDir = sprintf "-I:%s" fsharpCoreDir
                let newtonsoftDir = sprintf "-I:%s" newtonsoft
                let runtimeDeps = "-r:System.Runtime -r:System.Net.WebClient"
                let compilerOptions = String.Join(' ',[
                    runtimeDeps
                    fsharpCoreDir
                    newtonsoftDir
                ])
                let fsiEvaluator = FSharp.Literate.FsiEvaluator([|
                    fsharpCoreDir
                    newtonsoftDir
                    |])
                match Path.GetExtension fileName with
                | ".fsx" ->
                    Literate.ParseScriptString(
                        source,
                        path = fileName,
                        compilerOptions = compilerOptions,
                        fsiEvaluator = fsiEvaluator)
                | ".md" ->
                    Literate.ParseMarkdownString(
                        source,
                        path = fileName,
                        compilerOptions = compilerOptions,
                        fsiEvaluator = fsiEvaluator
                    )
                | others -> failwithf "FSharp.Literal does not support %s file extensions" others
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
                let changeExtension ext path = IO.Path.ChangeExtension(path,ext)
                filePath.Replace(docsSrcDir, docsDir)
                |> changeExtension ".html"
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
        //TODO: Read fsharp core and dependent libs from dotnet-proj-info
        let fsharpCoreDir = locateDLL "FSharp.Core" "netstandard2.0"
        let newtonsoft = locateDLL "Newtonsoft.Json" "netstandard2.0"
        let mscorlibDir =
            (Uri(typedefof<System.Runtime.MemoryFailPoint>.GetType().Assembly.CodeBase)) //Find runtime dll
                .AbsolutePath // removes file protocol from path
                |> Path.GetDirectoryName

        let libDirs = fsharpCoreDir :: mscorlibDir :: newtonsoft  :: libDirs
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

    let buildDocs githubRepoName (dllGlob : IGlobbingPattern) =
        generateDocs (docsFileGlob) githubRepoName
        generateAPI githubRepoName dllGlob

    let watchDocs githubRepoName (dllGlob : IGlobbingPattern) =
        buildDocs githubRepoName dllGlob
        let d1 =
            docsFileGlob
            |> ChangeWatcher.run (fun changes ->
                printfn "changes %A" changes
                changes
                |> Seq.iter (fun m ->
                    printfn "watching %s" m.FullPath
                    generateDocs (!! m.FullPath) githubRepoName
                    refereshWebpageEvent.Trigger m.FullPath
                )
            )

        let d2 =
            dllGlob
            |> ChangeWatcher.run(fun changes ->
                changes
                |> Seq.iter(fun c -> Trace.logf "Regenerating API docs due to %s" c.FullPath )
                generateAPI githubRepoName dllGlob
                refereshWebpageEvent.Trigger "Api"
            )
        d1, d2


module WebServer =

    /// Helper to determine if port is in use
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

    /// Async version of IApplicationBuilder.Use
    let useAsync (middlware : HttpContext -> (unit -> Async<unit>) -> Async<unit>) (app:IApplicationBuilder) =
        app.Use(fun env next ->
            middlware env (next.Invoke >> Async.AwaitTask)
            |> Async.StartAsTask
            :> System.Threading.Tasks.Task
        )

    let createWebsocketForLiveReload (httpContext : HttpContext) (next : unit -> Async<unit>) = async {
        if httpContext.WebSockets.IsWebSocketRequest then
            let! websocket = httpContext.WebSockets.AcceptWebSocketAsync() |> Async.AwaitTask
            use d =
                refereshWebpageEvent.Publish
                |> Observable.subscribe (fun m ->
                    let segment = ArraySegment<byte>(m |> Text.Encoding.UTF8.GetBytes)
                    websocket.SendAsync(segment, WebSocketMessageType.Text, true, httpContext.RequestAborted)
                    |> Async.AwaitTask
                    |> Async.Start

                )
            while websocket.State <> WebSocketState.Closed do
                do! Async.Sleep(1000)
        else
            do! next ()
    }

    let configureWebsocket (appBuilder : IApplicationBuilder) =
        appBuilder.UseWebSockets()
        |> useAsync (createWebsocketForLiveReload)
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

    let openBrowser url =
        //https://github.com/dotnet/corefx/issues/10361
        let psi = ProcessStartInfo(FileName = url, UseShellExecute = true)
        let proc = Process.Start psi
        proc.WaitForExit()
        if proc.ExitCode <> 0 then failwithf "opening browser failed"

    let serveDocs () =
        let hostname = "localhost"
        let port = 5000
        async {
            waitForPortInUse hostname port
            sprintf "http://%s:%d/index.html" hostname port |> openBrowser
        } |> Async.Start
        startWebserver (sprintf "http://%s:%d" hostname port)


open Argu


type WatchArgs =
    | SrcBinGlob of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | SrcBinGlob _  -> "The glob for the dlls to generate API documentation"

type BuildArgs =
    | SrcBinGlob of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | SrcBinGlob _  -> "The glob for the dlls to generate API documentation"

type CLIArguments =
    | [<CustomCommandLine("watch")>]  Watch of ParseResults<WatchArgs>
    | [<CustomCommandLine("build")>]  Build of ParseResults<BuildArgs>
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Watch _ -> "Builds the docs, serves the content, and watches for changes to the content."
            | Build _ -> "Builds the docs"

[<EntryPoint>]
let main argv =
    let srcBinGlob = "/Users/jimmybyrd/Documents/GitHub/MiniScaffold/Content/Library/src/MyLib.1/bin/**/netstandard2.0/MyLib.*.dll"
    printfn "%A" srcBinGlob
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CLIArguments>(programName = "gadget.exe", errorHandler = errorHandler)
    let parsedArgs = parser.Parse argv
    match parsedArgs.GetSubCommand() with
    | Build args ->
        GenerateDocs.buildDocs "MyLib.1" (!! srcBinGlob)
    | Watch args ->
        let ds = GenerateDocs.watchDocs "MyLib.1" (!! srcBinGlob)
        WebServer.serveDocs()
    0 // return an integer exit code
