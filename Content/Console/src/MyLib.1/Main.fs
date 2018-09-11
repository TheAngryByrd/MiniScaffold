namespace MyLib._1
open System.Reflection

module AssemblyInfo =

    let metaDataValue  (mda : AssemblyMetadataAttribute) = mda.Value
    let getMetaDataAttribute (assembly : Assembly) key =
        assembly.GetCustomAttributes(typedefof<AssemblyMetadataAttribute>)
                              |> Seq.cast<AssemblyMetadataAttribute>
                              |> Seq.find(fun x -> x.Key = key)

    let getReleaseDate assembly =
        "ReleaseDate"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let getGitHash assembly =
        "GitHash"
        |> getMetaDataAttribute assembly
        |> metaDataValue

    let getVersion assembly =
        "AssemblyVersion"
        |> getMetaDataAttribute assembly
        |> metaDataValue
    let assembly = lazy(Assembly.GetEntryAssembly())
    let printVersion () =
        let version = assembly.Force().GetName().Version
        printfn "%A" version

    let printInfo () =
        let assembly = assembly.Force()
        let name = assembly.GetName()
        let version = assembly.GetName().Version
        let releaseDate = getReleaseDate assembly
        let githash  = getGitHash assembly
        printfn "%s - %A - %s - %s" name.Name version releaseDate githash

module Say =
    open System
    let nothing name =
        name |> ignore

    let hello name =
        sprintf "Hello %s" name

    let colorizeIn color str =
        let oldColor = Console.ForegroundColor
        Console.ForegroundColor <- (Enum.Parse(typedefof<ConsoleColor>, color) :?> ConsoleColor)
        printfn "%s" str
        Console.ForegroundColor <- oldColor

module Main =
    open Argu
    type CLIArguments =
        | Info
        | Version
        | Favorite_Color of string // Look in App.config
        | [<MainCommand>] Hello of string
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Info -> "More detailed information"
                | Version -> "Version of application"
                | Favorite_Color _ -> "Favorite color"
                | Hello _-> "Who to say hello to"

    [<EntryPoint>]
    let main (argv : string array) =
        let parser = ArgumentParser.Create<CLIArguments>(programName = "MyLib._1")
        let results = parser.Parse(argv)
        if results.Contains Version then
            AssemblyInfo.printVersion ()
        elif results.Contains Info then
            AssemblyInfo.printInfo ()
        elif results.Contains Hello then
            match results.TryGetResult Hello with
            | Some v ->
                let color = results.GetResult Favorite_Color
                Say.hello v |> Say.colorizeIn color
            | None ->  parser.PrintUsage() |> printfn "%s"
        else
            parser.PrintUsage() |> printfn "%s"
        0
