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
    let assembly =  Assembly.GetEntryAssembly()
    let printVersion () =
        let version = assembly.GetName().Version
        printfn "%A" version

    let printInfo () =
        let name = assembly.GetName()
        let version = assembly.GetName().Version
        let releaseDate = getReleaseDate assembly
        let githash  = getGitHash assembly
        printfn "%s - %A - %s - %s" name.Name version releaseDate githash


module Say =
    let nothing name =
        name |> ignore

    let hello name =
        sprintf "Hello %s" name


module Main =
    open Argu
    type CLIArguments =
        | Info
        | Version
        | [<MainCommand>] Hello of string
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Info -> "More detailed information"
                | Version -> "Version of application"
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
            | Some v -> Say.hello v |> printfn "%s"
            | None ->  parser.PrintUsage() |> printfn "%s"
        else
            parser.PrintUsage() |> printfn "%s"
        0
