namespace MyLib._1
open System

/// Initial Module
module Say =

    /// **Person**
    type Person = {
        /// First
        First : string
        Last : string
        FavoriteNumber : int
        DateOfBirth : DateTimeOffset
    }


    /// **Description**
    ///
    /// **Parameters**
    ///   * `person` - parameter of type `Person`
    ///
    /// **Output Type**
    ///   * `string`
    ///
    /// **Exceptions**
    ///
    let helloPerson (person : Person) =
        sprintf
            "Hello %s %s. You were born on %s and your favorite number is %d."
            person.First
            person.Last
            (person.DateOfBirth.ToString("o"))
            person.FavoriteNumber

    /// ## Description
    /// I do nothing, ever.
    ///
    /// ## Parameters
    ///   * `name` - parameter of type `'a`
    ///
    /// ## Output Type
    ///   * `unit`
    ///
    /// ## Exceptions
    /// None
    let nothing name =
        name |> ignore


    /// **Description**
    ///
    /// **Parameters**
    ///   * `name` - parameter of type `string`
    ///
    /// **Output Type**
    ///   * `string`
    ///
    /// **Exceptions**
    ///
    [<CompiledName("Hiya")>]
    let hello name =
        sprintf "Hello %s" name

