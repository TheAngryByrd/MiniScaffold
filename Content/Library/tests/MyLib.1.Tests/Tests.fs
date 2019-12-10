module Tests

open System
open Expecto
open MyLib._1
open MyLib._1.Say

[<Tests>]
let tests =
    testList "samples" [
        testCase "Add two integers" <| fun _ ->
            let subject = Say.add 1 2
            Expect.equal subject 3 "Addition works"
        testCase "Say nothing" <| fun _ ->
            let subject = Say.nothing ()
            Expect.equal subject () "Not an absolute unit"
        testCase "Say hello all" <| fun _ ->
            let person = {
                Name = "Jean-Luc Picard"
                FavoriteNumber = 4
                FavoriteColor = Red
                DateOfBirth = DateTimeOffset.Parse("July 13, 2305")
            }
            let subject = Say.helloPerson person
            Expect.equal subject "Hello Jean-Luc Picard. You were born on 2305/07/13 and your favorite number is 4. You like Red." "You didn't say hello" ]
