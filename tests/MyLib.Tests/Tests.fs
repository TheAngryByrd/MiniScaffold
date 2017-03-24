module Tests

open System
open Xunit
open MyLib

[<Fact>]
let ``My test`` () =
    Assert.Equal("Hello all", Say.hello "all")
