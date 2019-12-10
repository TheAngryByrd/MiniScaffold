namespace MyLib._1
open System

/// <summary> Initial module </summary>
module Say =

    /// <summary> Finite list of Colors </summary>
    type FavoriteColor =
    | Red
    | Yellow
    | Blue

    /// <summary> A person with many different field types </summary>
    type Person = {
        Name : string
        FavoriteNumber : int
        FavoriteColor : FavoriteColor
        DateOfBirth : DateTimeOffset
    }


    /// <summary>Says hello to a specific person</summary>
    let helloPerson (person : Person) =
        sprintf
            "Hello %s. You were born on %s and your favorite number is %d. You like %A."
            person.Name
            (person.DateOfBirth.ToString("yyyy/MM/dd"))
            person.FavoriteNumber
            person.FavoriteColor

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


    /// I do nothing
    let nothing name =
        name |> ignore

