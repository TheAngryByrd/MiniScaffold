// Learn more about F# at http://fsharp.org


open System
open Fake.IO.FileSystemOperators

let refreshWebpageEvent = new Event<string>()

let docsDir = IO.FileInfo(__SOURCE_DIRECTORY__ @@ ".." @@ "docs").FullName
let docsApiDir = docsDir @@ "api"
let docsSrcDir = IO.FileInfo(__SOURCE_DIRECTORY__ @@ ".." @@ "docsSrc").FullName

module Helpers =
    open System
    type DisposableList =
        {
            disposables : IDisposable list
        } interface IDisposable with
            member x.Dispose () =
                x.disposables |> List.iter(fun s -> s.Dispose())
open Helpers

module ProjInfo =
    open System.IO

    type References = FileInfo []
    type TargetPath = FileInfo

    type ProjInfo = {
        References : References
        TargetPath : TargetPath
    }

    open Dotnet.ProjInfo.Workspace
    open Dotnet.ProjInfo.Workspace.FCS
    let createFCS () =
        let checker =
            FCS_Checker.Create(
              projectCacheSize = 200,
              keepAllBackgroundResolutions = true,
              keepAssemblyContents = true)
        checker.ImplicitlyStartBackgroundWork <- true
        checker

    let createLoader () =
        let msbuildLocator = MSBuildLocator()
        let config = LoaderConfig.Default msbuildLocator
        let loader = Loader.Create(config)
        let netFwconfig = NetFWInfoConfig.Default msbuildLocator
        let netFwInfo = NetFWInfo.Create(netFwconfig)

        loader, netFwInfo

    let findReferences projPath : ProjInfo=
        let fcs = createFCS ()
        let loader, netFwInfo = createLoader ()
        loader.LoadProjects [ projPath ]
        let fcsBinder = FCSBinder(netFwInfo, loader, fcs)
        match fcsBinder.GetProjectOptions(projPath) with
        | Some options ->
            let references =
                options.OtherOptions
                |> Array.filter(fun s ->
                    s.StartsWith("-r:")
                )
                |> Array.map(fun s ->
                    s.Remove(0,3)
                    |> FileInfo
                )
            let targetPath =
                match options.ExtraProjectInfo with
                | Some (:? ProjectOptions as dpwPo) ->
                    dpwPo.ExtraProjectInfo.TargetPath |> FileInfo
                | x -> failwithf "invalid project info %A" x
            { References = references ; TargetPath = targetPath}

        | None ->
            failwithf "Couldn't read project %s" projPath





module GenerateDocs =
    open Fake.Core
    open Fake.IO.Globbing.Operators
    open Fake.IO
    open Fable.React
    open Fable.React.Helpers
    open FSharp.Literate
    open System.IO
    open FSharp.MetadataFormat

    let docsFileGlob =
        !! (docsSrcDir @@ "**/*.fsx")
        ++ (docsSrcDir @@ "**/*.md")

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

    let copyAssets () =
        Shell.copyDir (docsDir </> "content")   ( docsSrcDir </> "content") (fun _ -> true)
        Shell.copyDir (docsDir </> "files")   ( docsSrcDir </> "files") (fun _ -> true)

    let generateDocs (libDirs : ProjInfo.References) (docSourcePaths : IGlobbingPattern) githubRepoName =
        let parse (fileName : string) source =
            let doc =
                let references =
                    libDirs
                    |> Array.map(fun fi -> fi.DirectoryName)
                    |> Array.distinct
                    |> Array.map(sprintf "-I:%s")
                let runtimeDeps =
                    [|
                        "-r:System.Runtime"
                        "-r:System.Net.WebClient"
                    |]
                let compilerOptions = String.Join(' ', Array.concat [runtimeDeps; references])
                let fsiEvaluator = FSharp.Literate.FsiEvaluator(references)
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


    let generateAPI (projInfo : ProjInfo.ProjInfo) gitRepoName =
        let mscorlibDir =
            (Uri(typedefof<System.Runtime.MemoryFailPoint>.GetType().Assembly.CodeBase)) //Find runtime dll
                .AbsolutePath // removes file protocol from path
                |> Path.GetDirectoryName
        let references =
            projInfo.References
            |> Array.toList
            |> List.map(fun fi -> fi.DirectoryName)
            |> List.distinct
        let libDirs = mscorlibDir :: references

        let generatorOutput = MetadataFormat.Generate(projInfo.TargetPath.FullName, libDirs = libDirs)
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

    let buildDocs (projInfo : ProjInfo.ProjInfo) githubRepoName =
        generateDocs projInfo.References (docsFileGlob) githubRepoName
        generateAPI projInfo githubRepoName

    let watchDocs (projInfo : ProjInfo.ProjInfo) githubRepoName =
        buildDocs projInfo githubRepoName
        let d1 =
            docsFileGlob
            |> ChangeWatcher.run (fun changes ->
                printfn "changes %A" changes
                changes
                |> Seq.iter (fun m ->
                    printfn "watching %s" m.FullPath
                    generateDocs projInfo.References (!! m.FullPath) githubRepoName
                    refreshWebpageEvent.Trigger m.FullPath
                )
            )

        let d2 =
            !!( projInfo.TargetPath.FullName )
            |> ChangeWatcher.run(fun changes ->
                changes
                |> Seq.iter(fun c -> Trace.logf "Regenerating API docs due to %s" c.FullPath )
                generateAPI projInfo githubRepoName
                refreshWebpageEvent.Trigger "Api"
            )
        { disposables = [d1; d2] } :> IDisposable


module WebServer =
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.Builder
    open Microsoft.Extensions.FileProviders
    open Microsoft.AspNetCore.Http
    open System.Net.WebSockets
    open System.Diagnostics

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
                refreshWebpageEvent.Publish
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
    | ProjectPath of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | ProjectPath _  -> "The glob for the dlls to generate API documentation"

type BuildArgs =
    | ProjectPath of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | ProjectPath _  -> "The glob for the dlls to generate API documentation"

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

    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CLIArguments>(programName = "gadget.exe", errorHandler = errorHandler)
    let parsedArgs = parser.Parse argv
    match parsedArgs.GetSubCommand() with
    | Build args ->
        let projpath = args.GetResult<@ BuildArgs.ProjectPath @>
        let projInfo = ProjInfo.findReferences  projpath
        GenerateDocs.buildDocs projInfo "MyLib.1"
    | Watch args ->
        let projpath = args.GetResult<@ WatchArgs.ProjectPath @>
        let projInfo = ProjInfo.findReferences  projpath
        use ds = GenerateDocs.watchDocs projInfo "MyLib.1"
        WebServer.serveDocs()
    0 // return an integer exit code
