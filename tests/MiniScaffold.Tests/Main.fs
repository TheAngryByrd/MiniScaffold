namespace MiniScaffold.Tests

module Main =
    open Infrastructure
    open System
    open Fake.Core
    open Expecto
    open Expecto.Logging

    [<EntryPoint>]
    let main argv =
        match ProcessCaptureTests.parseWorkerArguments argv with
        | ProcessCaptureTests.OrdinaryArguments -> Tests.runTestsInAssemblyWithCLIArgs [] argv
        | ProcessCaptureTests.InvalidWorkerArguments _ -> 2
        | ProcessCaptureTests.CaptureWorker request ->
            MiniScaffold.Tests.Tests.tests
            |> ignore

            ProcessCaptureTests.runCaptureWorker request
