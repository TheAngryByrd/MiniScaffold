namespace DocsTool

module WebServer =
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.Builder
    open Microsoft.Extensions.FileProviders
    open Microsoft.AspNetCore.Http
    open System
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
        app.Use(fun env (next : Func<Threading.Tasks.Task>) ->
            middlware env (next.Invoke >> Async.AwaitTask)
            |> Async.StartAsTask
            :> System.Threading.Tasks.Task
        )

    let createWebsocketForLiveReload (refreshWebpageEvent : Event<string>) (httpContext : HttpContext) (next : unit -> Async<unit>) = async {
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

    let configureWebsocket (refreshWebpageEvent : Event<string>) (appBuilder : IApplicationBuilder) =
        appBuilder.UseWebSockets()
        |> useAsync (createWebsocketForLiveReload refreshWebpageEvent)
        |> ignore

    let startWebserver (refreshWebpageEvent : Event<string>) docsDir (url : string) =
        WebHostBuilder()
            .UseKestrel()
            .UseUrls(url)
            .Configure(fun app ->
                let opts =
                    StaticFileOptions(
                        FileProvider =  new PhysicalFileProvider(docsDir)
                    )
                app.UseStaticFiles(opts) |> ignore
                configureWebsocket refreshWebpageEvent app
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

    let serveDocs refreshEvent docsDir =
        async {
            waitForPortInUse hostname port
            sprintf "http://%s:%d/index.html" hostname port |> openBrowser
        } |> Async.Start
        startWebserver refreshEvent docsDir (sprintf "http://%s:%d" hostname port)
