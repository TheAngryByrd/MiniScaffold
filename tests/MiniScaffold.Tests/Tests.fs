namespace MiniScaffold.Tests
open Fake.SystemHelper



module Tests =
    open System
    open Fake.Core
    open Fake.DotNet
    open Expecto
    open Infrastructure
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

        CreateProcess.fromRawCommand cmd args
        |> CreateProcess.withWorkingDirectory workingDir
        |> CreateProcess.ensureExitCode
        |> CreateProcess.redirectOutput
        |> Proc.run
        |> ignore

    [<Tests>]
    let tests =
        testList "samples" [
            yield! [
                "-n MyCoolLib --githubUsername CoolPersonNo2", "DotnetPack"
                // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
                "-n fsharp-data-sample --githubUsername CoolPersonNo2", "DotnetPack"
                "-n MyCoolApp --githubUsername CoolPersonNo2 --outputType Console", "CreatePackages"

            ] |> Seq.map(fun (args,target) -> testCase args <| fun _ ->
                use d = Disposables.DisposableDirectory.Create()
                let newArgs = [
                    sprintf "mini-scaffold -lang F# %s" args
                ]
                Dotnet.New.cmd (fun opt -> { opt with WorkingDirectory = d.Directory}) newArgs

                let projectDir =
                    d.DirectoryInfo.GetDirectories ()
                    |> Seq.head

                executeBuild projectDir.FullName target
            )

        ]
