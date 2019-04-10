namespace MiniScaffold.Tests




module Main =
    open Infrastructure
    open System
    open Fake.Core
    open Expecto
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
        // ensure we're installing the one from our dist folder
        Dotnet.New.uninstall nugetPkgName
        Dotnet.New.install nugetPkgPath

    [<EntryPoint>]
    let main argv =
        setup ()
        Tests.runTestsInAssembly defaultConfig argv
