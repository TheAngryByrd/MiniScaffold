namespace MiniScaffold.Tests

module Main =
    open Infrastructure
    open System
    open Fake.Core
    open Expecto
    open Expecto.Logging

    [<EntryPoint>]
    let main argv =
        Tests.runTestsInAssemblyWithCLIArgs [] argv
