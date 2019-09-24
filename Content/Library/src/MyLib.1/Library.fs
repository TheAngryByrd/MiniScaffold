namespace MyLib._1
open System

/// Initial Module
module Say =

    // Some janky payment union
    type PaymentTypes =
    | Cash
    | Check of int*int
    | Credit of string*DateTime*int

    /// Person
    type Person = {
        /// First
        First : string
        Last : string
        FavoriteNumber : int
        DateOfBirth : DateTimeOffset
    }


    /// <summary>Says hello to a specific person</summary>
    let helloPerson (person : Person) =
        sprintf
            "Hello %s %s. You were born on %s and your favorite number is %d."
            person.First
            person.Last
            (person.DateOfBirth.ToString("o"))
            person.FavoriteNumber

    /// I do nothing
    let nothing name =
        name |> ignore


    [<CompiledName("Hiya")>]
    let hello name =
        sprintf "Hello %s" name

    /// We did a bad api design
    [<Obsolete>]
    let reallyOldCode name =
        nothing name
