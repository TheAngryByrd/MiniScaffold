module Program

open BenchmarkDotNet.Running
open MyLib._1.Benchmarks

[<EntryPoint>]
let main args =
    BenchmarkRunner.Run<LibraryBenchmarks>()
    |> ignore

    0
