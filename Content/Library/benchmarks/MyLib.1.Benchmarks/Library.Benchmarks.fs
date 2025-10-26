namespace MyLib._1.Benchmarks

open BenchmarkDotNet.Attributes
open MyLib._1
open System

[<MemoryDiagnoser>]
type LibraryBenchmarks() =

    let samplePerson = {
        Say.Person.Name = "Benchmark"
        Say.Person.FavoriteNumber = 42
        Say.Person.FavoriteColor = Say.FavoriteColor.Blue
        Say.Person.DateOfBirth = DateTimeOffset.Now
    }

    [<Benchmark>]
    member _.HelloPerson() = Say.helloPerson samplePerson

    [<Benchmark>]
    member _.AddNumbers() = Say.add 100 200

    [<Benchmark>]
    member _.AddNumbersLoop() =
        let mutable result = 0

        for i in 1..1000 do
            result <- Say.add result i

        result
