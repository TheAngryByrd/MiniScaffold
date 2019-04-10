namespace MiniScaffold.Tests
open Fake.SystemHelper



module Tests =
    open System
    open Fake.Core
    open Expecto
    open Infrastructure
    open Fake.IO.FileSystemOperators
    let executeBuild workingDir testTarget =
        let cmd, args =
            if Environment.isUnix then
                "bash", [sprintf "./build.sh %s" testTarget]
            else
                ".\\build.cmd", [testTarget]

        CreateProcess.fromRawCommand cmd args
        |> CreateProcess.withWorkingDirectory workingDir
        |> CreateProcess.ensureExitCode
        |> fun x -> printfn "%A" x ; x
        |> Proc.run
        |> ignore
        // Process.execSimple (fun psi ->
        //         let psi = psi.WithWorkingDirectory(workingDir)
        //         let psi =
        //             if Environment.isUnix then
        //                 psi
        //                     .WithFileName("bash")
        //                     .WithArguments(sprintf "./build.sh %s" testTarget)
        //             else
        //                 psi
        //                     .WithFileName(IO.Directory.GetCurrentDirectory() @@ "build.cmd")
        //                     .WithArguments(sprintf "%s" testTarget)
        //         psi
        //         ) (TimeSpan.FromMinutes(5.))
        // |> fun exitCode ->
        //     if exitCode <> 0 then failwithf "Intregration test failed with params %s" testTarget
    [<Tests>]
    let tests =
        testList "samples" [
            yield! [
                "-n MyCoolLib --githubUsername CoolPersonNo2", "DotnetPack"
                // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
                "-n fsharp-data-sample --githubUsername CoolPersonNo2", "DotnetPack"
                "-n MyCoolApp --githubUsername CoolPersonNo2 --outputType Console", "CreatePackages"

            ] |> Seq.map(fun (args,target) -> testCase args <| fun _ ->
                let d = Disposables.DisposableDirectory.Create()
                let newArgs = [
                    sprintf "mini-scaffold -lang F# %s" args
                ]
                Dotnet.New.cmd (fun opt -> { opt with WorkingDirectory = d.Directory }) newArgs

                let projectDir =
                    d.DirectoryInfo.GetDirectories ()
                    |> Seq.head

                executeBuild projectDir.FullName target
            )

        ]
