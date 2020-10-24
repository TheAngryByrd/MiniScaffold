// Learn more about F# at http://fsharp.org


open System
open Fake.IO.FileSystemOperators
open Fake.IO
open Fake.Core

let refreshWebpageEvent = new Event<string>()

type Configuration = {
    SiteBaseUrl         : Uri
    GitHubRepoUrl       : Uri
    RepositoryRoot      : IO.DirectoryInfo
    DocsOutputDirectory : IO.DirectoryInfo
    DocsSourceDirectory : IO.DirectoryInfo
    ProjectName         : string
    ProjectFilesGlob    : IGlobbingPattern
    ReleaseVersion      : string
    PublishPath         : IO.DirectoryInfo
}

let docsApiDir docsDir = docsDir @@ "Api_Reference"

type DisposableList =
        {
            disposables : IDisposable list
        } interface IDisposable with
            member x.Dispose () =
                x.disposables |> List.iter(fun s -> s.Dispose())

module GenerateDocs =
    open DocsTool
    open Fake.IO.Globbing.Operators
    open Fable.React
    open FSharp.Literate
    open System.IO
    open FSharp.MetadataFormat


    type GeneratedDoc = {
        SourcePath : FileInfo option
        OutputPath : FileInfo
        Content : ReactElement list
        Title : string
    }


    let docsFileGlob docsSrcDir =
        !! (docsSrcDir @@ "**/*.fsx")
        ++ (docsSrcDir @@ "**/*.md")

    let render html =
        fragment [] [
            RawText "<!doctype html>"
            RawText "\n"
            html ]
        |> Fable.ReactServer.renderToString

    let renderWithMasterTemplate masterCfg navBar titletext bodytext pageSource =
        Master.masterTemplate masterCfg navBar titletext bodytext pageSource
        |> render

    let renderWithMasterAndWrite masterCfg (outPath : FileInfo) navBar titletext bodytext pageSource   =
        let contents = renderWithMasterTemplate masterCfg navBar titletext bodytext pageSource
        IO.Directory.CreateDirectory(outPath.DirectoryName) |> ignore

        IO.File.WriteAllText(outPath.FullName, contents)
        Fake.Core.Trace.tracefn "Rendered to %s" outPath.FullName

    let generateNav (cfg : Configuration) (generatedDocs : GeneratedDoc list) =
        let docsDir = cfg.DocsOutputDirectory.FullName
        let pages =
                generatedDocs
                |> List.map(fun gd -> gd.OutputPath)
                |> List.filter(fun f -> f.FullName.StartsWith(docsDir </> "content") |> not)
                |> List.filter(fun f -> f.FullName.StartsWith(docsDir </> "files") |> not)
                |> List.filter(fun f -> f.FullName.StartsWith(docsDir </> "index.html") |> not)

        let topLevelNavs : Nav.TopLevelNav = {
            DocsRoot = IO.DirectoryInfo docsDir
            DocsPages = pages
        }

        let navCfg : Nav.NavConfig = {
            SiteBaseUrl = cfg.SiteBaseUrl
            GitHubRepoUrl = cfg.GitHubRepoUrl
            ProjectName = cfg.ProjectName
            TopLevelNav = topLevelNavs
        }

        Nav.generateNav navCfg

    let renderGeneratedDocs isWatchMode (cfg : Configuration)  (generatedDocs : GeneratedDoc list) =
        let nav = generateNav cfg generatedDocs
        let masterCfg : Master.MasterTemplateConfig = {
            SiteBaseUrl = cfg.SiteBaseUrl
            GitHubRepoUrl = cfg.GitHubRepoUrl
            ProjectName = cfg.ProjectName
            ReleaseVersion = cfg.ReleaseVersion
            ReleaseDate = DateTimeOffset.Now
            RepositoryRoot = cfg.RepositoryRoot
            IsWatchMode = isWatchMode
        }
        generatedDocs
        |> Seq.iter(fun gd ->
            let pageSource =
                gd.SourcePath
                |> Option.map(fun sp ->
                    sp.FullName.Replace(cfg.RepositoryRoot.FullName, "").Replace("\\", "/")
                )
            renderWithMasterAndWrite masterCfg gd.OutputPath nav gd.Title gd.Content pageSource
        )


    let copyAssets (cfg : Configuration) =
        Shell.copyDir (cfg.DocsOutputDirectory.FullName </> "content")   ( cfg.DocsSourceDirectory.FullName </> "content") (fun _ -> true)
        Shell.copyDir (cfg.DocsOutputDirectory.FullName </> "files")   ( cfg.DocsSourceDirectory.FullName </> "files") (fun _ -> true)


    let regexReplace (cfg : Configuration) source =
        let replacements =
            [
                "{{siteBaseUrl}}", (cfg.SiteBaseUrl.ToString().TrimEnd('/'))
            ]
        (source, replacements)
        ||> List.fold(fun state (pattern, replacement) ->
            Text.RegularExpressions.Regex.Replace(state, pattern, replacement)
        )

    let stringContainsInsenstive (filter : string) (textToSearch : string) =
        textToSearch.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0

    let generateDocs (docSourcePaths : IGlobbingPattern) (cfg : Configuration) =
        let parse (fileName : string) source =
            let doc =
                let fsiArgs =
                    [|
                        yield "--noframework" // error FS1222: When mscorlib.dll or FSharp.Core.dll is explicitly referenced the --noframework option must also be passed
                        yield sprintf "-I:\"%s\"" cfg.PublishPath.FullName
                    |]

                let dlls =
                    cfg.PublishPath.EnumerateFiles()
                    |> Seq.map(fun fi -> fi.FullName)
                    |> Seq.filter(fun f -> f.EndsWith(".dll"))
                    |> Seq.map (sprintf "-r:%s")

                let compilerOptions =
                    [|
                        yield "--targetprofile:netstandard"
                        yield! dlls
                    |]
                let fsiEvaluator = FSharp.Literate.FsiEvaluator(fsiArgs)
                match Path.GetExtension fileName with
                | ".fsx" ->
                    Literate.ParseScriptString(
                        source,
                        path = fileName,
                        compilerOptions = (compilerOptions |> String.concat " "),
                        fsiEvaluator = fsiEvaluator)
                | ".md" ->
                    let source = regexReplace cfg source
                    Literate.ParseMarkdownString(
                        source,
                        path = fileName,
                        compilerOptions = (compilerOptions |> String.concat " "),
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



        docSourcePaths
        |> Array.ofSeq
        |> Seq.map(fun filePath ->

            Fake.Core.Trace.tracefn "Rendering %s" filePath
            let file = IO.File.ReadAllText filePath
            let outPath =
                let changeExtension ext path = IO.Path.ChangeExtension(path,ext)
                filePath.Replace(cfg.DocsSourceDirectory.FullName, cfg.DocsOutputDirectory.FullName)
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

            {
                SourcePath = FileInfo filePath |> Some
                OutputPath = outPath
                Content = contents
                Title = sprintf "%s-%s" outPath.Name cfg.ProjectName
            }
        )
        |> Seq.toList

    let publish (cfg : Configuration) =
        cfg.ProjectFilesGlob
        |> Seq.iter(fun p ->
            Fake.DotNet.DotNet.publish
                (fun opts ->
                    { opts
                        with
                            OutputPath = Some cfg.PublishPath.FullName
                            Framework = Some "netstandard2.1"
                    })
                p
        )

    let generateAPI (cfg : Configuration) =

        let generate (projInfo :  string) =
            Trace.tracefn "Generating API Docs for %s" projInfo
            let libDirs = [cfg.PublishPath.FullName]
            let projName = IO.Path.GetFileNameWithoutExtension(projInfo)
            let targetApiDir = docsApiDir cfg.DocsOutputDirectory.FullName @@ projName
            let projDll = cfg.PublishPath.FullName @@ sprintf "%s.dll" projName
            let generatorOutput =
                MetadataFormat.Generate(
                    projDll,
                    libDirs = libDirs,
                    sourceFolder = cfg.RepositoryRoot.FullName,
                    sourceRepo = (cfg.GitHubRepoUrl |> Uri.simpleCombine "tree/master" |> string),
                    markDownComments = false
                    )

            let fi = FileInfo <| targetApiDir @@ (sprintf "%s.html" generatorOutput.AssemblyGroup.Name)
            let indexDoc = {
                SourcePath = None
                OutputPath = fi
                Content = [Namespaces.generateNamespaceDocs generatorOutput.AssemblyGroup generatorOutput.Properties]
                Title = sprintf "%s-%s" fi.Name cfg.ProjectName
            }

            let moduleDocs =
                generatorOutput.ModuleInfos
                |> List.map (fun m ->
                    let fi = FileInfo <| targetApiDir @@ (sprintf "%s.html" m.Module.UrlName)
                    let content = Modules.generateModuleDocs m generatorOutput.Properties
                    {
                        SourcePath = None
                        OutputPath = fi
                        Content = content
                        Title = sprintf "%s-%s" m.Module.Name cfg.ProjectName
                    }
                )
            let typeDocs =
                generatorOutput.TypesInfos
                |> List.map (fun m ->
                    let fi = FileInfo <| targetApiDir @@ (sprintf "%s.html" m.Type.UrlName)
                    let content = Types.generateTypeDocs m generatorOutput.Properties
                    {
                        SourcePath = None
                        OutputPath = fi
                        Content = content
                        Title = sprintf "%s-%s" m.Type.Name cfg.ProjectName
                    }
                )
            [ indexDoc ] @ moduleDocs @ typeDocs
        cfg.ProjectFilesGlob
        |> Seq.collect(generate)
        |> Seq.toList

    let renderDocs (cfg : Configuration) =
        copyAssets cfg
        let generateDocs =
            async {
                try
                    return generateDocs (docsFileGlob cfg.DocsSourceDirectory.FullName) cfg
                with e ->
                    eprintfn "generateDocs failure %A" e
                    return raise e
            }
        let generateAPI =
            async {
                return (generateAPI cfg)
            }


        publish cfg
        Async.Parallel [generateDocs; generateAPI]
        |> Async.RunSynchronously
        |> Array.toList
        |> List.collect id

    let buildDocs (cfg : Configuration) =
        renderDocs cfg
        |> renderGeneratedDocs false cfg

    let watchDocs (cfg : Configuration) =
        let initialDocs = renderDocs cfg
        let renderGeneratedDocs = renderGeneratedDocs true
        initialDocs |> renderGeneratedDocs cfg

        let docsSrcWatcher =
            docsFileGlob cfg.DocsSourceDirectory.FullName
            |> ChangeWatcher.run (fun changes ->
                printfn "changes %A" changes
                changes
                |> Seq.iter (fun m ->
                    printfn "watching %s" m.FullPath
                    let generated = generateDocs (!! m.FullPath) cfg
                    initialDocs
                    |> List.filter(fun x -> generated |> List.exists(fun y -> y.OutputPath =  x.OutputPath) |> not )
                    |> List.append generated
                    |> List.distinctBy(fun gd -> gd.OutputPath.FullName)
                    |> renderGeneratedDocs cfg
                )
                refreshWebpageEvent.Trigger "m.FullPath"
            )

        let contentWatcher =
            !! (cfg.DocsSourceDirectory.FullName </> "content" </> "**/*")
            ++ (cfg.DocsSourceDirectory.FullName </> "files"  </> "**/*")
            |> ChangeWatcher.run(fun changes ->
                printfn "changes %A" changes
                copyAssets cfg
                refreshWebpageEvent.Trigger "Assets"
            )

        let typesToWatch = [
            ".fs"
            ".fsx"
            ".fsproj"
        ]
        let apiDocsWatcher =
            // NOTE: ChangeWatch doesn't seem to like globs in some case and wants full paths
            let glob =
                cfg.ProjectFilesGlob // Get all src projects
                |> Seq.map(fun p -> (FileInfo p).Directory.FullName </> "**") // Create glob for all files in fsproj folder
                |> Seq.fold ((++)) (!! "") //Expand to get all files
                |> Seq.filter(fun file -> typesToWatch |> Seq.exists file.EndsWith) // Filter for only F# style files
                |> Seq.fold ((++)) (!! "") // Turn into glob for ChangeWatcher
            glob
            |> ChangeWatcher.run
              (fun changes ->
                changes
                |> Seq.iter(fun c -> Trace.logf "Regenerating API docs due to %s" c.FullPath )
                publish cfg
                let generated = generateAPI cfg
                initialDocs
                |> List.filter(fun x -> generated |> List.exists(fun y -> y.OutputPath =  x.OutputPath) |> not )
                |> List.append generated
                |> List.distinctBy(fun gd -> gd.OutputPath.FullName)
                |> renderGeneratedDocs cfg
                refreshWebpageEvent.Trigger "Api"
            )



        let ds =
            [
                docsSrcWatcher
                contentWatcher
                apiDocsWatcher
            ]
        { disposables = ds } :> IDisposable


module WebServer =
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.Builder
    open Microsoft.Extensions.FileProviders
    open Microsoft.AspNetCore.Http
    open System.Net.WebSockets
    open System.Diagnostics
    open System.Runtime.InteropServices

    let hostname = "localhost"
    let port = 5000

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

    let startWebserver docsDir (url : string) =
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
        let waitForExit (proc : Process) =
            proc.WaitForExit()
            if proc.ExitCode <> 0 then eprintf "opening browser failed, open your browser and navigate to url to see the docs site."
        try
            let psi = ProcessStartInfo(FileName = url, UseShellExecute = true)
            Process.Start psi
            |> waitForExit
        with e ->
            //https://github.com/dotnet/corefx/issues/10361
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                let url = url.Replace("&", "&^")
                let psi = ProcessStartInfo("cmd", (sprintf "/c %s" url), CreateNoWindow=true)
                Process.Start psi
                |> waitForExit
            elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                Process.Start("xdg-open", url)
                |> waitForExit
            elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                Process.Start("open", url)
                |> waitForExit
            else
                failwithf "failed to open browser on current OS"

    let serveDocs docsDir =
        async {
            waitForPortInUse hostname port
            sprintf "http://%s:%d/index.html" hostname port |> openBrowser
        } |> Async.Start
        startWebserver docsDir (sprintf "http://%s:%d" hostname port)


open FSharp.Formatting.Common
open System.Diagnostics

let setupFsharpFormattingLogging () =
    let setupListener listener =
        [
            FSharp.Formatting.Common.Log.source
            Yaaf.FSharp.Scripting.Log.source
        ]
        |> Seq.iter (fun source ->
            source.Switch.Level <- System.Diagnostics.SourceLevels.All
            Log.AddListener listener source)
    let noTraceOptions = TraceOptions.None
    Log.ConsoleListener()
    |> Log.SetupListener noTraceOptions System.Diagnostics.SourceLevels.Verbose
    |> setupListener

open Argu
open Fake.IO.Globbing.Operators
open DocsTool.CLIArgs
open DocsTool.Directory
[<EntryPoint>]
let main argv =
    try
        use tempDocsOutDir = DisposableDirectory.Create()
        use publishPath = DisposableDirectory.Create()
        use __ = AppDomain.CurrentDomain.ProcessExit.Subscribe(fun _ ->
            dispose tempDocsOutDir
        )
        use __ = Console.CancelKeyPress.Subscribe(fun _ ->
            dispose tempDocsOutDir
        )
        let defaultConfig = {
            SiteBaseUrl = Uri(sprintf "http://%s:%d/" WebServer.hostname WebServer.port )
            GitHubRepoUrl = Uri "https://github.com"
            RepositoryRoot = IO.DirectoryInfo (__SOURCE_DIRECTORY__ @@ "..")
            DocsOutputDirectory = tempDocsOutDir.DirectoryInfo
            DocsSourceDirectory = IO.DirectoryInfo "docsSrc"
            ProjectName = ""
            ProjectFilesGlob = !! ""
            PublishPath = publishPath.DirectoryInfo
            ReleaseVersion = "0.1.0"
        }

        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
        let programName =
            let name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name
            if Environment.isWindows then
                sprintf "%s.exe" name
            else
                name

        let parser = ArgumentParser.Create<CLIArguments>(programName = programName, errorHandler = errorHandler)
        let parsedArgs = parser.Parse argv
        match parsedArgs.GetSubCommand() with
        | Build args ->
            let config =
                (defaultConfig, args.GetAllResults())
                ||> List.fold(fun state next ->
                    match next with
                    | BuildArgs.SiteBaseUrl url ->  { state with SiteBaseUrl = Uri url }
                    | BuildArgs.ProjectGlob glob -> { state with ProjectFilesGlob = !! glob}
                    | BuildArgs.DocsOutputDirectory outdir -> { state with DocsOutputDirectory = IO.DirectoryInfo outdir}
                    | BuildArgs.DocsSourceDirectory srcdir -> { state with DocsSourceDirectory = IO.DirectoryInfo srcdir}
                    | BuildArgs.GitHubRepoUrl url -> { state with GitHubRepoUrl = Uri url}
                    | BuildArgs.ProjectName repo -> { state with ProjectName = repo}
                    | BuildArgs.ReleaseVersion version -> { state with ReleaseVersion = version}
                )
            GenerateDocs.buildDocs config
        | Watch args ->
            let config =
                (defaultConfig, args.GetAllResults())
                ||> List.fold(fun state next ->
                    match next with
                    | WatchArgs.ProjectGlob glob -> {state with ProjectFilesGlob = !! glob}
                    | WatchArgs.DocsSourceDirectory srcdir -> { state with DocsSourceDirectory = IO.DirectoryInfo srcdir}
                    | WatchArgs.GitHubRepoUrl url -> { state with GitHubRepoUrl = Uri url}
                    | WatchArgs.ProjectName repo -> { state with ProjectName = repo}
                    | WatchArgs.ReleaseVersion version -> { state with ReleaseVersion = version}
                )
            use ds = GenerateDocs.watchDocs config
            WebServer.serveDocs config.DocsOutputDirectory.FullName
        0
    with e ->
        eprintfn "Fatal error: %A" e
        1
