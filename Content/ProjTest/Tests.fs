namespace MyLib._1

open System
open Expecto


module SayTests =
    [<Tests>]
    let tests =
        testList "samples" [
            testCase "Add two integers"
            <| fun _ ->
                let result = 2 + 2
                Expect.equal result 4 "Addition works"
        ]
