namespace MiniScaffold.Tests



module Tests =
    open System
    open Fake.Core
    open Fake.DotNet
    open Expecto
    open Infrastructure
    open Expecto.Logging
    open Fake.SystemHelper

    let logger = Expecto.Logging.Log.create "setup"
    let nugetPkgName =  "MiniScaffold"
    let nugetPkgPath =
        match Environment.environVarOrNone "MINISCAFFOLD_NUPKG_LOCATION" with
        | Some v -> v
        | None ->
            let dist = IO.Path.Combine(__SOURCE_DIRECTORY__, "../../dist") |> IO.DirectoryInfo
            dist.EnumerateFiles("*.nupkg")
            |> Seq.head
            |> fun fi -> fi.FullName


    let setup () =
        // if not(System.Diagnostics.Debugger.IsAttached) then
        //     printfn "Please attach a debugger, PID: %d" (System.Diagnostics.Process.GetCurrentProcess().Id)
        // while not(System.Diagnostics.Debugger.IsAttached) do
        //     System.Threading.Thread.Sleep(100)
        // System.Diagnostics.Debugger.Break()
        // ensure we're installing the one from our dist folder
        printfn "nugetPkgPath %s" nugetPkgPath

        Dotnet.New.uninstall nugetPkgName
        Dotnet.New.install nugetPkgPath
    let executeBuild workingDir testTarget =
        let cmd, args =
            if Environment.isUnix then
                "bash", [
                    sprintf "./build.sh"
                    testTarget
                ]
            else
                ".\\build.cmd", [
                    testTarget
                ]
        let result =
            CreateProcess.fromRawCommand cmd args
            |> CreateProcess.withWorkingDirectory workingDir
            // |> CreateProcess.ensureExitCode
            |> CreateProcess.redirectOutput
            |> Proc.run

        if result.ExitCode <> 0 then
            failwithf "exit code was %d with output: %s \n\n\n error: %s" result.ExitCode result.Result.Output result.Result.Error

    let commonAsserts = [
        Assert.``paket.dependencies exists``
        Assert.``paket.lock exists``
    ]

    [<Tests>]
    let tests =
        testList "samples" [
            do setup ()
            yield! [
                "-n MyCoolLib --githubUsername CoolPersonNo2", "DotnetPack", [yield! commonAsserts]
                // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
                "-n fsharp-data-sample --githubUsername CoolPersonNo2", "DotnetPack", [yield! commonAsserts]
                "-n MyCoolApp --githubUsername CoolPersonNo2 --outputType Console", "CreatePackages", [yield! commonAsserts]

            ] |> Seq.map(fun (args,target, additionalAsserts) -> testCase args <| fun _ ->
                use d = Disposables.DisposableDirectory.Create()
                let newArgs = [
                    sprintf "mini-scaffold -lang F# %s" args
                ]
                Dotnet.New.cmd (fun opt -> { opt with WorkingDirectory = d.Directory}) newArgs

                let projectDir =
                    d.DirectoryInfo.GetDirectories ()
                    |> Seq.head

                executeBuild projectDir.FullName target

                additionalAsserts
                |> Seq.iter(fun asserter -> asserter projectDir)
            )

        ]
