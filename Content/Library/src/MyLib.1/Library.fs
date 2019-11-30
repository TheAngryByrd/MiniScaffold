namespace MyLib._1
open System
open Newtonsoft.Json.Linq

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

    /// <summary>
    /// Adds two integers <paramref name="a"/> and <paramref name="b"/> and returns the result.
    /// </summary>
    ///
    /// <remarks>
    /// This usually contains some really important information that you'll miss if you don't read the docs.
    /// </remarks>
    ///
    /// <param name="a">An integer.</param>
    /// <param name="b">An integer.</param>
    ///
    /// <returns>
    /// The sum of two integers.
    /// </returns>
    ///
    /// <exceptions cref="M:System.OverflowException">Thrown when one parameter is max
    /// and the other is greater than 0.</exceptions>
    let add a b =
        a + b

    /// <summary>Subtracts two numbers</summary>
    let subtract a b =
        a + b

    /// I do nothing
    let nothing name =
        name |> ignore


    [<CompiledName("Hiya")>]
    let hello name =
        sprintf "Hello %s" name

    /// We did a bad api design
    [<Obsolete>]
    let reallyOldCode (name : 'a) =
        nothing name


    /// Who doesn't like json?
    let personJToken (person : Person) =
        JToken.FromObject person
